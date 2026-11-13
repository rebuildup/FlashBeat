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
        /// 歌詞スロット (design i30vM4 / HcP8N) の寸法と位置 (Issue #433)。
        ///
        /// 共有 UI フレーム (1728×972 を 1:1 で画面中央へ置く) のメイン領域
        /// 6 列グリッド (1680 幅・列 280) の中央 4 列 = フレーム内 (304,250)
        /// 1120×220 と一致する。かつてはプレイフィールドと同じ高さ合わせ
        /// (画面高さ / 972) で幅を拡大していたため、2K / Mac で design より
        /// 大きな板になっていた。フレームと同じ 1:1 写像へ改める。
        /// </summary>
        public const float LyricSlotDesignX = 304f;
        public const float LyricDesignY = 250f;
        public const float LyricDesignWidth = 1120f;
        public const float LyricDesignHeight = 220f;

        /// <summary>
        /// 歌詞スロットの画面上の左上座標 (px、画面左上原点)。
        /// 共有フレームの origin ((w-1728)/2, (h-972)/2 切り捨て) + (304,250)。
        /// </summary>
        public static Vector2 LyricSlotOrigin(float screenWidth, float screenHeight)
        {
            Vector2 frameOrigin = FlashBeat.UI.V6.Common.V6UiProfile.OriginFor(
                Mathf.RoundToInt(screenWidth), Mathf.RoundToInt(screenHeight));
            return new Vector2(frameOrigin.x + LyricSlotDesignX, frameOrigin.y + LyricDesignY);
        }

        /// <summary>
        /// 歌詞スロットの幅・高さ。幅はフレームの 1:1 px (1120 固定)。
        /// 高さは文字の行構成から求める (design の 220 は 3 行構成の値で、
        /// 本文 1 行だけになった今は空白が大きすぎる)。
        /// </summary>
        public static Vector2 LyricSize(float lyricFs)
        {
            return new Vector2(LyricDesignWidth, LyricContentHeight(lyricFs));
        }

        /// <summary>歌詞スロットの上下 padding (design 16)。</summary>
        public const float LyricPaddingY = 16f;

        /// <summary>
        /// 歌詞本文 1 行と Start Prompt の高い方に padding を足した箱の高さ
        /// (Issue #433: ルビ・ローマ字行は廃止し本文だけを出す)。
        /// </summary>
        public static float LyricContentHeight(float lyricFs)
        {
            float lyricLine = lyricFs * 1.2f;
            float content = Mathf.Max(lyricLine, ReadyPromptCapHeight);
            return content + LyricPaddingY * 2f;
        }

        /// <summary>スコア値の 1 行固定の下限フォントサイズ。design は 38 (FHD)。</summary>
        public const float ScoreAutoSizeMin = 28f;

        // --- 判定ポップアップ (Issue #432) ---

        /// <summary>判定が判定線から弾けて静止位置へ戻るまでの時間 (秒)。</summary>
        public const float VerdictPopupSeconds = 0.26f;

        /// <summary>判定テキストの静止位置 = 判定線のこの距離だけ上 (px)。</summary>
        public const float VerdictRestGapAboveLine = 72f;

        /// <summary>判定が消えるまでの時間 (秒)。残り時間に比例してフェードする。</summary>
        public const float VerdictFadeSeconds = 0.22f;

        /// <summary>判定テキストの不透明度。出るときは素早く出て、寿命の最後に短く消える。
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

        /// <summary>
        /// 判定テキストの縦オフセット (px、正 = 静止位置より下)。
        ///
        /// Issue #432: 出現は判定線の位置 (下) から始まり、静止位置を行き過ぎる
        /// ほど上へ弾けてから静止位置へ戻る。負の値は静止位置より上
        /// (オーバーシュート)。「下から上へ跳ねて出る」表現にするため、
        /// リニアなフェードではなく 1 回のバウンドになる曲線を使う。
        /// </summary>
        /// <param name="elapsed">出現からの秒。</param>
        /// <param name="startDrop">静止位置から判定線までの距離 (px)。</param>
        public static float VerdictPopupOffset(float elapsed, float startDrop)
        {
            if (elapsed <= 0f)
            {
                return startDrop;
            }

            if (elapsed >= VerdictPopupSeconds)
            {
                return 0f;
            }

            float t = elapsed / VerdictPopupSeconds;
            // (1-t)·cos(πt): t=0 で startDrop (判定線)、途中で 0 (静止位置) を通過し、
            // いったん負 (上 = オーバーシュート) になって t=1 で 0 に戻る。
            return startDrop * (1f - t) * Mathf.Cos(Mathf.PI * t);
        }

        // --- ポップ・パンチ系の共通イージング (Issue #434) ---

        /// <summary>スコア加算ポップの最大倍率・立ち上がり・全体時間。</summary>
        public const float ScorePopPeak = 1.08f;
        public const float ScorePopAttack = 0.05f;
        public const float ScorePopTotal = 0.22f;

        /// <summary>コンボ加算ポップ。スコアより attack を短くして「遅れて見える」を解消。</summary>
        public const float ComboPopPeak = 1.12f;
        public const float ComboPopAttack = 0.04f;
        public const float ComboPopTotal = 0.16f;

        /// <summary>判定テキストのポップ (弾ける出現と併用)。</summary>
        public const float VerdictPopPeak = 1.18f;
        public const float VerdictPopAttack = 0.05f;
        public const float VerdictPopTotal = 0.20f;

        /// <summary>コンボボーナスの塗り色が戻るまでの時間 (秒)。</summary>
        public const float ComboBonusPulseSeconds = 0.3f;

        /// <summary>EaseOutCubic: 速く立ち上がって滑らかに止まる。</summary>
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        /// <summary>EaseOutBack: 行き過ぎてから戻る (弾ける感じ)。戻り値は 1 を超え得る。</summary>
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>
        /// ポップ倍率。attack 時間で peak へ弾けさせ (EaseOutBack で行き過ぎ)、
        /// 残りで滑らかに 1 へ戻す。経過が total を超えたら 1。
        /// </summary>
        public static float PunchScale(float elapsed, float peak, float attack, float total)
        {
            if (elapsed < 0f || elapsed >= total || attack <= 0f)
            {
                return 1f;
            }

            if (elapsed < attack)
            {
                return Mathf.Lerp(1f, peak, EaseOutBack(elapsed / attack));
            }

            float u = (elapsed - attack) / (total - attack);
            // smoothstep でピークから静止へ減衰。
            float s = u * u * (3f - 2f * u);
            return Mathf.Lerp(peak, 1f, s);
        }

        /// <summary>
        /// 目標値へ指数的に近づける (フレームレート非依存の tween)。
        /// speed が大きいほど速く追従する。dt は秒。
        /// </summary>
        public static float Approach(float current, float target, float dt, float speed)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
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

        public static float SongEndTitleFontSize(float scale)
        {
            return SongEndTitleDesignFontSize * scale;
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
