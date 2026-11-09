using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;

/// <summary>
/// Issue #226 の受入条件テストが共有する「実機と同じ経路」の譜面リグ。
///
/// 作り方は <see cref="JudgeSettlementInvariantTests"/> と同じ:
///
///   1. 譜面 JSON を **本番と同じ読み込み経路** (<c>NotesManager.LoadRoutine</c>)
///      で作る (GManager.maxScore = ノーツ数 x 5 の書き込みも本番のコードが行う)。
///   2. <see cref="Judge.JudgeStep"/> を直接叩いて判定させる
///      (テスト側で判定を再現しない = false green を避ける)。
///   3. 曲終了の期限 (<c>SettleDeadline</c>) は **本番の計算**
///      (<c>MusicManager.ComputeSettleDeadline</c>) をリフレクションで呼んで得る。
///
/// 3 つ目だけがテスト側の式だと「テストは通るが本番は違う」状態になり得るので、
/// 必ず本番の private メソッドを呼ぶ。
/// </summary>
public static class SongEndAcceptanceRig
{
    public const int Lanes = 8;

    /// <summary>組み立てた譜面と Judge の参照。</summary>
    public sealed class Rig
    {
        public GameObject Root;
        public Judge Judge;
        public NotesManager Notes;

        public readonly List<int> NoteIndices = new List<int>();
        public readonly List<float> NoteTime = new List<float>();
        public readonly List<float> NoteEndTime = new List<float>();
        public readonly List<int> NoteLane = new List<int>();
        public readonly List<int> NoteType = new List<int>();

        /// <summary>判定キュー (Judge の private フィールド)。</summary>
        public JudgeQueue Queue
        {
            get
            {
                FieldInfo field = typeof(Judge).GetField("queue", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(field, "Judge.queue が見つからない");
                return (JudgeQueue)field.GetValue(Judge);
            }
        }
    }

    /// <summary>
    /// 譜面と Judge / NotesManager を組み立てる。
    /// 呼び出し前に GManager の判定カウンタを 0 にしておくこと
    /// (<see cref="SongEndAcceptanceTests.ResetSessionState"/> と同じ手順)。
    /// </summary>
    public static Rig BuildRig(string chartJson, int expectedNoteCount, string songTitle = "w226-acceptance")
    {
        Data chart = CloudCatalog.ParseChartResponse(
            "{\"songId\":\"w226-test\",\"chartVersion\":1,\"chart\":" + chartJson + "}");
        Assert.IsNotNull(chart, "譜面が読めない");
        Assert.AreEqual(expectedNoteCount, chart.notes.Length, "譜面のノーツ数が違う");

        GameObject root = new GameObject("SongEndAcceptanceRig");
        // 非アクティブ: OnEnable (譜面の読み込みとノーツ生成) が走らないようにする。
        root.SetActive(false);

        NotesManager notes = root.AddComponent<NotesManager>();
        Judge judge = root.AddComponent<Judge>();

        Rig rig = new Rig { Root = root, Judge = judge, Notes = notes };

        LoadChartThroughProduction(root, notes, chartJson, songTitle);

        for (int i = 0; i < chart.notes.Length; i++)
        {
            Note note = chart.notes[i];
            float time = NotesManager.NoteTimeSeconds(chart.BPM, note) + GManager.noteTiming * 0.01f;
            float endTime = 0f;
            if (note.type == LongNote.TYPE_HOLD)
            {
                endTime = LongNote.EndSeconds(chart.BPM, note) + GManager.noteTiming * 0.01f;
                if (endTime <= time) endTime = time + (60f / chart.BPM);
            }

            // 本番が作った時刻と一致すること (読み込み経路の検証)。
            Assert.AreEqual(time, notes.NotesTime[i], 0.0001f, $"ノーツ {i} の時刻が違う");
            Assert.AreEqual(note.block, notes.LaneNum[i], $"ノーツ {i} のレーンが違う");
            Assert.AreEqual(note.type, notes.NoteType[i], $"ノーツ {i} の種別が違う");
            Assert.AreEqual(endTime, notes.NotesEndTime[i], 0.0001f, $"ノーツ {i} の終端時刻が違う");

            rig.NoteIndices.Add(i);
            rig.NoteTime.Add(time);
            rig.NoteEndTime.Add(endTime);
            rig.NoteLane.Add(note.block);
            rig.NoteType.Add(note.type);
        }

        // Judge の private フィールド (譜面の参照とエフェクトの配列) を差し込む。
        SetPrivateField(judge, "notesManager", notes);
        SetPrivateField(judge, "MessageObj", new GameObject[4]);

        return rig;
    }

