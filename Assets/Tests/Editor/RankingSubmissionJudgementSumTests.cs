using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Result;

/// <summary>
/// Issue #226 の受入条件「ランキング送信」の回帰テスト。
///
/// ランキング API (POST /api/v1/scores) は送信された判定数を **譜面のノーツ数と
/// 照合して** 受理する (contracts/openapi.v1.yaml:
/// 「Server validates judgment sum against published version and recalculates
/// score」/ 422 = "Invalid counts or chart version"。
/// 実装は web/routes/api/v1/_score-model.ts の
/// <c>scoreSemanticsDiagnostics</c>、テストは web/tests/score-model.test.ts)。
///
/// クライアント側に「合計が合うか」を判定するコードは無い (クライアントは
/// 判定数をそのまま送り、サーバーが判断する)。ここで固定するのは:
//
///
///   1. **クライアントは判定数をそのまま送る** (水増し / 補完しない)。
///      → 全部判定できたプレイは合計 = noteCount になり API が受理する。
///   2. **未判定ノーツが残ったプレイ** (期限切れ強制完了) は合計が noteCount に
///      届かず、API が 422 で拒否する。クライアントはそれを
///      「登録成功」(Success) として扱わない (Rejected)。
///   3. クライアントが送る score は API が再計算する値と一致する
///      (両者とも単精度の式で計算される)。
///
/// サーバ側はネットワーク無しでは動かないので、API の判定規則そのものは
/// web/tests/score-model.test.ts が担保する。ここでは **クライアントが送るもの**
/// を固定する。
/// </summary>
public class RankingSubmissionJudgementSumTests
{
    private GameObject rigRoot;
    private SongEndAcceptanceRig.Rig rig;

    [SetUp]
    public void SetUp()
    {
        SongEndAcceptanceTests.ResetSessionState();
    }

    [TearDown]
    public void TearDown()
    {
        if (rig != null)
        {
            SongEndAcceptanceRig.DestroyNotes(rig);
        }
        if (rigRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(rigRoot);
            rigRoot = null;
        }
        SongEndAcceptanceTests.ClearSessionState();
    }

    /// <summary>API に渡す曲 id (サーバの規則 ^[a-z0-9-]{3,64}$)。</summary>
    private const string SongId = "w226-test";

    private const int ChartVersion = 1;

    /// <summary>3 ノーツ (Tap 2 + HOLD 1) の譜面。</summary>
    private const string ChartJson =
        "{\"name\":\"ranking\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[" +
        "{\"type\":1,\"num\":4,\"block\":0,\"LPB\":4}," +
        "{\"type\":1,\"num\":8,\"block\":1,\"LPB\":4}," +
        "{\"type\":2,\"num\":12,\"endNum\":24,\"block\":2,\"LPB\":4}" +
        "]}";

    private const int NoteCount = 3;

    // ------------------------------------------------------------------
    // 1) 全部判定できたプレイ
    // ------------------------------------------------------------------

    /// <summary>
    /// 全ノーツが判定されたプレイは、送信する判定数の合計が noteCount に一致し、
    /// API が受理する (Success) 側になる。
    /// </summary>
    [Test]
    public void ACompleteRun_SumsToTheNoteCount_AndIsAccepted()
    {
        rig = SongEndAcceptanceRig.BuildRig(ChartJson, NoteCount);
        rigRoot = rig.Root;
        SongEndAcceptanceRig.Player player = new SongEndAcceptanceRig.Player(rig);

        // 全ノーツを押す (HOLD は終端で離す)。
        foreach (int index in rig.NoteIndices)
        {
            player.SchedulePress(rig.NoteTime[index], rig.NoteLane[index]);
            if (rig.NoteType[index] == LongNote.TYPE_HOLD)
            {
                player.ScheduleRelease(rig.NoteEndTime[index], rig.NoteLane[index]);
            }
        }
        player.Run(SongEndAcceptanceRig.Frames(0f, 4f, 0.05f));

        Assert.AreEqual(NoteCount, SongEndAcceptanceTests.JudgedCount(),
            "判定合計が noteCount に一致しない");
        Assert.AreEqual(NoteCount, GManager.settledNoteCount);

        // 実際に送られる本文 (本番の RankingClient が作るもの)。
        string json = RankingClient.BuildScoreRequestJson(SongId, ChartVersion, "player",
            GManager.score, GManager.perfect, GManager.great, GManager.bad, GManager.miss, GManager.maxCombo);
        Assert.IsNotNull(json, "ランキング送信の本文が作れない");

        var payload = JsonUtility.FromJson<RankingClient.ScoreRequest>(json);
        Assert.AreEqual(GManager.perfect, payload.perfect);
        Assert.AreEqual(GManager.great, payload.great);
        Assert.AreEqual(GManager.bad, payload.bad);
        Assert.AreEqual(GManager.miss, payload.miss);
        Assert.AreEqual(GManager.maxCombo, payload.maxCombo);

        // API の受理条件: 判定数の合計 = 譜面のノーツ数。
        int sum = payload.perfect + payload.great + payload.bad + payload.miss;
        Assert.AreEqual(NoteCount, sum, "送信する判定数の合計が noteCount と一致しない");

        // API は score も再計算して照合する。単精度の式なのでクライアントの値と一致する。
        Assert.AreEqual(ApiRecomputedScore(payload.perfect, payload.great, payload.bad, NoteCount),
            payload.score, "送信する score が API の再計算と一致しない");

        // サーバが 201 を返したときは「登録成功」。
        Assert.AreEqual(V6ResultLogic.SubmitOutcome.Success,
            V6ResultLogic.ClassifySubmitResult(true, 201, null));
    }

