using NoteEditor.Model;
using NoteEditor.Notes;
using System.Collections;
using System.IO;
using UniRx;
using UnityEngine;

namespace NoteEditor.Presenter
{
    public class MusicLoader : MonoBehaviour
    {
        void Awake()
        {
            ResetEditor();
        }

        public void Load(string fileName)
        {
            StartCoroutine(LoadMusic(fileName));
        }

        IEnumerator LoadMusic(string fileName)
        {
            Debug.LogWarning($"[MusicLoader.LoadMusic] is deprecated. Use FlashBeatSongLoader.OnFileSelected instead. (fileName={fileName})");
            yield break;
        }

        void LoadEditData()
        {
            var fileName = Path.ChangeExtension(EditData.Name.Value, "json");
            var directoryPath = Path.Combine(Path.GetDirectoryName(MusicSelector.DirectoryPath.Value), "Notes");
            var filePath = Path.Combine(directoryPath, fileName);

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                EditDataSerializer.Deserialize(json);
            }
        }

        public void ResetEditor()
        {
            Audio.TimeSamples.Value = 0;
            Audio.SmoothedTimeSamples.Value = 0;
            Audio.IsPlaying.Value = false;
            Audio.Source.clip = null;
            EditState.NoteType.Value = NoteTypes.Single;
            EditState.LongNoteTailPosition.Value = NotePosition.None;
            EditData.BPM.Value = 120;
            EditData.OffsetSamples.Value = 0;
            EditData.Name.Value = "Note Editor";
            EditData.MaxBlock.Value = Settings.MaxBlock;
            EditData.LPB.Value = 4;

            foreach (var note in EditData.Notes.Values)
            {
                note.Dispose();
            }

            EditData.Notes.Clear();
            Resources.UnloadUnusedAssets();
        }
    }
}
