using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;

/// <summary>
/// Issue #682: レーンキークリック音 (<see cref="Judge"/> の PlayLaneClickSound、
/// Issue #604 で追加) の実挙動を EditMode で固定するテスト。
///
/// 検証する実挙動:
///   - 有効なレーンキー押下で <c>audioHit.PlayOneShot(clickSound)</c> が
///     Update 1 回につき 1 回だけ呼ばれる (空押しを含む)。
///   - 同時押し (複数レーン) でも 1 フレーム 1 回にまとまる (和音にならない)。
///   - clickSound / audioHit が null のとき例外なく return する。
///   - <see cref="GameplayInputGate.CanAcceptInput"/> が false のとき
///     (曲開始前 / ポーズ中 / 曲終了表示後) 鳴らない。
///   - hitSound == clickSound のとき判定ヒット側が重ねて鳴らない
///     (Issue #672 のガード。クリックとヒットで 2 回鳴らない)。
///
/// 観測方法:
///   - <see cref="Judge.PlayOneShotClip"/> (protected virtual、Issue #682 で追加した
///     テストシーム) をサブクラス <see cref="CountingJudge"/> で差し替え、
///     PlayOneShot に渡されたクリップを記録する。実際の AudioSource.PlayOneShot
///     は EditMode では鳴らないため、呼び出し点そのものを観測する。
///   - レーン押下は <see cref="KeyBindings.InjectLaneKeyDown"/> (EditMode 専用
///     シーム。Judge.IsLaneKeyPressed が KeyBindings.IsLaneKeyDown 経由で見る)。
///     実キーのフレームエッジ (GetKeyDownOrInjected) は Time.frameCount 依存で
///     EditMode では進められないため、「1 Update = 最大 1 回」と
///     「押下が無ければ鳴らない」までを固定する。エッジの 1 フレーム性は
///     KeyBindingsTests 側の責務。
///   - Update は private なのでリフレクションで呼ぶ
///     (JudgeSettlementInvariantTests.Update_PassesGManagerStepTimeToJudgeStep と同じ手法)。
/// </summary>
public class JudgeClickSoundTests
{
    /// <summary>PlayOneShot の呼び出し (クリップ) を記録する Judge。</summary>
    private sealed class CountingJudge : Judge
    {
        public readonly List<AudioClip> PlayedClips = new List<AudioClip>();

        protected override void PlayOneShotClip(AudioClip clip)
        {
            PlayedClips.Add(clip);
        }
    }

    private GameObject root;
    private CountingJudge judge;
    private AudioSource hitSource;
    private AudioClip clickClip;
    private AudioClip hitClip;
    private NotesManager notesManager;

    private bool savedStart;
    private float savedStepTime;

