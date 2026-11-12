using UnityEngine;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Game.Playfield;

namespace FlashBeat.UI.V6.Game.Hud
{
    /// <summary>
    /// Game HUD (Issue #220) の純ロジック。Unity API は Mathf のみなので
    /// EditMode テストから直接呼べる。
    /// スコア整形 / ランク / コンボボーナスは既存の
    /// <see cref="FlashBeat.UI.V5.GameHudLogic"/> を再利用する (二重実装しない)。
    /// </summary>
    public static class V6HudLogic
    {
        /// <summary>ランク/判定クローンの接尾辞。</summary>
        private const string CloneSuffix = "(Clone)";

        /// <summary>
        /// 進捗バーの塗り比率 (0..1)。duration 無効 (0 以下) は 0。
        /// mediaTime はメディア (動画) タイムライン上の秒 (GManager.step_time)。
        /// </summary>
        public static float ProgressFraction(float mediaTime, float duration)
        {
            if (duration <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(mediaTime / duration);
        }

        /// <summary>進捗バーの塗り幅 (px)。</summary>
        public static float ProgressFillWidth(float mediaTime, float duration, float barWidth)
        {
            return ProgressFraction(mediaTime, duration) * barWidth;
        }

        /// <summary>
        /// スコアゲージの塗り幅 (px)。塗りは + セグメントの手前 (fillMax) まで。
        /// </summary>
        public static float GaugeFillWidth(int score, float fillMax)
        {
            return Mathf.Clamp01(score / 1_000_000f) * fillMax;
        }

        /// <summary>スコアゲージの Best マーカー x (px)。
        /// 点数空間は 0..1,000,000 (既存 gauge.fillAmount = score/1000000f と同じ)。
        /// design の Best x=553 は hiscore 1,043,396 をクランプせず 530 で割った
        /// 範囲外サンプル。ゲームの点数は空間外に出ないので、マーカーが
        /// トラック外に出ないよう 1,000,000 でクランプする (トラック端に留まる)。</summary>
        public static float GaugeBestX(int hiscore, float fillMax)
        {
            return Mathf.Clamp01(hiscore / 1_000_000f) * fillMax;
        }

        /// <summary>
        /// 歌詞スロット (design i30vM4) の寸法。ゲームフレーム 1728×972 の
        /// (304,250) 1120×220 を、プレイフィールドと同じ高さ合わせ写像で実画面へ置く
        /// (契約 §8「歌詞スロットの位置」)。
        /// </summary>
        public const float LyricDesignY = 250f;
        public const float LyricDesignWidth = 1120f;
        public const float LyricDesignHeight = 220f;

        /// <summary>
        /// 歌詞スロット上端の y (画面左上原点)。
        /// </summary>
        public static float LyricTopY(float screenHeight)
        {
            return LyricDesignY * V6LaneGeometry.ScreenScale(new Vector2(0f, screenHeight));
        }

        /// <summary>
        /// 歌詞スロットの幅・高さ。
        ///
        /// 幅は design の 1120 を実画面へ写す。**高さは design の 220 ではなく、
        /// 実際の行構成の高さから求める** (2026-11-12 オーナー指摘)。
        ///
        /// design の 220 は 3 行 (振り仮名 22 / 歌詞 44 / ローマ字 20) + Start Prompt
        /// を想定した値だが、実装のフォントは画面倍率に追従しない固定 px なので、
        /// Mac (倍率 2.21) では文字が箱の 1 割しか埋めず、大きな空の板に見えていた。
        /// 行の高さから箱を決めれば、文字に対して箱が大きくなりすぎない。
        /// </summary>
        /// <param name="screenHeight">画面の高さ (幅の写像に使う)。</param>
        /// <param name="furiganaFs">実際に使う振り仮名のフォントサイズ (px)。</param>
        /// <param name="lyricFs">実際に使う歌詞のフォントサイズ (px)。</param>
        /// <param name="romajiFs">実際に使うローマ字のフォントサイズ (px)。</param>
        public static Vector2 LyricSize(float screenHeight, float furiganaFs, float lyricFs, float romajiFs)
        {
            float scale = V6LaneGeometry.ScreenScale(new Vector2(0f, screenHeight));
            float width = LyricDesignWidth * scale;
            float height = LyricContentHeight(furiganaFs, lyricFs, romajiFs);
            return new Vector2(width, height);
        }

        /// <summary>歌詞スロットの上下 padding (design 16)。</summary>
        public const float LyricPaddingY = 16f;

        /// <summary>歌詞スロットの行間 (design 6)。</summary>
        public const float LyricRowGap = 6f;

        /// <summary>
        /// 3 行 (振り仮名 / 歌詞 / ローマ字) の高さ合計に padding と gap を足した、
        /// 箱の最小の高さ。行のフォントサイズは呼び出し側 (HUD ビュー) の値を使う。
        /// </summary>
        public static float LyricContentHeight(float furiganaFs, float lyricFs, float romajiFs)
        {
            float furiganaLine = furiganaFs * 1.4f;
            float lyricLine = lyricFs * 1.2f;
            float romajiLine = romajiFs * 1.4f;
            float rows = furiganaLine + lyricLine + romajiLine;
            return rows + LyricRowGap * 2f + LyricPaddingY * 2f;
        }

        /// <summary>スコア値の 1 行固定の下限フォントサイズ。design は 38 (FHD)。</summary>
        public const float ScoreAutoSizeMin = 28f;

        /// <summary>
        /// スコア加算時のポップ倍率。0〜80ms で 1→1.06、80〜200ms で 1.06→1。
        /// 表示だけを変え、レイアウトは動かさない (契約 §8)。
        /// </summary>
        public static float ScorePulseScale(float elapsed)
        {
            if (elapsed < 0f)
            {
                return 1f;
            }

            if (elapsed < 0.08f)
            {
                return Mathf.Lerp(1f, 1.06f, elapsed / 0.08f);
            }

            if (elapsed < 0.2f)
            {
                return Mathf.Lerp(1.06f, 1f, (elapsed - 0.08f) / 0.12f);
            }

            return 1f;
        }

        public const float ScorePulseSeconds = 0.2f;

        // --- 判定の出現アニメーション (2026-11-12 オーナー指摘) ---

        /// <summary>判定が判定線から立ち上がる距離 (design px、上方向が正)。</summary>
        public const float VerdictRiseDistance = 28f;

        /// <summary>判定が立ち上がりきるまでの時間 (秒)。</summary>
        public const float VerdictRiseSeconds = 0.14f;

        /// <summary>判定が消えるまでの時間 (秒)。</summary>
        public const float VerdictFadeSeconds = 0.22f;

        /// <summary>
        /// 判定テキストの出現位置 (design px、上方向が正の戻り値)。
        ///
        /// 判定線のすぐ上に出て、短い時間で所定の位置まで立ち上がる。
        /// 「正しく叩けた」手応えを判定線の近くで返すため、出現は判定線に近い
        /// 位置から始める (0 = 所定位置、正 = その分だけ下)。
        /// </summary>
        public static float VerdictRiseOffset(float elapsed)
        {
            if (elapsed <= 0f)
            {
                return VerdictRiseDistance;
            }

            if (elapsed >= VerdictRiseSeconds)
            {
                return 0f;
            }

            float t = elapsed / VerdictRiseSeconds;
            // EaseOut: 素早く立ち上がり、ゆっくり止まる。
            float eased = 1f - (1f - t) * (1f - t);
            return Mathf.Lerp(VerdictRiseDistance, 0f, eased);
        }

        /// <summary>
        /// 判定テキストの不透明度。出るときは素早く出て、寿命の最後に短く消える。
        /// <paramref name="remaining"/> は表示が終わるまでの残り秒。
        /// </summary>
        public static float VerdictAlpha(float remaining)
        {
            if (remaining <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(remaining / VerdictFadeSeconds);
        }

        /// <summary>再開カウントダウンの数字サイズ (design 1728×972 基準)。</summary>
        public const float CountdownDesignFontSize = 132f;

        /// <summary>カウントダウン数字の実ピクセルサイズ (高さ合わせ写像)。</summary>
        public static float CountdownFontSize(float screenHeight)
        {
            return CountdownDesignFontSize * V6LaneGeometry.ScreenScale(new Vector2(0f, screenHeight));
        }

        /// <summary>
        /// 開始待ち (startTiming) の「START」ラベルの design フォントサイズ。
        ///
        /// カウントダウンの数字 (132px 中央) と同じ枠を使うが、数字ほど大きくすると
        /// 画面の主役に見える。歌詞スロットに収まる行の大きさに合わせ、
        /// 同じ枠の中で「状態の見出し」として読めるサイズにする
        /// (2026-11-12 オーナー指摘: 体裁を保つ)。
        /// </summary>
        public const float StartHoldDesignFontSize = 64f;

        public static float StartHoldFontSize(float screenHeight)
        {
            return StartHoldDesignFontSize * V6LaneGeometry.ScreenScale(new Vector2(0f, screenHeight));
        }

        /// <summary>曲終了「終了」の design フォントサイズ (1728×972 基準)。</summary>
        public const float SongEndTitleDesignFontSize = 64f;
        public const float SongEndSubDesignFontSize = 22f;

        public static float SongEndTitleFontSize(float scale)
        {
            return SongEndTitleDesignFontSize * scale;
        }

        public static float SongEndSubFontSize(float scale)
        {
            return SongEndSubDesignFontSize * scale;
        }

        /// <summary>コンボステータス (NO MISS / FULL COMBO) を出すか。
        /// 1 判定も無いうちは出さない (v5 GameHudView と同じ挙動)。
        /// </summary>
        public static bool ShowComboStatus(int miss, int totalJudged)
        {
            return miss == 0 && totalJudged > 0;
        }

        /// <summary>
        /// 契約 §8 / §12 の強調下地色 <c>#3A3220</c>。
        /// スコアゲージの「+」セグメントと FULL COMBO チップの背景。
        /// v5 トークンにはこの値が無い (Key0 #131313 / WarnTint #4A4029 は近いが別値) ので
        /// v6 側で契約値を保持する。
        /// </summary>
        public static readonly Color AccentBase = new Color32(0x3A, 0x32, 0x20, 0xFF);

        // --- 開始待ちの案内 (design HcP8N の Start Prompt W1ppF) ---
        // 「Space to Start」の一文ではなく、KeyCap「Space」(36 高 / padding [0,10] /
        // fs22 / #3A3A3A r4) + ラベル「to start」(fs22 $text-muted) の行 (gap 10)。
        public const float ReadyPromptCapHeight = 36f;
        public const float ReadyPromptFontSize = 22f;
        public const float ReadyPromptGap = 10f;

        /// <summary>コンボトラックの塗り比率 (0..1)。milestone 無効は 0。</summary>
        public static float ComboFillFraction(int combo, int milestone)
        {
            if (milestone <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01(combo / (float)milestone);
        }

        /// <summary>
        /// 判定エフェクトのクローン名 ("Perfect(Clone)" 等) を判定ラベルへ。
        /// Judge (所有者外) がレーン上に Instantiate する MessageObj の名前を観測する。
        /// </summary>
        public static string VerdictLabel(string cloneName)
        {
            if (string.IsNullOrEmpty(cloneName))
            {
                return string.Empty;
            }

            if (cloneName.EndsWith(CloneSuffix, System.StringComparison.Ordinal))
            {
                cloneName = cloneName.Substring(0, cloneName.Length - CloneSuffix.Length);
            }

            return cloneName.ToUpperInvariant();
        }

        /// <summary>判定ラベルの表示色 (v5 GameHudView.JudgeColor と同じ割り当て)。</summary>
        public static Color VerdictColor(string label)
        {
            switch (label)
            {
                case "PERFECT": return V5Tokens.AccentLight;
                case "GREAT": return V5Tokens.Text;
                case "BAD": return V5Tokens.WarnLight;
                default: return V5Tokens.DangerLight;
            }
        }

        /// <summary>
        /// レーンの代表キー文字列。ホーム行の正規キー (A S D F J K L ;) を優先し、
        /// 無ければ最初の文字キー、それも無ければ割り当ての先頭キー。
        /// v5 GameHudView.RepresentativeKey と同じ規約 (v6 デザインのキーガイドは
        /// レーン 1 列 = 代表キー 1 個)。
        /// </summary>
        public static string RepresentativeKeyText(KeyCode[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return string.Empty;
            }

            KeyCode[] canonical =
            {
                KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F,
                KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.Semicolon,
            };
            for (int i = 0; i < canonical.Length; i++)
            {
                if (System.Array.IndexOf(keys, canonical[i]) >= 0)
                {
                    return KeyGuideUI.GetKeyDisplayName(canonical[i]);
                }
            }

            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] >= KeyCode.A && keys[i] <= KeyCode.Z)
                {
                    return keys[i].ToString();
                }
            }

            if (System.Array.IndexOf(keys, KeyCode.Semicolon) >= 0)
            {
                return ";";
            }

            return KeyGuideUI.GetKeyDisplayName(keys[0]);
        }

        /// <summary>
        /// スクリーン x (左原点 px) を HUD フレーム内のテンプレート x (左原点) へ写す。
        /// フレームは frameScale 倍の等比拡大で画面全体を覆う
        /// (V6HudMetrics.LogicalViewportSize) ので、テンプレート x = screen x / frameScale。
        /// frameScale 0 以下 (未初期化) は等倍として扱う。
        /// </summary>
        public static float ScreenXToTemplateX(float screenX, float frameScale)
        {
            if (frameScale <= 0f)
            {
                return screenX;
            }

            return screenX / frameScale;
        }

        /// <summary>
        /// 判定クローンのワールド x からレーン番号 (0..7)。
        /// Judge.EffectPosition は x = lane - 3.5 に置く (Judge.cs 実装)。
        /// 範囲外は 0 / 7 に丸める。
        /// </summary>
        public static int LaneFromEffectPositionX(float effectX)
        {
            int lane = Mathf.RoundToInt(effectX - Judge.LaneFirstCenterX);
            return Mathf.Clamp(lane, 0, 7);
        }
    }
}
