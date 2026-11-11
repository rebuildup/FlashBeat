using System.Collections.Generic;
using System.Text;

namespace FlashBeat.UI.V6.Select.View
{
    /// <summary>
    /// design 空間 (1728x972) の矩形。x/y は左上で原点、w/h はサイズ。
    /// どのパネル (ヘッダー / フッター / 曲リスト) のローカル座標かは
    /// これを返す関数ごとに明記している。
    /// </summary>
    public struct V6Rect
    {
        public float X;
        public float Y;
        public float W;
        public float H;

        public V6Rect(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        /// <summary>右端。</summary>
        public float Right
        {
            get { return X + W; }
        }

        /// <summary>下端。</summary>
        public float Bottom
        {
            get { return Y + H; }
        }

        /// <summary>
        /// 2 つの矩形が重なっているか。辺が接するだけ (0 面積) は重なりとみなさない。
        /// </summary>
        public bool Overlaps(V6Rect other)
        {
            return X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
        }

        /// <summary>other がこの矩形の中に収まるか。</summary>
        public bool Contains(V6Rect other)
        {
            return other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;
        }
    }

    /// <summary>フッターの凡例 1 項目 (キーキャップ + 説明ラベル)。</summary>
    public struct V6LegendItem
    {
        /// <summary>キャップに載るキー名 ("Space" / "Tab" ...)。</summary>
        public string Key;

        /// <summary>説明ラベル ("決定" / "検索" ...)。</summary>
        public string Label;

        /// <summary>キャップの幅 (design px)。</summary>
        public float CapWidth;

        /// <summary>説明ラベルの幅 (design px)。</summary>
        public float LabelWidth;

        /// <summary>キャップの x (フッター ローカル)。</summary>
        public float X;

        /// <summary>項目全体の幅 (キャップ + 間隔 + ラベル)。</summary>
        public float Width;
    }

    /// <summary>v6 選曲画面のキーボードアクション (実キーへの割り当ては V6SelectView 側)。</summary>
    public enum V6SelectAction
    {
        None,
        MoveUp,
        MoveDown,
        Confirm,
        Cancel,
        ToggleFilter,
        UnfocusSearch,
    }

    /// <summary>
    /// v6 選曲画面の純ロジック (EditMode テスト対象)。Unity API に依存しない。
    /// 寸法は design v6 の 1728x972 共通フレーム基準。
    /// </summary>
    public static class V6SelectViewLogic
    {
        /// <summary>design v6 共通フレームの基準サイズ。</summary>
        public const float DesignW = 1728f;
        public const float DesignH = 972f;

        /// <summary>
        /// design px → 実ピクセルの倍率。実画面 3 解像度
        /// (1920x1080 / 2560x1440 / 2560x1664) でフレームが収まるよう
        /// min(W/1728, H/972) 倍する (16:9 では等比、16:10 では幅を優先)。
        /// </summary>
        public static float DesignScale(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return 1f;
            }

            float byW = width / DesignW;
            float byH = height / DesignH;
            return byW < byH ? byW : byH;
        }

        // ============================================================
        // フレーム配置 (design 空間 1728×972 を実画面に 1:1 で中央配置)
        // 引き伸ばし (等比スケール) は行わない。実機の各解像度での原点は
        // ((w-1728)/2, (h-972)/2) の床切り (design の Resolution Check 表の値)。
        // ============================================================

        /// <summary>
        /// design frame を実画面に 1:1 で中央配置したときの左上原点。
        /// </summary>
        public static void FrameOrigin(int screenW, int screenH, out int x, out int y)
        {
            x = FloorHalf(screenW - (int)DesignW);
            y = FloorHalf(screenH - (int)DesignH);
        }

        /// <summary>v/2 の床切り (負も正しく床切る)。</summary>
        private static int FloorHalf(int v)
        {
            return v >= 0 ? v / 2 : -((-v + 1) / 2);
        }

