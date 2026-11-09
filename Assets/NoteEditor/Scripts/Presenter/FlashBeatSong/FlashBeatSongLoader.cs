using System.Collections.Generic;
using System.IO;
using System.Linq;
using NoteEditor.Model;
using UnityEngine;

namespace NoteEditor.Presenter.FlashBeatSong
{
    public class FlashBeatSongLoader : MonoBehaviour
    {
        [SerializeField] Transform fileItemContainer = default;
        [SerializeField] GameObject fileItemPrefab = default;
        [SerializeField] UnityEngine.UI.Text emptyMessageText = default;
        [SerializeField] UnityEngine.UI.Button refreshButton = default;

        SongSwitchCoordinator coordinator_;

        void Awake()
        {
            coordinator_ = BuildCoordinator();
        }

        void Start()
        {
            if (refreshButton != null) refreshButton.onClick.AddListener(RefreshList);
            RefreshList();
        }

        public void RefreshList()
        {
            if (fileItemContainer == null)
                return;

            foreach (Transform child in fileItemContainer) DestroyItem(child.gameObject);

            var jsonPaths = EnumerateJsonFilesForTest(ResolveResourcesDirectory()).ToList();

            foreach (var path in jsonPaths)
            {
                var item = Instantiate(fileItemPrefab, fileItemContainer);
                item.GetComponent<FileListItem>().SetInfo(new FileItemInfo(false, path));
                var btn = item.GetComponent<UnityEngine.UI.Button>();
                if (btn != null)
                {
                    var capturedPath = path;
                    btn.onClick.AddListener(() => OnFileSelected(capturedPath));
                }
            }

            if (emptyMessageText != null) emptyMessageText.gameObject.SetActive(jsonPaths.Count == 0);
        }

        // 一覧を作り直すときに古い項目を消す。
        // EditMode (Editor 上) では Destroy が使えないため DestroyImmediate にする。
        static void DestroyItem(GameObject item)
        {
            if (item == null)
                return;

            if (Application.isPlaying)
                Destroy(item);
            else
                DestroyImmediate(item);
        }

        // 曲切替の入口。
        // 未保存の変更がある場合は確認ダイアログを出し、
        // 保存 / 破棄 / キャンセルの選択に従ってから読み込む。
        public void OnFileSelected(string jsonPath)
        {
            if (coordinator_ == null)
                coordinator_ = BuildCoordinator();

            coordinator_.RequestSwitch(jsonPath);
        }

        SongSwitchCoordinator BuildCoordinator()
        {
            var prompt = GetComponent<SongSwitchPromptDialog>();
            if (prompt == null)
                prompt = gameObject.AddComponent<SongSwitchPromptDialog>();

            return new SongSwitchCoordinator(
                () => EditData.IsDirty.Value,
                () => ChartFileWriter.CurrentSongName(),
                prompt,
                SaveCurrentSong,
                LoadSongFile);
        }

        // いま編集中の曲を本番の Resources へ保存する。
        // 保存に失敗した場合は切り替えを中止する。
        bool SaveCurrentSong()
        {
            try
            {
                var path = ChartFileWriter.SaveCurrentSong(ResolveResourcesDirectory());
                Debug.Log($"[FlashBeatSongLoader] Saved before switching: {path}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FlashBeatSongLoader] Save before switching failed: {ex.Message}");
                return false;
            }
        }

        // 本番の譜面置き場 (ChartFileWriter.ResourcesDir) の実体パス。
        //
        // 相対パスは Editor の作業ディレクトリ (プロジェクトルート) に
        // 解決できるが、作業ディレクトリが別的地方のときは
        // プロジェクトルートから組み立てた絶対パスを使う。
        // ディレクトリ名は ChartFileWriter が 1 箇所で持つ (二重定義しない)。
        public static string ResolveResourcesDirectory()
        {
            if (Directory.Exists(ChartFileWriter.ResourcesDir))
                return ChartFileWriter.ResourcesDir;

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (!string.IsNullOrEmpty(projectRoot))
            {
                var absolute = Path.Combine(projectRoot, ChartFileWriter.ResourcesDir);
                if (Directory.Exists(absolute))
                    return absolute;
            }

            return ChartFileWriter.ResourcesDir;
        }

        // 指定した JSON を読み込む。
        //
        // 失敗した場合は false を返し、error に理由を載せる。
        // 失敗しても旧曲の状態は壊さない (EditDataSerializer.Deserialize が
        // 置換トランザクションで実装されている。Issue #225 B-3)。
        public bool LoadSongFile(string jsonPath, out string error)
        {
            error = "";

            try
            {
                var json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                NoteEditor.Model.EditDataSerializer.Deserialize(json);
                NoteEditor.Model.EditData.Name.Value = SongName.Normalize(Path.GetFileName(jsonPath));
                NoteEditor.Model.EditData.IsDirty.Value = false;

                // 新曲のセッションへ切り替える: 旧曲の再生位置・再生状態を持ち込まない。
                // undo/redo 履歴は Audio.OnLoad → EditCommandManager.Clear で破棄される。
                NoteEditor.Model.Audio.TimeSamples.Value = 0;
                NoteEditor.Model.Audio.SmoothedTimeSamples.Value = 0;
                NoteEditor.Model.Audio.IsPlaying.Value = false;

                ProvideSyntheticAudioClip();
                NoteEditor.Model.Audio.OnLoad.OnNext(UniRx.Unit.Default);
                Debug.Log($"[FlashBeatSongLoader] Loaded: {jsonPath}");
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                Debug.LogError($"[FlashBeatSongLoader] Failed to load {jsonPath}: {ex.Message}");
                return false;
            }
        }

        // 44100Hz / 1秒 / 無音の合成 AudioClip を Audio.Source.clip にセット。
        // Audio.OnLoad 後に Audio.Source.clip を null-guard せず参照する
        // Presenter (PlaybackPositionPresenter 等) が NPE しないために必要。
        static void ProvideSyntheticAudioClip()
        {
            const int sampleRate = 44100;
            const float lengthSeconds = 1f;
            int sampleCount = Mathf.CeilToInt(sampleRate * lengthSeconds);
            var silent = new float[sampleCount];
            var clip = AudioClip.Create("__FlashBeatSynthetic", sampleCount, 1, sampleRate, false);
            clip.SetData(silent, 0);
            NoteEditor.Model.Audio.Source.clip = clip;
        }

        // テスト用: 純粋関数の static メソッドとして公開。Product code では ResourcesDir を固定で読む。
        public static IEnumerable<string> EnumerateJsonFilesForTest(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return Enumerable.Empty<string>();

            return Directory.GetFiles(dir, "*.json")
                .Where(p => !Path.GetFileNameWithoutExtension(p).EndsWith("_text"))
                .Where(p => Path.GetFileNameWithoutExtension(p) != "E2EProbeSong")
                .OrderBy(p => p);
        }
    }
}