    /// <summary>本番の読み込み経路 (NotesManager.LoadRoutine) で譜面を読み込む。</summary>
    private static void LoadChartThroughProduction(GameObject root, NotesManager target, string chartJson, string songTitle)
    {
        var song = new SongData(9002, songTitle, "tester", "testVideo", 120, 5, 0, 60f);
        PlaySessionSnapshot.Begin(song, 1,
            "{\"songId\":\"w226-test\",\"chartVersion\":1,\"chart\":" + chartJson + "}");

        // ノーツ prefab: LoadRoutine が Instantiate する。null だと破碎するので
        // テスト用の空オブジェクトを差し込む。
        GameObject notePrefab = new GameObject("W226TestNotePrefab");
        notePrefab.transform.SetParent(root.transform);
        SetPrivateField(target, "noteObj", notePrefab);

        // 旧ローディング画面 (uGUI) を出さないようにする (UI 副作用を避ける)。
        bool savedGameHud = FlashBeat.UI.V6.V6UiMode.GameHud;
        FlashBeat.UI.V6.V6UiMode.GameHud = true;
        try
        {
            MethodInfo load = typeof(NotesManager).GetMethod("LoadRoutine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(load, "NotesManager.LoadRoutine が見つからない");

            var routine = load.Invoke(target, new object[] { PlaySessionSnapshot.Current.Song }) as IEnumerator;
            Assert.IsNotNull(routine, "LoadRoutine のコルーチンが取得できない");

            while (routine.MoveNext())
            {
            }
        }
        finally
        {
            FlashBeat.UI.V6.V6UiMode.GameHud = savedGameHud;
        }
    }

    /// <summary>
    /// 本番の「最後のノーツが確定する時刻」(<c>MusicManager.ComputeSettleDeadline</c>)。
    /// private メソッドをリフレクションで呼ぶ (テスト側の式にしない)。
    /// </summary>
    public static float ProductionSettleDeadline(NotesManager notes)
    {
        GameObject root = new GameObject("SettleDeadlineProbe");
        root.SetActive(false);
        try
        {
            MusicManager music = root.AddComponent<MusicManager>();
            SetPrivateField(music, "notesManager", notes);

            MethodInfo method = typeof(MusicManager).GetMethod("ComputeSettleDeadline",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "MusicManager.ComputeSettleDeadline が見つからない");

            var value = method.Invoke(music, null);
            Assert.IsNotNull(value, "ComputeSettleDeadline が値を返さない");
            return (float)value;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// 本番の曲終了の破綻経路 (<c>MusicManager.FailSongEnd</c>) を呼んで、
    /// 公開されている状態 (<c>SongEndState</c> / <c>ResultTriggered</c>) を読む。
    /// </summary>
    public static void InvokeProductionFailSongEnd(GameObject root, SongEndFailure reason)
    {
        MusicManager music = root.GetComponent<MusicManager>();
        Assert.IsNotNull(music, "MusicManager が無い");

        MethodInfo method = typeof(MusicManager).GetMethod("FailSongEnd",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "MusicManager.FailSongEnd が見つからない");

        method.Invoke(music, new object[] { reason, "[test] FailSongEnd" });
    }

    public static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"private field '{fieldName}' が見つからない");
        field.SetValue(target, value);
    }

    public static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"private field '{fieldName}' が見つからない");
        return (T)field.GetValue(target);
    }

