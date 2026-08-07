using NoteEditor.Notes;
using NoteEditor.Utility;
using System.Collections.Generic;
using UniRx;

namespace NoteEditor.Model
{
    public class EditData : SingletonMonoBehaviour<EditData>
    {
        ReactiveProperty<string> name_ = new ReactiveProperty<string>();
        ReactiveProperty<int> maxBlock_ = new ReactiveProperty<int>(5);
        ReactiveProperty<int> LPB_ = new ReactiveProperty<int>(4);
        ReactiveProperty<int> BPM_ = new ReactiveProperty<int>(120);
        ReactiveProperty<int> offsetSamples_ = new ReactiveProperty<int>(0);
        ReactiveProperty<string> videoId_ = new ReactiveProperty<string>("");
        ReactiveProperty<bool> isDirty_ = new ReactiveProperty<bool>(false);
        Dictionary<NotePosition, NoteObject> notes_ = new Dictionary<NotePosition, NoteObject>();

        public static ReactiveProperty<string> Name { get { return Instance.name_; } }
        public static ReactiveProperty<int> MaxBlock { get { return Instance.maxBlock_; } }
        public static ReactiveProperty<int> LPB { get { return Instance.LPB_; } }
        public static ReactiveProperty<int> BPM { get { return Instance.BPM_; } }
        public static ReactiveProperty<int> OffsetSamples { get { return Instance.offsetSamples_; } }
        public static ReactiveProperty<string> VideoId { get { return Instance.videoId_; } }
        public static ReactiveProperty<bool> IsDirty { get { return Instance.isDirty_; } }
        public static Dictionary<NotePosition, NoteObject> Notes { get { return Instance.notes_; } }

        public static int AudioFrequency()
        {
            return Audio.Source.clip != null ? Audio.Source.clip.frequency : 44100;
        }

        public static class Lyrics
        {
            public static ReactiveProperty<float[]> StartTime = new ReactiveProperty<float[]>(new float[0]);
            public static ReactiveProperty<string[]> Furigana = new ReactiveProperty<string[]>(new string[0]);
            public static ReactiveProperty<string[]> Mondai = new ReactiveProperty<string[]>(new string[0]);
            public static ReactiveProperty<string[]> Romaji = new ReactiveProperty<string[]>(new string[0]);
            public static ReactiveProperty<float[]> EndTime = new ReactiveProperty<float[]>(new float[0]);
        }
    }
}