        // ============================================================
        // ドットインジケータ (契約 §5)
        // 曲リスト (幅 560) の右端レール x=536、開始 y=136、間隔 4。
        // アクティブ 10×10 $accent (#3FA9F5) / 非アクティブ 8×8 (#FFFFFF26, 角丸 5)。
        // ============================================================
        public const float DotX = 536f;
        public const float DotY = 136f;
        public const float DotSize = 8f;
        public const float DotActiveSize = 10f;
        public const float DotGap = 4f;
        public const float DotRadius = 5f;

        /// <summary>ドットの x (dot コンテナ ローカル)。レール幅 (最大ドット 10) の中央。</summary>
        public static float DotRectX(float dotSize)
        {
            return (DotActiveSize - dotSize) * 0.5f;
        }

        /// <summary>ドットの上端 y (dot コンテナ ローカル)。i 番目は startY + i*(DotSize+DotGap)。</summary>
        public static float[] DotOffsets(int count, float startY = DotY)
        {
            var offsets = new float[count > 0 ? count : 0];
            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = startY + (DotSize + DotGap) * i;
            }

            return offsets;
        }

        /// <summary>最下端のドットの下端 y (dot コンテナ ローカル)。</summary>
        public static float DotBottom(int count, float startY = DotY)
        {
            if (count <= 0)
            {
                return startY;
            }

            return startY + (DotSize + DotGap) * (count - 1) + DotSize;
        }

        // --- クイック設定 (契約 §5 右カラム / v5 OptionView 参照) ---
        // 右カラム (幅 560) に、ヘッダー「このプレイの設定」(24,8) fs26 w500 と
        // 行 512×56 (背景 #2D2D2D r8) を 68 間隔で 3 行 (Key Guide / Lyrics / NoteSpeed)。
        public const int SettingsRowCount = 3;
        public const float SettingsHeadX = 24f;
        public const float SettingsHeadY = 8f;
        public const float SettingsRowX = 24f;
        public const float SettingsRowY0 = 64f;
        public const float SettingsRowStep = 68f;
        public const float SettingsRowW = 512f;
        public const float SettingsRowH = 56f;

        /// <summary>設定行 row の矩形 (右カラム ローカル)。</summary>
        public static V6Rect SettingsRowRect(int row)
        {
            return new V6Rect(SettingsRowX, SettingsRowY0 + SettingsRowStep * row, SettingsRowW, SettingsRowH);
        }

        /// <summary>アクティブ ドットの index (範囲外は端に留まる)。</summary>
        public static int DotActiveIndex(int selectedIndex, int dotCount)
        {
            if (dotCount <= 0)
            {
                return -1;
            }

            if (selectedIndex < 0) return 0;
            if (selectedIndex >= dotCount) return dotCount - 1;
            return selectedIndex;
        }

        /// <summary>スクロール位置 (先頭の見えている行 index) を範囲内に収める。</summary>
        public static int ClampScroll(int scroll, int rowCount, int visibleRows)
        {
            if (visibleRows <= 0 || rowCount <= visibleRows)
            {
                return 0;
            }

            int max = rowCount - visibleRows;
            if (scroll < 0) return 0;
            if (scroll > max) return max;
            return scroll;
        }

        /// <summary>行 index が見えるようにスクロール位置を動かす (選択移動の追従)。</summary>
        public static int ScrollToReveal(int index, int scroll, int visibleRows)
        {
            if (visibleRows <= 0 || index < 0)
            {
                return scroll;
            }

            if (index < scroll)
            {
                return index;
            }

            if (index >= scroll + visibleRows)
            {
                return index - visibleRows + 1;
            }

            return scroll;
        }

