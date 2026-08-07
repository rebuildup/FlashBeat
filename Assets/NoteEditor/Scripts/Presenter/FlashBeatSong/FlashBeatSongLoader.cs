using System.Collections.Generic;
using System.IO;
using System.Linq;
using NoteEditor.Model;
using UnityEngine;

namespace NoteEditor.Presenter.FlashBeatSong
{
    public class FlashBeatSongLoader : MonoBehaviour
    {
        public const string ResourcesDir = "Assets/Game/Resources";

        [SerializeField] Transform fileItemContainer = default;
        [SerializeField] GameObject fileItemPrefab = default;
        [SerializeField] UnityEngine.UI.Text emptyMessageText = default;
        [SerializeField] UnityEngine.UI.Button refreshButton = default;

        void Start()
        {
            if (refreshButton != null) refreshButton.onClick.AddListener(RefreshList);
            RefreshList();
        }

        public void RefreshList()
        {
            foreach (Transform child in fileItemContainer) Destroy(child.gameObject);

            var jsonPaths = EnumerateJsonFilesForTest(ResourcesDir).ToList();

            foreach (var path in jsonPaths)
            {
                var item = Instantiate(fileItemPrefab, fileItemContainer);
                item.GetComponent<FileListItem>().SetInfo(new FileItemInfo(false, path));
                var btn = item.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.onClick.AddListener(() => OnFileSelected(path));
            }

            if (emptyMessageText != null) emptyMessageText.gameObject.SetActive(jsonPaths.Count == 0);
        }

        public void OnFileSelected(string jsonPath)
        {
            try
            {
                var json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                NoteEditor.Model.EditDataSerializer.Deserialize(json);
                NoteEditor.Model.EditData.Name.Value = Path.GetFileNameWithoutExtension(jsonPath);
                NoteEditor.Model.EditData.IsDirty.Value = false;
                ProvideSyntheticAudioClip();
                NoteEditor.Model.Audio.OnLoad.OnNext(UniRx.Unit.Default);
                Debug.Log($"[FlashBeatSongLoader] Loaded: {jsonPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FlashBeatSongLoader] Failed to load {jsonPath}: {ex.Message}");
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
