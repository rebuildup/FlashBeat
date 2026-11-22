using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Game.Pause;
using FlashBeat.UI.V6.Option;
using FlashBeat.UI.V6.Result;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// issue #748: コンパクト画面でのモーダル用パン退避の共通機構
    /// (<see cref="V6FramePan.SuspendForModal"/> / <see cref="V6ModalPan"/>) と、
    /// Option / Pause / KeyConfig / Result の各モーダルへの適用を固定する。
    ///
    /// 検証する実挙動:
    ///   - モーダルを開くとダイアログ中心がビューポート中心へ来るパンになり、
    ///     ドラッグ / ホイール / キーパンが止まる (PanSuspended)。
    ///   - 閉じると開く前のパン位置へ戻り、パン入力が再開する。
    ///   - 複数モーダルが重なった場合は開閉順に正しく復帰する。
    ///   - 初回配置のパンはフレーム中央 (従来の (0,0) = 左上ではない)。
    /// </summary>
    public sealed class V6ModalPanTests
    {
        private GameObject canvasGo;
        private int savedSongId;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6ModalPanTests Canvas", typeof(RectTransform));
            // コンパクト画面を強制する (Screen.* はテストから変えられない)。
            V6CompactScreen.ScreenSizeOverrideForTests = new Vector2Int(1024, 768);
            // Pause view のヘッダーがジャケット取得 (ネットワーク) を走らせないように。
            savedSongId = GManager.songID;
            GManager.songID = -1;
        }

        [TearDown]
        public void TearDown()
        {
            V6CompactScreen.ScreenSizeOverrideForTests = Vector2Int.zero;
            GManager.songID = savedSongId;
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
            canvasGo = null;
            KeyBindings.ResetToDefaults();
        }

        // --- 期待値ヘルパ ---

        /// <summary>1024×768 のビューポートで dialogRect 中心を中央へ合わせるパン。</summary>
        private static Vector2 ExpectedModalPan(Rect dialogRect, float viewW, float viewH)
        {
            return V6CompactScreen.ClampPan(
                viewW * 0.5f - (dialogRect.x + dialogRect.width * 0.5f),
                viewH * 0.5f - (dialogRect.y + dialogRect.height * 0.5f),
                viewW, viewH);
        }

        private static V6FramePan PanOf(RectTransform frame)
        {
            var viewport = V6CompactScreen.FindViewport(frame);
            Assert.IsNotNull(viewport, "compact viewport must exist");
            var pan = viewport.GetComponent<V6FramePan>();
            Assert.IsNotNull(pan);
            return pan;
        }

        // --- V6FramePan.SuspendForModal / ResumeModal (単体) ---

        [Test]
        public void SuspendForModal_CentersDialog_StopsInput_AndRestores()
        {
            var go = new GameObject("pan", typeof(RectTransform), typeof(V6FramePan));
            try
            {
                var pan = go.GetComponent<V6FramePan>();
                pan.ViewportSize = new Vector2(1024f, 768f);
                pan.Pan = Vector2.zero;
                Vector2 before = pan.Pan;

                // 共有確認 Dialog (444,231) 840×510。
                pan.SuspendForModal(new Rect(444f, 231f, 840f, 510f));

                Vector2 expected = ExpectedModalPan(new Rect(444f, 231f, 840f, 510f), 1024f, 768f);
                Assert.AreEqual(expected.x, pan.Pan.x, 0.01f, "pan.x centers the dialog");
                Assert.AreEqual(expected.y, pan.Pan.y, 0.01f, "pan.y centers the dialog");
                Assert.IsTrue(pan.PanSuspended, "pan input suspended while modal open");
                Assert.AreEqual(1, pan.ModalPanDepthForAudit);

                // ホイール / ドラッグは止まる。
                pan.OnScroll(new UnityEngine.EventSystems.PointerEventData(null)
                {
                    scrollDelta = new Vector2(0f, 1f),
                });
                Assert.AreEqual(expected, pan.Pan, "scroll must be ignored while suspended");

                pan.ResumeModal();
                Assert.AreEqual(before, pan.Pan, "pan restored after modal close");
                Assert.IsFalse(pan.PanSuspended, "pan input resumed");
                Assert.AreEqual(0, pan.ModalPanDepthForAudit);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SuspendForModal_Nested_ResumeReturnsToPreviousDialog()
        {
            var go = new GameObject("pan", typeof(RectTransform), typeof(V6FramePan));
            try
            {
                var pan = go.GetComponent<V6FramePan>();
                pan.ViewportSize = new Vector2(1024f, 768f);
                pan.Pan = Vector2.zero;
                Vector2 saved = pan.Pan;

                var outer = new Rect(444f, 231f, 840f, 510f);   // 確認 Dialog
                var inner = new Rect(444f, 124f, 840f, 724f);   // KeyConfig Dialog
                pan.SuspendForModal(outer);
                Vector2 outerPan = pan.Pan;
                pan.SuspendForModal(inner);

                Vector2 expectedInner = ExpectedModalPan(inner, 1024f, 768f);
                Assert.AreEqual(expectedInner.x, pan.Pan.x, 0.01f);
                Assert.AreEqual(expectedInner.y, pan.Pan.y, 0.01f);
                Assert.AreEqual(2, pan.ModalPanDepthForAudit);
                Assert.IsTrue(pan.PanSuspended);

                pan.ResumeModal();
                Assert.AreEqual(outerPan, pan.Pan, "closing inner returns to outer dialog pan");
                Assert.IsTrue(pan.PanSuspended, "still suspended while outer modal open");

                pan.ResumeModal();
                Assert.AreEqual(saved, pan.Pan, "closing last modal restores saved pan");
                Assert.IsFalse(pan.PanSuspended);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // --- V6ModalPan (frame → viewport 解決) ---

        [Test]
        public void ModalPan_ViaFrame_SuspendAndResume()
        {
            var frameGo = new GameObject("V6 ModalPan Test Frame", typeof(RectTransform));
            try
            {
                var frame = frameGo.GetComponent<RectTransform>();
                frame.SetParent(canvasGo.transform, false);
                V6CompactScreen.Place(frame, 1024, 768);
                var pan = PanOf(frame);
                pan.Pan = Vector2.zero;
                Vector2 before = pan.Pan;

                var dialog = new Rect(444f, 231f, 840f, 510f);
                V6ModalPan.Suspend(frame, dialog);
                Assert.IsTrue(V6ModalPan.IsSuspended(frame));
                Vector2 expected = ExpectedModalPan(dialog, 1024f, 768f);
                Assert.AreEqual(expected.x, pan.Pan.x, 0.01f);
                Assert.AreEqual(expected.y, pan.Pan.y, 0.01f);

                V6ModalPan.Resume(frame);
                Assert.IsFalse(V6ModalPan.IsSuspended(frame));
                Assert.AreEqual(before, pan.Pan);
            }
            finally
            {
                Object.DestroyImmediate(frameGo);
            }
        }

        [Test]
        public void ModalPan_NormalResolution_IsNoOp()
        {
            var frameGo = new GameObject("V6 ModalPan Normal Frame", typeof(RectTransform));
            try
            {
                var frame = frameGo.GetComponent<RectTransform>();
                frame.SetParent(canvasGo.transform, false);
                // SetUp がコンパクト画面を強制しているので、このテストだけ
                // 差し替えを解除して通常解像度の Place を検証する
                // (TearDown が zero に戻す)。
                V6CompactScreen.ScreenSizeOverrideForTests = Vector2Int.zero;
                V6CompactScreen.Place(frame, 1920, 1080);
                Assert.IsNull(V6CompactScreen.FindViewport(frame));

                // 例外なく no-op で戻る。
                V6ModalPan.Suspend(frame, new Rect(444f, 231f, 840f, 510f));
                Assert.IsFalse(V6ModalPan.IsSuspended(frame));
                V6ModalPan.Resume(frame);
            }
            finally
            {
                Object.DestroyImmediate(frameGo);
            }
        }

        // --- 初期パン (issue #748): フレーム中央から始まる ---

        [Test]
        public void Place_Compact_InitialPan_CentersFrame()
        {
            var frameGo = new GameObject("V6 InitialPan Frame", typeof(RectTransform));
            try
            {
                var frame = frameGo.GetComponent<RectTransform>();
                frame.SetParent(canvasGo.transform, false);
                V6CompactScreen.Place(frame, 1024, 768);
                var pan = PanOf(frame);

                // フレーム中央 (864,486) がビューポート中央 (512,384) に来る位置。
                Assert.AreEqual(-352f, pan.Pan.x, 0.01f, "initial pan.x centers frame horizontally");
                Assert.AreEqual(-102f, pan.Pan.y, 0.01f, "initial pan.y centers frame vertically");

                // ユーザーがパンした後の再配置では初期位置へ戻らない。
                pan.Pan = Vector2.zero;
                V6CompactScreen.Place(frame, 1024, 768);
                Assert.AreEqual(Vector2.zero, pan.Pan, "user pan persists across re-place");
            }
            finally
            {
                Object.DestroyImmediate(frameGo);
            }
        }

        // --- Option: 確認モーダル ---

        [Test]
        public void Option_ConfirmModal_SuspendsAndRestoresPan()
        {
            var view = V6OptionView.Create(
                (RectTransform)canvasGo.transform, () => { });
            try
            {
                var pan = PanOf(view.Root);
                pan.Pan = Vector2.zero; // 開く前の位置を明示 (初期中央と区別する)
                Vector2 before = pan.Pan;

                // Data タブ (index 3) の "映像キャッシュ" 行を実行 → 確認モーダル。
                view.SelectTabForAudit(3);
                view.SelectRowForAudit(0);
                view.ActivateSelectedRowForAudit();

                Assert.AreEqual(1, pan.ModalPanDepthForAudit, "confirm modal suspended pan");
                Assert.IsTrue(pan.PanSuspended);
                Vector2 expected = ExpectedModalPan(
                    new Rect(V6Frame.DialogPos, V6Frame.DialogSize), 1024f, 768f);
                Assert.AreEqual(expected.x, pan.Pan.x, 0.01f, "pan.x centers confirm dialog");
                Assert.AreEqual(expected.y, pan.Pan.y, 0.01f, "pan.y centers confirm dialog");

                // 「キャンセル」で閉じる (消去は実行しない)。
                var modal = V6Frame.GetModal(view.Root.gameObject);
                Assert.IsNotNull(modal);
                var cancel = modal.transform.Find("Dialog/Op Cancel");
                Assert.IsNotNull(cancel, "Op Cancel button not found");
                cancel.GetComponent<Button>().onClick.Invoke();

                Assert.AreEqual(0, pan.ModalPanDepthForAudit, "pan resumed after close");
                Assert.IsFalse(pan.PanSuspended);
                Assert.AreEqual(before, pan.Pan, "pan restored to pre-modal position");
            }
            finally
            {
                view.Dispose();
            }
        }

        // --- Option / Pause: KeyConfig モーダル ---

        [Test]
        public void Option_KeyConfigModal_SuspendsAndRestoresPan()
        {
            var view = V6OptionView.Create(
                (RectTransform)canvasGo.transform, () => { });
            try
            {
                var pan = PanOf(view.Root);
                pan.Pan = Vector2.zero;
                Vector2 before = pan.Pan;

                view.KeyConfigModalForAudit.Show();
                Assert.IsTrue(view.KeyConfigModalForAudit.IsOpen);
                Assert.AreEqual(1, pan.ModalPanDepthForAudit, "key config modal suspended pan");
                Assert.IsTrue(pan.PanSuspended);

                // KeyConfig Dialog (444,124) 840×724 の中心も (864,486)。
                Vector2 expected = ExpectedModalPan(
                    new Rect(V6KeyConfigModal.DialogX, V6KeyConfigModal.DialogY,
                        V6KeyConfigModal.DialogW, V6KeyConfigModal.DialogH),
                    1024f, 768f);
                Assert.AreEqual(expected.x, pan.Pan.x, 0.01f);
                Assert.AreEqual(expected.y, pan.Pan.y, 0.01f);

                view.KeyConfigModalForAudit.Hide();
                Assert.AreEqual(0, pan.ModalPanDepthForAudit);
                Assert.IsFalse(pan.PanSuspended);
                Assert.AreEqual(before, pan.Pan);
            }
            finally
            {
                view.Dispose();
            }
        }

        [Test]
        public void Pause_KeyConfigModal_SuspendsAndRestoresPan()
        {
            var view = V6PauseView.Create(
                (RectTransform)canvasGo.transform);
            Assert.IsNotNull(view, "V6PauseView.Create returned null");
            try
            {
                var pan = PanOf(view.Root);
                pan.Pan = Vector2.zero;
                Vector2 before = pan.Pan;

                view.KeyConfigModalForAudit.Show();
                Assert.IsTrue(view.KeyConfigModalForAudit.IsOpen);
                Assert.AreEqual(1, pan.ModalPanDepthForAudit);
                Assert.IsTrue(pan.PanSuspended);

                view.KeyConfigModalForAudit.Hide();
                Assert.AreEqual(0, pan.ModalPanDepthForAudit);
                Assert.IsFalse(pan.PanSuspended);
                Assert.AreEqual(before, pan.Pan);
            }
            finally
            {
                view.Dispose();
            }
        }

        // --- Pause: Key Layout リセット確認 ---

        [Test]
        public void Pause_ResetConfirm_SuspendsAndRestoresPan()
        {
            // 確認はカスタムレイアウトのときだけ出る。既定と違う割当を作る。
            Assert.IsTrue(KeyBindings.SetLane(0, new[] { KeyCode.I }),
                "カスタムレイアウトを作れない");

            var view = V6PauseView.Create(
                (RectTransform)canvasGo.transform);
            Assert.IsNotNull(view, "V6PauseView.Create returned null");
            try
            {
                var pan = PanOf(view.Root);
                pan.Pan = Vector2.zero;
                Vector2 before = pan.Pan;

                view.RequestResetKeyLayout();
                Assert.IsTrue(view.HasConfirmDialog, "reset confirm dialog must open");
                Assert.AreEqual(1, pan.ModalPanDepthForAudit,
                    "reset confirm modal suspended pan");
                Assert.IsTrue(pan.PanSuspended);

                view.CancelResetKeyLayout();
                Assert.IsFalse(view.HasConfirmDialog);
                Assert.AreEqual(0, pan.ModalPanDepthForAudit);
                Assert.IsFalse(pan.PanSuspended);
                Assert.AreEqual(before, pan.Pan);
            }
            finally
            {
                view.Dispose();
            }
        }

        // --- Result: 登録モーダル (既存 API が共通機構へ移管済み) ---

        [Test]
        public void Result_RegisterModal_SuspendsAndRestoresPan()
        {
            var view = V6ResultView.Create(
                (RectTransform)canvasGo.transform, null, null, null);
            try
            {
                var pan = PanOf(view.Root);
                pan.Pan = Vector2.zero;
                Vector2 before = pan.Pan;

                view.AlignModalPanForViewport();
                Assert.AreEqual(1, pan.ModalPanDepthForAudit,
                    "register modal suspended pan via shared mechanism");
                Assert.IsTrue(pan.PanSuspended);

                view.RestoreModalPan();
                Assert.AreEqual(0, pan.ModalPanDepthForAudit);
                Assert.IsFalse(pan.PanSuspended);
                Assert.AreEqual(before, pan.Pan);
            }
            finally
            {
                view.Dispose();
            }
        }
    }
}
