using UnityEngine;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// v6 UI 共通ビルダ。v6 の各ビューは 1728×972 (Game HUD は 1920×1080) の design 空間を
    /// frame 内に置き、親フレームで等比拡大する (<see cref="V6SelectViewLogic"/>.DesignScale 等)。
    /// そのため部品サイズもフォントサイズも **design 空間** で指定しなければならない。
    ///
    /// <see cref="V5Ui.KeyCap"/>/<see cref="V5Ui.Tag"/> の既定 fs は
    /// <see cref="UISizeProfile.Current"/>.FsS (実画面ピクセル基準) のため、design 空間で
    /// 直接使うと文字だけ実寸より大きく描かれ、キャップからはみ出して折り返す
    /// (例: 2560×1664 で 76×32 キャップに fs36 → 実描画 53px、"Space" が 2 行)。
    /// v6 からは本クラス経由で作り、fs をキャップ高さと同一座標空間で決める。
    ///
    /// 契約 (docs/design/v6/flashbeat-v6-contract.md):
    /// - 汎用 KeyCap: 高さ 36, Key fs22 → 比 22/36 = 0.611
    /// - Mac Key Guide: KeyCap 75×48, Key fs32 → 比 32/48 = 0.667
    /// </summary>
    public static class V6Ui
    {
        /// <summary>契約の汎用 KeyCap 比: Key fs22 / 高さ 36。</summary>
        public const float KeyCapFontSizeRatio = 22f / 36f;

        /// <summary>契約の KeyCap 左右パディング (design px)。幅フィットの安全マージン。</summary>
        public const float KeyCapPaddingX = 10f;

        /// <summary>NotoSansJP の ASCII 字形の平均送り幅 (em) の上側推定。</summary>
        public const float AsciiAdvanceEm = 0.62f;

        /// <summary>ASCII 以外 (全角・矢印など) の送り幅 (em) の上側推定。</summary>
        public const float FullWidthAdvanceEm = 1.0f;

        /// <summary>
        /// キャップ 1 行に収まるラベル fs (design 空間)。
        /// 高さ比 (<see cref="KeyCapFontSizeRatio"/>) と幅フィットの最小値を床切りで返す。
        /// 幅フィット: label の推定送り幅 em あたり必要幅で利用幅 (幅 - 左右パディング) を割る。
        /// </summary>
        public static float KeyCapFontSize(float capWidth, float capHeight, string label)
        {
            float byHeight = capHeight * KeyCapFontSizeRatio;
            float available = capWidth - KeyCapPaddingX * 2f;
            float em = LabelAdvanceEm(label);
            if (em <= 0f || available <= 0f)
            {
                return Mathf.Floor(byHeight);
            }

            float byWidth = available / em;
            return Mathf.Max(1f, Mathf.Floor(Mathf.Min(byHeight, byWidth)));
        }

        /// <summary>ラベル 1 行の推定送り幅 (em)。空/null は 0。</summary>
        public static float LabelAdvanceEm(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return 0f;
            }

            float em = 0f;
            foreach (var c in label)
            {
                em += c <= 0x7F ? AsciiAdvanceEm : FullWidthAdvanceEm;
            }

            return em;
        }

        /// <summary>
        /// v6 design 空間用 KeyCap。fs はキャップ高さと同一座標空間で決める
        /// (契約比 22/36 を基準に、幅から 1 行に収まる値へ縮める)。
        /// fontSize &gt; 0 で明示指定も可 (HUD の Key fs24 など契約値を直接与える)。
        /// 位置・余白は design 空間のまま (フレームが既に実ピクセルへスケール済み)。
        /// </summary>
        public static GameObject KeyCap(GameObject parent, string label, Vector2 size, float fontSize = 0f)
        {
            float fs = fontSize > 0f ? fontSize : KeyCapFontSize(size.x, size.y, label);
            return V5Ui.KeyCap(parent, label, size, fs);
        }

        /// <summary>
        /// v6 design 空間用 Tag。<see cref="KeyCap"/> と同じ座標空間ルールで fs を決める。
        /// </summary>
        public static GameObject Tag(GameObject parent, string label, Vector2 size, float fontSize = 0f)
        {
            float fs = fontSize > 0f ? fontSize : KeyCapFontSize(size.x, size.y, label);
            return V5Ui.Tag(parent, label, size, fs);
        }
    }
}
