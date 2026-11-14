using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Issue #167: v5 UI が「実動作」していることを検証する EditMode テスト。
    ///
    /// 方針: フェイクイベントを自前で発火するのではなく、
    /// **実際の v5 View を生成し、実際に钮の onClick を呼んで実ハンドラが
    /// 呼ばれること** を検証する (画面生成 → クリック → 実結果)。
    /// </summary>
    public static class V5UiWiringTests
    {
        // --- Canvas / Raycaster (Scenario 1: マウスで押せること) ---
        // Title / Option の v5 View は生成経路ゼロのため削除済み (Issue #497)。
        // 同じ契約 (Canvas の Raycaster / 実ハンドラ) は残る v5 View
        // (Result / Select / Pause 等) のテストが引き続き固定する。

        [Test]
        public static void ResultCanvas_CarriesGraphicRaycaster_AndRetryButtonCallsRealHandler()
        {
            bool retried = false;
            bool selected = false;
            var snapshot = new ResultView.Snapshot
            {
                Score = 123456,
                Perfect = 10,
                Great = 2,
                Bad = 1,
                Miss = 0,
                Combo = 12,
                PrevHiscore = 1000,
                Hiscore = 123456,
                SongTitle = "Test Song",
                Musician = "Tester",
                Bpm = 120,
                Level = 5,
                TotalHits = 13,
            };
            var view = ResultView.Build(snapshot, () => retried = true, () => selected = true);
            try
            {
                var canvas = view.GetComponentInParent<Canvas>();
                Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>(),
                    "Result の Canvas に GraphicRaycaster が無い (クリックが効かない原因)。");

                var retryButton = FindButton(canvas.gameObject, "Button Retry");
                Assert.IsNotNull(retryButton, "Result の Retry ボタンが見つからない。");
                retryButton.onClick.Invoke();
                Assert.IsTrue(retried, "Retry ボタンを押しても実ハンドラが呼ばれていない。");

                var okButton = FindButton(canvas.gameObject, "Button OK");
                Assert.IsNotNull(okButton, "Result の OK ボタンが見つからない。");
                okButton.onClick.Invoke();
                Assert.IsTrue(selected, "OK ボタンを押しても実ハンドラが呼ばれていない。");
            }
            finally
            {
                Object.DestroyImmediate(view.GetComponentInParent<Canvas>().gameObject);
            }
        }

        // --- Ready の設定が HUD に届くこと (Scenario 2) ---

        [Test]
        public static void ReadyOptions_AreForwardedToGameHud()
        {
            // Off: ガイドを出さない。
            PlaySession.SetKeyGuide(KeyGuideMode.Off);
            var hudOff = new GameObject("V5 HUD Test").AddComponent<GameHudView>();
            try
            {
                hudOff.Build();
                Assert.IsNull(hudOff.Canvas.transform.Find("Key Guide"),
                    "Key Guide = Off なのにガイド strip が作られている。");
            }
            finally
            {
                Object.DestroyImmediate(hudOff.Canvas.gameObject);
                Object.DestroyImmediate(hudOff.gameObject);
            }

            // Compact: 代表キー 1 個の帯。代表キーは GameHudView の規則
            // (レーンのキーから最初の文字キー。レーン 1 の既定 1 / Q / A / Z なら Q)。
            PlaySession.SetKeyGuide(KeyGuideMode.Compact);
            var hudCompact = new GameObject("V5 HUD Test").AddComponent<GameHudView>();
            try
            {
                hudCompact.Build();
                var row = hudCompact.Canvas.transform.Find("Key Guide");
                Assert.IsNotNull(row, "Key Guide = Compact なのにガイド strip が作られていない。");
                var cap = row.GetChild(0).GetComponentInChildren<TMPro.TextMeshProUGUI>();
                Assert.IsNotNull(cap, "ガイドのキーキャップに文字が無い。");
                Assert.IsFalse(string.IsNullOrEmpty(cap.text), "Compact モードの代表キーが空。");
                Assert.IsFalse(cap.text.Contains(" "),
                    "Compact モードで代表キー以外 (レーンの全キー) を出している: " + cap.text);
                Assert.IsTrue(cap.text == "1" || cap.text == "Q" || cap.text == "A" || cap.text == "Z",
                    "Compact モードの代表キーがレーン 1 のキーでない: " + cap.text);
            }
            finally
            {
                Object.DestroyImmediate(hudCompact.Canvas.gameObject);
                Object.DestroyImmediate(hudCompact.gameObject);
            }

            // Full: レーンの全キーを出す。
            PlaySession.SetKeyGuide(KeyGuideMode.Full);
            var hudFull = new GameObject("V5 HUD Test").AddComponent<GameHudView>();
            try
            {
                hudFull.Build();
                var row = hudFull.Canvas.transform.Find("Key Guide");
                Assert.IsNotNull(row, "Key Guide = Full なのにガイド strip が作られていない。");
                var cap = row.GetChild(0).GetComponentInChildren<TMPro.TextMeshProUGUI>();
                Assert.IsNotNull(cap, "ガイドのキーキャップに文字が無い。");
                Assert.IsTrue(cap.text.Contains("Q"),
                    "Full モードでレーンの全キーが表示されていない: " + cap.text);
            }
            finally
            {
                Object.DestroyImmediate(hudFull.Canvas.gameObject);
                Object.DestroyImmediate(hudFull.gameObject);
                PlaySession.Reset();
            }
        }

        [Test]
        public static void SelectReadyRow_WritesPlaySession()
        {
            int previousSong = GManager.songID;
            GManager.songID = 1; // 再生可能な固定曲
            var ownerGo = new GameObject("SelectSceneManager Test");
            ownerGo.AddComponent<SelectSceneManager>();
            SelectView view = null;
            try
            {
                view = SelectView.Create(ownerGo.GetComponent<SelectSceneManager>());
                // SelectView は Canvas を View の **子** に作る (Title 等は親方向)。
                // どちらの構成でも拾えるように両方向を探索する。
                var canvas = view.GetComponentInParent<Canvas>() ?? view.GetComponentInChildren<Canvas>();
                Assert.IsNotNull(canvas, "Select の Canvas が生成されていない。");
                Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>(),
                    "Select の Canvas に GraphicRaycaster が無い (クリックが効かない原因)。");

                // Ready 画面へ進む (実ボタン)。
                var readyButton = FindButton(canvas.gameObject, "Ready");
                Assert.IsNotNull(readyButton, "Select の Ready ボタンが見つからない。");
                readyButton.onClick.Invoke();

                // Key Guide 行の → ボタン (Compact → Full)。
                PlaySession.SetKeyGuide(KeyGuideMode.Compact);
                var nextButton = FindButton(canvas.gameObject, "Next");
                Assert.IsNotNull(nextButton, "Ready 画面の Next ボタンが見つからない。");
                nextButton.onClick.Invoke();
                Assert.AreEqual(KeyGuideMode.Full, PlaySession.KeyGuide,
                    "Ready のキーガイド変更がセッション状態に残っていない。");
            }
            finally
            {
                if (view != null)
                {
                    var canvas = view.GetComponentInParent<Canvas>() ?? view.GetComponentInChildren<Canvas>();
                    if (canvas != null) { Object.DestroyImmediate(canvas.gameObject); }
                }

                Object.DestroyImmediate(ownerGo);
                PlaySession.Reset();
                GManager.songID = previousSong;
            }
        }

        // --- 死んだ行が残っていないこと (Scenario 3) ---

        [Test]
        public static void PauseDisplay_HasNoDeadRows()
        {
            var canvasGo = new GameObject("V5PauseCanvas Test",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var pause = canvasGo.AddComponent<PauseMenuView>();
            pause.Build(canvas);
            try
            {
                var display = canvas.transform.Find("Pause/Display Page");
                Assert.IsNotNull(display, "Pause の Display タブが見つからない。");

                Assert.IsNotNull(display.Find("List/Row Key Guide"),
                    "Key Guide 行がが無い (実動作する行なのに消えている)。");
                Assert.IsNotNull(display.Find("List/Row Lyrics"),
                    "Lyrics 行がが無い (実動作する行なのに消えている)。");

                string[] deadRows = { "Row Live Rank", "Row Hit Timing", "Row Judge Text", "Row Progress Bar" };
                for (int i = 0; i < deadRows.Length; i++)
                {
                    Assert.IsNull(display.Find("List/" + deadRows[i]),
                        "実装の無い行 [" + deadRows[i] + "] がまだ画面に残っている。");
                }
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        // --- 設定の保存と復元 ---

        [Test]
        public static void OptionSettings_SaveAndLoad_RoundTrips()
        {
            float main = GManager.mainVolume;
            float effect = GManager.effectVolume;
            float bgm = GManager.BGMVolume;
            float speed = GManager.noteSpeed;
            int timing = GManager.noteTiming;

            try
            {
                GManager.mainVolume = 0.42f;
                GManager.effectVolume = 0.31f;
                GManager.BGMVolume = 0.23f;
                GManager.noteSpeed = 12f;
                GManager.noteTiming = 4;
                OptionSettings.Save();

                GManager.mainVolume = 1f;
                GManager.effectVolume = 1f;
                GManager.BGMVolume = 1f;
                GManager.noteSpeed = 1f;
                GManager.noteTiming = 0;
                OptionSettings.Load();

                Assert.AreEqual(0.42f, GManager.mainVolume, 0.001f, "Main Volume が復元されていない。");
                Assert.AreEqual(0.31f, GManager.effectVolume, 0.001f, "Effect Volume が復元されていない。");
                Assert.AreEqual(0.23f, GManager.BGMVolume, 0.001f, "BGM Volume が復元されていない。");
                Assert.AreEqual(12f, GManager.noteSpeed, 0.001f, "NoteSpeed が復元されていない。");
                Assert.AreEqual(4, GManager.noteTiming, "NoteTiming が復元されていない。");
            }
            finally
            {
                GManager.mainVolume = main;
                GManager.effectVolume = effect;
                GManager.BGMVolume = bgm;
                GManager.noteSpeed = speed;
                GManager.noteTiming = timing;
                OptionSettings.Save();
            }
        }

        // --- Findings F2 / F6 / F7 / F8 / F10 / F15 / F16 / F17 の直接検証 ---

        [Test]
        public static void LegacyCanvasRaycaster_IsDisabled()
        {
            // F2: GameScene のレガシー Canvas は sortingOrder 0 で v5 HUD (-1) より手前。
            // GraphicRaycaster が有効なままだとレガシー側が v5 のボタンを横取りする。
            // GameHudController.DisableLegacyRaycastersIn (Update と同じ処理をシーン単位で
            // 抽出した公開静的関数) を、実際に作ったシーンで検証する。
            // EditMode では NewScene(Additive) が「未保存の untitled シーンがあると
            // 追加シーンを作れない」で落ちる (batchmode では必ずそうなる) ため、
            // プレビューシーンを使う。GameObject はアクティブシーンに作ってから
            // EditorSceneManager.MoveGameObjectToScene で移す。
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var canvasGo = new GameObject("Legacy Canvas", typeof(Canvas), typeof(GraphicRaycaster));
                UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(canvasGo, scene);

                Assert.AreEqual(1, GameHudController.DisableLegacyRaycastersIn(scene),
                    "レガシー Canvas の GraphicRaycaster が無効化されていない (F2)。");
                Assert.IsFalse(canvasGo.GetComponent<GraphicRaycaster>().enabled,
                    "GraphicRaycaster が enabled のまま (v5 のボタンが横取りされる)。");

                // 冪等: 2 回目は無効化するものが無い。
                Assert.AreEqual(0, GameHudController.DisableLegacyRaycastersIn(scene),
                    "2 回目の呼び出しで個数が増えた (冪等でない)。");

                // Raycaster を持たない Canvas は数えない。
                var plainGo = new GameObject("Plain Canvas", typeof(Canvas));
                UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(plainGo, scene);
                Assert.AreEqual(0, GameHudController.DisableLegacyRaycastersIn(scene),
                    "GraphicRaycaster の無い Canvas を数えている。");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public static void PauseRanking_IsLabelledLocal()
        {
            // F6: Pause Ranking タブの中身は GManager.Hiscore のローカル総計なので、
            // 「ローカル」であることを UI 上で明記する (R5)。
            int prevSong = GManager.songID;
            GManager.songID = 1; // ローカル固定曲
            var canvasGo = new GameObject("V5PauseCanvas Test",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var pause = canvasGo.AddComponent<PauseMenuView>();
            pause.Build(canvasGo.GetComponent<Canvas>());
            try
            {
                // 実タブボタン (Tab Ranking) を押して Ranking ページへ切り替える。
                var tabButton = FindButton(canvasGo, "Tab Ranking");
                Assert.IsNotNull(tabButton, "Pause の Ranking タブが見つからない。");
                tabButton.onClick.Invoke();

                var rankingPage = canvasGo.transform.Find("Pause/Ranking Page");
                Assert.IsNotNull(rankingPage, "Pause の Ranking ページが無い。");
                Assert.IsTrue(rankingPage.gameObject.activeSelf,
                    "Ranking タブを押してもページが切り替わらない。");

                var perSong = FindTextContaining(canvasGo, "この曲のベスト (ローカル)");
                Assert.IsNotNull(perSong,
                    "Pause Ranking に「この曲のベスト (ローカル)」の見出しが無い (F6)。");
                var cumulative = FindTextContaining(canvasGo, "この端末の合計 (ローカル)");
                Assert.IsNotNull(cumulative,
                    "Pause Ranking に「この端末の合計 (ローカル)」の見出しが無い (F6)。");
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                GManager.songID = prevSong;
            }
        }

        [Test]
        public static void SelectRanking_HidesOnlineSegmentForNonCloudSong()
        {
            // F7: ローカル曲 (isRemote = false) のランキング画面に Online セグメントを
            // 出さない (未接続表示だけの列を作らない)。
            int prevSong = GManager.songID;
            GManager.songID = 1; // ローカル固定曲
            var ownerGo = new GameObject("SelectSceneManager Test");
            ownerGo.AddComponent<SelectSceneManager>();
            SelectView view = null;
            try
            {
                view = SelectView.Create(ownerGo.GetComponent<SelectSceneManager>());
                // F3 キーと同じ経路。EnterRanking は private のため reflection で 1 回だけ呼ぶ
                // (テストのためだけに公開 API は広げない)。
                typeof(SelectView).GetMethod("EnterRanking",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(view, null);

                var canvas = view.GetComponentInParent<Canvas>() ?? view.GetComponentInChildren<Canvas>();
                Assert.IsNotNull(FindButton(canvas.gameObject, "Local"),
                    "Ranking 画面に Local セグメントが無い。");
                Assert.IsNull(FindButton(canvas.gameObject, "Online"),
                    "ローカル曲なのに Online セグメントが出ている (F7)。");
                Assert.IsFalse(SelectView.CanFetchOnlineRanking(GManager.GetSong(GManager.songID)),
                    "ローカル曲が Online 取得の条件を満たしている (F7)。");
            }
            finally
            {
                if (view != null) { Object.DestroyImmediate(view.gameObject); }
                Object.DestroyImmediate(ownerGo);
                GManager.songID = prevSong;
            }
        }

        [Test]
        public static void SelectRanking_ShowsOnlineSegmentForCloudSong()
        {
            // F7 の cloud 側。方式の明記: GManager.ActiveSongs は静的カタログで、テストから
            // cloud 曲を 1 曲だけ差し込む公共 API が無い (ApplyRemoteCatalog はカタログ全体を
            // 作り直すためテストには過剰)。そこで spec が許す直接検証に切り替えた:
            // CanFetchOnlineRanking を public static にしたので、SongData を組み立てて
            // cloud 曲 / 非 cloud 曲の真偽を直接検証する。
            var cloud = new SongData(1, "Cloud Song", "tester", "VIjqWffacio", 120, 5, 100, 60f);
            cloud.isRemote = true;
            cloud.remoteId = "chart-abc123";
            cloud.chartVersion = 2;
            Assert.IsTrue(SelectView.CanFetchOnlineRanking(cloud),
                "cloud 曲 (isRemote && remoteId && chartVersion>0) で Online セグメントの条件が偽 (F7)。");

            // 境界: remoteId 無し / chartVersion 0 / ローカル曲は不可。
            var noRemoteId = new SongData(2, "No RemoteId", "tester", "VIjqWffacio", 120, 5, 100, 60f);
            noRemoteId.isRemote = true;
            noRemoteId.chartVersion = 2;
            Assert.IsFalse(SelectView.CanFetchOnlineRanking(noRemoteId),
                "remoteId 無しの曲が Online 取得の条件を満たしている (F7)。");

            var noChart = new SongData(3, "No Chart", "tester", "VIjqWffacio", 120, 5, 100, 60f);
            noChart.isRemote = true;
            noChart.remoteId = "chart-abc123";
            Assert.IsFalse(SelectView.CanFetchOnlineRanking(noChart),
                "chartVersion 0 の曲が Online 取得の条件を満たしている (F7)。");

            var local = new SongData(4, "Local Song", "tester", "VIjqWffacio", 120, 5, 100, 60f);
            Assert.IsFalse(SelectView.CanFetchOnlineRanking(local),
                "ローカル曲が Online 取得の条件を満たしている (F7)。");
        }

        [Test]
        public static void SelectRanking_LabelsPlayersAsLocal()
        {
            // F8: 「登録名ごとのランキング」パネルの実体はこの端末の記録だけなので、
            // 見出しで「ローカル」と明記する (R5)。
            int prevSong = GManager.songID;
            GManager.songID = 1; // ローカル固定曲
            var ownerGo = new GameObject("SelectSceneManager Test");
            ownerGo.AddComponent<SelectSceneManager>();
            SelectView view = null;
            try
            {
                view = SelectView.Create(ownerGo.GetComponent<SelectSceneManager>());
                typeof(SelectView).GetMethod("EnterRanking",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(view, null);

                var canvas = view.GetComponentInParent<Canvas>() ?? view.GetComponentInChildren<Canvas>();
                var playersHead = FindTextContaining(canvas.gameObject, "この端末の記録 (ローカル)");
                Assert.IsNotNull(playersHead,
                    "Select Ranking のプレイヤーパネルに「この端末の記録 (ローカル)」の見出しが無い (F8)。");
                var playersSub = FindTextContaining(canvas.gameObject, "オンライン未接続");
                Assert.IsNotNull(playersSub,
                    "Select Ranking のプレイヤーパネルにオンライン未接続の注記が無い (F8)。");
            }
            finally
            {
                if (view != null) { Object.DestroyImmediate(view.gameObject); }
                Object.DestroyImmediate(ownerGo);
                GManager.songID = prevSong;
            }
        }

        [Test]
        public static void ResultRegister_IsLabelledLocal()
        {
            // F10: Result の登録導線はオンライン登録ではなく「この端末の名前」の保存。
            // ボタン名は Register → Name に改名され、モーダルに「ローカル」バッジが出る (R5)。
            bool retried = false;
            var snapshot = new ResultView.Snapshot
            {
                Score = 123456,
                Perfect = 10,
                Great = 2,
                Bad = 1,
                Miss = 0,
                Combo = 12,
                PrevHiscore = 1000,
                Hiscore = 123456,
                SongTitle = "Test Song",
                Musician = "Tester",
                Bpm = 120,
                Level = 5,
                TotalHits = 13,
            };
            var view = ResultView.Build(snapshot, () => retried = true, () => { });
            try
            {
                var canvas = view.GetComponentInParent<Canvas>();
                var nameButton = FindButton(canvas.gameObject, "Button Name");
                Assert.IsNotNull(nameButton,
                    "Result の Name ボタン (旧 Register) が見つからない (F10)。");
                Assert.IsNull(FindButton(canvas.gameObject, "Button Register"),
                    "旧 Register ボタンがまだ残っている (F10 の誤ラベルが戻っている)。");

                // 実ボタン → 実ハンドラ (OpenModal)。
                nameButton.onClick.Invoke();

                var modal = view.transform.Find("Register Modal");
                Assert.IsNotNull(modal, "Register モーダルが無い。");
                Assert.IsTrue(modal.gameObject.activeSelf,
                    "Name ボタンを押してもモーダルが開かない。");
                var head = FindTextContaining(modal.gameObject, "この端末の名前");
                Assert.IsNotNull(head,
                    "モーダル見出しが「この端末の名前」でない (F10 の誤ラベルが戻っている)。");
                var badge = FindTextContaining(modal.gameObject, "ローカル");
                Assert.IsNotNull(badge,
                    "モーダルに「ローカル」バッジが無い (F10)。");
                Assert.IsFalse(retried, "Name ボタンで Retry が発火した (配線の混線)。");
            }
            finally
            {
                Object.DestroyImmediate(view.GetComponentInParent<Canvas>().gameObject);
            }
        }

        [Test]
        public static void LegacyBackOpensMenu_TruthTable()
        {
            // F15: レガシー MenuPanel が Esc を担うのは、v5 Pause と v6 ポーズの
            // どちらも無いときだけ (二重 handling の防止)。
            Assert.IsTrue(GameSceneManager.LegacyBackOpensMenu(false, false),
                "v5 Pause も v6 ポーズも無いときはレガシーが Esc を担う。");
            Assert.IsFalse(GameSceneManager.LegacyBackOpensMenu(true, false),
                "v5 Pause があるときにレガシーが Esc を担うと二重処理になる (F15)。");
            Assert.IsFalse(GameSceneManager.LegacyBackOpensMenu(false, true),
                "v6 ポーズが有効なときにレガシーが Esc を担うと二重処理になる (Issue #224)。");
            Assert.IsFalse(GameSceneManager.LegacyBackOpensMenu(true, true),
                "v5 Pause と v6 ポーズの両方があるときにレガシーが Esc を担うと三重処理になる。");
        }

        [Test]
        public static void ResultBestScore_IsAppliedInAwake()
        {
            // F16: ベストスコアの更新は Awake で確定させる (Start の実行順に依存しない)。
            // 抽出した ApplyBestScore を直接検証する。返り値は更新前の値。
            int[] backup = (int[])GManager.Hiscore.Clone();
            try
            {
                // 新記録: 更新前の値を返し、テーブルが上がる。
                GManager.Hiscore[1] = 100;
                Assert.AreEqual(100, ResultSceneManager.ApplyBestScore(1, 500),
                    "返り値が更新前の値でない (F16)。");
                Assert.AreEqual(500, GManager.Hiscore[1],
                    "新記録が保存テーブルに反映されていない (F16)。");

                // 下回るスコア: テーブルは変わらない。
                Assert.AreEqual(500, ResultSceneManager.ApplyBestScore(1, 50),
                    "下回るスコアで返り値が更新後の値になった (F16)。");
                Assert.AreEqual(500, GManager.Hiscore[1],
                    "下回るスコアで記録が書き換わった (F16)。");

                // 同点: 記録を置き換える (>= で更新)。
                Assert.AreEqual(500, ResultSceneManager.ApplyBestScore(1, 500), "同点の返り値が不正。");
                Assert.AreEqual(500, GManager.Hiscore[1], "同点で記録が変わった。");

                // 範囲外 songId (cloud カタログで増えた曲) は 0 番スロットを
                // 共有しない (issue-162 A-6 再照合)。安定キーの保存先へ書く。
                // 保存先はテスト用のメモリに差し替える (実セーブデータを汚さない)。
                var previousStorage = FlashBeat.Persistence.ScorePersistence.Storage;
                FlashBeat.Persistence.ScorePersistence.Storage =
                    new FlashBeat.Tests.Editor.MemoryScoreStorage();
                FlashBeat.Persistence.ScorePersistence.Unload();
                try
                {
                    GManager.Hiscore[0] = 0;
                    int outOfRange = GManager.Hiscore.Length + 3;
                    Assert.AreEqual(0, ResultSceneManager.ApplyBestScore(outOfRange, 700),
                        "範囲外 songId の返り値が安定キー保存先の前回値でない (F16)。");
                    Assert.AreEqual(0, GManager.Hiscore[0],
                        "範囲外 songId を 0 番スロットに書いてはいけない (issue-162 A-6)。");
                    Assert.AreEqual(700, FlashBeat.Persistence.ScorePersistence.BestOf(outOfRange),
                        "範囲外 songId のベストが安定キーの保存先に入っていない (issue-162 A-6)。");
                }
                finally
                {
                    FlashBeat.Persistence.ScorePersistence.Unload();
                    FlashBeat.Persistence.ScorePersistence.Storage = previousStorage;
                }
            }
            finally
            {
                for (int i = 0; i < backup.Length; i++) { GManager.Hiscore[i] = backup[i]; }
            }
        }

        [Test]
        public static void SelectReadyPanel_HasSingleStartButton_AndDisablesWhenNoPlayableSong()
        {
            // F17: Ready 画面の Start は画面右下の 1 個だけ (パネル内の重複は削除済み)。
            // 対象曲が再生できない (placeholder) ときは Start が押せない (R9)。
            int prevSong = GManager.songID;
            GManager.songID = 1; // 再生可能な固定曲
            var ownerGo = new GameObject("SelectSceneManager Test");
            ownerGo.AddComponent<SelectSceneManager>();
            SelectView view = null;
            try
            {
                view = SelectView.Create(ownerGo.GetComponent<SelectSceneManager>());
                var canvas = view.GetComponentInParent<Canvas>() ?? view.GetComponentInChildren<Canvas>();

                var readyButton = FindButton(canvas.gameObject, "Ready");
                Assert.IsNotNull(readyButton, "Select の Ready ボタンが見つからない。");
                readyButton.onClick.Invoke();

                var startButtons = ButtonsNamed(canvas.gameObject, "Start");
                Assert.AreEqual(1, startButtons.Count,
                    "Start ボタンが複数ある (F17 の重複 control が戻っている)。");
                Assert.IsTrue(startButtons[0].interactable,
                    "再生可能な曲なのに Start が無効。");

                // 未プレイ可能曲 (ニセモノ / noSong) では Start が押せない。
                int unplayable = FindUnplayableSongId();
                Assert.Greater(unplayable, 0, "未プレイ可能曲 (placeholder) がカタログに無い。");
                GManager.songID = unplayable;
                // Rebuild は private のため reflection で 1 回だけ呼ぶ (画面の張り直し)。
                typeof(SelectView).GetMethod("Rebuild",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(view, null);

                var start = FindButton(canvas.gameObject, "Start");
                Assert.IsNotNull(start, "Ready 画面の Start ボタンが見つからない。");
                Assert.IsFalse(start.interactable,
                    "再生できない曲 (placeholder) なのに Start が押せる (F17 / R9)。");
            }
            finally
            {
                if (view != null) { Object.DestroyImmediate(view.gameObject); }
                Object.DestroyImmediate(ownerGo);
                PlaySession.Reset();
                GManager.songID = prevSong;
            }
        }

        // --- ヘルパ ---

        /// <summary>名前から Button を探す (階層を探索)。</summary>
        private static Button FindButton(GameObject root, string name)
        {
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].gameObject.name == name) { return buttons[i]; }
            }

            return null;
        }

        /// <summary>名前が一致する Button をすべて返す (非アクティブも含む)。</summary>
        private static List<Button> ButtonsNamed(GameObject root, string name)
        {
            var found = new List<Button>();
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].gameObject.name == name) { found.Add(buttons[i]); }
            }

            return found;
        }

        /// <summary>表示文字列に needle を含む TextMeshProUGUI を返す (非アクティブも含む)。</summary>
        private static TMPro.TextMeshProUGUI FindTextContaining(GameObject root, string needle)
        {
            var texts = root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].text != null && texts[i].text.Contains(needle))
                {
                    return texts[i];
                }
            }

            return null;
        }

        /// <summary>カタログから再生できない曲 (placeholder) の id を返す。無ければ -1。</summary>
        private static int FindUnplayableSongId()
        {
            var songs = GManager.ActiveSongs;
            for (int i = 1; i < songs.Length; i++)
            {
                if (songs[i] != null && songs[i].unplayable) { return i; }
            }

            return -1;
        }
    }
}
