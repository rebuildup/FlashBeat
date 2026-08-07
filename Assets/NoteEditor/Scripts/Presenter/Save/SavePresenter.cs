using NoteEditor.Model;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.IO;
using System.Text;

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
            var editPresenter = EditNotesPresenter.Instance;

            this.UpdateAsObservable()
                .Where(_ => Input.GetKeyDown(KeyCode.Escape))
                .Subscribe(_ => Application.Quit());

            var saveActionObservable = this.UpdateAsObservable()
                .Where(_ => KeyInput.CtrlPlus(KeyCode.S))
                .Merge(saveButton.OnClickAsObservable());

            mustBeSaved = Observable.Merge(
                    EditData.BPM.Select(_ => true),
                    EditData.OffsetSamples.Select(_ => true),
                    EditData.MaxBlock.Select(_ => true),
                    editPresenter.RequestForEditNote.Select(_ => true),
                    editPresenter.RequestForAddNote.Select(_ => true),
                    editPresenter.RequestForRemoveNote.Select(_ => true),
                    editPresenter.RequestForChangeNoteStatus.Select(_ => true),
                    EditData.Lyrics.Mondai.Select(_ => true),
                    saveActionObservable.Select(_ => false),
                    EditData.IsDirty.Select(_ => EditData.IsDirty.Value))
                .Do(unsaved => saveButton.GetComponent<Image>().color = unsaved ? unsavedStateButtonColor : savedStateButtonColor)
                .ToReactiveProperty();

            mustBeSaved.SubscribeToText(messageText, unsaved => unsaved ? "保存が必要な状態" : "");

            saveActionObservable.Subscribe(_ => Save());

            dialogSaveButton.AddListener(
                EventTriggerType.PointerClick,
                (e) =>
                {
                    mustBeSaved.Value = false;
                    saveDialog.SetActive(false);
                    Save();
                    Application.Quit();
                });

            dialogDoNotSaveButton.AddListener(
                EventTriggerType.PointerClick,
                (e) =>
                {
                    mustBeSaved.Value = false;
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

        const string ResourcesDir = "Assets/Game/Resources";

        public void Save()
        {
            try
            {
                var songName = Path.GetFileNameWithoutExtension(EditData.Name.Value);
                if (string.IsNullOrEmpty(songName))
                {
                    Debug.LogError("[SavePresenter] EditData.Name is empty");
                    if (messageText != null) messageText.text = "曲名が空のため保存できません";
                    return;
                }
                var path = Path.Combine(ResourcesDir, songName + ".json");
                if (!Directory.Exists(ResourcesDir)) Directory.CreateDirectory(ResourcesDir);
                var json = EditDataSerializer.Serialize();
                File.WriteAllText(path, json, Encoding.UTF8);
                EditData.IsDirty.Value = false;
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
