using NoteEditor.Common;
using NoteEditor.Model;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace NoteEditor.Presenter
{
    public class MusicSelectorPresenter : MonoBehaviour
    {
        [SerializeField]
        InputField directoryPathInputField = default;
        [SerializeField]
        GameObject fileItemPrefab = default;
        [SerializeField]
        GameObject fileItemContainer = default;
        [SerializeField]
        Transform fileItemContainerTransform = default;
        [SerializeField]
        Button redoButton = default;
        [SerializeField]
        Button undoButton = default;
        [SerializeField]
        Button loadButton = default;
        [SerializeField]
        MusicLoader musicLoader = default;

        void Start()
        {
            // Disabled: replaced by FlashBeatSongLoader.
            // Kept as a no-op so existing scene references don't NRE.
        }
    }
}