        /// <summary>
        /// 選択行 (index) がリストの中央に来るスクロール位置を返す。
        /// スクロール位置はクランプしない (負値 / rowCount-visibleRows 超えを許す)。
        /// 先頭 / 末尾の曲では中央スロット以外の行が空になり、選択行は常に中央に来る。
        /// ただし index が範囲外 (-1 等) または rowCount &lt; visibleRows のときは
        /// 既存挙動 (index を範囲内に直し、スクロールも 0..rowCount-visibleRows に収める) を保つ。
        /// 表示 (V6SelectView) は <see cref="ListScroll"/> でクランプしてから使う。
        /// </summary>
        public static int CenterScroll(int index, int rowCount, int visibleRows)
        {
            if (visibleRows <= 0 || rowCount <= 0)
            {
                return 0;
            }

            if (index < 0 || index > rowCount - 1 || rowCount < visibleRows)
            {
                int clamped = index < 0 ? 0 : (index > rowCount - 1 ? rowCount - 1 : index);
                int max = rowCount - visibleRows;
                if (max <= 0)
                {
                    return 0;
                }

                int fallback = clamped - visibleRows / 2;
                if (fallback < 0)
                {
                    return 0;
                }

                if (fallback > max)
                {
                    return max;
                }

                return fallback;
            }

            return index - visibleRows / 2;
        }

        /// <summary>
        /// 描画に使う曲リストのスクロール位置 (先頭に描く曲 index)。
        /// <see cref="CenterScroll"/> の中央固定値を <see cref="ClampScroll"/> で
        /// 0..rowCount-ListVisibleRows に戻す。負の scroll のまま描くと空スロットが
        /// 下側にずれて行が下端へ寄る (r1実機 2026-11-11 の症状) ので、表示は必ず
        /// この関数を通す。選択 index ≥ ListVisibleRows/2 では選択行は中央スロット
        /// (上に ListVisibleRows/2 行) に来る。
        /// </summary>
        public static int ListScroll(int selectIndex, int rowCount)
        {
            return ClampScroll(CenterScroll(selectIndex, rowCount, ListVisibleRows),
                rowCount, ListVisibleRows);
        }

        /// <summary>スコア表示。未プレイ (負値) は design どおり「–」。</summary>
        public static string FormatScore(int score)
        {
            if (score < 0)
            {
                return "–";
            }

            return score.ToString("#,0");
        }