    /// <summary>
    /// 本番の読み込み経路が作ったノーツのインスタンスを片付ける
    /// (シーンに残ると他テストの_scene 検索を壊すため)。
    /// </summary>
    public static void DestroyNotes(Rig rig)
    {
        if (rig == null || rig.Notes == null) return;
        for (int n = 0; n < rig.Notes.NotesObj.Count; n++)
        {
            if (rig.Notes.NotesObj[n] != null)
            {
                Object.DestroyImmediate(rig.Notes.NotesObj[n]);
            }
        }
    }

    /// <summary>呼び出し側のフレーム列 (等間隔)。</summary>
    public static List<float> Frames(float from, float to, float step)
    {
        List<float> frames = new List<float>();
        for (float t = from; t <= to + 0.0001f; t += step)
        {
            frames.Add(t);
        }
        return frames;
    }

    /// <summary>
    /// キー入力を再現して <see cref="Judge.JudgeStep"/> を駆動する。
    /// 実機と同じ順序 (押下 → HOLD 保持 → Miss) をそのまま使う。
    /// </summary>
    public sealed class Player
    {
        private struct Event
        {
            public float Time;
            public int Lane;
            public bool Down;
        }

        private readonly Rig rig;
        private readonly List<Event> events = new List<Event>();
        private readonly bool[] keyDown = new bool[Lanes];
        private readonly bool[] pressedThisFrame = new bool[Lanes];

        /// <summary>次に適用する入力イベント。Run を複数回呼んでも重複適用しない。</summary>
        private int next;

        public Player(Rig rig)
        {
            this.rig = rig;
        }

        public void SchedulePress(float time, int lane)
        {
            events.Add(new Event { Time = time, Lane = lane, Down = true });
        }

        public void ScheduleRelease(float time, int lane)
        {
            events.Add(new Event { Time = time, Lane = lane, Down = false });
        }

        public void Run(List<float> frames)
        {
            events.Sort((a, b) => a.Time.CompareTo(b.Time));

            foreach (float frameTime in frames)
            {
                while (next < events.Count && events[next].Time <= frameTime)
                {
                    Event e = events[next];
                    if (e.Down)
                    {
                        keyDown[e.Lane] = true;
                        pressedThisFrame[e.Lane] = true;
                    }
                    else
                    {
                        keyDown[e.Lane] = false;
                    }
                    next++;
                }

                rig.Judge.JudgeStep(frameTime, lane => pressedThisFrame[lane], lane => keyDown[lane]);

                for (int lane = 0; lane < Lanes; lane++)
                {
                    pressedThisFrame[lane] = false;
                }
            }
        }
    }
}

/// <summary>
/// Issue #226 の受入条件テストが共有するセッション状態のリセット。
/// GManager の static な判定カウンタを 0 に戻し、プレイセッションの固定を破棄する。
/// </summary>
public static class SongEndAcceptanceTests
{
    /// <summary>テストごとに GManager の判定状態と譜面固定を初期化する。</summary>
    public static void ResetSessionState()
    {
        GManager.perfect = 0;
        GManager.great = 0;
        GManager.bad = 0;
        GManager.miss = 0;
        GManager.score = 0;
        GManager.combo = 0;
        GManager.maxCombo = 0;
        GManager.ratioScore = 0f;
        GManager.maxScore = 0f;
        // MusicManager は曲開始時に nextNotes = 1 にする。Judge は
        // 「1 + 確定ノーツ数」を書くので、ここでも 1 から始める。
        GManager.nextNotes = 1;
        GManager.settledNoteCount = 0;
        GManager.noteTiming = 0;
        PlaySessionSnapshot.Clear();
    }

    /// <summary>テスト終了時に判定状態と譜面固定を戻す。</summary>
    public static void ClearSessionState()
    {
        ResetSessionState();
        GManager.nextNotes = 0;
        GManager.noteTiming = 5;
        PlaySessionSnapshot.Clear();
    }

    /// <summary>判定されたノーツ数 (Perfect + Great + Bad + Miss)。</summary>
    public static int JudgedCount()
    {
        return GManager.perfect + GManager.great + GManager.bad + GManager.miss;
    }
}
