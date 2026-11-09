using NoteEditor.Model;
using NoteEditor.Utility;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NoteEditor.Presenter
{
    public class SavePresenter : MonoBehaviour
    {
        [SerializeField]
        Button saveButton = default;
        [SerializeField]
        Text messageText = default;
        [SerializeField]
        Color unsavedStateButtonColor = default;
        [SerializeField]
        Color savedStateButtonColor = Color.white;

        [SerializeField]
        GameObject saveDialog = default;
        [SerializeField]
        Button dialogSaveButton = default;
        [SerializeField]
        Button dialogDoNotSaveButton = default;
        [SerializeField]
        Button dialogCancelButton = default;
        [SerializeField]
        Text dialogMessageText = default;

        ReactiveProperty<bool> mustBeSaved = new ReactiveProperty<bool>();

        void Awake()
        {
            this.UpdateAsObservable()
                .Where(_ => Input.GetKeyDown(KeyCode.Escape))
                .Subscribe(_ => Application.Quit());

            var saveActionObservable = this.UpdateAsObservable()
                .Where(_ => KeyInput.CtrlPlus(KeyCode.S))
                .Merge(saveButton.OnClickAsObservable());

            // 「未保存の変更」の notion は EditData.IsDirty 1 箇所に集約する
            // (Issue #225 / 独立レビュー S-5)。
            //
            // ノーツ編集は NoteEditDirtyTracker、歌詞編集は LyricsTabPresenter、
            // BPM / MaxBlock / offset の編集は EditData 自身が IsDirty を立てる。
            // 以前は 2 つの notion (mustBeSaved と IsDirty) が食い違い、
            // 曲切替の確認をすり抜ける可能性があった。
            mustBeSaved = EditData.IsDirty
                .Do(unsaved => saveButton.GetComponent<Image>().color = unsaved ? unsavedStateButtonColor : savedStateButtonColor)
                .ToReactiveProperty();

            mustBeSaved.SubscribeToText(messageText, unsaved => unsaved ? "保存が必要な状態" : "");

            saveActionObservable.Subscribe(_ => Save());

            dialogSaveButton.AddListener(
                EventTriggerType.PointerClick,
                (e) =>
                {
                    // 保存は ChartFileWriter が行い、成功時に IsDirty を落とす。
                    // 保存に失敗した場合は IsDirty が残るので、終了確認が再度出る。
                    saveDialog.SetActive(false);
                    Save();
                    Application.Quit();
                });

            dialogDoNotSaveButton.AddListener(
                EventTriggerType.PointerClick,
                (e) =>
                {
                    // 変更を破棄するので「保存済み」扱いにする。
                    EditData.IsDirty.Value = false;
                    saveDialog.SetActive(false);
                    Application.Quit();
                });

            dialogCancelButton.AddListener(
                EventTriggerType.PointerClick,
                (e) =>
                {
                    saveDialog.SetActive(false);
                });

            Application.wantsToQuit += ApplicationQuit;
        }

        bool ApplicationQuit()
        {
            if (mustBeSaved.Value)
            {
                dialogMessageText.text = "Do you want to save the changes you made in the note '"
                    + EditData.Name.Value + "' ?" + System.Environment.NewLine
                    + "Your changes will be lost if you don't save them.";
                saveDialog.SetActive(true);
                return false;
            }

            return true;
        }

        // 公開中の Resources は ChartFileWriter に集約する。
        // 保存先は必ず「いま編集中の曲」の JSON になり、
        // 別の曲の JSON を上書きすることがない (Issue #225)。
        public void Save()
        {
            try
            {
                var path = ChartFileWriter.SaveCurrentSong(ChartFileWriter.ResourcesDir);
                Debug.Log($"[NoteEditor] Saved: {path}");
                if (messageText != null) messageText.text = path + " に保存しました";
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NoteEditor] Save failed: {ex.Message}");
                if (messageText != null) messageText.text = "保存に失敗しました: " + ex.Message;
            }
        }
    }
}
