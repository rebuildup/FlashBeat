using NUnit.Framework;
using FlashBeat.Gameplay;
using FlashBeat.Persistence;
using FlashBeat.UI.V6.Result;

/// <summary>
/// scoring v2 (ADR-0028 / Issue #577) の EditMode テスト。
///
/// 検証する契約:
///
///   - 式: judgment = 5P+3G+B / comboBonus = maxCombo*5 /
///     noMissBonus = miss==0 ? (P+G+B)*5 : 0 / maxRatio = 15*N /
///     score = round(1e6 * ratio / maxRatio) で満点は 1,000,000。
///   - 途中の Miss でノーミス分が消えてスコアが下がる (comboBonus は残る)。
///   - Bad はコンボを切らない (maxCombo は Bad 継続を数える)。
///   - ローカル記録は scoringVersion で分離される
///     (bare key = v1, "@2" = v2)。
///   - 送信 JSON に scoringVersion=2 が載る。
///   - 結果画面の内訳テキストが実際のボーナスを反映する。
/// </summary>
public class ScoringV2Tests
{
    // --- 式 -------------------------------------------------------------

    [Test]
    public void PerfectRun_ScoresExactlyOneMillion()
    {
        // 全 Perfect・ノーミスなし・maxCombo = N: 5N + 5N + 5N = 15N。
        Assert.AreEqual(1000000, Scoring.ScoreV2(10, 0, 0, 0, 10, 10));
    }

    [Test]
    public void Breakdown_MatchesAdrFormula()
    {
        // P=4 G=3 B=2 M=1, maxCombo=6, N=10。
        // judgment = 20+9+2 = 31, comboBonus = 30, noMissBonus = 0 (miss>0)。
        Scoring.V2Breakdown b = Scoring.BreakdownV2(4, 3, 2, 1, 6, 10);
        Assert.AreEqual(31f, b.Judgment, 0.0001f);
        Assert.AreEqual(30, b.ComboBonus);
        Assert.AreEqual(0, b.NoMissBonus);
        Assert.AreEqual(61f, b.RatioScore, 0.0001f);
        Assert.AreEqual(150f, b.MaxRatio, 0.0001f);
    }

    [Test]
    public void NoMissBonus_AppliesOnlyWhileMissIsZero()
    {
        // miss=0: (P+G+B)*5 = 9*5 = 45。
        Scoring.V2Breakdown clean = Scoring.BreakdownV2(4, 3, 2, 0, 6, 10);
        Assert.AreEqual(45, clean.NoMissBonus);

        // miss=1: ノーミスボーナスは消える (途中 MISS でスコアが下がる)。
        Scoring.V2Breakdown missed = Scoring.BreakdownV2(4, 3, 2, 1, 6, 10);
        Assert.AreEqual(0, missed.NoMissBonus);
        Assert.Less(Scoring.ScoreV2(4, 3, 2, 1, 6, 10),
            Scoring.ScoreV2(4, 3, 2, 0, 6, 10),
            "Miss 後のスコアが Miss 前を下回っていない");
    }

    [Test]
    public void ComboBonus_SurvivesALaterMiss()
    {
        // maxCombo は確定済みの最大値なので、Miss 後もコンボボーナスは残る
        // (ADR-0028: comboBonus = maxCombo*5、Miss で減らない)。
        Scoring.V2Breakdown b = Scoring.BreakdownV2(4, 3, 2, 1, 6, 10);
        Assert.AreEqual(30, b.ComboBonus);
    }

    [Test]
    public void Score_DecreasesWhenAnIntermediateMissCostsTheNoMissBonus()
    {
        // N=10。途中まで全命中 (P=5, maxCombo=5, miss=0) → Miss 1 回で
        // noMissBonus が (hits*5) 分だけ失われ、スコアは下がる。
        int before = Scoring.ScoreV2(5, 0, 0, 0, 5, 10);
        int after = Scoring.ScoreV2(5, 0, 0, 1, 5, 10);
        Assert.Greater(before, after);
    }

    [Test]
    public void EmptyChart_ScoresZero()
    {
        Assert.AreEqual(0, Scoring.ScoreV2(0, 0, 0, 0, 0, 0));
    }

    [Test]
    public void BoundaryValues_StayInRange()
    {
        // 全 Bad (combo は維持されるが maxCombo=0 想定) / 全 Miss。
        Assert.GreaterOrEqual(Scoring.ScoreV2(0, 0, 10, 0, 0, 10), 0);
        Assert.LessOrEqual(Scoring.ScoreV2(0, 0, 10, 0, 0, 10), 1000000);
        Assert.AreEqual(0, Scoring.ScoreV2(0, 0, 0, 10, 0, 10));
        // maxCombo = N (契約上の上限) でも満点を超えない。
        Assert.AreEqual(1000000, Scoring.ScoreV2(10, 0, 0, 0, 10, 10));
    }

