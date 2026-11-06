using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
}

public class NotesManager : MonoBehaviour
{
    public int noteNum;//総ノーツ数
    private string songName;

    public List<int> LaneNum = new List<int>();
    public List<int> NoteType = new List<int>();
    public List<float> NotesTime = new List<float>();
    public List<GameObject> NotesObj = new List<GameObject>();

    //[SerializeField] public static float NotesSpeed = 1.0f;
    //[SerializeField] private float NotesSpeed = 1.0f;
    [SerializeField] GameObject noteObj;

    /// <summary>
    /// 譜面の解析とノーツ生成が完了したか。MusicManager が Space での開始を
    /// 許可する条件に使う (譜面が無い場合も「準備完了」になる)。
    /// </summary>
    public bool IsReady { get; private set; }

    /// <summary>1 フレームあたりのノーツ生成の時間予算 (ミリ秒)。</summary>
    internal const double InstantiateBudgetMs = 8.0;

    /// <summary>LoadingOverlay でのオーナーキー。</summary>
    internal const string OverlayOwner = "notes";

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
        SongData song = GManager.ActiveSongs[GManager.songID];
        songName = song.title;
        IsReady = false;
        StartCoroutine(LoadRoutine(song));
    }

    /// <summary>
    /// 譜面読み込みを段階的に進める (旧実装は OnEnable で全ノーツを同期生成し、
    /// 大きい譜面でフレームが固まった)。
    /// 1. JSON の解析とノーツ時刻の計算 (純粋な数値処理、1 フレーム以内)
    /// 2. ノーツ prefab の生成を 1 フレームあたり InstantiateBudgetms 以下の
    ///    バッチに分けて実行し、進捗を LoadingOverlay に出す
    /// MusicManager は IsReady になるまで Space での開始を許可しないので、
    /// 判定タイミングへの影響はない。
    /// </summary>
    IEnumerator LoadRoutine(SongData song)
    {
        // Every run starts with an empty list: a retry must not keep the note
        // times of the previous attempt.
        GameSceneManager.SaveNotesTime.Clear();

        var parseStopwatch = Stopwatch.StartNew();
        Data inputJson = ParseChart(song);
        long parseMs = parseStopwatch.ElapsedMilliseconds;

        if (inputJson == null)
        {
            Debug.LogWarning($"[NotesManager] no chart for '{song.title}' - playing without notes");
            IsReady = true;
            yield break;
        }

        int total = inputJson.notes.Length;
        noteNum = total;
        GManager.maxScore = total * 5;

        // 時刻計算は純粋な数値処理なので一括で行う (生成は後続のバッチで)。
        var times = new List<float>(total);
        var lanes = new List<int>(total);
        var types = new List<int>(total);
        var positionsZ = new List<float>(total);
        for (int i = 0; i < total; i++)
        {
            float time = NoteTimeSeconds(inputJson.BPM, inputJson.notes[i]) + GManager.noteTiming * 0.01f;
            times.Add(time);
            lanes.Add(inputJson.notes[i].block);
            types.Add(inputJson.notes[i].type);
            positionsZ.Add(time * GManager.noteSpeed);
            GameSceneManager.SaveNotesTime.Add(time);
        }

        Debug.Log($"[Perf] notes parse {parseMs}ms ({total} notes)");

        // ノーツ生成: 1 フレームあたり時間予算つきのバッチで少しずつ。
        var batcher = new TimeBudgetBatcher(InstantiateBudgetMs);
        var instantiateStopwatch = Stopwatch.StartNew();
        LoadingOverlay.Show("譜面を準備中…", OverlayOwner);
        int done = 0;
        while (done < total)
        {
            int batch = batcher.NextBatchSize(total - done);
            var batchStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < batch; i++)
            {
                int index = done + i;
                NotesTime.Add(times[index]);
                LaneNum.Add(lanes[index]);
                NoteType.Add(types[index]);
                NotesObj.Add(Instantiate(noteObj, new Vector3(lanes[index] - 3.5f, 0.55f, positionsZ[index]), Quaternion.identity));
            }
            batchStopwatch.Stop();
            batcher.Report(batch, batchStopwatch.Elapsed.TotalSeconds);
            done += batch;
            LoadingOverlay.SetProgress(LoadingOverlayLogic.MapProgress((float)done / total), OverlayOwner);
            if (done < total) yield return null;
        }
        LoadingOverlay.Hide(OverlayOwner);

        for (int i = 0; i < 10; i++)
        {
            NotesTime.Add(10000.0f);
            LaneNum.Add(1);
            NoteType.Add(1);
            GameSceneManager.SaveNotesTime.Add(1000000f);
        }

        Debug.Log($"[Perf] notes instantiate {total} notes in {instantiateStopwatch.ElapsedMilliseconds}ms");
        IsReady = true;
    }

    Data ParseChart(SongData song)
    {
        if (song.isRemote && CloudChartHolder.TryGet(song.remoteId, song.chartVersion, out string held))
        {
            // Cloud Catalog (#17): the chart snapshot pinned at playlist start.
            return CloudCatalog.ParseChartResponse(held);
        }
        TextAsset asset = Resources.Load<TextAsset>(song.title);
        if (asset == null) return null;
        return JsonUtility.FromJson<Data>(asset.text);
    }
}
