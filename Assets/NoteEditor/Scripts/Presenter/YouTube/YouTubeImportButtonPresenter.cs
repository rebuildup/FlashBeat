using UnityEngine;
using UnityEngine.UI;

namespace NoteEditor.Presenter.YouTube
{
    public class YouTubeImportButtonPresenter : MonoBehaviour
    {
        [SerializeField]
        Button importButton = default;

        void Awake()
        {
            if (importButton != null)
            {
                importButton.onClick.AddListener(YouTubeImportDialog.Open);
            }
        }
    }
}