        /// <summary>
        /// 検索欄への文字入力を query に反映する。Input.inputString 前提で
        /// '\b' (バックスペース) は 1 文字削除、表示可能文字以外は無視する。
        /// </summary>
        public static string ApplySearchInput(string query, string input)
        {
            query ??= "";
            if (string.IsNullOrEmpty(input))
            {
                return query;
            }

            var sb = new StringBuilder(query);
            foreach (char c in input)
            {
                if (c == '\b')
                {
                    if (sb.Length > 0)
                    {
                        sb.Length -= 1;
                    }
                }
                else if (c >= ' ' && c != '\u007f')
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// キー入力をアクションに写像する (検索フォーカス中は Esc/Enter/Tab を
        /// フォーカス解除として消費し、確定や戻るを同時に発火させない)。
        ///
        /// 正本 OENRv のフッター凡例は「Tab = 絞り込み」「A-Z = 検索」。
        /// 検索フォーカス中以外の Tab は「未プレイのみ」の絞り込みトグル。
        /// 検索の開始は A-Z の文字入力 (<see cref="SearchSeed"/>)。
        /// </summary>
        public static V6SelectAction MapKey(bool searchFocused, bool up, bool down, bool confirm, bool cancel,
            bool tab)
        {
            if (searchFocused)
            {
                if (tab || confirm || cancel)
                {
                    return V6SelectAction.UnfocusSearch;
                }

                return V6SelectAction.None;
            }

            if (up) return V6SelectAction.MoveUp;
            if (down) return V6SelectAction.MoveDown;
            if (confirm) return V6SelectAction.Confirm;
            if (cancel) return V6SelectAction.Cancel;
            if (tab) return V6SelectAction.ToggleFilter;
            return V6SelectAction.None;
        }

        /// <summary>
        /// 検索を開始する文字か (正本 OENRv のキャップ "A-Z")。
        /// 入力欄が非フォーカスのときにアルファベットを押したら検索へ入る。
        /// 制御文字 (Tab/Enter/Backspace) と空白は対象外。
        /// </summary>
        public static bool IsSearchSeedChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        /// <summary>
        /// 選曲画面の L / R は History / Ranking への切替キー (<c>SelectSceneManager</c>)。
        /// 同じ文字を検索の先頭文字にもすると、一覧で R を押したときに
        /// 画面が切り替わると同時に検索語 "r" が入ってしまうため、検索の開始文字から外す。
        /// 検索欄がフォーカス中の L / R は検索語として扱う (検索中は切替を止める)。
        /// </summary>
        public static bool IsScreenSwitchKey(char c)
        {
            char lower = (c >= 'A' && c <= 'Z') ? (char)(c + ('a' - 'A')) : c;
            return lower == 'l' || lower == 'r';
        }

        /// <summary>
        /// Input.inputString から検索開始の 1 文字を取り出す (無ければ '\0')。
        /// 文字列全体をそのまま流すと制御文字まで入るため、最初の英字だけを使う。
        /// <see cref="IsScreenSwitchKey"/> の文字 (L / R) は画面切替に使うため含めない。
        /// </summary>
        public static char SearchSeed(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return '\0';
            }

            for (int i = 0; i < input.Length; i++)
            {
                if (IsSearchSeedChar(input[i]) && !IsScreenSwitchKey(input[i]))
                {
                    return input[i];
                }
            }

            return '\0';
        }

        // ============================================================
        // レイアウト (design 空間 1728x972 のみ。実解像度には依存しない)
        //
        // 実機では frame を DesignScale 倍して画面中央に置くだけなので、
        // 数値はすべて design px で決める。ヘッダー / フッター / 曲リスト
        // パネルの枠は frame ローカル、パネル内の要素は各パネルの
        // ローカル座標で持つ。
        // ============================================================

        /// <summary>ヘッダー帯 (frame ローカル)。</summary>
        public const float HeaderX = 24f;
        public const float HeaderY = 0f;
        public const float HeaderW = 1680f;
        public const float HeaderH = 96f;

        /// <summary>フッター帯 (frame ローカル)。</summary>
        public const float FooterX = 24f;
        public const float FooterY = 876f;
        public const float FooterW = 1680f;
        public const float FooterH = 96f;

        /// <summary>曲リストパネル (frame ローカル)。</summary>
        public const float ListX = 24f;
        public const float ListY = 96f;
        public const float ListW = 560f;
        public const float ListH = 780f;

        /// <summary>曲リスト行 (リストパネル ローカル)。</summary>
        public const float RowX = 16f;
        public const float RowY0 = 48f;
        public const float RowW = 504f;
        public const float RowH = 72f;
        public const float RowStep = 76f;
        public const int ListVisibleRows = 9;

        /// <summary>キーキャップの Key fs (design OENRv Legend KeyCap / Prefab/KeyCap: fs22)。</summary>
        public const float CapFontSize = 22f;

        /// <summary>説明ラベルのフォントサイズ (design OENRv Legend Label: fs22)。</summary>
        public const float LabelFontSize = 22f;

        /// <summary>キャップの左右余白 (design OENRv Legend KeyCap: padding [0,8])。</summary>
        public const float CapPadX = 8f;

        /// <summary>キャップと説明ラベルの間隔 (design OENRv Legend: gap 8)。</summary>
        public const float LegendLabelGap = 8f;

        /// <summary>凡例キャップの高さ (design OENRv Zone · キー操作: h32)。</summary>
        public const float LegendCapHeight = 32f;

        /// <summary>凡例 項目どうしの間隔 (design px)。</summary>
        public const float LegendItemGap = 24f;

        /// <summary>
        /// 概算文字幅は実フォントより広く取る (安全係数)。
        /// 折り返しそのものを禁じて 1 行に収めるので、幅は「少なくとも」の保証。
        /// </summary>
        public const float TextWidthSafety = 1.06f;

        /// <summary>ヘッダー帯の矩形 (frame ローカル)。</summary>
        public static V6Rect HeaderPanelRect()
        {
            return new V6Rect(HeaderX, HeaderY, HeaderW, HeaderH);
        }

        /// <summary>ヘッダー帯の矩形 (ヘッダー ローカル)。</summary>
        public static V6Rect HeaderLocalRect()
        {
            return new V6Rect(0f, 0f, HeaderW, HeaderH);
        }

        /// <summary>フッター帯の矩形 (frame ローカル)。</summary>
        public static V6Rect FooterPanelRect()
        {
            return new V6Rect(FooterX, FooterY, FooterW, FooterH);
        }

        /// <summary>フッター帯の矩形 (フッター ローカル)。</summary>
        public static V6Rect FooterLocalRect()
        {
            return new V6Rect(0f, 0f, FooterW, FooterH);
        }

        /// <summary>曲リストパネルの矩形 (frame ローカル)。</summary>
        public static V6Rect ListPanelRect()
        {
            return new V6Rect(ListX, ListY, ListW, ListH);
        }

        /// <summary>フレーム全体の矩形 (frame ローカル)。</summary>
        public static V6Rect FrameRect()
        {
            return new V6Rect(0f, 0f, DesignW, DesignH);
        }

        /// <summary>パネル内ローカル矩形を frame ローカル矩形へ変換する。</summary>
        public static V6Rect ToFrame(V6Rect panel, V6Rect local)
        {
            return new V6Rect(panel.X + local.X, panel.Y + local.Y, local.W, local.H);
        }

        /// <summary>
        /// 親要素 (ボタンなど) のローカル矩形に、子要素のローカル矩形を
        /// 平行移動して重ねる (親の子として置いた位置を親基準で表した矩形)。
        /// </summary>
        public static V6Rect Offset(V6Rect origin, V6Rect local)
        {
            return new V6Rect(origin.X + local.X, origin.Y + local.Y, local.W, local.H);
        }

        /// <summary>ヘッダーの戻るボタン (ヘッダー ローカル)。</summary>
        public static V6Rect HeaderBackButton()
        {
            return new V6Rect(0f, 20f, 56f, 56f);
        }

        /// <summary>ヘッダーのタイトル (ヘッダー ローカル)。</summary>
        public static V6Rect HeaderTitle()
        {
            return new V6Rect(80f, 20f, 300f, 56f);
        }

        /// <summary>ヘッダーの検索欄 (ヘッダー ローカル)。</summary>
        public static V6Rect HeaderSearch()
        {
            return new V6Rect(864f, 24f, 580f, 48f);
        }

        /// <summary>ヘッダーの曲数 (ヘッダー ローカル)。</summary>
        public static V6Rect HeaderCount()
        {
            return new V6Rect(1456f, 24f, 140f, 48f);
        }

        /// <summary>ヘッダーの絞り込みボタン (ヘッダー ローカル)。</summary>
        public static V6Rect HeaderFilter()
        {
            return new V6Rect(1608f, 24f, 48f, 48f);
        }

        /// <summary>
        /// 検索欄の虫めがねアイコン (検索欄 ローカル)。
        /// design OENRv `/W1K1R/sIkNa/FiU1X`: lucide "search" 20x20 @(12,14)。
        /// </summary>
        public static V6Rect HeaderSearchIcon()
        {
            return new V6Rect(12f, 14f, 20f, 20f);
        }

        /// <summary>
        /// 検索欄の入力テキスト (検索欄 ローカル)。
        /// design OENRv `/W1K1R/sIkNa/E5R3c`: Placeholder @(40,8) h32 (fs22)。
        /// 幅は design の自然幅 154 ではなく A-Z キャップ手前まで伸ばす
        /// (入力が長くなってもキャップの下へ潜り込まない)。
        /// </summary>
        public static V6Rect HeaderSearchQuery()
        {
            var cap = HeaderSearchCap();
            return new V6Rect(40f, 8f, cap.X - 8f - 40f, 32f);
        }

        /// <summary>
        /// 検索欄の A-Z キャップ (検索欄 ローカル)。
        /// design OENRv `/W1K1R/sIkNa/mMt7c`: 50x32 @(518,8) (右上 inset 12)。
        /// 幅は design 空間のフォントから求めた自然幅 + 余白で決める
        /// (推定値は実際のフォントより広いので、右端だけ design に合わせる)。
        /// </summary>
        public static V6Rect HeaderSearchCap()
        {
            float w = KeyCapWidth("A-Z", CapFontSize);
            float x = HeaderSearch().W - w - 12f;
            return new V6Rect(x, 8f, w, 32f);
        }

        /// <summary>設定ボタン (フッター ローカル)。</summary>
        public static V6Rect FooterSettings()
        {
            return new V6Rect(1198f, 20f, 197f, 56f);
        }

        /// <summary>設定ボタンのアイコン (設定ボタン ローカル)。</summary>
        public static V6Rect FooterSettingsIcon()
        {
            return new V6Rect(44f, 12f, 32f, 32f);
        }

        /// <summary>設定ボタンのラベル (設定ボタン ローカル)。</summary>
        public static V6Rect FooterSettingsLabel()
        {
            return new V6Rect(92f, 8f, 80f, 40f);
        }

        /// <summary>Ready ボタン (フッター ローカル)。</summary>
        public static V6Rect FooterReady()
        {
            return new V6Rect(1407f, 20f, 273f, 56f);
        }

        /// <summary>Ready ボタンのアイコン (Ready ボタン ローカル)。</summary>
        public static V6Rect FooterReadyIcon()
        {
            return new V6Rect(20f, 12f, 26f, 32f);
        }

        /// <summary>Ready ボタンのラベル (Ready ボタン ローカル)。</summary>
        public static V6Rect FooterReadyLabel()
        {
            return new V6Rect(56f, 8f, 110f, 40f);
        }

        /// <summary>Ready ボタン内の Space キャップ (Ready ボタン ローカル)。</summary>
        public static V6Rect FooterReadyCap()
        {
            return new V6Rect(173f, 12f, 84f, 32f);
        }

        /// <summary>凡例を置ける範囲 (フッター ローカル)。設定ボタンの手前まで。</summary>
        public static V6Rect FooterLegendArea()
        {
            return new V6Rect(0f, 30f, 1150f, 36f);
        }

        /// <summary>
        /// フッターの凡例。design 空間の自然幅から各項目の位置を決める
        /// (HorizontalLayoutGroup は子要素の幅が解らないので使わない)。
        ///
        /// 正本 OENRv の文字列そのまま:
        ///   - 決定: KeyActions.Confirm (キー設定の正本。ビューから
        ///     ActionKeyHint.Primary(KeyActions.KeyAction.Confirm) を渡す)。
        ///   - 曲送り: ↑↓ (←→ も同じ動作をするが、正本のキャップは ↑↓)。
        ///   - 検索: A-Z (英字を押すと検索欄へ入る)。
        ///   - 絞り込み: Tab (未プレイのみトグル)。
        /// </summary>
        /// <param name="confirmKey">決定のキーキャップ表示 (ActionKeyHint.Primary(Confirm))。</param>
        public static V6LegendItem[] FooterLegend(string confirmKey)
        {
            var defs = new string[][]
            {
                new[] { string.IsNullOrEmpty(confirmKey) ? "Space" : confirmKey, "決定" },
                new[] { "↑↓", "曲送り" },
                new[] { "A-Z", "検索" },
                new[] { "Tab", "絞り込み" },
            };

            var items = new V6LegendItem[defs.Length];
            float x = 0f;
            for (int i = 0; i < defs.Length; i++)
            {
                float capW = KeyCapWidth(defs[i][0], CapFontSize);
                float labelW = EstimateTextWidth(defs[i][1], LabelFontSize);
                float width = capW + LegendLabelGap + labelW;
                items[i] = new V6LegendItem
                {
                    Key = defs[i][0],
                    Label = defs[i][1],
                    CapWidth = capW,
                    LabelWidth = labelW,
                    X = x,
                    Width = width,
                };
                x += width + LegendItemGap;
            }

            return items;
        }

        /// <summary>凡例全体の幅 (フッター ローカル)。最後の項目の右端。</summary>
        public static float FooterLegendWidth(string confirmKey)
        {
            var items = FooterLegend(confirmKey);
            float right = 0f;
            for (int i = 0; i < items.Length; i++)
            {
                right = items[i].X + items[i].Width;
            }

            return right;
        }

        /// <summary>スクロールボタン (曲リストパネル ローカル)。</summary>
        public static V6Rect ListScrollButton(bool down)
        {
            return new V6Rect(210f, down ? 732f : 0f, 140f, 44f);
        }

        /// <summary>
        /// 曲リスト行の矩形 (曲リストパネル ローカル)。
        /// リスト下端はスクロールボタンとドットのみ (正本 OENRv)。
        /// 選択位置テキスト用の帯は持たない。
        /// </summary>
        public static V6Rect ListRowRect(int rowIndex)
        {
            return new V6Rect(RowX, RowY0 + RowStep * rowIndex, RowW, RowH);
        }

        /// <summary>
        /// 文字の概算幅 (design px)。実フォントの字幅より広く取り、
        /// 「この幅があれば 1 行に収まる」ことの保証に使う。
        /// </summary>
        public static float EstimateTextWidth(string text, float fontSize)
        {
            if (string.IsNullOrEmpty(text) || fontSize <= 0f)
            {
                return 0f;
            }

            return TextEmWidth(text) * fontSize * TextWidthSafety;
        }

        /// <summary>文字の概算幅 (em 単位)。安全係数をかけない素の字幅。</summary>
        public static float TextEmWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            float em = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                em += CharEmWidth(text[i]);
            }

