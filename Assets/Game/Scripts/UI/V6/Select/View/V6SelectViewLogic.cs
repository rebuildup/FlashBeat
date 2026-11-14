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
    /// Issue #385 Phase 2.10 で領域別 partial ファイルへ分割
    /// (V6SelectViewLogicLayout.cs / V6SelectViewLogicText.cs は同名 partial)。
    /// </summary>
    public static partial class V6SelectViewLogic
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

        /// <summary>
        /// 曲リストをスケルトンで描くか (Issue #374)。初回の読み込み中だけ出す。
        /// 既に表示できている行がある状態でカタログ / ランキングを再取得しても
        /// スケルトンへ戻さない (点滅させない)。
        /// </summary>
        public static bool ShouldShowSkeleton(bool isLoading, int visibleSongCount)
        {
            return isLoading && visibleSongCount == 0;
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
