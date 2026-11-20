using System.Reflection;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Game.Hud;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Issue #680: HUD 判定行のイベント経路。
///
/// 以前は V6GameHudView がシーンルートを走査し、"Perfect(Clone)" 等の
/// 名前に一致する GameObject (= Judge.message が Instantiate する
/// 3D 判定エフェクト) を検出していた。prefab 構成・親付け・命名が
/// 変わると HUD 判定行が静かに出なくなるため、判定確定時に Judge が
/// 積むイベントキュー (<see cref="Judge.TryDequeueVerdict"/>) を
/// 主経路に変えた。
///
/// このテストは <see cref="Judge.JudgeStep"/> (実機と同じ判定経路) で
/// イベントを発生させ、シーンルートにクローンを置かなくても
/// v6 HUD のレーン別判定行へラベルが届くことを検証する。
/// </summary>
public class VerdictEventPathTests
{
    private GameObject rigRoot;
    private Judge judge;
    private NotesManager notesManager;
    private GameObject canvasGo;
    private V6GameHudView view;

    private bool savedSuppressEffects;
    private bool savedVerdictRow;

    [SetUp]
    public void SetUp()
    {
        savedSuppressEffects = Judge.SuppressJudgementEffects;
        savedVerdictRow = FlashBeat.UI.V6.V6UiMode.VerdictRow;

        ResetGManager();

        // 前のテスト・前のプレイの残りを持ち越さない。
        Judge.ClearPendingVerdicts();
        Judge.SuppressJudgementEffects = false;
        FlashBeat.UI.V6.V6UiMode.VerdictRow = true;

        rigRoot = new GameObject("VerdictEventPathTestsRig");
        // 非アクティブ: Update / Start を走らせず JudgeStep を直接駆動する
        // (JudgeSettlementInvariantTests と同じ方式)。
        rigRoot.SetActive(false);

        notesManager = rigRoot.AddComponent<NotesManager>();
        judge = rigRoot.AddComponent<Judge>();

        // タップ 3 個: lane2 @1.0s / lane5 @1.0s (同時押し) / lane0 @2.0s。
        AddNote(2, 1f);
        AddNote(5, 1f);
        AddNote(0, 2f);

        // Judge の private フィールド (譜面の参照とエフェクトの配列) を差し込む。
        // MessageObj の要素は null なので 3D エフェクトは Instantiate されない
        // — このテストが検証するのはイベント経路だけで、クローンには依存しない。
        SetPrivateField(judge, "notesManager", notesManager);
        SetPrivateField(judge, "MessageObj", new GameObject[4]);
    }

    [TearDown]
    public void TearDown()
    {
        if (canvasGo != null)
        {
            Object.DestroyImmediate(canvasGo);
        }

        if (rigRoot != null)
        {
            Object.DestroyImmediate(rigRoot);
        }

        Judge.ClearPendingVerdicts();
        Judge.SuppressJudgementEffects = savedSuppressEffects;
        FlashBeat.UI.V6.V6UiMode.VerdictRow = savedVerdictRow;
        ResetGManager();

        canvasGo = null;
        view = null;
        rigRoot = null;
        judge = null;
        notesManager = null;
    }

