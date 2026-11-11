using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 のリスト行部品。行の内部レイアウトは design 実測座標 (正本: Pencil ノード
    /// 曲行 mcCOI/Odv71 = 504×72、ランキング行 Hbxqf = 496×56 / T8C1Hu = 496×52、
    /// プレイヤー行 tcgRX = 496×56)、行の位置/寸法/行間は引数で受ける
    /// (Select/Ranking の曲一覧は 76 間隔、Ranking のスコア行は 64、History は 60)。
    /// 選択行は背景を $k6 (#525252) に差し替える (selected = true)。
    /// すべて design 空間 (左上原点・y_down)。座標計算を各画面に書き戻さないこと。
    /// </summary>
    public static class V6Lists
    {
        // --- 行の左右 padding / 要素間 gap (design: padding [0,12] gap 12) ---
        // 契約 doc §5 の「ランキング行 496×44 / Name w140」は Select 画面の
        // SCORE RANKING 列 (OENRv jnIY9 系 = 496×44 Name140) の値で、この部品の
        // 使用先 (Ranking/History) の正本ではない。Ranking/History の実測値は
        // Hbxqf 496×56 / T8C1Hu 496×52 で Name w160。design 実測値を優先する
        // (2026-11-11 再現レビュー)。
        public const float RowPaddingX = 12f;
        public const float RowGap = 12f;

        // --- 曲行 (mcCOI 504×72 r8) ---
        public const float SongRowW = 504f;
        public const float SongRowH = 72f;
        public const float SongLevelSize = 44f;
        public const float SongLevelX = 12f;
        public const float SongLevelY = 14f;
        public const float SongThumbW = 96f;
        public const float SongThumbH = 54f;
        public const float SongThumbX = 68f;
        public const float SongThumbY = 9f;
        public const float SongTextsX = 176f;
        public const float SongTextsY = 2f;
        public const float SongTextsW = 248f;
        public const float SongTextsH = 69f;
        /// <summary>Title 矩形高 (design 38)。NotoSansJP の行送り ≈1.45fs なので
        /// fs26 には 38px 必要 (これ未満だと Ellipsis で 1 行も描画されない)。</summary>
        public const float SongTitleH = 38f;
        /// <summary>Composer 矩形高 (design 29 = fs20 の行送り相当)。</summary>
        public const float SongComposerH = 29f;
        /// <summary>Texts 内の Composer 上端 y (design 40、gap 2)。</summary>
        public const float SongComposerY = 40f;
        public const float SongGradeW = 56f;
        public const float SongGradeH = 36f;
        public const float SongGradeX = 436f;
        public const float SongGradeY = 18f;
        public const float SongTitleFs = 26f;
        public const float SongComposerFs = 20f;
        public const float SongBadgeFs = 22f;

        // --- RankRow / PlayerRow (Hbxqf 496×56 / T8C1Hu 496×52 / tcgRX 496×56) ---
        /// <summary>Ranking 行の design 高 (Hbxqf)。History は 52 (T8C1Hu) を size で渡す。</summary>
        public const float RankRowW = 496f;
        public const float RankRowH = 56f;
        public const float RankPosSize = 36f;
        public const float RankPosX = 12f;
        public const float RankNameX = 60f;
        /// <summary>Name 幅 (design 実測 160。契約 doc §5 の 140 は Select のランキング列の値)。</summary>
        public const float RankNameW = 160f;
        public const float RankValueX = 232f;
        public const float RankValueW = 180f;
        public const float RankGradeX = 424f;
        public const float RankGradeW = 56f;
        public const float RankGradeH = 36f;
        /// <summary>Name / Value の矩形高 (design 32、縦中央寄せ)。</summary>
        public const float RankTextH = 32f;
        public const float RankFs = 22f;
        public const float PlayerPlaysX = 424f;
        public const float PlayerPlaysW = 56f;
        public const float PlayerPlaysH = 29f;
        public const float PlayerPlaysFs = 20f;

        /// <summary>
        /// 曲リスト行 (design mcCOI「Song · ロストアンブレラ」504×72 r8):
        /// Level (12,14) 44×44 r22 (fs22 中央) / Thumb (68,9) 96×54 r4 /
        /// Texts (176,2) 248×69 (Title (0,0) 248×38 fs26 + gap2 + Composer (0,40) 248×29 fs20
        /// $text-muted) / Grade (436,18) 56×36 r4 (fs22 中央)。背景は非選択 $k1 / 選択 $k6。
        /// Title・Composer は 1 行固定 (NoWrap + Ellipsis、はみ出す分は … で省略)。
        /// ジャケット画像は呼び出し側が <see cref="Thumb"/> の子に設定する。
        /// グレード文字は grade 引数で渡す (空なら "–")。
        /// </summary>
        public static GameObject SongRow(GameObject parent, string title, string composer, string level, string grade,
            Vector2 designTopLeft, Vector2 size, bool selected = false)
        {
            var go = V6Controls.Panel(parent, "SongRow " + title, designTopLeft, size,
                selected ? V6Tokens.Key6 : V6Tokens.Key1, V6Tokens.RadiusMedium);

            // Level 44×44 r22 (12,14)。数値は中央 (fs22 w500)。
            var levelGo = V6Controls.Panel(go, "Level", new Vector2(SongLevelX, SongLevelY),
                new Vector2(SongLevelSize, SongLevelSize), V6Tokens.Key4, SongLevelSize * 0.5f);
            StretchText(levelGo, "Level", level, SongBadgeFs, V6Tokens.Text);

            // Thumb 96×54 r4 (68,9)。画像は呼び出し側で設定 (無ければ $k2 のまま)。
            V6Controls.Panel(go, "Thumb", new Vector2(SongThumbX, SongThumbY),
                new Vector2(SongThumbW, SongThumbH), V6Tokens.Key2, V6Tokens.RadiusSmall);

            // Texts (176,2) 248×69: Title 248×38 (fs26) + gap2 + Composer 248×29 (fs20)。
            var titleText = V6Controls.Text(go, "Title", title,
                new Vector2(SongTextsX, SongTextsY), new Vector2(SongTextsW, SongTitleH),
                SongTitleFs, V6Tokens.Text);
            titleText.overflowMode = TextOverflowModes.Ellipsis;
            var composerText = V6Controls.Text(go, "Composer", composer,
                new Vector2(SongTextsX, SongTextsY + SongComposerY), new Vector2(SongTextsW, SongComposerH),
                SongComposerFs, V6Tokens.TextMuted);
            composerText.overflowMode = TextOverflowModes.Ellipsis;

            // Grade 56×36 r4 (436,18)。グレード文字は呼び出し側から渡す。
            var gradeGo = V6Controls.Panel(go, "Grade", new Vector2(SongGradeX, SongGradeY),
                new Vector2(SongGradeW, SongGradeH), V6Tokens.Key4, V6Tokens.RadiusSmall);
            StretchText(gradeGo, "Grade", string.IsNullOrEmpty(grade) ? "–" : grade,
                SongBadgeFs, V6Tokens.Text);
            return go;
        }

        /// <summary>
        /// 曲リスト行が作ったジャケット枠 (design 名 "Thumb")。
        /// 呼び出し側がこの子の Image へ実ジャケットを設定する
        /// (行の座標計算を画面側へ書き戻させないためのヘルパ)。
        /// </summary>
        public static GameObject Thumb(GameObject songRow)
        {
            Transform t = songRow != null ? songRow.transform.Find("Thumb") : null;
            return t != null ? t.gameObject : null;
        }

        /// <summary>
        /// 曲リスト行が作ったグレード枠 (design 名 "Grade")。
        /// グレード文字は SongRow の grade 文字列で渡すのが基本 (枠単体への差し替え用)。
        /// </summary>
        public static GameObject Grade(GameObject songRow)
        {
            Transform t = songRow != null ? songRow.transform.Find("Grade") : null;
            return t != null ? t.gameObject : null;
        }

        /// <summary>
        /// ランキング行 (design Hbxqf 496×56 / T8C1Hu 496×52、高さは size):
        /// Pos (12,中央) 36×36 r18 (fs22、選択行 $text / 非選択 $text-muted) /
        /// Name (60,中央) 160×32 fs22 / Value (232,中央) 180×32 fs22 右寄せ /
        /// Grade (424,中央) 56×36 r4 (fs22 中央)。背景は非選択 $k1 / 選択 (YOU) $k6。
        /// </summary>
        public static GameObject RankRow(GameObject parent, string pos, string name, string value, string grade,
            Vector2 designTopLeft, Vector2 size, bool selected = false)
        {
            var go = V6Controls.Panel(parent, "RankRow " + pos, designTopLeft, size,
                selected ? V6Tokens.Key6 : V6Tokens.Key1, V6Tokens.RadiusMedium);
            float h = size.y;

            var posGo = V6Controls.Panel(go, "Pos", new Vector2(RankPosX, (h - RankPosSize) * 0.5f),
                new Vector2(RankPosSize, RankPosSize), V6Tokens.Key4, RankPosSize * 0.5f);
            StretchText(posGo, "Pos", pos, RankFs, selected ? V6Tokens.Text : V6Tokens.TextMuted);

            V6Controls.Text(go, "Name", name, new Vector2(RankNameX, (h - RankTextH) * 0.5f),
                new Vector2(RankNameW, RankTextH), RankFs, V6Tokens.Text);

            V6Controls.Text(go, "Value", value, new Vector2(RankValueX, (h - RankTextH) * 0.5f),
                new Vector2(RankValueW, RankTextH), RankFs, V6Tokens.Text, TextAlignmentOptions.Right);

            var gradeGo = V6Controls.Panel(go, "Grade", new Vector2(RankGradeX, (h - RankGradeH) * 0.5f),
                new Vector2(RankGradeW, RankGradeH), V6Tokens.Key4, V6Tokens.RadiusSmall);
            StretchText(gradeGo, "Grade", grade, RankFs, V6Tokens.Text);
            return go;
        }

        // --- コンパクトなランキング行 (Select jnIY9 / Result Chlqd 496×44) ---
        /// <summary>Select の SCORE RANKING 列と Result の曲別ランキングの行 (design 496×44)。</summary>
        public const float CompactRankRowW = 496f;
        public const float CompactRankRowH = 44f;
        public const float CompactRankPosX = 12f;
        public const float CompactRankPosY = 4f;
        public const float CompactRankNameX = 60f;
        /// <summary>Name 幅 (design 実測 140。Ranking/History の RankRow は 160 で別ノード)。</summary>
        public const float CompactRankNameW = 140f;
        public const float CompactRankValueX = 212f;
        public const float CompactRankValueW = 180f;
        /// <summary>Name / Value の矩形 (design (x,6) 140/180×32)。</summary>
        public const float CompactRankTextY = 6f;
        public const float CompactRankTextH = 32f;
        public const float CompactRankGradeX = 404f;
        public const float CompactRankGradeY = 4f;
        public const float CompactRankGradeW = 56f;
        public const float CompactRankGradeH = 36f;

        /// <summary>
        /// コンパクトなランキング行 (design OENRv の jnIY9 = Select の SCORE RANKING 列 /
        /// zcSHh の Chlqd = Result の曲別ランキング。両者は同寸・同内部座標 496×44 r8):
        /// Pos (12,4) 36×36 r18 (fs22、選択行 $text / 非選択 $text-muted) /
        /// Name (60,6) 140×32 fs22 / Value (212,6) 180×32 fs22 右寄せ /
        /// Grade (404,4) 56×36 r4 (fs22 中央)。背景は非選択 $k1 / 選択 (YOU) $k6。
        /// Ranking/History の <see cref="RankRow"/> (496×56/52、Name w160) とは別 design なので
        /// 共有しないこと (旧実装は両方を 32px 右へずらした座標で写経していた)。
        /// </summary>
        public static GameObject CompactRankRow(GameObject parent, string pos, string name, string value, string grade,
            Vector2 designTopLeft, bool selected = false)
        {
            var go = V6Controls.Panel(parent, "Score " + pos, designTopLeft,
                new Vector2(CompactRankRowW, CompactRankRowH),
                selected ? V6Tokens.Key6 : V6Tokens.Key1, V6Tokens.RadiusMedium);

            var posGo = V6Controls.Panel(go, "Pos", new Vector2(CompactRankPosX, CompactRankPosY),
                new Vector2(RankPosSize, RankPosSize), V6Tokens.Key4, RankPosSize * 0.5f);
            StretchText(posGo, "PosText", pos, RankFs, selected ? V6Tokens.Text : V6Tokens.TextMuted);

            V6Controls.Text(go, "Name", name, new Vector2(CompactRankNameX, CompactRankTextY),
                new Vector2(CompactRankNameW, CompactRankTextH), RankFs, V6Tokens.Text);

            V6Controls.Text(go, "Value", value, new Vector2(CompactRankValueX, CompactRankTextY),
                new Vector2(CompactRankValueW, CompactRankTextH), RankFs, V6Tokens.Text,
                TextAlignmentOptions.Right);

            var gradeGo = V6Controls.Panel(go, "Grade", new Vector2(CompactRankGradeX, CompactRankGradeY),
                new Vector2(CompactRankGradeW, CompactRankGradeH), V6Tokens.Key4, V6Tokens.RadiusSmall);
            StretchText(gradeGo, "GradeText", string.IsNullOrEmpty(grade) ? "–" : grade, RankFs, V6Tokens.Text);
            return go;
        }

        /// <summary>
        /// プレイヤー行 (design tcgRX 496×56): RankRow の Pos/Name/Value (同座標) に
        /// Grade の代わりに Plays (424,中央) 56×29 fs20 $text-dim 右寄せを載せる。
        /// </summary>
        public static GameObject PlayerRow(GameObject parent, string pos, string name, string value, string plays,
            Vector2 designTopLeft, Vector2 size, bool selected = false)
        {
            var go = V6Controls.Panel(parent, "PlayerRow " + pos, designTopLeft, size,
                selected ? V6Tokens.Key6 : V6Tokens.Key1, V6Tokens.RadiusMedium);
            float h = size.y;

            var posGo = V6Controls.Panel(go, "Pos", new Vector2(RankPosX, (h - RankPosSize) * 0.5f),
                new Vector2(RankPosSize, RankPosSize), V6Tokens.Key4, RankPosSize * 0.5f);
            StretchText(posGo, "Pos", pos, RankFs, selected ? V6Tokens.Text : V6Tokens.TextMuted);

            V6Controls.Text(go, "Name", name, new Vector2(RankNameX, (h - RankTextH) * 0.5f),
                new Vector2(RankNameW, RankTextH), RankFs, V6Tokens.Text);

            V6Controls.Text(go, "Value", value, new Vector2(RankValueX, (h - RankTextH) * 0.5f),
                new Vector2(RankValueW, RankTextH), RankFs, V6Tokens.Text, TextAlignmentOptions.Right);

            V6Controls.Text(go, "Plays", plays, new Vector2(PlayerPlaysX, (h - PlayerPlaysH) * 0.5f),
                new Vector2(PlayerPlaysW, PlayerPlaysH), PlayerPlaysFs, V6Tokens.TextDim,
                TextAlignmentOptions.Right);
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
