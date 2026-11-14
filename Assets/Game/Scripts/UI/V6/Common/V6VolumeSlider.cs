using System;
using UnityEngine;
using TMPro;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 共通の音量スライダ (design <c>o0zh7</c> / <c>J7tBYP</c> "Volume Slider"、
    /// 設計判断 <c>docs/design/v6/review-2026-11-11-decisions.md</c> §3)。
    /// Option の Sound タブと Pause の Sound タブが同じ部品を使う。
    ///
    /// 操作部 (トラック / フィル / ノブ / ドラッグ) は <see cref="V6Slider"/>
    /// (Issue #385 Phase 1.3) に集約済み。このクラスは音量スライダ固有の
    /// chrome — 値ラベル (fs58) と操作ヒント (fs18) — と % 表記・キー刻みを持つ。
    ///
    /// - 寸法は design 空間 (1728×972 基準) の px。値ラベル / トラック (464×20) /
    ///   フィル / ノブ (24×24) / 操作ヒントを上から並べる。
    /// - ノブ中心 x は <c>24 + 440 × value</c> (0% で 24、100% で 464)。トラックの
    ///   左端から右端までが 0〜100% に対応する (<see cref="V6Slider"/> と同じ)。
    /// - ドラッグはトラック上のポインタ位置をそのまま値へ写す (押した瞬間にジャンプし、
    ///   そのまま連続変化する)。キーボード操作はこの部品ではなく各画面が行い、
    ///   同じ値モデル (<see cref="Func{TResult}"/> の getter と setter) を共有する。
    /// - 値の所有者は GManager (mainVolume / effectVolume / BGMVolume)。この部品は
    ///   値を保持しない (getValue で読み、onValue で書き戻す)。
    /// </summary>
    public sealed class V6VolumeSlider : V6Slider
    {
        // --- design px (o0zh7 J7tBYP)。操作部の寸法は V6Slider が持つ ---

        /// <summary>値ラベルの寸法 (fs58)。</summary>
        public const float ValueW = 464f, ValueH = 58f;

        /// <summary>トラックのルート内 y (design px)。</summary>
        public const float TrackY = 84f;

        /// <summary>ノブ上端の y (トラック縦中央に重なる)。</summary>
        public const float KnobY = TrackY - 2f;

        /// <summary>操作ヒント (fs18 $text-dim)。</summary>
        public const float HintY = 112f, HintH = 26f;

        /// <summary>部品全体の高さ (値 + トラック + ヒント)。</summary>
        public const float Height = 160f;

        /// <summary>操作ヒントの文言 (キーボードは各画面の凡例と同じ刻み)。</summary>
        public const string HintText = "ドラッグ / ←→ 1% (Shift 10%)";

        // --- ジオメトリの正本は V6Slider。既存の参照互換のため別名を残す ---

        /// <summary>トラック幅 (<see cref="V6Slider.TrackW"/> と同じ)。</summary>
        public const float TrackW = V6Slider.TrackW;

        /// <summary>トラック高さ (<see cref="V6Slider.TrackH"/> と同じ)。</summary>
        public const float TrackH = V6Slider.TrackH;

        /// <summary>ノブ一辺 (<see cref="V6Slider.KnobSize"/> と同じ)。</summary>
        public const float KnobSize = V6Slider.KnobSize;

        /// <summary>ノブ中心軌跡の左端 x (<see cref="V6Slider.KnobCenterFrom"/> と同じ)。</summary>
        public const float KnobCenterFrom = V6Slider.KnobCenterFrom;

        /// <summary>ノブ中心軌跡の幅 (<see cref="V6Slider.KnobCenterSpan"/> と同じ)。</summary>
        public const float KnobCenterSpan = V6Slider.KnobCenterSpan;

        private TMP_Text valueLabel;

        /// <summary>
        /// 部品を組み立てる。designTopLeft は部品左上の design 座標、
        /// getValue は現在値 (0..1) の読み出し、onValue は書き戻し
        /// (第 2 引数は「操作が確定したか」= 保存してよいタイミング)。
        /// 子の並びは Value → Track → Fill → Knob → Hint (従来どおり)。
        /// </summary>
        public static V6VolumeSlider Build(GameObject parent, string name, Vector2 designTopLeft,
            Func<float> getValue, Action<float, bool> onValue)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent.transform, false);
            V6Layout.Place(root.GetComponent<RectTransform>(), designTopLeft, new Vector2(ValueW, Height));

            var slider = root.AddComponent<V6VolumeSlider>();
            slider.getValue = getValue;
            slider.onValue = onValue;

            slider.valueLabel = V6Controls.Text(root, "Value", Format(slider.Value),
                Vector2.zero, new Vector2(ValueW, ValueH), 58f, V6Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);

            // 操作部 (Track / Fill / Knob) は共通部品 V6Slider が作る。
            slider.AttachParts(root, new Vector2(0f, TrackY));

            V6Controls.Text(root, "Hint", HintText, new Vector2(0f, HintY),
                new Vector2(ValueW, HintH), 18f, V6Tokens.TextDim);

            slider.Refresh();
            return slider;
        }

        /// <summary>値の表示 (0..1 → "42%")。</summary>
        public static string Format(float value)
        {
            return Mathf.RoundToInt(Clamp01(value) * 100f) + "%";
        }

        /// <summary>ポインタ x (トラック左端 = 0) を値 (0..1) へ写す。</summary>
        public static float ValueAt(float trackLocalX)
        {
            return V6Slider.ValueAt(trackLocalX);
        }

        /// <summary>ノブ中心の x (トラック左端 = 0)。</summary>
        public static float KnobCenter(float value)
        {
            return V6Slider.KnobCenter(value);
        }

        /// <summary>キー操作の刻み (Shift で 10%、通常 1%)。</summary>
        public static float Step(bool shift)
        {
            return shift ? 0.1f : 0.01f;
        }

        /// <summary>値を刻み分だけ動かす (0..1 にクランプ、1% 単位に丸める)。</summary>
        public static float Nudge(float value, int delta, bool shift)
        {
            float shifted = Clamp01(value) + delta * Step(shift);
            return Clamp01((float)Math.Round(shifted * 100f) / 100f);
        }

        /// <summary>
        /// 現在値の表示・フィル・ノブを更新する (レイアウトは動かさない)。
        /// 値ラベルは音量固有なのでここで更新し、フィル / ノブは基底に任せる。
        /// </summary>
        public override void Refresh()
        {
            if (valueLabel != null)
            {
                valueLabel.text = Format(Value);
            }

            base.Refresh();
        }

        /// <summary>値ラベルの表示文字列 (検証用)。</summary>
        internal string ValueLabelForAudit
        {
            get { return valueLabel != null ? valueLabel.text : string.Empty; }
        }
    }
}