    /// <summary>
    /// JudgeStep → FlushScore が書く GManager を既定値へ戻す
    /// (JudgeSettlementInvariantTests と同じ項目。他テストへ持ち越さない)。
    /// </summary>
    private static void ResetGManager()
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
        GManager.comboBonus = 0;
        GManager.noMissBonus = 0;
        GManager.comboMilestones = 0;
        GManager.milestoneBonus = 0;
        GManager.finalBonus = 0;
        GManager.judgmentsComplete = false;
        GManager.nextNotes = 0;
        GManager.settledNoteCount = 0;
    }

    private void AddNote(int lane, float time)
    {
        notesManager.LaneNum.Add(lane);
        notesManager.NoteType.Add(LongNote.TYPE_TAP);
        notesManager.NotesTime.Add(time);
        notesManager.NotesEndTime.Add(0f);
        notesManager.noteNum++;
    }

    private void CreateHud()
    {
        canvasGo = new GameObject("VerdictEventPathTests Canvas", typeof(Canvas));
        var canvasRect = (RectTransform)canvasGo.transform;
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
        view = V6GameHudView.Create(canvasRect);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"private field '{fieldName}' が見つからない");
        field.SetValue(target, value);
    }

    /// <summary>
    /// 判定確定で (判定, レーン) が確定順にキューへ積まれる。
    /// 同時押し (lane2 / lane5 @1.0s) はレーン順に 2 件とも届く。
    /// </summary>
    [Test]
    public void JudgeStep_EnqueuesVerdictEventsInLaneOrder()
    {
        judge.JudgeStep(1f, lane => lane == 2 || lane == 5, lane => false);

        Assert.AreEqual(2, Judge.PendingVerdictCount, "同時押しの 2 判定がキューに積まれていない");

        Assert.IsTrue(Judge.TryDequeueVerdict(out int j1, out int l1));
        Assert.AreEqual((int)LongNote.Judgement.Perfect, j1);
        Assert.AreEqual(2, l1, "先に処理されるのは小さいレーン番号");

        Assert.IsTrue(Judge.TryDequeueVerdict(out int j2, out int l2));
        Assert.AreEqual((int)LongNote.Judgement.Perfect, j2);
        Assert.AreEqual(5, l2);

        Assert.IsFalse(Judge.TryDequeueVerdict(out _, out _), "キューは空になる");
        Assert.AreEqual(0, Judge.PendingVerdictCount);
    }

    /// <summary>
    /// 時刻切れの Miss も同じキューへ (判定=Miss, 押されなかったレーン) で届く。
    /// </summary>
    [Test]
    public void JudgeStep_Miss_EnqueuesMissEventWithNoteLane()
    {
        // 1.0s の 2 ノーツを判定したあと、誰も押さないまま 3.0s へ進めると
        // lane0 @2.0s が Miss になる。
        judge.JudgeStep(1f, lane => lane == 2 || lane == 5, lane => false);
        Judge.ClearPendingVerdicts();

        judge.JudgeStep(3f, lane => false, lane => false);

        Assert.AreEqual(1, Judge.PendingVerdictCount);
        Assert.IsTrue(Judge.TryDequeueVerdict(out int judgement, out int lane));
        Assert.AreEqual((int)LongNote.Judgement.Miss, judgement);
        Assert.AreEqual(0, lane, "Miss はノーツのレーンに出る");
    }

    /// <summary>
    /// JudgeStep → キュー → HUD 判定行、がシーンルートにクローンを
    /// 置かなくても繋がる (Issue #680 の主経路)。
    /// </summary>
    [Test]
    public void HudVerdictRow_ShowsJudgementFromEventQueue()
    {
        judge.JudgeStep(1f, lane => lane == 2 || lane == 5, lane => false);

        CreateHud();
        Assert.DoesNotThrow(() => view.Refresh(), "Refresh が例外を投げた");

        Assert.AreEqual("PERFECT", view.VerdictTextForAudit(2), "lane2 の判定行");
        Assert.AreEqual("PERFECT", view.VerdictTextForAudit(5), "lane5 の判定行");
        Assert.AreEqual("", view.VerdictTextForAudit(0), "未判定レーンには出ない");
        Assert.AreEqual(0, Judge.PendingVerdictCount, "HUD がキューをドレインしていない");
    }

    /// <summary>
    /// 判定行が有効な間は 3D 判定エフェクトの Instantiate を抑制する
    /// (クローンを生成してから隠すのではなく、生成しない)。
    /// HUD 破棄でフラグは戻る。
    /// </summary>
    [Test]
    public void HudVerdictRow_SuppressesJudgeEffectClonesWhileEnabled()
    {
        CreateHud();
        view.Refresh();

        Assert.IsTrue(Judge.SuppressJudgementEffects,
            "VerdictRow 有効時は Judge の 3D エフェクトを抑制する");

        Object.DestroyImmediate(canvasGo);
        canvasGo = null;

        // EditMode の DestroyImmediate では OnDestroy が呼ばれない
        // (Play モードでは必ず呼ばれる)。リセット処理そのものを
        // 直接呼んで、HUD 破棄時に抑制が戻ることを検証する。
        var onDestroy = typeof(V6GameHudView).GetMethod("OnDestroy",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(onDestroy, "V6GameHudView.OnDestroy が見つからない");
        onDestroy.Invoke(view, null);

        Assert.IsFalse(Judge.SuppressJudgementEffects,
            "HUD 破棄後はエフェクト抑制が残らない");
    }

    /// <summary>
    /// シーンルートに "Perfect(Clone)" 等の名前を持つ GameObject があっても
    /// HUD はそれを検出・非表示にしない (名前規約への依存をなくした証明)。
    /// </summary>
    [Test]
    public void HudVerdictRow_DoesNotScanSceneRootsByCloneName()
    {
        var decoy = new GameObject("Perfect(Clone)");
        try
        {
            CreateHud();
            view.Refresh();

            for (int lane = 0; lane < 8; lane++)
            {
                Assert.AreEqual("", view.VerdictTextForAudit(lane),
                    $"lane{lane}: キューが空なら判定行は出ない");
            }

            Assert.IsTrue(decoy.activeSelf,
                "HUD はシーンルートのクローン名を走査して非表示にしない");
        }
        finally
        {
            Object.DestroyImmediate(decoy);
        }
    }

    /// <summary>
    /// Retry 等で GManager の判定カウンタが戻ったとき、表示中の判定を
    /// クリアし、前のプレイで積まれた未消費イベントも捨てる。
    /// </summary>
    [Test]
    public void HudVerdictRow_CounterReset_ClearsPendingEvents()
    {
        judge.JudgeStep(1f, lane => lane == 2 || lane == 5, lane => false);

        CreateHud();
        view.Refresh();
        Assert.AreEqual("PERFECT", view.VerdictTextForAudit(2));

        // 次の判定イベントが積まれた直後にリトライ (カウンタが 0 に戻る)。
        judge.JudgeStep(2f, lane => lane == 0, lane => false);
        Assert.Greater(Judge.PendingVerdictCount, 0);

        GManager.perfect = 0;
        GManager.great = 0;
        GManager.bad = 0;
        GManager.miss = 0;

        view.Refresh();

        Assert.AreEqual(0, Judge.PendingVerdictCount,
            "カウンタ減少時に前のプレイの未消費イベントを捨てる");
    }

    /// <summary>
    /// Issue #793 (v0.2.3 実機受入): 同一レーンの連続判定は前の表示を
    /// 上書きせず、イベントごとの独立インスタンスとして共存する。
    /// 旧実装 (レーンごと 1 テキスト) では 2 件目が 1 件目を消していた。
    /// </summary>
    [Test]
    public void HudVerdictRow_SameLaneRapidEvents_KeepBothInstances()
    {
        EnqueueVerdict((int)LongNote.Judgement.Perfect, 2);
        EnqueueVerdict((int)LongNote.Judgement.Great, 2);

        CreateHud();
        view.Refresh();

        Assert.AreEqual(0, Judge.PendingVerdictCount, "HUD がキューをドレインしていない");
        Assert.AreEqual(2, view.ActiveVerdictCountForAudit(2),
            "同一レーンの 2 イベントが別インスタンスで表示中 (上書きで消えない)");
        Assert.AreEqual("GREAT", view.VerdictTextForAudit(2),
            "監査ラベルは最新の判定を指す");
    }

    /// <summary>Judge の確定経路と同じキューへ直接イベントを積む (タイミング非依存)。</summary>
    private static void EnqueueVerdict(int judgement, int lane)
    {
        var method = typeof(Judge).GetMethod("EnqueueVerdict",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "Judge.EnqueueVerdict が見つからない");
        method.Invoke(null, new object[] { judgement, lane });
    }
}
