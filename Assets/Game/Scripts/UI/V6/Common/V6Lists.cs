using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 契約 §5 のリスト行部品。行の内部レイアウトは契約の座標、
    /// 行の位置/寸法/行間は引数で受ける (Select の一覧スクロールは 76 間隔、
    /// Ranking/Players は 52 間隔で、同じ行部品を使い回す)。
    /// 選択行は背景を $k6 (#525252) に差し替える (selected = true)。
    /// すべて design 空間 (左上原点・y_down)。
    /// </summary>
    public static class V6Lists
    {
        // --- 契約 §5 の行内寸法 (design px) ---
        public const float RowPaddingX = 12f;
        public const float RowGap = 12f;

        // SongRow
        public const float SongRowW = 504f;
        public const float SongRowH = 72f;
        public const float SongThumbW = 96f;
        public const float SongThumbH = 54f;
        public const float SongLevelSize = 44f;
        public const float SongGradeW = 56f;
        public const float SongGradeH = 36f;
        public const float SongTitleFs = 26f;
        public const float SongComposerFs = 20f;
        public const float SongBadgeFs = 22f;

        // RankRow / PlayerRow
        public const float RankRowW = 496f;
        public const float RankRowH = 44f;
        public const float RankPosSize = 36f;
        public const float RankNameW = 140f;
        public const float RankValueW = 180f;
        public const float RankGradeW = 56f;
        public const float RankGradeH = 36f;
        public const float RankFs = 22f;
        public const float PlayerPlaysW = 56f;
        public const float PlayerPlaysFs = 20f;

        /// <summary>
        /// 曲リスト行 (契約 §5 Select): 504×72 / 背景 #1B1B1B r8 (選択行は $k6) /
        /// padding [0,12] gap12: Thumb 96×54 r4 / Texts (Title fs26 w500 上 + Composer fs20
        /// $text-muted 下、gap2) / Grade 56×36 $k4 r4 / Level 44×44 $k4 r22 (fs22 w500)。
        /// Grade は契約上 Texts 系に記載されているが、縦 3 段 (26+20+36+gap) は行高 72 に
        /// 収まらないため、Texts と Level の間の独立要素 (Grade バッジ枠) として配置する。
        /// ジャケット画像の設定は各画面側で行う。
        /// </summary>
        public static GameObject SongRow(GameObject parent, string title, string composer, string level,
            Vector2 designTopLeft, Vector2 size, bool selected = false)
        {
            var go = V6Controls.Panel(parent, "SongRow " + title, designTopLeft, size,
                selected ? V6Tokens.Key6 : V6Tokens.Key1, V6Tokens.RadiusMedium);
            float h = size.y;

            // Thumb 96×54 r4 (行の左端、上下中央)。
            V6Controls.Panel(go, "Thumb", new Vector2(RowPaddingX, (h - SongThumbH) * 0.5f),
                new Vector2(SongThumbW, SongThumbH), V6Tokens.Key2, V6Tokens.RadiusSmall);

            // Level 44×44 r22 (行の右端、上下中央)。
            float levelX = size.x - RowPaddingX - SongLevelSize;
            var levelGo = V6Controls.Panel(go, "Level", new Vector2(levelX, (h - SongLevelSize) * 0.5f),
                new Vector2(SongLevelSize, SongLevelSize), V6Tokens.Key4, SongLevelSize * 0.5f);
            StretchText(levelGo, "Level", level, SongBadgeFs, V6Tokens.Text);

            // Grade 56×36 r4 (Level の左、上下中央)。グレード文字は各画面側で設定する。
            float gradeX = levelX - RowGap - SongGradeW;
            V6Controls.Panel(go, "Grade", new Vector2(gradeX, (h - SongGradeH) * 0.5f),
                new Vector2(SongGradeW, SongGradeH), V6Tokens.Key4, V6Tokens.RadiusSmall);

            // Texts: Title (上) + Composer (下)、gap2。幅は Thumb の右から Grade の左まで。
            float textsLeft = RowPaddingX + SongThumbW + RowGap;
            float textsW = Mathf.Max(0f, gradeX - RowGap - textsLeft);
            float titleH = SongTitleFs * 1.25f; // TMP 既定 line height 比の上側目安
            V6Controls.Text(go, "Title", title, new Vector2(textsLeft, 0f), new Vector2(textsW, titleH),
                SongTitleFs, V6Tokens.Text);
            float composerY = titleH + 2f;
            V6Controls.Text(go, "Composer", composer, new Vector2(textsLeft, composerY),
                new Vector2(textsW, h - composerY), SongComposerFs, V6Tokens.TextMuted);
            return go;
        }

        /// <summary>
        /// ランキング行 (契約 §5 Ranking): 496×44 / 背景 $k1 r8 (選択行は $k6) /
        /// padding [0,12] gap12: Pos 36×36 $k4 r18 (fs22) / Name w140 fs22 /
        /// Value w180 fs22 右寄せ / Grade 56×36 $k4 r4。
        /// </summary>
        public static GameObject RankRow(GameObject parent, string pos, string name, string value, string grade,
            Vector2 designTopLeft, Vector2 size, bool selected = false)
        {
            var go = V6Controls.Panel(parent, "RankRow " + pos, designTopLeft, size,
                selected ? V6Tokens.Key6 : V6Tokens.Key1, V6Tokens.RadiusMedium);
            float h = size.y;

            var posGo = V6Controls.Panel(go, "Pos", new Vector2(RowPaddingX, (h - RankPosSize) * 0.5f),
                new Vector2(RankPosSize, RankPosSize), V6Tokens.Key4, RankPosSize * 0.5f);
            StretchText(posGo, "Pos", pos, RankFs, V6Tokens.Text);

            V6Controls.Text(go, "Name", name, new Vector2(RowPaddingX + RankPosSize + RowGap, 0f),
                new Vector2(RankNameW, h), RankFs, V6Tokens.Text);

            float gradeX = size.x - RowPaddingX - RankGradeW;
            var gradeGo = V6Controls.Panel(go, "Grade", new Vector2(gradeX, (h - RankGradeH) * 0.5f),
                new Vector2(RankGradeW, RankGradeH), V6Tokens.Key4, V6Tokens.RadiusSmall);
            StretchText(gradeGo, "Grade", grade, RankFs, V6Tokens.Text);

            // Value: w180 右寄せ。右端は Grade の左 12px。
            float valueX = gradeX - RowGap - RankValueW;
            V6Controls.Text(go, "Value", value, new Vector2(valueX, 0f), new Vector2(RankValueW, h),
                RankFs, V6Tokens.Text, TextAlignmentOptions.Right);
            return go;
        }

        /// <summary>
        /// プレイヤー行 (契約 §5 Players): RankRow 構成の Pos/Name/Value (Ranking と同寸) から
        /// Grade を外し、右端に Plays w56 fs20 $text-dim 右寄せを載せる。
        /// </summary>
        public static GameObject PlayerRow(GameObject parent, string pos, string name, string value, string plays,
            Vector2 designTopLeft, Vector2 size, bool selected = false)
        {
            var go = V6Controls.Panel(parent, "PlayerRow " + pos, designTopLeft, size,
                selected ? V6Tokens.Key6 : V6Tokens.Key1, V6Tokens.RadiusMedium);
            float h = size.y;

            var posGo = V6Controls.Panel(go, "Pos", new Vector2(RowPaddingX, (h - RankPosSize) * 0.5f),
                new Vector2(RankPosSize, RankPosSize), V6Tokens.Key4, RankPosSize * 0.5f);
            StretchText(posGo, "Pos", pos, RankFs, V6Tokens.Text);

            V6Controls.Text(go, "Name", name, new Vector2(RowPaddingX + RankPosSize + RowGap, 0f),
                new Vector2(RankNameW, h), RankFs, V6Tokens.Text);

            float playsX = size.x - RowPaddingX - PlayerPlaysW;
            V6Controls.Text(go, "Plays", plays, new Vector2(playsX, 0f), new Vector2(PlayerPlaysW, h),
                PlayerPlaysFs, V6Tokens.TextDim, TextAlignmentOptions.Right);

            float valueX = playsX - RowGap - RankValueW;
            V6Controls.Text(go, "Value", value, new Vector2(valueX, 0f), new Vector2(RankValueW, h),
                RankFs, V6Tokens.Text, TextAlignmentOptions.Right);
            return go;
        }

        // --- 内部 ---

        private static void StretchText(GameObject parent, string name, string content, float fontSize, Color color)
        {
            var tmp = V6Controls.Text(parent, name, content, Vector2.zero, Vector2.zero, fontSize, color,
                TextAlignmentOptions.Center);
            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
