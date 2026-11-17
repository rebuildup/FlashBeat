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
        /// 幅が画面幅にクランプされた場合 (<see cref="LyricSlotWidth"/>) は
        /// 中央寄せのまま左右に等分のインセットを足した実際の左端を返す
        /// (監査・レイアウト契約が描画結果と一致するための値)。
        /// </summary>
        public static Vector2 LyricSlotOrigin(float screenWidth, float screenHeight)
        {
            Vector2 frameOrigin = FlashBeat.UI.V6.Common.V6UiProfile.OriginFor(
                Mathf.RoundToInt(screenWidth), Mathf.RoundToInt(screenHeight));
            float inset = (LyricDesignWidth - LyricSlotWidth(screenWidth)) * 0.5f;
            return new Vector2(frameOrigin.x + LyricSlotDesignX + inset, frameOrigin.y + LyricDesignY);
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

        /// <summary>
        /// 歌詞スロットの実幅 (#96/#523)。design 幅 1120 が画面幅を超える
        /// 小画面 (1024×768 等) では画面幅に収める。スロットは水平中央
        /// アンカーなので、幅を詰めても見た目の中心は変わらない。
        /// </summary>
        public static float LyricSlotWidth(float screenWidth)
        {
            return Mathf.Min(LyricDesignWidth, Mathf.Max(0f, screenWidth));
        }

        /// <summary>
        /// 歌詞スロットの上下 padding。Issue #582: design の 16 では行が窮屈だった
        /// ため 24 へ拡大 (左右は <see cref="LyricPaddingX"/>)。
        /// </summary>
        public const float LyricPaddingY = 24f;

        /// <summary>歌詞スロットの左右 padding (design 24 → Issue #582 で 32)。</summary>
        public const float LyricPaddingX = 32f;

        /// <summary>
        /// 歌詞行の行高係数 (fontSize 倍)。Issue #582: 従来の暗黙の 1.2 / 1.25 では
        /// 文字が行に対して窮屈だったため 1.5 へ拡大する。NotoSansJP の行送りは
        /// fs × 約 1.448 なので、行高がこれを下回ると TMP は行を描画しない。
        /// </summary>
        public const float LyricLineHeightFactor = 1.5f;

        /// <summary>
        /// 歌詞本文 1 行と Start Prompt の高い方に padding を足した箱の高さ
        /// (Issue #433: ルビ・ローマ字行は廃止し本文だけを出す。
        /// Issue #582: 行高係数と padding を拡大し、読みやすい縦幅にした)。
        /// </summary>
        public static float LyricContentHeight(float lyricFs)
        {
            float lyricLine = lyricFs * LyricLineHeightFactor;
            float content = Mathf.Max(lyricLine, ReadyPromptCapHeight);
            return content + LyricPaddingY * 2f;
        }

        // --- 判定ポップアップ (Issue #605: 判定線からの立ち上がり + 滞在表示) ---

        /// <summary>
        /// 判定テキストの静止位置 = 判定線のこの距離だけ上 (px)。
        /// §10.3 の「所定位置」。立ち上がりの終点であり、滞在中の表示位置。
        /// </summary>
        public const float VerdictRestGapAboveLine = 72f;

        /// <summary>
        /// 判定が現れてから静止位置へ立ち上がるまでの時間 (秒)。
        /// design §10.3: 140ms。
        /// </summary>
        public const float VerdictRiseSeconds = 0.14f;

        /// <summary>
        /// 立ち上がりの移動量 (px)。静止位置よりこの距離だけ判定線側 (下) に
        /// 出て、<see cref="VerdictRiseSeconds"/> かけて静止位置へ上がる。
        /// design §10.3: 28px。静止位置は判定線の 72px 上なので、出現点は
        /// 判定線の約 44px 上 = 「判定線のすぐ上」に相当する。
        /// </summary>
        public const float VerdictRiseDistance = 28f;

        /// <summary>判定が不透明のまま留まる時間 (秒)。読み取りの主時間。</summary>
        public const float VerdictHoldSeconds = 0.58f;

        /// <summary>
        /// 判定が消えるまでのフェードアウト時間 (秒)。
        /// design §10.3: 寿命の最後の 220ms。
        /// </summary>
        public const float VerdictFadeOutSeconds = 0.22f;

        /// <summary>
        /// 判定テキストが見える全体時間 (秒) =
        /// 静止保持 (<see cref="VerdictHoldSeconds"/>) + フェードアウト
        /// (<see cref="VerdictFadeOutSeconds"/>) = 0.80 秒。
        /// Issue #581 の滞在時間 (0.5〜0.8 秒) を維持しつつ、
        /// §10.3 のフェードアウト 220ms に合わせて配分した。
        /// 立ち上がり (<see cref="VerdictRiseSeconds"/>) は保持時間の
        /// 冒頭に重なり、総寿命には別途加算しない。
        /// </summary>
        public const float VerdictTotalSeconds =
            VerdictHoldSeconds + VerdictFadeOutSeconds;

        /// <summary>
        /// 判定テキストの不透明度。出現は即時 (§10.3: フェードインしない。
        /// Issue #605 の設計判断。立ち上がりの動きが出現の合図を担うため、
        /// #581 のフェードインは廃止した)。寿命の最後は
        /// <see cref="VerdictFadeOutSeconds"/> で 1→0。
        /// </summary>
        /// <param name="remaining">表示が終わるまでの残り秒。</param>
        public static float VerdictAlpha(float remaining)
        {
            if (remaining <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(remaining / VerdictFadeOutSeconds);
        }

        /// <summary>
        /// 静止位置より判定線側 (下) へのオフセット (px、正 = 下)。
        /// design §10.3: 出現時は <see cref="VerdictRiseDistance"/> (28px) だけ
        /// 下にあり、<see cref="VerdictRiseSeconds"/> で EaseOut に従って
        /// 静止位置 (0) へ立ち上がる。立ち上がり完了後は 0。
        /// </summary>
        /// <param name="elapsed">表示開始からの秒。</param>
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

            return VerdictRiseDistance * (1f - EaseOutCubic(elapsed / VerdictRiseSeconds));
        }

        // --- ポップ・パンチ系の共通イージング (Issue #434 / #445) ---

        /// <summary>
        /// 桁別ポップ (Issue #445): スコア / コンボの値を 1 文字 = 1 スロットに
        /// 分け、<b>文字が変わった桁だけ</b>に掛けるポップの最大倍率・立ち上がり・
        /// 全体時間。全体をまとめて拡縮・発光する旧方式 (ScorePop*/ComboPop*) は
        /// 「安っぽい」との指摘で廃止した。
        /// </summary>
        public const float DigitPopPeak = 1.15f;
        public const float DigitPopAttack = 0.05f;
        public const float DigitPopTotal = 0.20f;

        /// <summary>コンボボーナスの塗り色が戻るまでの時間 (秒)。</summary>
        public const float ComboBonusPulseSeconds = 0.3f;

        /// <summary>節目ボーナス獲得表示が消えるまでの時間 (秒)。</summary>
        public const float MilestoneBonusSeconds = 1.2f;

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

        /// <summary>
        /// 次の節目ボーナス (scoring v3, ADR-0029) までの進捗 0..1。
        /// 節目に達した瞬間は 0 に戻り、次の節目へ向けて再び満ちる。
        /// </summary>
        public static float ComboFillFraction(int combo)
        {
            int interval = FlashBeat.Gameplay.Scoring.MilestoneInterval;
            return (combo % interval) / (float)interval;
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

        // --- 桁別アニメーション (Issue #445) ---

        /// <summary>
        /// 前回と今回の表示文字列を先頭 (左) から比較し、表示が変わった桁の
        /// フラグを返す。戻り値の長さは両者の長い方。
        ///
        /// - 文字が変わり、かつ新しい文字が可変文字 (数字など) のときだけ true。
        /// - ',' などの区切り文字は静的なので、現れても消えてもアニメーション
        ///   しない (新しい文字が ',' のスロットは false)。
        /// - 文字が消えただけのスロット (新しい文字が無い) も false
        ///   (スロットごと隠すので動かす必要がない)。
        /// - 長さが変わるときは左詰めで比較する。桁数が増減する場面
        ///   (例: コンボ 99→100) では同じ位置の文字が別の位を指すが、
        ///   「その位置の字形が変わった」ことだけをアニメーションする。
        /// </summary>
        public static bool[] ChangedCharFlags(string previous, string current)
        {
            int prevLen = previous != null ? previous.Length : 0;
            int currLen = current != null ? current.Length : 0;
            int len = Mathf.Max(prevLen, currLen);
            var flags = new bool[len];
            for (int i = 0; i < len; i++)
            {
                char prev = i < prevLen ? previous[i] : '\0';
                char curr = i < currLen ? current[i] : '\0';
                flags[i] = prev != curr && curr != '\0' && curr != ',';
            }

            return flags;
        }

        /// <summary>
        /// コンボ値の表示文字列。0 はコンボではないので空文字を返す
        /// (値の表示自体を隠す。Issue #445 オーナー指摘「コンボは 0 の表示に
        /// してはいけない」)。1 以上はそのままの数字列。
        /// </summary>
        public static string ComboValueText(int combo)
        {
            return combo > 0 ? combo.ToString() : string.Empty;
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
