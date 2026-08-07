using UnityEngine;
using UnityEngine.UI;

namespace NoteEditor.Presenter.YouTube
{
    public class YouTubeImportButtonPresenter : MonoBehaviour
    {
        public delegate void ImportHandlerDelegate();

        public static ImportHandlerDelegate ImportHandler;

        [SerializeField]
        Button importButton = default;

        void Awake()
        {
            if (importButton != null)
            {
                importButton.onClick.AddListener(OnClick);
            }
        }

        void OnDestroy()
        {
            if (importButton != null)
            {
                importButton.onClick.RemoveListener(OnClick);
            }
        }

        void OnClick()
        {
            ImportHandler?.Invoke();
        }
    }
}
