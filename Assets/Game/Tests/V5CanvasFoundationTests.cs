using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FlashBeat.UI.V5;

namespace FlashBeat.V5CanvasTests
{
    /// <summary>
    /// Issue #169: 全画面 / Canvas / EventSystem 基盤の EditMode テスト。
    /// - CanvasConfigurator の冪等性 (CanvasScaler / GraphicRaycaster / EventSystem の重複排除)
    /// - プロファイル選択が 3 解像度 (FHD / 2K / Mac 16:10) で破綻しないこと。
    /// 実行は Supervisor が TestRunner (EditMode) で行う。
    /// </summary>
    public class V5CanvasFoundationTests
    {
        [SetUp]
        public void SetUp()
        {
            UISizeProfile.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            // テスト生成物を掃除し、EventSystem の残留が他テストに漏れないようにする。
            foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(es.gameObject);
            }

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(canvas.gameObject);
            }

            UISizeProfile.ClearCache();
        }

        // --- T3: CanvasConfigurator の冪等性 ---

        [Test]
        public void Configure_SetsConstantPixelSizeScale1()
        {
            var canvas = NewCanvas();
            CanvasConfigurator.Configure(canvas);
            var scaler = canvas.GetComponent<CanvasScaler>();
            Assert.IsNotNull(scaler);
            Assert.AreEqual(CanvasScaler.ScaleMode.ConstantPixelSize, scaler.uiScaleMode);
            Assert.AreEqual(1f, scaler.scaleFactor);
        }

        [Test]
        public void Configure_AddsGraphicRaycasterToCanvasWithoutOne()
        {
            var canvas = NewCanvas();
            Assert.IsNull(canvas.GetComponent<GraphicRaycaster>(), "前提: Raycaster 無しで作成");

            CanvasConfigurator.Configure(canvas);

            Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>(), "Configure 後に raycaster が付与される");
        }

        [Test]
        public void Configure_Twice_SingleRaycasterAndConfigurator()
        {
            var canvas = NewCanvas();
            CanvasConfigurator.Configure(canvas);
            CanvasConfigurator.Configure(canvas);

            Assert.AreEqual(1, canvas.GetComponents<GraphicRaycaster>().Length, "GraphicRaycaster の重複生成をしない");
            Assert.AreEqual(1, canvas.GetComponents<CanvasConfigurator>().Count());
            Assert.AreEqual(1, canvas.GetComponents<CanvasScaler>().Length);
        }

        [Test]
        public void EnsureEventSystem_CreatesExactlyOne()
        {
            Assert.IsNull(Object.FindFirstObjectByType<EventSystem>(), "前提: EventSystem 無し");

            CanvasConfigurator.EnsureEventSystem();
            CanvasConfigurator.EnsureEventSystem();

            var systems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            Assert.AreEqual(1, systems.Length, "EventSystem は重複生成されない");
            Assert.IsNotNull(systems[0].GetComponent<StandaloneInputModule>(),
                "StandaloneInputModule 付きで生成される");
        }

        [Test]
        public void Configure_TwoCanvases_ReusesSingleEventSystem()
        {
            var canvas1 = NewCanvas();
            var canvas2 = NewCanvas();
            CanvasConfigurator.Configure(canvas1);
            CanvasConfigurator.Configure(canvas2);

            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length,
                "Canvas 複数でも EventSystem は 1 系統");
        }

        // --- T2: プロファイル契約 (3 解像度) ---

        [Test]
        public void ProfileSelect_ThreeResolutions()
        {
            Assert.AreEqual("fhd", UISizeProfile.Select(1920, 1080).Name);
            Assert.AreEqual("2k", UISizeProfile.Select(2560, 1440).Name);
            // Mac ネイティブ 2560x1664 (縦横比 1.538) は height>=1440 の 2k 規則より先に
            // 縦横比判定で mac に選ばれる (Issue #162/#169 指示)。
            Assert.AreEqual("mac", UISizeProfile.Select(2560, 1664).Name);
        }

        [Test]
        public void ProfileSelect_MacByAspect_Before2kPixelRule()
        {
            // 2560x1664 は画素数では 2K 相当だが、縦横比 1.538 の判定を先に行うので mac。
            Assert.AreEqual("mac", UISizeProfile.Select(2560, 1664).Name);
        }

        [Test]
        public void ProfileSelect_1440x900_NoLongerMac()
        {
            // 旧 1440x900 mac 設計は無効 (Issue #162)。16:10 は新規則のどれにも当てはまらず
            // fhd 縮小 (900/1080 = 0.833) に流れる。
            var p = UISizeProfile.Select(1440, 900);
            Assert.AreEqual("fhd*0.833", p.Name);
            Assert.LessOrEqual(p.CardCenter.y, 900f, "縮小 fhd が 1440x900 に収まる");
        }

        [Test]
        public void ProfileSelect_1280x720_FhdScaled()
        {
            Assert.AreEqual("fhd*0.667", UISizeProfile.Select(1280, 720).Name);
        }

        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(2560, 1664)]
        public void ProfileRepresentativeSizes_NoClip(int width, int height)
        {
            var p = UISizeProfile.Select(width, height);
            Assert.LessOrEqual(p.Btn.x, width, $"Btn.x fits {width}x{height}");
            Assert.LessOrEqual(p.Btn.y, height, $"Btn.y fits {width}x{height}");
            Assert.LessOrEqual(p.CardMid.x, width, $"CardMid.x fits {width}x{height}");
            Assert.LessOrEqual(p.RowH, height, $"RowH fits {width}x{height}");
            Assert.LessOrEqual(p.DrawerW, width, $"DrawerW fits {width}x{height}");
            Assert.LessOrEqual(p.StepperW, width, $"StepperW fits {width}x{height}");
        }

        private static Canvas NewCanvas()
        {
            var go = new GameObject("TestCanvas", typeof(Canvas));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }
    }
}
