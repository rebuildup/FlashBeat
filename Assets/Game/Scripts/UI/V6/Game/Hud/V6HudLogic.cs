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
        /// スコアゲージの塗り幅 (px)。塗りは + セグメントを含むトラック全長
        /// (<paramref name="fillMax"/>) まで。点数空間は最新の scoringVersion の
        /// 満点 (<see cref="FlashBeat.UI.V6.Common.V6GradeLogic.ScoreMaxOf"/>、
        /// scoring v4 では 2,000,000 = 基礎 + ボーナス) で、v4 の合計スコアが
        /// そのまま 0..満点 の 1 本のレールに写る (Issue #758 / ADR-0034)。
        /// </summary>
        public static float GaugeFillWidth(int score, float fillMax)
        {
            return Mathf.Clamp01(score / (float)FlashBeat.UI.V6.Common.V6GradeLogic
                .ScoreMaxOf(FlashBeat.Gameplay.Scoring.LatestVersion)) * fillMax;
        }

        /// <summary>スコアゲージの Best マーカー x (px)。
        /// 点数空間は最新 scoringVersion の満点 (scoring v4 では 0..2,000,000)。
        /// マーカーがトラック外に出ないよう満点でクランプする。</summary>
        public static float GaugeBestX(int hiscore, float fillMax)
        {
            return Mathf.Clamp01(hiscore / (float)FlashBeat.UI.V6.Common.V6GradeLogic
                .ScoreMaxOf(FlashBeat.Gameplay.Scoring.LatestVersion)) * fillMax;
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

        // --- 歌詞スロットの共有寸法 (Issue #674) ---
        // 歌詞本文・Start Prompt・カウントダウン数字・終了タイトルが同じ
        // スロットを使う。状態ごとに高さを変えるとパネルが伸縮して見えるため、
        // 全状態がここの 1 つの高さを共有する。

        /// <summary>カウントダウン数字 1 行が必要とする高さ係数 (fontSize 倍)。</summary>
        public const float CountdownLineFit = 1.3f;

        /// <summary>終了タイトル 1 行が必要とする高さ係数 (fontSize 倍)。</summary>
        public const float SongEndTitleLineFit = 1.6f;

        /// <summary>
        /// 歌詞スロットの共有高さ (px)。歌詞本文 + Start Prompt
        /// (<see cref="LyricContentHeight"/>)・カウントダウン数字
        /// (<see cref="CountdownFontSize"/>)・終了タイトル
        /// (<see cref="SongEndTitleFontSize"/>) の最大必要高を取る。
        /// Issue #674: 歌詞 / 準備 / カウントダウン / 曲終了の全状態で
        /// パネルの RectTransform サイズ・位置を不変にするための正本。
        /// </summary>
        /// <param name="lyricFs">歌詞本文の実フォントサイズ (px)。</param>
        /// <param name="screenHeight">HUD キャンバスの高さ (px)。</param>
        public static float LyricSlotHeight(float lyricFs, float screenHeight)
        {
            float lyric = LyricContentHeight(lyricFs);
            float countdown = CountdownFontSize(screenHeight) * CountdownLineFit;
            float endTitle = SongEndTitleFontSize(
                screenHeight / V6LaneGeometry.DesignHeight) * SongEndTitleLineFit;
            return Mathf.Max(lyric, Mathf.Max(countdown, endTitle));
        }

        /// <summary>
        /// 歌詞スロットの共有寸法 (幅・高さ)。全状態で同一の (width, height) を返す。
        /// 幅は <see cref="LyricSlotWidth"/>、高さは <see cref="LyricSlotHeight"/>。
        /// </summary>
        public static Vector2 LyricSlotSize(float lyricFs, float screenWidth, float screenHeight)
        {
            return new Vector2(LyricSlotWidth(screenWidth), LyricSlotHeight(lyricFs, screenHeight));
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

        // --- 判定ポップアップ (Issue #731: 判定線起点の上昇 → 保持 → 下方退場) ---

        /// <summary>
        /// 判定テキストが読み取りのために留まる頂点の高さ = 判定線のこの距離だけ上。
        /// 判定確定 (t=0) に判定線の位置へ出現し、<see cref="VerdictRiseSeconds"/>
        /// かけてこの高さまで上昇する。
        ///
        /// Issue #793 (v0.2.3 実機受入): 旧固定値 72px は判定ボックス
        /// (<see cref="V6HudMetrics.VerdictH"/>、FHD で 44px) の約 1.6 個分しかなく、
        /// 判定線・キーガイドに近すぎて読み取りにくいと差し戻された。
        /// 頂点距離は解像度別メトリクスの判定ボックス高さに比例させ、
        /// <see cref="VerdictPeakRisePx"/> が正本 (FHD で約 4 個分)。
        /// </summary>
        public const float VerdictPeakRiseRatio = 4f;

        /// <summary>
        /// 頂点の高さ (判定線からの px、正 = 上)。解像度別メトリクスの
        /// 判定ボックス高さ × <see cref="VerdictPeakRiseRatio"/>。
        /// </summary>
        public static float VerdictPeakRisePx(V6HudMetrics m)
        {
            return m.VerdictH * VerdictPeakRiseRatio;
        }

        /// <summary>
        /// 出現時のスケールポップの山 (1 より大きい = ふくらんでから戻る)。
        /// 判定イベントごとの独立インスタンスが重なっても、出現の瞬間に
        /// 「新しい判定」が視認できるようにする (Issue #793)。
        /// </summary>
        public const float VerdictPopPeakScale = 1.18f;

        /// <summary>スケールポップの立ち上がり時間 (秒)。</summary>
        public const float VerdictPopScaleAttack = 0.06f;

        /// <summary>スケールポップ全体の時間 (秒)。これ以降は等倍。</summary>
        public const float VerdictPopScaleTotal = 0.24f;

        /// <summary>
        /// レガシー 3D プレイフィールド経路 (V6UiMode.Playfield=false) の
        /// 判定アンカーとなるワールド座標。描画される判定線
        /// (GameScene "JudgeLine" LineRenderer = y 0.5 / z 0 の水平線、
        /// ノーツが判定時刻に到達する面) の中央。
        ///
        /// 判定エフェクトの出現点 <c>Judge.EffectPosition</c> (y 0.76 /
        /// z 0.15) ではない — あちらは 3D 判定エフェクトが浮かぶ位置で、
        /// カメラ投影が描画判定線より縦に約 50px (FHD) ずれる。
        /// design §10.3 の「判定線」は描画される線を指す
        /// (v6 プレイフィールド経路は design y=792 の描画線を
        /// アンカーに使っており、両経路で同じ意味に揃える。Issue #715)。
        /// Issue #731 のタイムラインの原点 (オフセット 0) はこの線の位置。
        /// </summary>
        public static Vector3 LegacyJudgeLineAnchor()
        {
            return new Vector3(0f, Judge.JudgeLineWorldY, Judge.JudgeLineWorldZ);
        }

        /// <summary>
        /// 判定線上の出現点から読み取りの頂点まで上昇する時間 (秒)。
        /// 「叩いた」位置 (判定線) から文字が立ち上がる動きを
        /// EaseOutCubic で素早く見せる。長すぎると判定の瞬間性が薄れる。
        /// v0.2.1: 頂点付近の減速区間を短く保つため 0.18 → 0.14。
        /// </summary>
        public const float VerdictRiseSeconds = 0.14f;

        /// <summary>
        /// 頂点から判定線を越えて下へ加速落下しながら消える時間 (秒)。
        /// 落下は EaseInCubic (加速) で、頂点での静止フェーズは持たない
        /// (v0.2.1 実機レビュー: 「一番上で完全に制止するべきではない」)。
        /// α は退場終盤 (<see cref="VerdictFadeStartU"/> 以降) で 1→0 に
        /// フェードするため、判定線付近に不透明のまま滞留しない。
        /// </summary>
        public const float VerdictFallSeconds = 0.42f;

        /// <summary>
        /// 退場フェードの開始位置 (落下進行 u = 0..1 のうち 0.6 から)。
        /// EaseIn の加速で判定線を割るのは終盤なので、線より下だけで
        /// フェードすると消えるまでの時間が短すぎて「残像」に見える。
        /// 落下の終盤 40% をフェード区間にして、落ちる様子が見えてから
        /// 自然に消える (v0.2.1 実機レビュー: 「落ちたとわかるくらいで
        /// 消えるべき」「判定線まで残す必要はない」)。
        /// </summary>
        public const float VerdictFadeStartU = 0.6f;

        /// <summary>
        /// 判定テキストが見える全体時間 (秒) =
        /// 上昇 (<see cref="VerdictRiseSeconds"/>) + 下方退場
        /// (<see cref="VerdictFallSeconds"/>) = 0.56 秒。
        /// 頂点での保持フェーズは無い (頂点は通過点)。
        /// </summary>
        public const float VerdictTotalSeconds =
            VerdictRiseSeconds + VerdictFallSeconds;

        /// <summary>
        /// 下方退場の終端 = 判定線のこの距離だけ下 (px、正の値)。
        /// 判定ボックス (<see cref="V6HudMetrics.VerdictH"/>) 1 個分 —
        /// 文字 1 行分の高さだけ判定線を割って落ちれば「下へ抜ける」動きとして
        /// 十分に読め、さらに先のキーガイド行には届かない。
        /// 解像度別メトリクス由来なので FHD/2K/Mac で適切にスケールする。
        /// </summary>
        public static float VerdictExitDropPx(V6HudMetrics m)
        {
            return m.VerdictH;
        }

        /// <summary>
        /// 判定ポップのある時刻の姿勢。<see cref="OffsetPx"/> は判定線からの
        /// 垂直オフセット (px、画面下原点で正 = 上。負は判定線より下)、
        /// <see cref="Alpha"/> は不透明度 (0..1)、<see cref="Scale"/> は
        /// 出現時のスケールポップ倍率 (Issue #793。1 が等倍)。
        /// </summary>
        public struct VerdictPose
        {
            public readonly float OffsetPx;
            public readonly float Alpha;
            public readonly float Scale;

            public VerdictPose(float offsetPx, float alpha, float scale)
            {
                OffsetPx = offsetPx;
                Alpha = alpha;
                Scale = scale;
            }
        }

        /// <summary>
        /// 判定ポップの純粋タイムライン (Issue #731)。本編 HUD
        /// (V6HudVerdict) と Option プレビュー (V6OptionViewPreviewOverlay) が
        /// 同じ関数・同じ定数で動かす。時刻の正本は判定確定時刻 t=0。
        ///
        /// - t=0: 判定線の位置に即時出現 (オフセット 0、α=1。フェードインなし)。
        /// - 0〜<see cref="VerdictRiseSeconds"/>: EaseOutCubic で頂点
        ///   (判定線 + <see cref="VerdictPeakRisePx"/>) まで上昇。
        /// - 〜+<see cref="VerdictFallSeconds"/>: EaseInCubic で加速しながら
        ///   頂点から判定線を越え、<see cref="VerdictExitDropPx"/> 分だけ下まで
        ///   落下。頂点での静止フェーズは無く、上昇の終端からそのまま落下へ移る。
        ///   α は退場終盤 (<see cref="VerdictFadeStartU"/> 以降の 40%) で
        ///   1→0 にフェードする (判定線付近に不透明のまま滞留しない)。
        /// - Scale: 出現直後 <see cref="VerdictPopScaleTotal"/> 秒だけ
        ///   <see cref="PunchScale"/> でふくらんで等倍へ戻る (Issue #793)。
        /// - 総寿命 <see cref="VerdictTotalSeconds"/> を過ぎると α=0。
        ///
        /// 旧軌道 (Issue #671: 静止位置の真上から落下して着地 → 静止 → フェード)
        /// はオーナー要求に反するため廃止した (#728 観測 3 / #731)。
        /// v0.2.0 の「頂点で 0.5 秒静止 → 落下」も v0.2.1 実機レビューで
        /// 不自然と差し戻され、頂点通過型の連続軌道へ改めた。
        /// v0.2.3 実機レビュー (Issue #793) で頂点距離の不足と出現スケール
        /// 欠如、同一レーン連打での上書きを指摘され、距離をメトリクス比例へ、
        /// スケールポップとイベント単位の表示インスタンスを追加した。
        /// </summary>
        /// <param name="elapsed">判定確定からの経過秒 (負は 0 として扱う)。</param>
        /// <param name="m">解像度別 HUD メトリクス (退場距離の算出に使う)。</param>
        public static VerdictPose VerdictPoseAt(float elapsed, V6HudMetrics m)
        {
            float t = Mathf.Max(0f, elapsed);
            float peak = VerdictPeakRisePx(m);
            float scale = PunchScale(t, VerdictPopPeakScale,
                VerdictPopScaleAttack, VerdictPopScaleTotal);

            // 上昇: 判定線 (0) → 頂点 (peak)。EaseOut で立ち上がりを減速させる。
            if (t < VerdictRiseSeconds)
            {
                return new VerdictPose(
                    peak * EaseOutCubic(t / VerdictRiseSeconds), 1f, scale);
            }

            // 下方退場: 頂点 → 判定線を越えて exitDrop 分だけ下。
            // EaseIn で加速する。頂点での静止は挟まず上昇から連続して落ちる。
            float u = Mathf.Clamp01((t - VerdictRiseSeconds) / VerdictFallSeconds);
            float travel = peak + VerdictExitDropPx(m);
            float offset = peak - travel * EaseInCubic(u);

            // α: 上昇と退場序盤 (u < FadeStartU) は不透明。終盤 40% で
            // smoothstep により 1→0 へフェードする。判定線を割るのは
            // EaseIn 加速の終盤なので「線の下だけでフェード」だと
            // 消滅が速すぎて残像に見える — 落下の終盤に合わせて消す。
            float fadeU = (u - VerdictFadeStartU) / (1f - VerdictFadeStartU);
            float alpha = fadeU <= 0f ? 1f : 1f - fadeU * fadeU * (3f - 2f * fadeU);
            return new VerdictPose(offset, alpha, scale);
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

        /// <summary>
        /// 節目ボーナス獲得表示が消えるまでの時間 (秒)。
        /// scoring v4 (ADR-0033 / Issue #736): 「+12,345 TOTAL +48,699」の
        /// ようなカンマ区切りの加点をプレイ中に読み取れるよう、視認に必要な
        /// 滞在時間から決める。HUD トーストの一般的な下限 (~1.5s) に、
        /// 数字 2 組の注視と周辺コンテキストの把握を足して 2.5s とする。
        /// 節目は最短でも ~50 コンボ間隔なので、通常は重ならない。
        /// </summary>
        public const float MilestoneBonusSeconds = 2.5f;

        /// <summary>EaseOutCubic: 速く立ち上がって滑らかに止まる。</summary>
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        /// <summary>EaseInCubic: 徐々に加速する (判定ポップの下方退場用)。</summary>
        public static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
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

        /// <summary>KeyCap「Space」の左右 padding (design [0,10] の水平成分)。</summary>
        public const float ReadyPromptPaddingX = 10f;

        /// <summary>KeyCap の角丸半径 (design r4)。</summary>
        public const float ReadyPromptCornerRadius = 4f;

        /// <summary>
        /// design の歌詞本文 fs (HcP8N Lyrics = 44)。Start Prompt の全寸法は
        /// この値を基準にした比率で決める (Issue #790)。
        /// </summary>
        public const float LyricDesignFontSize = 44f;

        /// <summary>
        /// Start Prompt の寸法一式。歌詞本文の実 fs (<paramref name="lyricFs"/>)
        /// を基準に、cap 高さ・fs・gap・左右 padding・角丸を <b>同じ比率</b>で
        /// 揃えた実 px 値を返す。
        ///
        /// Issue #790 (v0.2.1 実機受入): かつてはフォントサイズだけを
        /// <c>LyricFontScale</c> で拡縮し、KeyCap の padding・gap・角丸は
        /// design px 固定のままだった。比率が解像度ごとにずれ、
        /// 「Space to start」の余白と文字組みが崩れた。寸法の正本をここに
        /// 置き、ビルド側はこの spec だけを使う。
        /// </summary>
        public struct ReadyPromptSpec
        {
            /// <summary>KeyCap の高さ (px)。ラベル行の高さもこれに揃える。</summary>
            public float CapHeight;
            /// <summary>KeyCap 内文字とラベルのフォントサイズ (px)。</summary>
            public float FontSize;
            /// <summary>KeyCap とラベルの間隔 (px)。</summary>
            public float Gap;
            /// <summary>KeyCap 内の文字左右 padding (px)。</summary>
            public float PaddingX;
            /// <summary>KeyCap の角丸半径 (px)。</summary>
            public float CornerRadius;
        }

        /// <summary>
        /// 歌詞本文 fs から Start Prompt の寸法一式を求める。
        /// design 比: fs 22/44, cap 36/44, gap 10/44, padding 10/44, r 4/44。
        /// </summary>
        public static ReadyPromptSpec ReadyPrompt(float lyricFs)
        {
            float k = LyricDesignFontSize > 0f ? lyricFs / LyricDesignFontSize : 1f;
            return new ReadyPromptSpec
            {
                CapHeight = ReadyPromptCapHeight * k,
                FontSize = ReadyPromptFontSize * k,
                Gap = ReadyPromptGap * k,
                PaddingX = ReadyPromptPaddingX * k,
                CornerRadius = ReadyPromptCornerRadius * k,
            };
        }

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
        /// コンボボーナス表示 (Issue #784 / ADR-0035) を出すか。
        /// scoring v5 では連続コンボボーナスが無く、コンボ由来の獲得済み
        /// ボーナスは節目到達 (GManager.milestoneBonus) が保持する。
        /// 1 回でも節目に達していれば (ratioPoints &gt; 0) 表示し続ける
        /// (Miss でコンボが切れても獲得済みは失われない)。
        /// </summary>
        public static bool ShowComboBonus(int comboBonusRatioPoints)
        {
            return comboBonusRatioPoints > 0;
        }

        /// <summary>
        /// 表示中のプレイで「獲得済みコンボボーナス」の ratio points。
        /// v5 (ADR-0035) は milestoneBonus、v4 以前は comboBonus
        /// (maxCombo * 5) を使う。GManager.comboBonus は v5 では常に 0。
        /// </summary>
        public static int EarnedComboBonusRatioPoints()
        {
            return FlashBeat.Gameplay.Scoring.LatestVersion >= FlashBeat.Gameplay.Scoring.Version5
                ? GManager.milestoneBonus
                : GManager.comboBonus;
        }

        /// <summary>
        /// 最新 scoringVersion のボーナス分母で ratio points をスコア点へ
        /// 換算する (v5: <see cref="FlashBeat.Gameplay.Scoring.ComponentScoreV5"/>、
        /// v4: <see cref="FlashBeat.Gameplay.Scoring.ComponentScoreV4"/>)。
        /// </summary>
        public static int ComponentScoreLatest(int bonusRatioPoints, float maxBonusRatio)
        {
            return FlashBeat.Gameplay.Scoring.LatestVersion >= FlashBeat.Gameplay.Scoring.Version5
                ? FlashBeat.Gameplay.Scoring.ComponentScoreV5(bonusRatioPoints, maxBonusRatio)
                : FlashBeat.Gameplay.Scoring.ComponentScoreV4(bonusRatioPoints, maxBonusRatio);
        }

        /// <summary>
        /// 最新 scoringVersion の 1 節目あたり ratio points
        /// (v5: <see cref="FlashBeat.Gameplay.Scoring.MilestoneBonusPointsV5"/>=250、
        /// v4: <see cref="FlashBeat.Gameplay.Scoring.MilestoneBonusPoints"/>=25)。
        /// </summary>
        public static int MilestoneBonusPointsLatest()
        {
            return FlashBeat.Gameplay.Scoring.LatestVersion >= FlashBeat.Gameplay.Scoring.Version5
                ? FlashBeat.Gameplay.Scoring.MilestoneBonusPointsV5
                : FlashBeat.Gameplay.Scoring.MilestoneBonusPoints;
        }

        /// <summary>
        /// コンボボーナスの表示スコア点 (Issue #784 / ADR-0035)。
        /// <paramref name="comboBonusRatioPoints"/> を最新 scoringVersion
        /// のボーナス分母 (GManager.maxBonusRatio) で実際に加算される
        /// スコア点へ換算する。結果画面の内訳表示と同じ値になる。
        /// 表示専用の目安で、送信フィールドではない。
        /// </summary>
        public static int ComboBonusScorePoints(int comboBonusRatioPoints, float maxBonusRatio)
        {
            return ComponentScoreLatest(comboBonusRatioPoints, maxBonusRatio);
        }

        /// <summary>
        /// 次の節目到達で確定するボーナスのスコア点 (ADR-0035)。
        /// v5 では 1 節目あたり MilestoneBonusPointsV5 ratio points が
        /// 常に同じスコア点になる。v4 以前は節目ごとの加点が一定でない
        /// ため 0 を返す (呼び出し側は非表示にする)。
        /// </summary>
        public static int NextMilestoneScorePoints(float maxBonusRatio)
        {
            if (FlashBeat.Gameplay.Scoring.LatestVersion < FlashBeat.Gameplay.Scoring.Version5)
            {
                return 0;
            }

            return FlashBeat.Gameplay.Scoring.ComponentScoreV5(
                FlashBeat.Gameplay.Scoring.MilestoneBonusPointsV5, maxBonusRatio);
        }

        /// <summary>
        /// 判定結果 (<see cref="FlashBeat.Gameplay.LongNote.Judgement"/>) を
        /// 判定ラベルへ。Judge の MessageObj (Perfect/Great/Bad/Miss.prefab)
        /// の表示名と同じ綴り。Option 画面の Game Preview (Issue #607) が
        /// JudgeQueue の判定結果を HUD と同じラベルで出すために使う。
        /// </summary>
        public static string VerdictLabel(FlashBeat.Gameplay.LongNote.Judgement judgement)
        {
            switch (judgement)
            {
                case FlashBeat.Gameplay.LongNote.Judgement.Perfect: return "PERFECT";
                case FlashBeat.Gameplay.LongNote.Judgement.Great: return "GREAT";
                case FlashBeat.Gameplay.LongNote.Judgement.Bad: return "BAD";
                case FlashBeat.Gameplay.LongNote.Judgement.Miss: return "MISS";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// BAD 判定の表示色。v0.2.0 までは <see cref="V5Tokens.WarnLight"/> (黄)
        /// だったが、実機レビューで判定体系の色相として不適切と判断され、
        /// 濃い青系 (#4A7EC2) へ変更した (v0.2.1)。
        /// </summary>
        public static readonly Color VerdictBadColor = new Color32(0x4A, 0x7E, 0xC2, 0xFF);

        /// <summary>判定ラベルの表示色 (v5 GameHudView.JudgeColor と同じ割り当て)。</summary>
        public static Color VerdictColor(string label)
        {
            switch (label)
            {
                case "PERFECT": return V5Tokens.AccentLight;
                case "GREAT": return V5Tokens.Text;
                case "BAD": return VerdictBadColor;
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
        /// Key Guide モードに応じたレーンのキー文字列。
        /// Full = レーンの全有効キーを空白区切り (v5 GuideKeyText と同じ規約)、
        /// それ以外 = <see cref="RepresentativeKeyText"/>。Issue #658: V6 が
        /// Full を無視して代表キー 1 個しか出さなかったため、全キー確認の
        /// 手段がゲーム内に無かった。
        /// </summary>
        public static string GuideKeyText(int lane)
        {
            KeyCode[] keys = KeyBindings.GetKeys(lane);
            if (PlaySession.KeyGuide == KeyGuideMode.Full)
            {
                if (keys == null || keys.Length == 0)
                {
                    return string.Empty;
                }

                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < keys.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(' ');
                    }

                    sb.Append(KeyGuideUI.GetKeyDisplayName(keys[i]));
                }

                return sb.ToString();
            }

            return RepresentativeKeyText(keys);
        }

        /// <summary>
        /// キーガイドのキャップに収まるフォントサイズ。design の fs を上限に、
        /// ラベル推定送り幅が利用幅 (幅 - 左右パディング) を超えるときだけ
        /// 縮める。Compact の代表キー 1 個では design fs がそのまま返る。
        /// </summary>
        public static float GuideKeyFontSize(float capWidth, float designFs, string label, float paddingX)
        {
            if (designFs <= 0f)
            {
                return designFs;
            }

            float em = V6Ui.LabelAdvanceEm(label);
            float available = capWidth - paddingX * 2f;
            if (em <= 0f || available <= 0f)
            {
                return designFs;
            }

            return Mathf.Max(1f, Mathf.Min(designFs, Mathf.Floor(available / em)));
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
    }
}
