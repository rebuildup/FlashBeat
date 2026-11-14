namespace FlashBeat.UI.V6.Select.View
{
    /// <summary>
    /// V6SelectViewLogic のレイアウト部分 (Issue #385 Phase 2.10 で分割)。
    /// ドットインジケータ・クイック設定行・パネル/ヘッダー/フッター/曲リストの
    /// design px 矩形とフッター凡例の構成を持つ。partial class のため
    /// 定数・メソッドは V6SelectViewLogic 本体と共有する。
    /// </summary>
    public static partial class V6SelectViewLogic
    {
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

        /// <summary>
        /// ドットレールの上端 y (Issue #427): レール全体を曲リストパネル
        /// (<see cref="ListH"/>) の上下中央に置く。design の開始位置
        /// <see cref="DotY"/> は契約・テスト互換のため残し、配置にはこちらを使う。
        /// </summary>
        public static float DotRailTopY(int count)
        {
            float total = DotBottom(count, 0f);
            float top = (ListH - total) * 0.5f;
            return top > 0f ? top : 0f;
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

        // --- 設定行の列 (design wSjAm の auto-layout を固定列へ展開) ---
        // 行は padding [0,12] / gap 12 の横並びで、右から
        // Arrow · chevron-right (40) / Value (120, 中央揃え) / Arrow · chevron-left (40)
        // の順に固定する (Issue #370)。Value の幅を値の文字数に合わせて縮めると
        // 左矢印が行ごとに動き、値が左右の矢印の中央から外れる。
        public const float SettingsRowPaddingX = 12f;
        public const float SettingsRowArrowSize = 40f;
        public const float SettingsRowValueW = 120f;
        public const float SettingsRowIconSize = 24f;
        public const float SettingsRowLabelX = 48f;

        /// <summary>右矢印の x (行ローカル)。右端から padding 12。</summary>
        public static float SettingsRowNextX
        {
            get { return SettingsRowW - SettingsRowPaddingX - SettingsRowArrowSize; }
        }

        /// <summary>値の x (行ローカル)。右矢印の左に gap 12。</summary>
        public static float SettingsRowValueX
        {
            get { return SettingsRowNextX - 12f - SettingsRowValueW; }
        }

        /// <summary>左矢印の x (行ローカル)。値の左に gap 12。</summary>
        public static float SettingsRowPrevX
        {
            get { return SettingsRowValueX - 12f - SettingsRowArrowSize; }
        }

        /// <summary>行アイコン (行ローカル)。</summary>
        public static V6Rect SettingsRowIcon()
        {
            return new V6Rect(SettingsRowPaddingX,
                (SettingsRowH - SettingsRowIconSize) * 0.5f, SettingsRowIconSize, SettingsRowIconSize);
        }

        /// <summary>行ラベル (行ローカル)。ラベルは伸縮し、列は左矢印で止まる。</summary>
        public static V6Rect SettingsRowLabel()
        {
            return new V6Rect(SettingsRowLabelX, 0f, SettingsRowPrevX - 12f - SettingsRowLabelX, SettingsRowH);
        }

        /// <summary>値テキスト (行ローカル)。左右の矢印の間で中央揃え。</summary>
        public static V6Rect SettingsRowValue()
        {
            return new V6Rect(SettingsRowValueX, 0f, SettingsRowValueW, SettingsRowH);
        }

        /// <summary>左矢印ボタン (行ローカル)。</summary>
        public static V6Rect SettingsRowPrev()
        {
            return new V6Rect(SettingsRowPrevX, (SettingsRowH - SettingsRowArrowSize) * 0.5f,
                SettingsRowArrowSize, SettingsRowArrowSize);
        }

        /// <summary>右矢印ボタン (行ローカル)。</summary>
        public static V6Rect SettingsRowNext()
        {
            return new V6Rect(SettingsRowNextX, (SettingsRowH - SettingsRowArrowSize) * 0.5f,
                SettingsRowArrowSize, SettingsRowArrowSize);
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

        /// <summary>
        /// 設定ボタン (フッター ローカル、閉じている状態)。右端は Ready の 12 手前で
        /// 固定し、幅はボタン規則 (§4: padding [0,24] / gap 12) から中身に合わせて決める。
        /// 固定幅 197 に「閉じる + x アイコン + Esc キャップ」を詰めると、
        /// 中央寄せの結果アイコンと文字が左右の端に貼り付いていた (Issue #370)。
        /// </summary>
        public static V6Rect FooterSettings()
        {
            return FooterSettings(false);
        }

        /// <summary>
        /// 設定ボタン。開いている状態はラベルが「設定」(2 全角) から
        /// 「閉じる」(3 全角) に変わるため、幅もその状態の中身から決める。
        /// 閉じた状態の幅を使い回すと、開いたときだけ文字が右へはみ出す。
        /// 右端はどちらの状態でも同じ (<see cref="FooterSettingsRight"/>)。
        /// </summary>
        public static V6Rect FooterSettings(bool open)
        {
            float w = FlashBeat.UI.V6.Common.V6Controls.ButtonHugWidth(
                open ? FooterSettingsOpenLabelText : FooterSettingsLabelText,
                FooterSettingsKeyText,
                open ? FooterSettingsOpenIconName : FooterSettingsIconName);
            return new V6Rect(FooterSettingsRight - w, 20f, w, 56f);
        }

        /// <summary>設定ボタンの右端 (フッター ローカル)。Ready の左 12。</summary>
        public const float FooterSettingsRight = 1395f;

        /// <summary>設定ボタンのラベル (閉じている状態)。</summary>
        public const string FooterSettingsLabelText = "設定";

        /// <summary>設定ボタンのラベル (パネルを開いている状態)。</summary>
        public const string FooterSettingsOpenLabelText = "閉じる";

        /// <summary>設定ボタンに出すキー (Esc)。幅の見積もりにも使う。</summary>
        public const string FooterSettingsKeyText = "Esc";

        /// <summary>閉じている状態のアイコン。</summary>
        public const string FooterSettingsIconName = "settings";

        /// <summary>開いている状態のアイコン。</summary>
        public const string FooterSettingsOpenIconName = "x";

        /// <summary>設定ボタンのアイコン (設定ボタン ローカル)。</summary>
        public static V6Rect FooterSettingsIcon(bool open = false)
        {
            return ButtonPart(V6SelectViewLogic.FooterSettings(open), 0, open);
        }

        /// <summary>設定ボタンのラベル (設定ボタン ローカル)。</summary>
        public static V6Rect FooterSettingsLabel(bool open = false)
        {
            return ButtonPart(V6SelectViewLogic.FooterSettings(open), 1, open);
        }

        /// <summary>設定ボタンのキーキャップ (設定ボタン ローカル)。</summary>
        public static V6Rect FooterSettingsCap(bool open = false)
        {
            return ButtonPart(V6SelectViewLogic.FooterSettings(open), 2, open);
        }

        /// <summary>
        /// 設定ボタン内の icon (0) / Label (1) / Cap (2) の矩形 (設定ボタン ローカル)。
        /// <see cref="FlashBeat.UI.V6.Common.V6Controls.ButtonSlots"/> と同じ中央寄せ規則。
        /// </summary>
        private static V6Rect ButtonPart(V6Rect button, int part, bool open)
        {
            FlashBeat.UI.V6.Common.V6Controls.ButtonSlot icon;
            FlashBeat.UI.V6.Common.V6Controls.ButtonSlot label;
            FlashBeat.UI.V6.Common.V6Controls.ButtonSlot cap;
            FlashBeat.UI.V6.Common.V6Controls.ButtonSlots(new UnityEngine.Vector2(button.W, button.H),
                open ? FooterSettingsOpenLabelText : FooterSettingsLabelText,
                FooterSettingsKeyText,
                open ? FooterSettingsOpenIconName : FooterSettingsIconName,
                out icon, out label, out cap);

            const float iconSize = FlashBeat.UI.V6.Common.V6Controls.ButtonIconSize;
            const float capH = FlashBeat.UI.V6.Common.V6Controls.ButtonKeyCapHeight;
            if (part == 0)
            {
                return new V6Rect(icon.X, (button.H - iconSize) * 0.5f, icon.W, iconSize);
            }

            if (part == 2)
            {
                return new V6Rect(cap.X, (button.H - capH) * 0.5f, cap.W, capH);
            }

            return new V6Rect(label.X, 0f, label.W, button.H);
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
    }
}