    private static readonly MethodInfo UpdateMethod =
        typeof(Judge).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);

    [SetUp]
    public void SetUp()
    {
        Assert.IsNotNull(UpdateMethod, "Judge.Update が見つからない");

        savedStart = GManager.Start;
        savedStepTime = GManager.step_time;

        // 非アクティブ: Start / Update がエディタのループで走らないようにする。
        // Update はテストからリフレクションで明示的に呼ぶ。
        root = new GameObject("JudgeClickSoundTests");
        root.SetActive(false);

        judge = root.AddComponent<CountingJudge>();
        hitSource = root.AddComponent<AudioSource>();
        clickClip = AudioClip.Create("click", 8, 1, 8000, false);
        hitClip = AudioClip.Create("hit", 8, 1, 8000, false);

        // private フィールドへ実機と同じ値を差し込む
        // (SongEndAcceptanceRig.SetPrivateField と同じ手法)。
        SetPrivateField(judge, "audioHit", hitSource);
        SetPrivateField(judge, "clickSound", clickClip);
        SetPrivateField(judge, "hitSound", hitClip);
        SetPrivateField(judge, "MessageObj", new GameObject[4]);

        PauseGate.HardReset();
        SongEndGate.Reset();
        KeyBindings.ClearInjectedLaneKeyDowns();
    }

    [TearDown]
    public void TearDown()
    {
        KeyBindings.ClearInjectedLaneKeyDowns();
        PauseGate.HardReset();
        SongEndGate.Reset();
        GManager.Start = savedStart;
        GManager.step_time = savedStepTime;

        if (root != null)
        {
            Object.DestroyImmediate(root);
        }
        if (clickClip != null)
        {
            Object.DestroyImmediate(clickClip);
        }
        if (hitClip != null)
        {
            Object.DestroyImmediate(hitClip);
        }
    }

    // ------------------------------------------------------------------
    // クリック音の発火
    // ------------------------------------------------------------------

    /// <summary>有効なレーンキー押下で clickSound が 1 回鳴る。押下が無いフレームは鳴らない。</summary>
    [Test]
    public void Update_LaneKeyPressed_PlaysClickSoundOnce_AndSilentWithoutPress()
    {
        GManager.Start = true;
        GManager.step_time = 1f;

        KeyBindings.InjectLaneKeyDown(0);
        InvokeUpdate();

        Assert.AreEqual(1, judge.PlayedClips.Count, "レーン押下 1 回でクリック音が 1 回鳴ること");
        Assert.AreSame(clickClip, judge.PlayedClips[0], "鳴るのは clickSound (判定音ではない)");

        // 次のフレームで押下が無ければ鳴らない (押しっぱなしの連続発火ではない)。
        judge.PlayedClips.Clear();
        KeyBindings.ClearInjectedLaneKeyDowns();
        InvokeUpdate();

        Assert.AreEqual(0, judge.PlayedClips.Count, "押下の無いフレームでは鳴らないこと");
    }

    /// <summary>複数レーンの同時押しでも 1 フレーム 1 回にまとまる (和音にならない)。</summary>
    [Test]
    public void Update_MultipleLanesPressedSameFrame_PlaysClickSoundOnlyOnce()
    {
        GManager.Start = true;
        GManager.step_time = 1f;

        KeyBindings.InjectLaneKeyDown(0);
        KeyBindings.InjectLaneKeyDown(3);
        KeyBindings.InjectLaneKeyDown(7);
        InvokeUpdate();

        Assert.AreEqual(1, judge.PlayedClips.Count,
            "同時押し (3 レーン) でも PlayOneShot は 1 回だけ (和音押しで音が割れない)");
        Assert.AreSame(clickClip, judge.PlayedClips[0]);
    }

    /// <summary>どのレーンも押されていなければ鳴らない (空フレーム)。</summary>
    [Test]
    public void Update_NoLanePressed_DoesNotPlayClickSound()
    {
        GManager.Start = true;
        GManager.step_time = 1f;

        InvokeUpdate();

        Assert.AreEqual(0, judge.PlayedClips.Count);
    }

    // ------------------------------------------------------------------
    // null 安全性
    // ------------------------------------------------------------------

    /// <summary>clickSound / audioHit が null のとき例外なく return する。</summary>
    [Test]
    public void Update_NullSoundOrSource_ReturnsWithoutExceptionOrSound()
    {
        GManager.Start = true;
        GManager.step_time = 1f;
        KeyBindings.InjectLaneKeyDown(0);

        // clickSound == null: クリップ未設定の Judge は鳴らせない。
        SetPrivateField(judge, "clickSound", null);
        Assert.DoesNotThrow(InvokeUpdate, "clickSound が null でも例外を投げないこと");
        Assert.AreEqual(0, judge.PlayedClips.Count);

        // audioHit == null: 音源の無い Judge は鳴らせない。
        SetPrivateField(judge, "clickSound", clickClip);
        SetPrivateField(judge, "audioHit", null);
        Assert.DoesNotThrow(InvokeUpdate, "audioHit が null でも例外を投げないこと");
        Assert.AreEqual(0, judge.PlayedClips.Count);

        // 両方 null でも同じ。
        SetPrivateField(judge, "clickSound", null);
        Assert.DoesNotThrow(InvokeUpdate, "両方 null でも例外を投げないこと");
        Assert.AreEqual(0, judge.PlayedClips.Count);
    }

    // ------------------------------------------------------------------
    // GameplayInputGate (Update の入口ゲート)
    // ------------------------------------------------------------------

    /// <summary>ポーズ中 (PauseGate.IsPaused) はクリック音を鳴らさない。再開後は鳴る。</summary>
    [Test]
    public void Update_WhilePaused_DoesNotPlayClickSound()
    {
        GManager.Start = true;
        GManager.step_time = 1f;
        KeyBindings.InjectLaneKeyDown(0);

        PauseGate.Enter();
        try
        {
            InvokeUpdate();
            Assert.AreEqual(0, judge.PlayedClips.Count, "ポーズ中はクリック音を鳴らさないこと");
        }
        finally
        {
            PauseGate.Exit();
        }

        InvokeUpdate();
        Assert.AreEqual(1, judge.PlayedClips.Count, "再開後は同じ押下で鳴ること");
    }

    /// <summary>曲終了が確定した後 (SongEndGate.BlocksJudging) はクリック音を鳴らさない。</summary>
    [Test]
    public void Update_AfterSongEndShown_DoesNotPlayClickSound()
    {
        GManager.Start = true;
        GManager.step_time = 1f;
        KeyBindings.InjectLaneKeyDown(0);

        SongEndGate.Enter();
        try
        {
            InvokeUpdate();
            Assert.AreEqual(0, judge.PlayedClips.Count, "曲終了表示後はクリック音を鳴らさないこと");
        }
        finally
        {
            SongEndGate.Reset();
        }

        InvokeUpdate();
        Assert.AreEqual(1, judge.PlayedClips.Count);
    }

    /// <summary>曲開始前 (GManager.Start == false) はクリック音を鳴らさない。</summary>
    [Test]
    public void Update_BeforeSongStart_DoesNotPlayClickSound()
    {
        GManager.Start = false;
        GManager.step_time = 1f;
        KeyBindings.InjectLaneKeyDown(0);

        InvokeUpdate();

        Assert.AreEqual(0, judge.PlayedClips.Count, "GManager.Start == false の間は鳴らないこと");
    }

    // ------------------------------------------------------------------
    // hitSound == clickSound の二重再生ガード (Issue #672)
    // ------------------------------------------------------------------

    /// <summary>
    /// hitSound と clickSound が同一クリップのとき、ノーツを押しても
    /// クリック音の 1 回だけで、判定ヒット側が重ねて鳴らない。
    /// </summary>
    [Test]
    public void Update_SameClipForHitAndClick_DoesNotDoublePlay()
    {
        BuildSingleNoteChart();
        SetPrivateField(judge, "hitSound", clickClip); // hitSound == clickSound

        GManager.Start = true;
        GManager.step_time = 0f;
        KeyBindings.InjectLaneKeyDown(0);
        InvokeUpdate();

        Assert.AreEqual(1, judge.PlayedClips.Count,
            "クリックとヒットが同一クリップなら合計 1 回 (二重再生しない)");
        Assert.AreSame(clickClip, judge.PlayedClips[0]);
    }

    /// <summary>
    /// 対照: hitSound が別クリップなら、同じフレームでクリック音と
    /// 判定ヒット音がそれぞれ 1 回ずつ鳴る (ガードが常に効いているのではない)。
    /// </summary>
    [Test]
    public void Update_DifferentHitSound_PlaysClickThenHit()
    {
        BuildSingleNoteChart();
        // hitSound は SetUp で別インスタンス (hitClip) が入っている。

        GManager.Start = true;
        GManager.step_time = 0f;
        KeyBindings.InjectLaneKeyDown(0);
        InvokeUpdate();

        Assert.AreEqual(2, judge.PlayedClips.Count,
            "クリック音 + 判定ヒット音で 2 回 (順序: クリック → ヒット)");
        Assert.AreSame(clickClip, judge.PlayedClips[0]);
        Assert.AreSame(hitClip, judge.PlayedClips[1]);
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

    /// <summary>時刻 0・レーン 0 のタップノーツ 1 個だけの譜面を Judge へ接続する。</summary>
    private void BuildSingleNoteChart()
    {
        notesManager = root.AddComponent<NotesManager>();
        notesManager.noteNum = 1;
        notesManager.NoteType.Add(LongNote.TYPE_TAP);
        notesManager.LaneNum.Add(0);
        notesManager.NotesTime.Add(0f);
        // NotesEndTime は空でよい (EnsureQueue は Count 範囲で読む)。

        SetPrivateField(judge, "notesManager", notesManager);
    }

    private void InvokeUpdate()
    {
        UpdateMethod.Invoke(judge, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        // フィールドは Judge 本体の private メンバー。CountingJudge 経由だと
        // 基底クラスの private は GetField で見つからないので typeof(Judge) で引く。
        FieldInfo field = typeof(Judge).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"private field '{fieldName}' が見つからない");
        field.SetValue(target, value);
    }
}
