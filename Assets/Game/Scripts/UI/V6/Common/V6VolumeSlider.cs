using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 共通の音量スライダ (design <c>o0zh7</c> / <c>J7tBYP</c> "Volume Slider"、
    /// 設計判断 <c>docs/design/v6/review-2026-11-11-decisions.md</c> §3)。
    /// Option の Sound タブと Pause の Sound タブが同じ部品を使う。
    ///
    /// - 寸法は design 空間 (1728×972 基準) の px。値ラベル (fs58) / トラック (464×20) /
    ///   フィル / ノブ (24×24) / 操作ヒント (fs18) を上から並べる。
    /// - ノブ中心 x は <c>24 + 440 × value</c> (0% で 24、100% で 464)。トラックの
    ///   左端から右端までが 0〜100% に対応する。
    /// - ドラッグはトラック上のポインタ位置をそのまま値へ写す (押した瞬間にジャンプし、
    ///   そのまま連続変化する)。キーボード操作はこの部品ではなく各画面が行い、
    ///   同じ値モデル (<see cref="Func{TResult}"/> の getter と setter) を共有する。
    /// - 値の所有者は GManager (mainVolume / effectVolume / BGMVolume)。この部品は
    ///   値を保持しない (getValue で読み、onValue で書き戻す)。
    /// </summary>
    public sealed class V6VolumeSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // --- design px (o0zh7 J7tBYP) ---

        /// <summary>値ラベルの寸法 (fs58)。</summary>
        public const float ValueW = 464f, ValueH = 58f;

        /// <summary>トラック (464×20 r10)。</summary>
        public const float TrackY = 84f, TrackW = 464f, TrackH = 20f;

        /// <summary>ノブ (24×24 r12)。トラックに重ねる (y = 82)。</summary>
        public const float KnobSize = 24f, KnobY = TrackY - 2f;

        /// <summary>操作ヒント (fs18 $text-dim)。</summary>
        public const float HintY = 112f, HintH = 26f;

        /// <summary>部品全体の高さ (値 + トラック + ヒント)。</summary>
        public const float Height = 160f;

        /// <summary>音量 0〜1 を表す軌跡 (ノブ中心) の x。0% で 24、100% で 464。</summary>
        public const float KnobCenterFrom = 24f, KnobCenterSpan = 440f;

        /// <summary>操作ヒントの文言 (キーボードは各画面の凡例と同じ刻み)。</summary>
        public const string HintText = "ドラッグ / ←→ 1% (Shift 10%)";

        private Func<float> getValue;
        private Action<float, bool> onValue;
        private RectTransform track;
        private RectTransform fill;
        private RectTransform knob;
        private TMP_Text valueLabel;

        /// <summary>現在値 (0..1)。</summary>
        public float Value
        {
            get { return getValue != null ? Clamp01(getValue()) : 0f; }
        }

        /// <summary>
        /// 部品を組み立てる。designTopLeft は部品左上の design 座標、
        /// getValue は現在値 (0..1) の読み出し、onValue は書き戻し
        /// (第 2 引数は「操作が確定したか」= 保存してよいタイミング)。
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

            var trackGo = V6Controls.Panel(root, "Track", new Vector2(0f, TrackY),
                new Vector2(TrackW, TrackH), V6Tokens.Key1, TrackH * 0.5f);
            slider.track = trackGo.GetComponent<RectTransform>();

            // フィルも角丸 10 (design o0zh7 の Fill)。値が小さいときは幅が角丸 2 個分を
            // 下回るが、そのときは 9-slice が縮むだけでレイアウトは崩れない。
            var fillGo = V6Controls.Panel(trackGo, "Fill", Vector2.zero,
                new Vector2(TrackW, TrackH), V6Tokens.Key6, TrackH * 0.5f);
            slider.fill = fillGo.GetComponent<RectTransform>();

            var knobGo = V6Controls.Panel(root, "Knob", new Vector2(0f, KnobY),
                new Vector2(KnobSize, KnobSize), V6Tokens.Text, KnobSize * 0.5f);
            slider.knob = knobGo.GetComponent<RectTransform>();

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

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        /// <summary>ポインタ x (トラック左端 = 0) を値 (0..1) へ写す。</summary>
        public static float ValueAt(float trackLocalX)
        {
            return Clamp01((trackLocalX - KnobCenterFrom) / KnobCenterSpan);
        }

        /// <summary>ノブ中心の x (トラック左端 = 0)。</summary>
        public static float KnobCenter(float value)
        {
            return KnobCenterFrom + KnobCenterSpan * Clamp01(value);
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

        /// <summary>現在値の表示・フィル・ノブを更新する (レイアウトは動かさない)。</summary>
        public void Refresh()
        {
            float value = Value;
            if (valueLabel != null)
            {
                valueLabel.text = Format(value);
            }

            if (fill != null)
            {
                fill.sizeDelta = new Vector2(Mathf.Round(TrackW * value), TrackH);
            }

            if (knob != null)
            {
                knob.anchoredPosition = new Vector2(Mathf.Round(KnobCenter(value) - KnobSize * 0.5f), -KnobY);
            }
        }

        // --- 検証ハーネス用 (プレイヤー操作では使わない) ---

        /// <summary>
        /// 値の位置に対応する画面上の点。検証ハーネスがこの点で
        /// OnPointerDown / OnDrag / OnPointerUp を呼ぶと、実際のドラッグと同じ
        /// 座標変換 (ScreenPointToLocalPointInRectangle) を通る。
        /// </summary>
        internal Vector2 ScreenPointForValue(float value)
        {
            if (track == null)
            {
                return Vector2.zero;
            }

            Vector3 world = track.TransformPoint(new Vector3(KnobCenter(value), -TrackH * 0.5f, 0f));
            return RectTransformUtility.WorldToScreenPoint(null, world);
        }

        /// <summary>値ラベルの表示文字列 (検証用)。</summary>
        internal string ValueLabelForAudit
        {
            get { return valueLabel != null ? valueLabel.text : string.Empty; }
        }

        /// <summary>ノブ中心の x (トラック左端 = 0、検証用)。未生成なら -1。</summary>
        internal float KnobCenterForAudit
        {
            get { return knob != null ? knob.anchoredPosition.x + KnobSize * 0.5f : -1f; }
        }

        // --- ポインタ操作 ---

        public void OnPointerDown(PointerEventData eventData)
        {
            Apply(eventData, false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Apply(eventData, false);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Apply(eventData, true);
        }

        private void Apply(PointerEventData eventData, bool final)
        {
            if (track == null)
            {
                return;
            }

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                track, eventData.position, eventData.pressEventCamera, out local))
            {
                return;
            }

            // Place() は anchor/pivot (0,1)。ローカル x はトラック左端が 0。
            float value = ValueAt(local.x);
            if (onValue != null)
            {
                onValue(value, final);
            }

            Refresh();
        }
    }
}