    // --- ローカル記録の scoringVersion 分離 -------------------------------

    [Test]
    public void ScoreKey_V2Record_UsesExtendedStorageForm()
    {
        var key = new ScoreKey("who", 3, Scoring.Version2);
        Assert.AreEqual("who@3@2", key.ToStorageString());

        Assert.IsTrue(ScoreKey.TryParse("who@3@2", out ScoreKey parsed));
        Assert.AreEqual(key, parsed);
        Assert.AreEqual(Scoring.Version2, parsed.scoringVersion);
    }

    [Test]
    public void ScoreKey_LegacyBareKey_ReadsAsV1()
    {
        // ADR-0028: "songId@chartVersion" は v1 の記録。
        Assert.IsTrue(ScoreKey.TryParse("who@3", out ScoreKey parsed));
        Assert.AreEqual(1, parsed.scoringVersion);
        Assert.AreEqual("who@3", parsed.ToStorageString(), "v1 は bare 形に戻る");
    }

    [Test]
    public void ScoreKey_V1AndV2_AreDifferentKeys()
    {
        var v1 = new ScoreKey("who", 3, Scoring.Version1);
        var v2 = new ScoreKey("who", 3, Scoring.Version2);
        Assert.AreNotEqual(v1, v2);
        Assert.AreNotEqual(v1.GetHashCode(), v2.GetHashCode());
    }

    [Test]
    public void ScoreKey_Parse_RejectsMalformedScoringVersion()
    {
        Assert.IsFalse(ScoreKey.TryParse("who@3@", out _), "空の scoringVersion");
        Assert.IsFalse(ScoreKey.TryParse("who@3@x", out _), "非数値の scoringVersion");
        Assert.IsFalse(ScoreKey.TryParse("who@3@0", out _), "0 の scoringVersion");
        Assert.IsFalse(ScoreKey.TryParse("who@3@2@1", out _), "3 要素目以降");
    }

    [Test]
    public void ScoreStore_KeepsV1AndV2RecordsSeparate()
    {
        var store = new ScoreStore();
        var v1 = new ScoreKey("who", 1, Scoring.Version1);
        var v2 = new ScoreKey("who", 1, Scoring.Version2);

        store.Submit(v1, 500000);
        store.Submit(v2, 800000);

        Assert.AreEqual(500000, store.Get(v1));
        Assert.AreEqual(800000, store.Get(v2));

        // v2 の記録が v1 を上書きしない (別スケールのため比較もできない)。
        store.Submit(v2, 900000);
        Assert.AreEqual(500000, store.Get(v1));
        Assert.AreEqual(900000, store.Get(v2));

        // シリアライズ → デシリアライズで分離が保たれる。
        ScoreStore loaded = ScoreStore.Deserialize(store.Serialize());
        Assert.AreEqual(500000, loaded.Get(v1));
        Assert.AreEqual(900000, loaded.Get(v2));
    }

    // --- 送信ペイロード -----------------------------------------------------

    [Test]
    public void Submission_AlwaysCarriesScoringVersion2()
    {
        string json = RankingClient.BuildScoreRequestJson(
            "test-song", 2, "なまえ", 800000, 8, 1, 0, 1, 8);
        Assert.IsNotNull(json);
        var payload = UnityEngine.JsonUtility.FromJson<RankingClient.ScoreRequest>(json);
        Assert.AreEqual(Scoring.LatestVersion, payload.scoringVersion);
        Assert.AreEqual(RankingClient.ScoringVersion, payload.scoringVersion);
    }

    // --- 結果画面の内訳 -----------------------------------------------------

    [Test]
    public void ResultBreakdown_ShowsRealBonusValues()
    {
        // P=8 G=1 B=0 M=1, maxCombo=8, N=10:
        // comboBonus = 40 ratio → ComponentScoreV2(40,10) = round(1e6*40/150)=266,667
        // noMissBonus = 0 (miss>0) → "–"
        V6ResultLogic.BonusBreakdownTexts(8, 1, 0, 1, 8,
            out string comboText, out string noMissText);
        Assert.AreEqual("+266,667", comboText);
        Assert.AreEqual("–", noMissText);
    }

    [Test]
    public void ResultBreakdown_NoMissBonusShownOnlyWhenEarned()
    {
        // miss=0 なら noMissBonus = (P+G+B)*5 が出る。
        V6ResultLogic.BonusBreakdownTexts(8, 1, 0, 0, 9,
            out string comboText, out string noMissText);
        Assert.IsTrue(noMissText.StartsWith("+"), "ノーミスボーナスが出ていない: " + noMissText);
        Assert.AreEqual("+300,000", noMissText); // 45/150 * 1e6 = 300,000
    }
}
