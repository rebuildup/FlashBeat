using System.Collections.Generic;

namespace NoteEditor.DTO
{
    public class MusicDTO
    {
        [System.Serializable]
        public class EditData
        {
            public string name;
            public int maxBlock;
            public int BPM;
            public int offset;

            // Issue #383: 実行時の曲終了・開始待ち・拍補正。0 は未指定 (既定値) を意味する。
            public float fadeOut;
            public float startTiming;
            public float beatOffset;
            public string videoId;
            public List<Note> notes;
            public LyricsDTO lyrics;
        }

        [System.Serializable]
        public class Note
        {
            public int LPB;
            public int num;
            public int block;
            public int type;
            public List<Note> notes;
        }

        [System.Serializable]
        public class LyricsDTO
        {
            public float[] startTime;
            public string[] furigana;
            public string[] mondai;
            public string[] romaji;
            public float[] endTime;
        }
    }
}