    // ------------------------------------------------------------------
    // 2) 未判定ノーツが残ったプレイ
    // ------------------------------------------------------------------

    /// <summary>
    /// 未判定ノーツが残ったプレイ (期限切れ強制完了) では、送信する判定数の合計が
    /// noteCount に **届かない**。サーバは 422 で拒否し、クライアントはそれを
    /// 「登録成功」にしない。
    /// </summary>
    [Test]
    public void ARunWithUnsettledNotes_IsRejected_NotAccepted()
    {
        rig = SongEndAcceptanceRig.BuildRig(ChartJson, NoteCount);
        rigRoot = rig.Root;
        SongEndAcceptanceRig.Player player = new SongEndAcceptanceRig.Player(rig);

        // 1 ノーツだけ判定して、曲が終わる (未判定ノーツが残った状態)。
        player.SchedulePress(rig.NoteTime[0], rig.NoteLane[0]);
        player.Run(SongEndAcceptanceRig.Frames(0f, 1f, 0.05f));

        Assert.AreEqual(1, SongEndAcceptanceTests.JudgedCount(), "判定数が想定と違う");
        Assert.Less(SongEndAcceptanceTests.JudgedCount(), NoteCount,
            "判定数が noteCount に達している (未判定のはず)");

        string json = RankingClient.BuildScoreRequestJson(SongId, ChartVersion, "player",
            GManager.score, GManager.perfect, GManager.great, GManager.bad, GManager.miss, GManager.maxCombo);
        Assert.IsNotNull(json, "ランキング送信の本文が作れない");

        var payload = JsonUtility.FromJson<RankingClient.ScoreRequest>(json);
        int sum = payload.perfect + payload.great + payload.bad + payload.miss;

        // クライアントは判定数を水増ししない (合計は noteCount に届かないまま)。
        Assert.AreEqual(SongEndAcceptanceTests.JudgedCount(), sum,
            "クライアントが判定数を補完している");
        Assert.AreEqual(1, sum, "未判定ノーツの分だけ判定数が足りない");

        // サーバの規則 (合計 = noteCount) を満たさない → 422 で拒否される。
        Assert.AreNotEqual(NoteCount, sum, "合計が noteCount に一致している (拒否されない)");

        // クライアントは 422 を「登録成功」にしない。
        Assert.AreEqual(V6ResultLogic.SubmitOutcome.Rejected,
            V6ResultLogic.ClassifySubmitResult(false, 422, "response"),
            "422 が登録成功扱いされている");
        Assert.AreNotEqual(V6ResultLogic.SubmitOutcome.Success,
            V6ResultLogic.ClassifySubmitResult(false, 422, "response"));
    }

    /// <summary>
    /// 判定数が noteCount に足りない场合、サーバの拒否 (422) と
    /// 輸送失敗 (オフライン) と重複 (429) が区別される。
    /// </summary>
    [Test]
    public void TheRejectedOutcomeIsDistinguishedFromOfflineAndDuplicate()
    {
        Assert.AreEqual(V6ResultLogic.SubmitOutcome.Rejected,
            V6ResultLogic.ClassifySubmitResult(false, 422, "response"));
        Assert.AreEqual(V6ResultLogic.SubmitOutcome.Offline,
            V6ResultLogic.ClassifySubmitResult(false, 0, RankingClient.UnavailableError));
        Assert.AreEqual(V6ResultLogic.SubmitOutcome.Duplicate,
            V6ResultLogic.ClassifySubmitResult(false, 429, null));
        Assert.AreEqual(V6ResultLogic.SubmitOutcome.Success,
            V6ResultLogic.ClassifySubmitResult(true, 201, null));
    }

    /// <summary>
    /// ランキング送信は、送信対象でない (ローカル曲 / 譜面未取得) 曲では
    /// 試行されない。ローカル曲でオンライン送信の語彙を出さないための条件。
    /// </summary>
    [Test]
    public void OnlyRemoteSongsWithAChartVersionAreSubmittable()
    {
        Assert.IsFalse(V6ResultLogic.IsSubmittable(false, null, 1), "ローカル曲を送信対象にしている");
        Assert.IsFalse(V6ResultLogic.IsSubmittable(true, null, 1), "remoteId がない曲を送信対象にしている");
        Assert.IsFalse(V6ResultLogic.IsSubmittable(true, SongId, 0), "譜面版がない曲を送信対象にしている");
        Assert.IsTrue(V6ResultLogic.IsSubmittable(true, SongId, ChartVersion));
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// API が再計算するスコア (web/routes/api/v1/_score-model.ts の
    /// <c>computeUnityScore</c> と同じ単精度の式)。
    /// </summary>
    private static int ApiRecomputedScore(int perfect, int great, int bad, int noteCount)
    {
        if (noteCount * 5 <= 0) return 0;
        float maxScore = noteCount * 5f;
        float ratioScore = perfect * 5f + great * 3f + bad;
        float ratio = ratioScore / maxScore;
        float scaled = ratio * 1000000f;
        float floored = Mathf.Floor(scaled);
        return (int)System.Math.Round(1000000 * floored / 1000000);
    }
}
