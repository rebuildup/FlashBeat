using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Option;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Option 画面 Game Preview の時計・判定まわりの回帰テスト (Issue #732)。
    ///
    /// オーナー実機で「判定がすべて MISS」になった件の再現と固定:
    ///
    /// - 入力が無いプレビューでは全ノーツが時刻切れで MISS になるのが
    ///   従来契約だった。デモ契約として決定的なオートプレイを入れ、
    ///   PERFECT/GREAT/BAD/MISS がすべて観察できることを検証する。
    /// - 既存テストは TickPreviewForAudit(mediaTime, dt) で監査用の
    ///   シームを通るため、本番だけが通る
    ///   「Mathf.Repeat(Time.time, loopSeconds) → AdvanceClock」の
    ///   折り返し・チャート変更時のリベースを検証する。
    /// </summary>
    public sealed class V6OptionPreviewClockTests
    {
        private GameObject canvasGo;
        private int savedSongId;
        private int savedNoteTiming;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6OptionPreviewClockTests Canvas",
                typeof(RectTransform), typeof(Canvas));
            savedSongId = GManager.songID;
            savedNoteTiming = GManager.noteTiming;
            GManager.songID = 1; // ロストアンブレラ (実チャートあり)
        }

        [TearDown]
        public void TearDown()
        {
            KeyBindings.ClearInjectedLaneKeyDowns();
            KeyBindings.ClearInjectedKeyHolds();
            GManager.songID = savedSongId;
            GManager.noteTiming = savedNoteTiming;

            var view = V6OptionView.Current;
            if (view != null)
            {
                view.Dispose();
            }

            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }
            canvasGo = null;
        }

        private V6OptionView CreateView()
        {
            return V6OptionView.Create(canvasGo.GetComponent<RectTransform>(), () => { });
        }

        /// <summary>
        /// 実機の <see cref="V6OptionPreviewRig.Tick"/> と同じく、
        /// メディア時刻をループ長で折り返してから監査シームへ流す。
        /// 1 フレーム = <paramref name="dt"/> 秒として t を進める。
        /// </summary>
        private static void DriveLoop(V6OptionView view, float loopSeconds,
            float fromSeconds, float toSeconds, float dt)
        {
            for (float t = fromSeconds; t <= toSeconds; t += dt)
            {
                view.TickPreviewForAudit(Mathf.Repeat(t, loopSeconds), dt);
            }
        }

        /// <summary>
        /// オートプレイ (既定 ON) が 1 ループを回したとき、
        /// PERFECT/GREAT/BAD/MISS の 4 種すべての判定ポップが出ること。
        /// これが「全 MISS ではない」デモ契約の本体 (Issue #732 受入条件)。
        /// </summary>
        [Test]
        public void AutoPlay_ProducesEveryJudgementKind()
        {
            V6OptionPreviewChart.BuildForCurrentSong(out float loopSeconds);
            Assert.Greater(loopSeconds, 1f, "ループ長が異常");

            var view = CreateView();
            Assert.IsTrue(view.PreviewAutoPlayForAudit,
                "オートプレイは既定で有効 (デモ契約)");

            // 実機と同じ折り返し時計で 1 ループ強を回す。
            DriveLoop(view, loopSeconds, 0f, loopSeconds + 1f, 0.02f);

            Assert.Greater(view.PreviewVerdictCountForAudit(LongNote.Judgement.Perfect), 0,
                "PERFECT が 1 件も出ていない");
            Assert.Greater(view.PreviewVerdictCountForAudit(LongNote.Judgement.Great), 0,
                "GREAT が 1 件も出ていない");
            Assert.Greater(view.PreviewVerdictCountForAudit(LongNote.Judgement.Bad), 0,
                "BAD が 1 件も出ていない");
            Assert.Greater(view.PreviewVerdictCountForAudit(LongNote.Judgement.Miss), 0,
                "MISS が 1 件も出ていない (オートプレイの skip 役が機能していない)");
            Assert.Greater(view.PreviewJudgementCountForAudit, 0);
        }

        /// <summary>
        /// 実機と同じ折り返し時計 (Mathf.Repeat → AdvanceClock) を
        /// ループ境界で跨いでも、previewTime が巻き戻らず・飛ばずに
        /// 進み続けること。これが壊れるとキュー済みノーツが一斉に
        /// 時刻切れになり全 MISS になる (Issue #732 の時計経路)。
        /// </summary>
        [Test]
        public void Clock_LoopWrap_KeepsPreviewTimeAdvancing()
        {
            V6OptionPreviewChart.BuildForCurrentSong(out float loopSeconds);
            var view = CreateView();
            view.PreviewAutoPlayForAudit = false;

            const float dt = 0.02f;
            float previous = view.PreviewTimeForAudit;
            DriveLoop(view, loopSeconds, 0f, loopSeconds + 1f, dt);

            // 折り返しを 1 回以上跨いだ総時間ぶん進んでいるはず。
            Assert.AreEqual(loopSeconds + 1f, view.PreviewTimeForAudit, 0.5f,
                "折り返しで previewTime が飛んだ/巻き戻った");
            Assert.Greater(view.PreviewTimeForAudit, previous);
        }

        /// <summary>
        /// チャート (曲) が変わったとき、旧ループ長の位相を持つ時計状態を
        /// そのまま使い回さずリベースすること (Issue #732 の根本原因)。
        ///
        /// 再現: 実チャートのループ末尾近くまで進めた状態で曲を
        /// 「チャート無しの曲」(GetSong が null → 疑似チャート 40 秒) へ
        /// 変える。旧実装は previousMediaTime が旧ループ長の値のまま残り、
        /// 次の mediaTime との差分が previewTime へそのまま混入して
        /// キュー済みノーツが一斉に時刻切れ (MISS バースト) になった。
        /// 修正後は previewTime が新しい mediaTime を原点にリベースされ、
        /// その場で MISS は出ない。
        /// </summary>
        [Test]
        public void Clock_SongChange_RebasesWithoutMissBurst()
        {
            V6OptionPreviewChart.BuildForCurrentSong(out float loopA);
            var view = CreateView();
            view.PreviewAutoPlayForAudit = false;

            // 実チャートのループ末尾近くまで進める (旧ループ長の位相を作る)。
            DriveLoop(view, loopA, 0f, loopA - 0.1f, 0.05f);
            float before = view.PreviewTimeForAudit;
            Assert.Greater(before, loopA * 0.5f, "前提: ループ途中まで進んでいない");

            int missBefore = view.PreviewVerdictCountForAudit(LongNote.Judgement.Miss);

            // 曲を「実チャート無し」へ切り替える → 疑似チャート (40 秒) へ
            // 組み替わり、ループ長のドメインが変わる。
            GManager.songID = -1;
            // 新ドメインの小さい mediaTime (Repeat が返し得る任意の値)。
            view.TickPreviewForAudit(0.1f, 0.016f);

            // リベース: previewTime は新しい mediaTime を原点に取り直す。
            // 旧実装では旧ループ長ぶんの値が残り続ける。
            Assert.AreEqual(0.1f, view.PreviewTimeForAudit, 0.05f,
                "曲変更で疑似時計がリベースされていない");

            // 新しいキューは未来のノーツだけを積むので、この時点で
            // MISS が新たに出てはいけない (バーストの検出)。
            Assert.AreEqual(missBefore,
                view.PreviewVerdictCountForAudit(LongNote.Judgement.Miss),
                "曲変更の直後に MISS がバーストしている");

            // その後も折り返し時計として破綻なく進むこと。
            float pseudoLoop = V6OptionPreviewChart.LoopSeconds;
            DriveLoop(view, pseudoLoop, 0.116f, pseudoLoop + 0.5f, 0.05f);
            Assert.Greater(view.PreviewTimeForAudit, 0.1f);
        }
    }
}
