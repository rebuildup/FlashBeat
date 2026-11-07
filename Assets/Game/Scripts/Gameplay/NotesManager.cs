using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Data
{
    public string name;
    public int maxBlock;
    public int BPM;
    public int offset;
    public Note[] notes;
}

[Serializable]
public class Note
{
    public int type;
    public int num;
    public int block;
    public int LPB;

    /// <summary>
    /// ロングノーツ (type 2) の終端の行番号 (Issue #164)。
    ///
    /// タップ (type 1) では読まれない。存在しない場合 (壊れた譜面) は
    /// ロングノーツの最低長 (1 行) として扱う。
    /// </summary>
    public int endNum;
}

public class NotesManager : MonoBehaviour
{
    public int noteNum;//総ノーツ数
    private string songName;

    public List<int> LaneNum = new List<int>();
    public List<int> NoteType = new List<int>();
    public List<float> NotesTime = new List<float>();
    public List<GameObject> NotesObj = new List<GameObject>();

    /// <summary>
    /// ロングノーツの終端時刻 (秒) (Issue #164)。
    ///
    /// タップでは 0 を持つ。judge が保持状態を判断する時に使う。
    /// </summary>
    public List<float> NotesEndTime = new List<float>();

    //[SerializeField] public static float NotesSpeed = 1.0f;
    //[SerializeField] private float NotesSpeed = 1.0f;
    [SerializeField] GameObject noteObj;

    /// <summary>
    /// Time of a note on the chart timeline (seconds). The chart counts in
    /// beats: num / LPB beats at the given BPM. The formula is the contract
    /// between the chart and the judge, so it lives in one place and is pinned
    /// by ChartDataIntegrityTests.
    /// </summary>
    public static float NoteTimeSeconds(int bpm, Note note)
    {
        float kankaku = 60 / (bpm * (float)note.LPB);
        float beatSec = kankaku * (float)note.LPB;
        return beatSec * note.num / (float)note.LPB;
    }

    void OnEnable()
    {
        //NotesSpeed = GManager.noteSpeed;
        noteNum = 0;
        // Issue #183: 遷移開始時に固定したプレイセッションの曲を使う。遷移中の
        // カタログ更新・選択変更が入っても、譜面と曲情報・版がずれない。
        PlaySessionSnapshot session = PlaySessionSnapshot.Current;
        if (session != null) session.RestoreSongId();
        SongData song = PlaySessionSnapshot.ResolveSong(GManager.songID);
        if (song == null)
        {
            Debug.LogWarning($"[NotesManager] no song for id {GManager.songID} - playing without notes");
            return;
        }
        songName = song.title;
        Load(song);
    }

    private void Load(SongData song)
    {
        // Every run starts with an empty list: a retry must not keep the note
        // times of the previous attempt.
        GameSceneManager.SaveNotesTime.Clear();

        Data inputJson = null;
        if (PlaySessionSnapshot.TryGetChartJson(song, out string pinned))
        {
            // Issue #183: 遷移開始時に固定した譜面スナップショットを最優先で使う。
            inputJson = CloudCatalog.ParseChartResponse(pinned);
        }
        if (inputJson == null && song.isRemote && CloudChartHolder.TryGet(song.remoteId, song.chartVersion, out string held))
        {
            // Cloud Catalog (#17): the chart snapshot pinned at playlist start.
            inputJson = CloudCatalog.ParseChartResponse(held);
        }
        if (inputJson == null)
        {
            TextAsset asset = Resources.Load<TextAsset>(song.title);
            if (asset == null)
            {
                Debug.LogWarning($"[NotesManager] no chart for '{song.title}' - playing without notes");
                return;
            }
            inputJson = JsonUtility.FromJson<Data>(asset.text);
        }

        noteNum = inputJson.notes.Length;
        // ロングノーツも 1 ノーツとして数える。配点がタップと同一なので
        // (Issue #164) 100万点の算出は notes x 5 のままで正しい。
        GManager.maxScore = noteNum * 5;

        for (int i = 0; i < inputJson.notes.Length; i++)
        {
            Note note = inputJson.notes[i];
            float time = NoteTimeSeconds(inputJson.BPM, note) + GManager.noteTiming * 0.01f;
            NotesTime.Add(time);
            LaneNum.Add(note.block);
            NoteType.Add(note.type);
            GameSceneManager.SaveNotesTime.Add(time);

            // ロングノーツの終端 (Issue #164)。タップは 0 (終端なし)。
            if (note.type == FlashBeat.Gameplay.LongNote.TYPE_HOLD)
            {
                float endTime = FlashBeat.Gameplay.LongNote.EndSeconds(inputJson.BPM, note) + GManager.noteTiming * 0.01f;
                // 終端は開始より後になければならない。壊れた譜面でも
                // 判定が破綻しないように、最低 1 拍ぶんは最低長を保つ。
                if (endTime <= time)
                {
                    endTime = time + (60f / inputJson.BPM);
                }
                NotesEndTime.Add(endTime);
            }
            else
            {
                NotesEndTime.Add(0f);
            }

            float z = NotesTime[i] * GManager.noteSpeed;
            GameObject instance = Instantiate(noteObj, new Vector3(note.block - 3.5f, 0.55f, z), Quaternion.identity);

            // ロングノーツは体を引き伸ばす (Issue #164)。タップは長さを
            // 持たない (0) ので、Notes は何も変えない。
            if (note.type == FlashBeat.Gameplay.LongNote.TYPE_HOLD)
            {
                Notes notes = instance.GetComponent<Notes>();
                if (notes != null)
                {
                    notes.holdSeconds = NotesEndTime[i] - NotesTime[i];
                }
            }

            NotesObj.Add(instance);

        }
        for (int i = 0; i < 10; i++)
        {
            NotesTime.Add(10000.0f);
            LaneNum.Add(1);
            NoteType.Add(1);
            NotesEndTime.Add(0f);
            GameSceneManager.SaveNotesTime.Add(1000000f);
        }

    }

}
