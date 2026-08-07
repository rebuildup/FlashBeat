using NoteEditor.Presenter.YouTube;
using UnityEditor;

namespace NoteEditor.Editor
{
    [InitializeOnLoad]
    public static class YouTubeImportRegistrar
    {
        static YouTubeImportRegistrar()
        {
            YouTubeImportButtonPresenter.ImportHandler = YouTubeImportDialog.Open;
        }
    }
}
