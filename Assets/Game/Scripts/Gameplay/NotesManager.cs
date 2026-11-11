using System;
using System.Collections;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;
using FlashBeat.Gameplay;

[Serializable]
public class Data
{
    public string name;
    public int maxBlock;
    public int BPM;
    /// <summary>NoteEditor が書き出す音声アラインメント (サンプル単位)。
    /// 実行時の判定には使わない。拍補正は beatOffset (Issue #383)。</summary>
    public int offset;

    /// <summary>メディア終端の何秒前からフェードを始めるか。0/欠損は既定 2.0 秒。</summary>
    public float fadeOut;

    /// <summary>開始入力後、音声と譜面を始めるまでの待ち時間 (秒)。</summary>
    public float startTiming;

    /// <summary>BPM グリッドと音源のずれ補正 (秒)。全ノート時刻に加算する。</summary>
    public float beatOffset;

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
    /// 譜面の解析とノーツ生成が完了したか。MusicManager が Space での開始を
    /// 許可する条件に使う (譜面が無い場合も「準備完了」になる)。
    /// </summary>
    public bool IsReady { get; private set; }

    /// <summary>
    /// 読み込んだ譜面の時間設定 (Issue #383)。MusicManager が終端フェードと
    /// 開始待ちに使う。譜面が無いとき・読み込み前は既定値。
    /// </summary>
    public static ChartTiming Timing { get; private set; } = ChartTiming.Default;

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

