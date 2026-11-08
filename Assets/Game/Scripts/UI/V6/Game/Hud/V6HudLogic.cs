using UnityEngine;
using FlashBeat.UI.V5;

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
        /// コンボステータス (NO MISS / FULL COMBO) を出すか。
        /// 1 判定も無いうちは出さない (v5 GameHudView と同じ挙動)。
        /// </summary>
        public static bool ShowComboStatus(int miss, int totalJudged)
        {
            return miss == 0 && totalJudged > 0;
        }

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
        /// 判定クローンのワールド x からレーン番号 (0..7)。
        /// Judge.EffectPosition は x = lane - 3.5 に置く (Judge.cs 実装)。
        /// 範囲外は 0 / 7 に丸める。
        /// </summary>
        public static int LaneFromEffectPositionX(float effectX)
        {
            int lane = Mathf.RoundToInt(effectX + 3.5f);
            return Mathf.Clamp(lane, 0, 7);
        }
    }
}
