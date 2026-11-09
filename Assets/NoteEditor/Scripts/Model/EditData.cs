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

        // BPM / MaxBlock / OffsetSamples の変更も dirty として扱うかどうかのフラグ。
        bool dirtyTrackingStarted_;

        public static ReactiveProperty<string> Name { get { return Instance.name_; } }
        public static ReactiveProperty<int> MaxBlock { get { var instance = Instance; instance.EnsureDirtyTracking(); return instance.maxBlock_; } }
        public static ReactiveProperty<int> LPB { get { return Instance.LPB_; } }
        public static ReactiveProperty<int> BPM { get { var instance = Instance; instance.EnsureDirtyTracking(); return instance.BPM_; } }
        public static ReactiveProperty<int> OffsetSamples { get { var instance = Instance; instance.EnsureDirtyTracking(); return instance.offsetSamples_; } }
        public static ReactiveProperty<string> VideoId { get { return Instance.videoId_; } }
        public static ReactiveProperty<bool> IsDirty { get { return Instance.isDirty_; } }
        public static Dictionary<NotePosition, NoteObject> Notes { get { return Instance.notes_; } }

        // 「未保存の変更」の notion を IsDirty 1 箇所に集約する (Issue #225 / 独立レビュー S-5)。
        //
        // ノーツ編集は NoteEditDirtyTracker、歌詞編集は LyricsTabPresenter が
        // IsDirty を立てる。BPM / MaxBlock / OffsetSamples の変更も
        // 曲切替の確認 (保存 / 破棄 / キャンセル) 対象なので、ここでも立てる。
        //
        // 購読は遅延開始 (初回アクセス時)。ReactiveProperty は購読時に
        // 現在の値を 1 度 emit するため、Skip(1) で初期値を「変更」から外す。
        void EnsureDirtyTracking()
        {
            if (dirtyTrackingStarted_)
                return;

            dirtyTrackingStarted_ = true;

            BPM_.Skip(1).Subscribe(_ => isDirty_.Value = true);
            maxBlock_.Skip(1).Subscribe(_ => isDirty_.Value = true);
            offsetSamples_.Skip(1).Subscribe(_ => isDirty_.Value = true);
        }

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
