using TMPro;
using UnityEngine;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 全画面共通のヘッダー帯 (design: frame ローカル (24,0) 1680×96、契約 §4)。
    ///
    /// 構成は「左 = 戻るボタン (または画面固有の左要素) + タイトル + 右スロット」。
    /// 右スロットは画面ごとに中身が違う (History の並び替え / Ranking の曲情報 /
    /// Select の検索欄 / Loading・Pause の曲バッジ / Title の更新・版数) ため、
    /// 部品としては固定せず、帯 (Root) 直下に header ローカル座標で追加する。
    /// 追加は <see cref="Create"/> / 帯ゾーンの GameObject を親にして行う
    /// (座標系は帯ローカルの design 空間のまま。変換は V6Layout に集約)。
    ///
    /// 画面ごとの差分の扱い:
    /// - 戻るボタン: 位置は全画面共通の (0,20) 56×56 (<see cref="V6Controls.BackButton"/>)。
    ///   表示キーは画面ごとに正本の取り方が違う (ActionKeyHint.Primary /
    ///   ShortcutKeyCap.PrimaryText / 固定 "Esc") ため、文字列は呼び出し側が渡す。
    /// - タイトル: 正本は (72,20) 400×56 fs38 w700 $text 左上揃え
    ///   (History / Ranking / Option / Result)。Select (80,20 300×56) /
    ///   Pause (72,20 185×56) / Title (72,20 185×56 "FlashBeat") は design が
    ///   違うので pos / size を明示する。Loading は design 上タイトル無し。
    /// - 左スロット: Title は Exit (V6TitleExit)、Result はトロフィー、
    ///   Pause は design 上の戻るゾーンが空 — これらは戻るボタンではないので
    ///   <see cref="BackButton"/> を呼ばず、画面側が帯直下に置く。
    ///
    /// 帯そのものの作り方は 2 系統ある (挙動を変えないため両方残す):
    /// - V6Frame の "Header" ゾーンを使う画面 (History / Ranking / Option / Title)
    ///   → ゾーンが既にあるので <see cref="Create"/> は呼ばない。
    /// - 自前で帯を作る画面 → <see cref="Create"/>。panel=true は Image 付きの
    ///   透明パネル (Select / Result の従来実装)、false は素の RectTransform
    ///   (Loading / Pause の従来実装)。
    /// </summary>
    public static class V6Header
    {
        /// <summary>帯の幅 (design px、契約 §4)。</summary>
        public const float Width = 1680f;
        /// <summary>帯の高さ (design px、契約 §4)。</summary>
        public const float Height = 96f;
        /// <summary>帯の frame ローカル左上 (契約 §4 Header ゾーン)。</summary>
        public static readonly Vector2 FramePos = new Vector2(24f, 0f);

        /// <summary>戻るボタンの帯ローカル左上 (design zUcsi/zCQBY: (0,20) 56×56)。</summary>
        public static readonly Vector2 BackPos = new Vector2(0f, 20f);

        /// <summary>タイトルの帯ローカル左上 (正本: History q9JZcH / Ranking S1TZD 等)。</summary>
        public static readonly Vector2 TitlePos = new Vector2(72f, 20f);
        /// <summary>タイトルの帯ローカル寸法 (正本 400×56)。</summary>
        public static readonly Vector2 TitleSize = new Vector2(400f, 56f);
        /// <summary>タイトルのフォントサイズ (design fs38 w700)。</summary>
        public const float TitleFontSize = 38f;

        /// <summary>
        /// ヘッダー帯 (frame ローカル (24,0) 1680×96) を frame 直下に作る。
        /// V6Frame を使わない画面 (Select / Loading / Result / Pause) 用。
        /// V6Frame の "Header" ゾーンを使う画面はこの関数を呼ばず、
        /// ゾーンの GameObject をそのまま <see cref="BackButton"/> /
        /// <see cref="Title"/> の親にする。
        /// </summary>
        /// <param name="panel">
        /// true なら Image 付きの透明パネル (Select / Result の従来実装。
        /// Image は raycastTarget=true のまま — 帯が背後の要素のクリックを
        /// 遮る従来挙動を維持する)。false なら素の RectTransform ノード
        /// (Loading / Pause の従来実装)。
        /// </param>
        public static GameObject Create(GameObject frame, bool panel, string name = "Header")
        {
            GameObject header;
            if (panel)
            {
                // V5Ui.Panel は sizeDelta だけを設定しアンカーは触らないので、
                // 後続の V6Layout.Place で (0,1) 原点・design 座標へ揃える
                // (Select / Result の At(…, 24, 0) と同じ結果)。
                header = V5Ui.Panel(frame, name, new Vector2(Width, Height), Clear(), 0f);
            }
            else
            {
                header = V5Ui.Node(frame, name);
            }

            V6Layout.Place(header.GetComponent<RectTransform>(), FramePos,
                new Vector2(Width, Height));
            return header;
        }

        /// <summary>
        /// 左上の戻るボタン (帯ローカル (0,20) 56×56 / $k4 r8 / arrow-left 24 +
        /// Key fs16)。中身と押下挙動は <see cref="V6Controls.BackButton"/> が持つ
        /// (Issue #370)。Nav 設定・クリック結線のばらつきを防ぐため全画面ここ経由。
        /// </summary>
        /// <param name="keyLabel">
        /// ボタン内 Key に出す文字列。確定キーはキー設定の正本から取るが、
        /// 画面ごとに参照元が違う (ActionKeyHint.Primary / ShortcutKeyCap.PrimaryText /
        /// 固定 "Esc") ため呼び出し側が解決して渡す。
        /// </param>
        /// <param name="pos">省略時は正本の <see cref="BackPos"/>。design が違う画面だけ明示する。</param>
        public static GameObject BackButton(GameObject header, string name, string keyLabel,
            UnityEngine.Events.UnityAction onClick, Vector2? pos = null)
        {
            return V6Controls.BackButton(header, name, pos ?? BackPos, keyLabel, onClick);
        }

        /// <summary>
        /// ヘッダーのタイトル文字 (fs38 w700 $text 左上揃え、NoWrap)。
        /// 既定は正本の <see cref="TitlePos"/> / <see cref="TitleSize"/>。
        /// Select / Pause / Title は design の矩形が違うので pos / size を明示する。
        /// 戻り値の TMP は「選択中の曲名」のように後から書き換える画面が保持する。
        /// </summary>
        public static TextMeshProUGUI Title(GameObject header, string text,
            Vector2? pos = null, Vector2? size = null, float fontSize = TitleFontSize)
        {
            return V6Controls.Text(header, "Title", text, pos ?? TitlePos, size ?? TitleSize,
                fontSize, V6Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
        }

        /// <summary>完全透明 (帯パネルの塗り色)。</summary>
        private static Color Clear()
        {
            return new Color(0f, 0f, 0f, 0f);
        }
    }
}
