using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 の「曲バッジ」共通ビルダ (Issue #385 Phase 1.2)。
    /// 「ジャケット画像 + 曲名 (+ 作家名 / 副題)」の表示を 1 部品に集約する。
    ///
    /// 対象画面 (design 実測値は各 <see cref="Spec"/> 呼び出し側が持つ):
    /// - Ranking ヘッダー右上: Thumb (864,30) 64×36 + Song 列 (944,10) 320×76
    ///   (Title fs30 / Artist fs20)。Divider・Stat はバッジに含めず画面側に残す。
    /// - Ranking SCORE RANKING の曲チップ: Thumb (0,4) 40×24 + Text (48,0) 111×32
    ///   fs22 (タイトルのみ、Sub 無し)。
    /// - Result「Song Info」: Thumb (16,28) 112×63 + Title (144,16) fs34 /
    ///   Artist (144,66) fs23 (列ノード無し、pivot (0,0.5))。
    /// - Result 曲名入力モーダルの Summary: Thumb (16,20) 88×50 + Title fs26 /
    ///   Artist fs18。
    /// - Pause 右上「Song Info」: Thumb (24,30) 64×36 $k2 + Song 列 (100,8) 320×80
    ///   (Title fs30 / Sub fs22 = 「作家・BPM」の複合副題。Artist ではない)。
    /// - Loading 右上「Song Info」: HorizontalLayoutGroup 配下の Jacket 56×56 +
    ///   Texts 列 113×56 (Song Title fs22 / Song Artist fs16)。
    ///   レイアウトグループ駆動のため <see cref="Spec.LayoutDriven"/> で作る。
    ///
    /// 対象外 (構造が違うため画面側に残す。安易に寄せると design が崩れる):
    /// - Select / History の「Selected Song Panel」: Hero (512×288) と Meta
    ///   (Title fs48 太字 + Tag No チップ + Artist) が別親で、Meta は
    ///   autosize / Artist Row (HLG) の差分を持つ。ジャケット単体は巨大で
    ///   「バッジ」の想定サイズを超える。
    /// - History「曲ランキング」の曲チップ: HLG 行内で LayoutElement 駆動、
    ///   Text は矩形指定なし (preferredWidth 任せ)。
    /// - Result ヘッダーの「Trophy Badge」: トロフィー表示で楽曲ではない。
    /// - Title 画面: 右スロットは更新・版数表示で曲バッジは無い。
    /// - リスト行 (V6Lists.SongRow) は既に共通部品。
    /// - Pause / Loading のジャケット画像更新 (YouTube 遠隔 / SquareCrop) は
    ///   取得経路が違うため画面側のまま。ここでは構造だけを共通化する。
    ///
    /// 画像の適用は <see cref="ApplyJacket"/> に一本化 (Issue #398 の
    /// V6JacketProvider 経路。Select の V6JacketResources.LoadSprite と
    /// 同一実体)。遠隔画像を使う画面は自前の更新を持つ。
    /// </summary>
    public static class V6SongBadge
    {
        /// <summary>テキスト 1 行分の指定 (design 座標は列ノードまたは parent 基準)。</summary>
        public sealed class Line
        {
            /// <summary>GameObject 名 ("Title" / "Artist" / "Sub" / "Text" 等)。</summary>
            public string Name = "Title";
            /// <summary>初期表示文字列 (後から書き換える画面は空のままでよい)。</summary>
            public string Text = "";
            /// <summary>左上原点の design 座標。</summary>
            public Vector2 Pos;
            /// <summary>矩形サイズ (design px)。</summary>
            public Vector2 Size;
            public float FontSize;
            public Color Color = V6Tokens.Text;
            public FontStyles Style = FontStyles.Normal;
            public TextAlignmentOptions Align = TextAlignmentOptions.Left;
            /// <summary>true で overflowMode=Ellipsis (全バッジの既定は true)。</summary>
            public bool Ellipsis = true;
            /// <summary>true で V5Ui.FitEllipsisToLineHeight を呼ぶ (TMP Ellipsis の 0 行化対策)。</summary>
            public bool FitLineHeight;
            /// <summary>
            /// FitEllipsisToLineHeight の矩形高さ明示。0 以下は rect から読む
            /// (LayoutGroup 配下など rect 未確定のときだけ正の値を渡す)。
            /// </summary>
            public float FitHeight;
            /// <summary>
            /// null は左上原点ピボット (<see cref="V6Layout.Place"/> と同じ)。
            /// Result の Song Info は design 実測どおり (0,0.5) を渡す。
            /// </summary>
            public Vector2? Pivot;
        }

        /// <summary>バッジ全体の指定。</summary>
        public sealed class Spec
        {
            /// <summary>ジャケット枠の GameObject 名 ("Thumb" / "Jacket")。</summary>
            public string ThumbName = "Thumb";
            /// <summary>ジャケット枠の design 左上座標。</summary>
            public Vector2 ThumbPos;
            public Vector2 ThumbSize = new Vector2(64f, 36f);
            /// <summary>ジャケット無しのときの空枠色 ($k3 が既定。Pause は $k2)。</summary>
            public Color ThumbColor = V6Tokens.Key3;
            /// <summary>角丸 (design px)。既定は <see cref="V6Tokens.RadiusSmall"/>。</summary>
            public float ThumbRadius = V6Tokens.RadiusSmall;
            /// <summary>false で Image.raycastTarget=false (Pause のみ)。</summary>
            public bool ThumbRaycastTarget = true;
            /// <summary>
            /// true で Thumb / Column を HorizontalLayoutGroup に委ね、
            /// V6Layout.Place での絶対配置をしない (Loading の Song Info)。
            /// この場合 ThumbPos / ColumnPos は使われない (sizeDelta だけ設定)。
            /// </summary>
            public bool LayoutDriven;

            /// <summary>
            /// テキスト列ノード名 ("Song" / "Texts")。null / 空なら
            /// Title / Sub を parent 直下に置く (Result / 曲チップ)。
            /// </summary>
            public string ColumnName;
            public Vector2 ColumnPos;
            public Vector2 ColumnSize;

            /// <summary>1 行目 (曲名)。必須。</summary>
            public Line Title = new Line();
            /// <summary>2 行目 (作家名 / 副題)。null ならタイトルのみ (曲チップ)。</summary>
            public Line Sub;
        }

        /// <summary><see cref="Create"/> の構築結果。後から text / sprite を更新する画面が保持する。</summary>
        public sealed class Badge
        {
            /// <summary>ジャケット枠 (Image 持ち。ApplyJacket の対象)。</summary>
            public GameObject Thumb;
            /// <summary>テキスト列ノード。ColumnName 未指定なら null。</summary>
            public GameObject Column;
            public TextMeshProUGUI Title;
            /// <summary>2 行目。Sub 未指定 (チップ) なら null。</summary>
            public TextMeshProUGUI Sub;
        }

        /// <summary>
        /// バッジ (Thumb + 任意のテキスト列 + Title / 任意の Sub) を parent 直下に作る。
        /// 座標・サイズ・フォント・色はすべて <paramref name="spec"/> の design 値。
        /// container 自体 ("Song Info" 等) の作成と配置は呼び出し側が行う
        /// (右上アンカー・HLG など画面ごとの差分をここへ持ち込まない)。
        /// </summary>
        public static Badge Create(GameObject parent, Spec spec)
        {
            if (parent == null)
            {
                throw new ArgumentNullException("parent");
            }

            if (spec == null)
            {
                throw new ArgumentNullException("spec");
            }

            if (spec.Title == null)
            {
                throw new ArgumentException("spec.Title is required", "spec");
            }

            var badge = new Badge();
            badge.Thumb = MakeThumb(parent, spec);

            GameObject textParent = parent;
            if (!string.IsNullOrEmpty(spec.ColumnName))
            {
                badge.Column = spec.LayoutDriven
                    ? V5Ui.Node(parent, spec.ColumnName)
                    : PlacedNode(parent, spec.ColumnName, spec.ColumnPos, spec.ColumnSize);
                textParent = badge.Column;
            }

            badge.Title = MakeLine(textParent, spec.Title);
            if (spec.Sub != null)
            {
                badge.Sub = MakeLine(textParent, spec.Sub);
            }

            return badge;
        }

        /// <summary>
        /// ジャケット枠へローカル Resources の画像を適用する
        /// (Select / Ranking / History / Result の SetJacket/SetThumb の共通実装)。
        /// 画像があれば Simple + preserveAspect、無ければ Sliced の空枠
        /// (RoundedSprite(4)、色は fallback ?? $k3)。Sprite の所有は
        /// <see cref="V6JacketProvider"/> (Issue #398)。
        /// </summary>
        public static void ApplyJacket(GameObject thumbGo, int songId, string title, Color? fallback = null)
        {
            ApplyJacket(thumbGo != null ? thumbGo.GetComponent<Image>() : null, songId, title, fallback);
        }

        /// <summary><see cref="ApplyJacket(GameObject, int, string, Color?)"/> の Image 版。</summary>
        public static void ApplyJacket(Image thumb, int songId, string title, Color? fallback = null)
        {
            if (thumb == null)
            {
                return;
            }

            Sprite sprite = V6JacketProvider.Local(songId, title);
            if (sprite != null)
            {
                thumb.type = Image.Type.Simple;
                thumb.sprite = sprite;
                thumb.color = Color.white;
                thumb.preserveAspect = true;
            }
            else
            {
                // ジャケット無しは空枠 ($k3。Pause は呼び出し側が遠隔画像を使うので来ない)。
                thumb.type = Image.Type.Sliced;
                thumb.sprite = V5Ui.RoundedSprite(4);
                thumb.color = fallback ?? V6Tokens.Key3;
            }
        }

        // --- 内部 ---

        private static GameObject MakeThumb(GameObject parent, Spec spec)
        {
            var thumb = V5Ui.Panel(parent, spec.ThumbName, spec.ThumbSize, spec.ThumbColor, spec.ThumbRadius);
            if (!spec.LayoutDriven)
            {
                V6Layout.Place(thumb.GetComponent<RectTransform>(), spec.ThumbPos, spec.ThumbSize);
            }

            thumb.GetComponent<Image>().raycastTarget = spec.ThumbRaycastTarget;
            return thumb;
        }

        private static GameObject PlacedNode(GameObject parent, string name, Vector2 pos, Vector2 size)
        {
            var go = V5Ui.Node(parent, name);
            V6Layout.Place(go.GetComponent<RectTransform>(), pos, size);
            return go;
        }

        private static TextMeshProUGUI MakeLine(GameObject parent, Line line)
        {
            var tmp = V5Ui.Text(parent, line.Name, line.Text, line.FontSize, line.Color,
                line.Align, line.Style);
            // バッジのテキストは全て 1 行固定 (design 上の矩形に収める)。
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            if (line.Ellipsis)
            {
                tmp.overflowMode = TextOverflowModes.Ellipsis;
            }

            var rt = tmp.rectTransform;
            if (line.Pivot.HasValue)
            {
                // 画面側の At(x,y,w,h,pivot) と同じ式 (design 左上原点の見た目を保つ)。
                var pivot = line.Pivot.Value;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = pivot;
                rt.sizeDelta = line.Size;
                rt.anchoredPosition = V6Layout.ToLocal(new Vector2(
                    line.Pos.x + pivot.x * line.Size.x,
                    line.Pos.y + (1f - pivot.y) * line.Size.y));
            }
            else
            {
                V6Layout.Place(rt, line.Pos, line.Size);
            }

            // 矩形と fontSize 確定後に呼ぶ (TMP Ellipsis の 0 行化対策、V5Ui の契約どおり)。
            if (line.FitLineHeight)
            {
                V5Ui.FitEllipsisToLineHeight(tmp, line.FitHeight);
            }

            return tmp;
        }
    }
}