    /// <summary>
    /// 譜面上の時刻へ判定幅 (NoteTiming) を加えた時刻 (秒) (Issue #226)。
    ///
    /// NoteTiming は 0.01 秒単位のオフセットで、**負の値も有効**である
    /// (負 = 少し早く判定する。上下限は OptionSettings.NoteTimingMin/Max が持つ)。
    /// 開始と終端のどちらにも同じオフセットを適用するので、式は 1 箇所に置く。
    /// </summary>
    public static float NoteTimeWithTiming(float baseTime, int noteTiming)
    {
        return baseTime + noteTiming * 0.01f;
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
        IsReady = false;
        // 前の曲の時間設定を持ち越さない (Issue #383)。
        Timing = ChartTiming.Default;
        // v6 ローディングの実進捗を「不明」に戻してから読み直す (Issue #224)。
        FlashBeat.UI.V6.Game.Loading.GameLoadingProgress.ResetNotes();
        if (song == null)
        {
            Debug.LogWarning($"[NotesManager] no song for id {GManager.songID} - playing without notes");
            // 譜面が無い場合も「準備完了」にする。MusicManager は IsReady が
            // 立つまで Space での開始を許可しないので、ここで立てないと開始できない。
            IsReady = true;
            return;
        }
        songName = song.title;
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
        // セッションの z スケールを確定する (PR #263 レビュー S7)。
        // 生成・時刻復元・移動・HOLD の胴体長が同じ値を使うので、曲中に
        // 速度オプションが変わってもノーツが飛びも復元値もずれない。
        FlashBeat.Gameplay.MediaTimeline.BeginSession(GManager.noteSpeed);

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

        Timing = ChartTiming.Normalize(
            inputJson.fadeOut, inputJson.startTiming, inputJson.beatOffset, song.duration);

        int total = inputJson.notes.Length;
        noteNum = total;
        // ロングノーツも 1 ノーツとして数える。配点がタップと同一なので
        // (Issue #164) 100万点の算出は notes x 5 のままで正しい。
        GManager.maxScore = total * 5;

        // 時刻計算は純粋な数値処理なので一括で行う (生成は後続のバッチで)。
        var times = new List<float>(total);
        var lanes = new List<int>(total);
        var types = new List<int>(total);
        var positionsZ = new List<float>(total);
        var endTimes = new List<float>(total);
        for (int i = 0; i < total; i++)
        {
            Note note = inputJson.notes[i];
            float time = NoteTimeWithTiming(NoteTimeSeconds(inputJson.BPM, note), GManager.noteTiming)
                + Timing.BeatOffset;
            times.Add(time);
            lanes.Add(note.block);
            types.Add(note.type);
            // The world z of a note is (noteTime - mediaTime) * zScale. The
            // scale is the same one Notes uses to move the note and to recover
            // its time, so the note reaches the judge line exactly at its own
            // judge time (Issue #233). zScale is pinned to the camera, so the
            // visible approach time is the designed 1.6 s at note speed 10.
            //
            // CurrentZScale is the single entry point for the scale
            // (generation / recovery / movement must never read the note
            // speed option directly - one note = one judgement).
            //
            // セッションごとに確定した値 (SessionZScale) を使う: 生成した
            // 位置から時刻を復元するときも、移動するときも同じ値なので、
            // 「判定する時刻」と「描画される時刻」がずれない。
            positionsZ.Add(time * FlashBeat.Gameplay.MediaTimeline.SessionZScale);
            GameSceneManager.SaveNotesTime.Add(time);

            // ロングノーツの終端 (Issue #164)。タップは 0 (終端なし)。
            if (note.type == FlashBeat.Gameplay.LongNote.TYPE_HOLD)
            {
                float endTime = NoteTimeWithTiming(FlashBeat.Gameplay.LongNote.EndSeconds(inputJson.BPM, note), GManager.noteTiming)
                    + Timing.BeatOffset;
                // 終端は開始より後になければならない。壊れた譜面でも
                // 判定が破綻しないように、最低 1 拍ぶんは最低長を保つ。
                if (endTime <= time)
                {
                    endTime = time + (60f / inputJson.BPM);
                }
                endTimes.Add(endTime);
            }
            else
            {
                endTimes.Add(0f);
            }
        }

        Debug.Log($"[Perf] notes parse {parseMs}ms ({total} notes)");

        // ノーツ生成: 1 フレームあたり時間予算つきのバッチで少しずつ。
        // 表示は所有者に従う: v6 のゲーム HUD が有効なら V6LoadingView が
        // 曲の準備 (メディア + 譜面) を単独で表示するので、旧 LoadingOverlay は出さない
        // (2 枚重なると読み込み画面が二重になる)。v6 無効時のみ旧経路で出す。
        bool showLegacyOverlay = GameLoadingRoute.UsesLegacyOverlay;
        var batcher = new TimeBudgetBatcher(InstantiateBudgetMs);
        var instantiateStopwatch = Stopwatch.StartNew();
        if (showLegacyOverlay) LoadingOverlay.Show("譜面を準備中…", OverlayOwner);
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
                NotesEndTime.Add(endTimes[index]);
                GameObject instance = Instantiate(noteObj, new Vector3(Judge.LaneWorldX(lanes[index]), 0.55f, positionsZ[index]), Quaternion.identity);

                // ロングノーツは体を引き伸ばす (Issue #164)。タップは長さを
                // 持たない (0) ので、Notes は何も変えない。
                if (types[index] == FlashBeat.Gameplay.LongNote.TYPE_HOLD)
                {
                    Notes notes = instance.GetComponent<Notes>();
                    if (notes != null)
                    {
                        notes.holdSeconds = NotesEndTime[index] - NotesTime[index];
                    }
                }

                NotesObj.Add(instance);
            }
            batchStopwatch.Stop();
            batcher.Report(batch, batchStopwatch.Elapsed.TotalSeconds);
            done += batch;
            FlashBeat.UI.V6.Game.Loading.GameLoadingProgress.ReportNotes((float)done / total);
            if (showLegacyOverlay)
            {
                LoadingOverlay.SetProgress(LoadingOverlayLogic.MapProgress((float)done / total), OverlayOwner);
            }
            if (done < total) yield return null;
        }
        if (showLegacyOverlay) LoadingOverlay.Hide(OverlayOwner);

        for (int i = 0; i < 10; i++)
        {
            NotesTime.Add(10000.0f);
            LaneNum.Add(1);
            NoteType.Add(1);
            NotesEndTime.Add(0f);
            GameSceneManager.SaveNotesTime.Add(1000000f);
        }

        Debug.Log($"[Perf] notes instantiate {total} notes in {instantiateStopwatch.ElapsedMilliseconds}ms");
        IsReady = true;
    }

    Data ParseChart(SongData song)
    {
        // Issue #183: 遷移開始時に固定した譜面スナップショットを最優先で使う。
        if (PlaySessionSnapshot.TryGetChartJson(song, out string pinned))
        {
            return CloudCatalog.ParseChartResponse(pinned);
        }
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