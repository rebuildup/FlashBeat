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

        [Test]
        public static void ScreenCanvas_CarriesGraphicRaycaster()
        {
            // v5 の画面はコードで Canvas を組み立てるので、実際に画面を生成して確かめる。
            bool started = false;
            var view = TitleView.Build(() => started = true, () => { }, () => { }, () => { });
            try
            {
                var canvas = view.GetComponentInParent<Canvas>();
                Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>(),
                    "v5 の Canvas に GraphicRaycaster が無いとマウスクリックが Button に届かない。");
                Assert.IsNotNull(canvas.GetComponent<CanvasScaler>(),
                    "v5 の Canvas に CanvasScaler が無いと解像度追従しない。");
            }
            finally
            {
                Object.DestroyImmediate(view.GetComponentInParent<Canvas>().gameObject);
            }
        }

        [Test]
        public static void TitleCanvas_CarriesGraphicRaycaster_AndMenuButtonCallsRealHandler()
        {
            bool started = false;
            bool exited = false;
            var view = TitleView.Build(() => started = true, () => { }, () => exited = true, () => { });
            try
            {
                var canvas = view.GetComponentInParent<Canvas>();
                Assert.IsNotNull(canvas, "Title Canvas が生成されていない。");
                Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>(),
                    "Title の Canvas に GraphicRaycaster が無い (クリックが効かない原因)。");

                var startButton = FindButton(canvas.gameObject, "Menu GameStart");
                Assert.IsNotNull(startButton, "Title の GameStart ボタンが見つからない。");
                startButton.onClick.Invoke();
                Assert.IsTrue(started, "GameStart ボタンを押しても実ハンドラが呼ばれていない。");

                var exitButton = FindButton(canvas.gameObject, "Menu End");
                Assert.IsNotNull(exitButton, "Title の End ボタンが見つからない。");
                exitButton.onClick.Invoke();
                Assert.IsTrue(exited, "End ボタンを押しても実ハンドラが呼ばれていない。");
            }
            finally
            {
                Object.DestroyImmediate(view.GetComponentInParent<Canvas>().gameObject);
            }
        }

        [Test]
        public static void OptionCanvas_CarriesGraphicRaycaster_AndBackButtonCallsRealHandler()
        {
            bool back = false;
            var view = OptionView.Build(() => back = true, () => { }, () => { }, () => { }, () => { });
            try
            {
                var canvas = view.GetComponentInParent<Canvas>();
                Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>(),
                    "Option の Canvas に GraphicRaycaster が無い (クリックが効かない原因)。");

                var backButton = FindButton(canvas.gameObject, "Back");
                Assert.IsNotNull(backButton, "Option の Back ボタンが見つからない。");
                backButton.onClick.Invoke();
                Assert.IsTrue(back, "Back ボタンを押しても実ハンドラが呼ばれていない。");
            }
            finally
            {
                Object.DestroyImmediate(view.GetComponentInParent<Canvas>().gameObject);
            }
        }

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

            // Compact: 代表キー 1 個の帯 (レーン 1 の既定キーは 1 / Q / A / Z)。
            PlaySession.SetKeyGuide(KeyGuideMode.Compact);
            var hudCompact = new GameObject("V5 HUD Test").AddComponent<GameHudView>();
            try
            {
                hudCompact.Build();
                var row = hudCompact.Canvas.transform.Find("Key Guide");
                Assert.IsNotNull(row, "Key Guide = Compact なのにガイド strip が作られていない。");
                var cap = row.GetChild(0).GetComponentInChildren<TMPro.TextMeshProUGUI>();
                Assert.IsNotNull(cap, "ガイドのキーキャップに文字が無い。");
                Assert.IsFalse(cap.text.Contains("Q"),
                    "Compact モードで代表キー以外のキー (Q) まで表示されている: " + cap.text);
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
                var canvas = view.GetComponentInParent<Canvas>();
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
                    var canvas = view.GetComponentInParent<Canvas>();
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
    }
}
