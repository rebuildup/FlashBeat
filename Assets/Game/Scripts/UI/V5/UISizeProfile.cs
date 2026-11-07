using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// v5 UI の寸法プロファイル。design/ui-size-profiles.json の profiles (fhd/2k/mac) を
    /// C# の不変クラスに写したもの。ScriptableObject は使わず、静的ファクトリで生成する。
    /// 値は各解像度の実ピクセル。Canvas は Constant Pixel Size x1 なのでそのまま使える。
    /// </summary>
    public sealed class UISizeProfile
    {
        // --- design/ui-size-profiles.json "select" の選択規則 ---
        // 縦横比 ~1.538 (±0.03, Mac ネイティブ 2560x1664) -> mac
        // 縦横比 ~1.778 (±0.03) かつ screen.height >= 1440 -> 2k
        // screen.height >= 1080 -> fhd
        // else -> fhd を min(1, height/1080) 倍に縮小
        //
        // 端末解像度の扱い (実装方針):
        // - 2560x1664 は mac (Mac ネイティブ実ピクセル。旧 1440x900 設計は無効, Issue #162)。
        // - 2560x1440 は 2k (縦横比 1.778 かつ height>=1440)。
        // - 1920x1080 は fhd。
        // - 1440x900 (16:10 = 1.6) は mac にも 2k にも当てはまらない → fhd を h/1080 倍に縮小。
        // - fhd は「1920x1080 基準の実ピクセル」だが、Constant Pixel Size x1 では
        //   それより小さい画面で UI が画面からはみ出る。そこで小さい画面では
        //   CanvasScaler.referenceResolution を使わず、プロファイル値自体を
        //   min(1, height/1080) 倍に縮めて返す (ClampToFhd)。これにより
        //   1024x768 (x0.71) / 1280x720 (x0.667) でも比率を保ち、決してクリップしない。

        public readonly string Name;
        public readonly float Scale;

        public readonly int Margin;
        public readonly float FsXl;
        public readonly float FsL;
        public readonly float FsM;
        public readonly float FsS;
        public readonly float FsRank;
        public readonly float FsCombo;
        public readonly int IconS;
        public readonly int IconM;
        public readonly int IconL;
        public readonly int GapS;
        public readonly int GapM;
        public readonly int GapL;
        public readonly Vector2 CardCenter;
        public readonly Vector2 CardMid;
        public readonly Vector2 CardSide;
        public readonly int DrawerW;
        public readonly int StepperW;
        public readonly int RowH;
        public readonly Vector2 Btn;

        private UISizeProfile(
            string name, float scale,
            int margin, float fsXl, float fsL, float fsM, float fsS, float fsRank, float fsCombo,
            int iconS, int iconM, int iconL, int gapS, int gapM, int gapL,
            Vector2 cardCenter, Vector2 cardMid, Vector2 cardSide,
            int drawerW, int stepperW, int rowH, Vector2 btn)
        {
            Name = name;
            Scale = scale;
            Margin = margin;
            FsXl = fsXl;
            FsL = fsL;
            FsM = fsM;
            FsS = fsS;
            FsRank = fsRank;
            FsCombo = fsCombo;
            IconS = iconS;
            IconM = iconM;
            IconL = iconL;
            GapS = gapS;
            GapM = gapM;
            GapL = gapL;
            CardCenter = cardCenter;
            CardMid = cardMid;
            CardSide = cardSide;
            DrawerW = drawerW;
            StepperW = stepperW;
            RowH = rowH;
            Btn = btn;
        }

        /// <summary>design/ui-size-profiles.json の profiles.fhd (1920x1080)。</summary>
        public static UISizeProfile Fhd()
        {
            return new UISizeProfile(
                "fhd", 1f,
                48, 52, 38, 30, 22, 420, 180,
                24, 36, 56, 10, 20, 40,
                new Vector2(460, 800), new Vector2(340, 660), new Vector2(280, 540),
                560, 300, 78, new Vector2(360, 120));
        }

        /// <summary>design/ui-size-profiles.json の profiles.2k (2560x1440)。</summary>
        public static UISizeProfile TwoK()
        {
            return new UISizeProfile(
                "2k", 1f,
                64, 68, 50, 40, 30, 560, 240,
                32, 48, 74, 14, 28, 54,
                new Vector2(610, 1060), new Vector2(450, 880), new Vector2(370, 720),
                740, 400, 104, new Vector2(480, 160));
        }

        /// <summary>
        /// design/ui-size-profiles.json の profiles.mac (2560x1664, Mac ネイティブ実ピクセル)。
        /// 旧 1440x900 設計は無効 (Issue #162 指示)。1440x900 は mac ではなく fhd 縮小側に流れる。
        /// </summary>
        public static UISizeProfile Mac()
        {
            return new UISizeProfile(
                "mac", 1f,
                64, 75, 57, 46, 36, 587, 249,
                39, 53, 82, 14, 28, 53,
                new Vector2(676, 1173), new Vector2(444, 978), new Vector2(356, 800),
                853, 462, 114, new Vector2(533, 178));
        }

        /// <summary>
        /// fhd プロファイルを scale 倍に縮めた派生プロファイル。
        /// fhd より小さい画面 (1024x768 / 1280x720 など) 用。UI がクリップしないよう全体を縮小する。
        /// 丸め規約: int フィールドは Mathf.RoundToInt、float / Vector2 フィールドは無丸め (全フィールド同一の scale)。
        /// </summary>
        public static UISizeProfile FhdScaled(float scale)
        {
            var f = Fhd();
            return new UISizeProfile(
                "fhd*" + scale.ToString("0.###"), scale,
                Mathf.RoundToInt(f.Margin * scale),
                f.FsXl * scale, f.FsL * scale, f.FsM * scale, f.FsS * scale,
                f.FsRank * scale, f.FsCombo * scale,
                Mathf.RoundToInt(f.IconS * scale), Mathf.RoundToInt(f.IconM * scale), Mathf.RoundToInt(f.IconL * scale),
                Mathf.RoundToInt(f.GapS * scale), Mathf.RoundToInt(f.GapM * scale), Mathf.RoundToInt(f.GapL * scale),
                f.CardCenter * scale, f.CardMid * scale, f.CardSide * scale,
                Mathf.RoundToInt(f.DrawerW * scale), Mathf.RoundToInt(f.StepperW * scale),
                Mathf.RoundToInt(f.RowH * scale), f.Btn * scale);
        }

        /// <summary>
        /// 現在の画面解像度からプロファイルを選択する。
        /// 選択規則 (Issue #162/#169 指示, 順序厳守):
        /// 1. 縦横比 ~1.538 (±0.03, Mac ネイティブ 2560x1664) -> mac
        /// 2. 縦横比 ~1.778 (±0.03) かつ height >= 1440 -> 2k
        /// 3. height >= 1080 -> fhd
        /// 4. 上記以外 (height &lt; 1080) -> fhd を height/1080 倍に縮小
        /// 旧 1440x900 (縦横比 1.6) 用の規則は廃止。
        /// </summary>
        public static UISizeProfile Select(int width, int height)
        {
            float aspect = (float)width / height;
            if (Mathf.Abs(aspect - 1.538f) < 0.03f) return Mac();
            if (Mathf.Abs(aspect - 1.778f) < 0.03f && height >= 1440) return TwoK();
            if (height >= 1080) return Fhd();
            // fhd (1080p) より小さい画面: 全体を縮小してクリップを防ぐ。
            return FhdScaled(Mathf.Min(1f, (float)height / 1080f));
        }

        private static UISizeProfile cached;
        private static int cachedWidth;
        private static int cachedHeight;

        /// <summary>
        /// 現在の Screen.width/height に対応するプロファイル (キャッシュ)。
        /// 解像度変更は <see cref="CanvasConfigurator"/> が検知して再適用する。
        /// </summary>
        public static UISizeProfile Current
        {
            get
            {
                var w = Screen.width;
                var h = Screen.height;
                if (cached == null || cachedWidth != w || cachedHeight != h)
                {
                    cached = Select(w, h);
                    cachedWidth = w;
                    cachedHeight = h;
                }

                return cached;
            }
        }

        /// <summary>テスト用: プロファイルキャッシュをクリアする。</summary>
        public static void ClearCache()
        {
            cached = null;
        }
    }

    /// <summary>
    /// Canvas を v5 プロファイル (Constant Pixel Size x1) に設定し、
    /// 解像度変更時に再適用する MonoBehaviour。生成した Canvas に AddComponent して使う。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CanvasConfigurator : MonoBehaviour
    {
        private CanvasScaler scaler;
        private int lastWidth;
        private int lastHeight;

        /// <summary>
        /// Canvas/CanvasScaler をプロファイル設定にする。生成直後に呼ぶ。
        /// 冪等: 同じ Canvas に何度呼んでも CanvasScaler / GraphicRaycaster /
        /// CanvasConfigurator / EventSystem は重複生成されない (Issue #169 T3)。
        /// </summary>
        public static CanvasConfigurator Configure(Canvas canvas)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.sortingOrder = 500;
            }

            var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            // T3: Canvas ごとの GraphicRaycaster を一元管理。無い Canvas は raycast 対象に
            // ならない (死に raycast) ので、無ければ付与する。既にあればそのまま使う。
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            // T3: EventSystem の単一系統化。画面ごとに重複生成しない。
            EnsureEventSystem();

            // [DisallowMultipleComponent] のため 2 回目の AddComponent はエラーになる。
            // 冪等にするため既存インスタンスを再利用する。
            var cfg = canvas.GetComponent<CanvasConfigurator>();
            if (cfg == null)
            {
                cfg = canvas.gameObject.AddComponent<CanvasConfigurator>();
            }

            cfg.scaler = scaler;
            cfg.lastWidth = Screen.width;
            cfg.lastHeight = Screen.height;
            return cfg;
        }

        /// <summary>
        /// EventSystem がシーンに 1 つも無ければ生成する (EventSystem + StandaloneInputModule)。
        /// 既にあれば何もしない。旧 Canvas を SetActive(false) にする方式と v5 動的 Canvas の
        /// 併用でも EventSystem が重複しないようにするための唯一の生成経路 (Issue #169 T3)。
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            // EditMode テストでは DontDestroyOnLoad は無意味 (警告が出る) ので再生中のみ。
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(go);
            }
        }

        private void Update()
        {
            if (Screen.width == lastWidth && Screen.height == lastHeight) return;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            Apply();
        }

        /// <summary>現在の解像度用に Constant Pixel Size x1 を再適用する (値は不変だが明示的に再設定)。</summary>
        public void Apply()
        {
            if (scaler == null) return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
        }
    }
}
