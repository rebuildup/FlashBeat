using System.IO;
using System.Linq;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.V5CanvasTests
{
    /// <summary>
    /// Issue #738: Result → Select の復帰で、直前にプレイした曲と
    /// Ready 時点の検索語を選曲画面へ復元する契約。
    ///
    /// 検証するのは実装定数ではなく要件:
    ///   - Result → Select (RequestRestore) のあと選曲画面を開くと、
    ///     直前の曲が選択され検索語も戻る (ユーザーは前回いた場所から継続できる)。
    ///   - アプリ起動 / Title → Select など復元要求の無い入口では
    ///     初期状態 (未選択 → ビューが先頭を選ぶ) のまま。
    ///   - Result → Title のセッション境界ではコンテキストが捨てられる。
    ///   - 直前の曲がカタログから消えていたら初期状態へフォールバックする。
    ///
    /// SelectReturnContext は static (セッション級) なので、シーンを
    /// ロードせず EditMode で検証できる。曲の復元は実データ結線
    /// (V6SelectDataSource + GManager.ActiveSongs) で確認する。
    /// </summary>
    public class SelectReturnContextTests
    {
        [SetUp]
        public void SetUp()
        {
            SelectReturnContext.ResetForAudit();
        }

        [TearDown]
        public void TearDown()
        {
            // static のまま残ると他のテストを壊すので、必ず捨てる。
            SelectReturnContext.ResetForAudit();
        }

        [Test]
        public void Record_Alone_DoesNotRestore()
        {
            // 新規起動・Title → Select の初期状態を変えない:
            // Ready で記録されただけでは復元要求にならない。
            SelectReturnContext.Record(5, "終焉");

            var source = new V6SelectDataSource();
            Assert.IsFalse(SelectReturnContext.TryApply(source),
                "復元要求が無いのに適用された (新規起動の初期選択が変わる)");
            Assert.IsNull(source.Selected, "初期状態は未選択のまま (ビューが先頭を選ぶ)");
            Assert.AreEqual("", source.Query, "検索語も戻っていない");
        }

        [Test]
        public void ResultToSelect_RestoresPlayedSongAndQuery()
        {
            // 結線: OnV6Ready → Record → (Result) SelectScene →
            // RequestRestore → 次の選曲画面で TryApply。
            SelectReturnContext.Record(5, "終焉"); // 終焉逃避行 (ローカル譜面あり)
            SelectReturnContext.RequestRestore();

            var source = new V6SelectDataSource();
            Assert.IsTrue(SelectReturnContext.TryApply(source), "復元が適用されなかった");

            Assert.IsNotNull(source.Selected, "直前の曲が選択されていない");
            Assert.AreEqual(5, source.Selected.SongId,
                "復帰先が直前にプレイした曲ではない");
            Assert.AreEqual("終焉", source.Query, "Ready 時点の検索語が戻っていない");
            Assert.IsTrue(source.FilteredSongs.Any(s => s.SongId == 5),
                "復元した曲が絞り込み結果に見えていない (Ready できない状態)");
        }

        [Test]
        public void TryApply_IsConsumedOnce()
        {
            SelectReturnContext.Record(5, "");
            SelectReturnContext.RequestRestore();

            var first = new V6SelectDataSource();
            Assert.IsTrue(SelectReturnContext.TryApply(first));
            Assert.IsFalse(SelectReturnContext.RestorePendingForAudit,
                "復元要求が消費後も残っている");

            // 2 回目の選曲画面 (例: そのまま Title → Select) では復元しない。
            var second = new V6SelectDataSource();
            Assert.IsFalse(SelectReturnContext.TryApply(second),
                "消費済みの要求が再度適用された");
            Assert.IsNull(second.Selected);
        }

        [Test]
        public void RequestRestore_WithoutRecord_DoesNothing()
        {
            // まだ 1 曲もプレイしていない Result はありえないが、
            // 記録が無い状態での復帰要求は初期状態のままにする。
            SelectReturnContext.RequestRestore();

            var source = new V6SelectDataSource();
            Assert.IsFalse(SelectReturnContext.TryApply(source));
            Assert.IsNull(source.Selected);
        }

        [Test]
        public void Clear_DropsRecordAndPendingRestore()
        {
            // Result → Title (来場者交代) の境界。以降の選曲画面は
            // 初期状態から始まる。
            SelectReturnContext.Record(5, "終焉");
            SelectReturnContext.RequestRestore();
            SelectReturnContext.Clear();

            var source = new V6SelectDataSource();
            Assert.IsFalse(SelectReturnContext.TryApply(source),
                "セッション境界のあとも復元された");
            Assert.IsNull(source.Selected);
            Assert.AreEqual(-1, SelectReturnContext.LastSongIdForAudit,
                "記録が残っている");
        }

        [Test]
        public void TryApply_WhenSongLeftTheCatalog_FallsBackToInitialState()
        {
            // 直前の曲が削除・非公開でカタログに無いときは、曲も検索語も
            // 適用せず初期状態 (ビューが先頭を選ぶ) にフォールバックする。
            SelectReturnContext.Record(999999, "存在しない検索語XYZ");
            SelectReturnContext.RequestRestore();

            var source = new V6SelectDataSource();
            Assert.IsFalse(SelectReturnContext.TryApply(source),
                "カタログに無い曲へ復元を適用してしまった");
            Assert.IsNull(source.Selected, "存在しない曲が選択されている");
            Assert.AreEqual("", source.Query,
                "曲が無いのに検索語だけ戻っている (選択曲が隠れる不整合)");
        }

        [Test]
        public void ProductionWiring_ResultToSelectAndTitleBoundary()
        {
            // 遷移の結線を固定する (SelectSceneTransitionTests と同じ
            // ソース検査方式。Fade を伴う遷移そのものは EditMode で呼べない)。
            string selectManager = File.ReadAllText(Path.Combine(
                Application.dataPath, "Game", "Scripts", "UI", "SelectSceneManager.cs"));
            string resultManager = File.ReadAllText(Path.Combine(
                Application.dataPath, "Game", "Scripts", "UI", "ResultSceneManager.cs"));

            // Ready 確定時に選曲状態を記録し、画面構築時に復元を試みる。
            Assert.IsTrue(selectManager.Contains("SelectReturnContext.Record("),
                "SelectSceneManager が Ready 時の選曲状態を記録していない");
            Assert.IsTrue(selectManager.Contains("SelectReturnContext.TryApply("),
                "SelectSceneManager が選曲画面構築時に復元を適用していない");

            // Result → Select が復帰要求を出し、Result → Title が境界で捨てる。
            int selectScene = resultManager.IndexOf("public void SelectScene()", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(selectScene, 0, "ResultSceneManager.SelectScene() が見つからない");
            int titleScene = resultManager.IndexOf("public void TitleScene()", System.StringComparison.Ordinal);
            Assert.Less(selectScene, titleScene, "SelectScene() が TitleScene() より後にある");
            string selectBody = resultManager.Substring(selectScene,
                titleScene - selectScene);
            Assert.IsTrue(selectBody.Contains("SelectReturnContext.RequestRestore()"),
                "Result → Select が復帰要求を出していない");

            int end = resultManager.IndexOf("public void ", titleScene + 1, System.StringComparison.Ordinal);
            if (end < 0) end = resultManager.Length;
            string titleBody = resultManager.Substring(titleScene, end - titleScene);
            Assert.IsTrue(titleBody.Contains("SelectReturnContext.Clear()"),
                "Result → Title がセッション境界でコンテキストを捨てていない");
        }
    }
}
