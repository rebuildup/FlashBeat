using System.Text;

namespace FlashBeat.UI.V6.Select.View
{
    /// <summary>v6 選曲画面のキーボードアクション (実キーへの割り当ては V6SelectView 側)。</summary>
    public enum V6SelectAction
    {
        None,
        MoveUp,
        MoveDown,
        Confirm,
        Cancel,
        FocusSearch,
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

        /// <summary>ドットインジケータの active index (範囲外は端に留まる)。</summary>
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
        /// </summary>
        public static V6SelectAction MapKey(bool searchFocused, bool up, bool down, bool confirm, bool cancel,
            bool toggleSearch)
        {
            if (searchFocused)
            {
                if (toggleSearch || confirm || cancel)
                {
                    return V6SelectAction.UnfocusSearch;
                }

                return V6SelectAction.None;
            }

            if (up) return V6SelectAction.MoveUp;
            if (down) return V6SelectAction.MoveDown;
            if (confirm) return V6SelectAction.Confirm;
            if (cancel) return V6SelectAction.Cancel;
            if (toggleSearch) return V6SelectAction.FocusSearch;
            return V6SelectAction.None;
        }
    }
}