            return em;
        }

        /// <summary>1 文字の字幅 (em 単位)。全角 = 1.0、半角は種類別に固定的。</summary>
        public static float CharEmWidth(char c)
        {
            if (c >= 0x1100 && (c <= 0x115F || c == 0x2329 || c == 0x232A
                || (c >= 0x2E80 && c <= 0xA4CF && c != 0x303F)
                || (c >= 0xAC00 && c <= 0xD7A3)
                || (c >= 0xF900 && c <= 0xFAFF)
                || (c >= 0xFE30 && c <= 0xFE6F)
                || (c >= 0xFF00 && c <= 0xFF60)
                || (c >= 0xFFE0 && c <= 0xFFE6)
                || (c >= 0x20000 && c <= 0x3FFFD)))
            {
                return 1f;
            }

            if (c >= 0x2000 && c <= 0x2BFF)
            {
                // 記号・矢印・絵文字 (↑ ↓ ← → ♪ ≡ ⚙ ▶ …) は全角扱いに倒す。
                return 1f;
            }

            switch (c)
            {
                case ' ':
                    return 0.28f;
                case 'i':
                case 'l':
                case 'j':
                case '.':
                case ',':
                case ':':
                case ';':
                case '!':
                case '\'':
                case '|':
                    return 0.30f;
                case 't':
                case 'f':
                case 'r':
                case 'I':
                case '(':
                case ')':
                case '[':
                case ']':
                case '{':
                case '}':
                case '/':
                case '\\':
                    return 0.36f;
                case 'm':
                case 'w':
                case 'M':
                case 'W':
                    return 0.85f;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return 0.56f;
                case 'A':
                case 'B':
                case 'C':
                case 'D':
                case 'E':
                case 'F':
                case 'G':
                case 'H':
                case 'J':
                case 'K':
                case 'L':
                case 'N':
                case 'O':
                case 'P':
                case 'Q':
                case 'R':
                case 'S':
                case 'T':
                case 'U':
                case 'V':
                case 'X':
                case 'Y':
                case 'Z':
                    return 0.66f;
                default:
                    return 0.58f;
            }
        }

        /// <summary>
        /// キーキャップの幅 (design px)。ラベルの自然幅 + 左右余白。
        /// キャップの中でラベルが 1 行に収まることを保証する。
        /// </summary>
        public static float KeyCapWidth(string label, float fontSize)
        {
            return EstimateTextWidth(label, fontSize) + CapPadX * 2f;
        }

        // ============================================================
        // Ready の有効性 (#221 / #222: 絞り込みで隠れた曲を Ready させない)
        // ============================================================

        /// <summary>
        /// Ready を有効にするか。選択中の曲が **表示リスト** (検索絞り込み +
        /// 「未プレイのみ」の両方を適用した結果) に見えるときだけ true。
        /// 隠れた曲は Ready できず、開始もされない。データ側 (V6SelectDataSource.
        /// CommitReady) の検索絞り込みガードと二重に守る。
        /// </summary>
        public static bool ReadyAvailable(V6SongEntry selected, IReadOnlyList<V6SongEntry> displaySongs)
        {
            if (selected == null || displaySongs == null)
            {
                return false;
            }

            for (int i = 0; i < displaySongs.Count; i++)
            {
                if (displaySongs[i] != null && displaySongs[i].SongId == selected.SongId)
                {
                    return true;
                }
            }

            return false;
        }

        // ============================================================
        // 中央カード (dRztp) の操作ボタン (#221 Task 3: 疑似ボタン禁止)
        // ============================================================

        /// <summary>中央カードの操作ボタンが発火する操作。実在する操作だけを列挙する。</summary>
        public enum V6SelectControlAction
        {
            /// <summary>前の曲へ (data.MoveSelection(-1))。</summary>
            MovePrev,

            /// <summary>Ready → プレイ開始 (onReady → CommitReady → GameScene)。</summary>
            Ready,

            /// <summary>次の曲へ (data.MoveSelection(+1))。</summary>
            MoveNext,
        }

        /// <summary>中央カードの操作ボタン 1 個分の定義。</summary>
        public struct V6CenterControlDef
        {
            /// <summary>V6Icons のアイコン名。</summary>
            public string Icon;

            /// <summary>ボタンの一辺 (design px)。</summary>
            public float Size;

            /// <summary>コントロール行 (512x88) ローカルの x。</summary>
            public float X;

            /// <summary>強調表示 (play ボタン = ButtonPrimaryBg)。</summary>
            public bool Primary;

            /// <summary>結線する実操作 (null にならない)。</summary>
            public V6SelectControlAction Action;
        }

        /// <summary>
        /// 中央カードの操作ボタン 3 個分の定義 (design dRztp の skip-back / play /
        /// skip-forward の位置・寸法を保持)。以前は onClick=null の装飾だったが、
        /// 実在する操作 (曲送り / Ready) への結線を必須にしたため、
        /// Action は常に有効な操作を返す (疑似ボタンは作らない)。
        /// </summary>
        public static V6CenterControlDef[] CenterControls()
        {
            return new V6CenterControlDef[]
            {
                new V6CenterControlDef { Icon = "skip-back", Size = 56f, X = 130f, Primary = false, Action = V6SelectControlAction.MovePrev },
                new V6CenterControlDef { Icon = "play", Size = 72f, X = 220f, Primary = true, Action = V6SelectControlAction.Ready },
                new V6CenterControlDef { Icon = "skip-forward", Size = 56f, X = 326f, Primary = false, Action = V6SelectControlAction.MoveNext },
            };
        }
    }
}
