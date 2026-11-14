using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 共通のスライダー操作部 (Issue #385 Phase 1.3)。design `Components v6`
    /// (o0zh7) の "Volume Slider" (J7tBYP) のうち、トラック / フィル / ノブと
    /// ポインタ操作を 1 部品に集約する。
    ///
    /// - 寸法は design 空間 (1728×972 基準) の px。トラック 464×20 r10 ($k1)、
    ///   フィルは値の割合 × 464 ($k6)、ノブ 24×24 r12 ($text)。
    /// - ノブ中心 x はトラック左端から <c>24 + 440 × value</c> (0% で 24、
    ///   100% で 464)。トラックの左端から右端までが 0〜1 に対応する。
    /// - ドラッグはトラック上のポインタ位置をそのまま値へ写す (押した瞬間に
    ///   ジャンプし、そのまま連続変化する)。キーボード操作はこの部品ではなく
    ///   各画面が行い、同じ値モデル (getValue / onValue) を共有する。
    /// - 値の所有者は呼び出し側 (GManager 等)。この部品は値を保持せず、
    ///   getValue で読み onValue で書き戻す (第 2 引数は「操作が確定したか」)。
    ///
    /// 値ラベル・操作ヒントなどの周辺 chrome は画面側が足す
    /// (音量スライダは <see cref="V6VolumeSlider"/> が値ラベルとヒントを持つ)。
    /// 読み取り専用のバー (Loading の進捗トラック、Result / HUD のスコア・
    /// コンボゲージ) はノブも入力も持たず寸法も別なので、ここには含めない。
    /// </summary>
    public class V6Slider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // --- design px (o0zh7 J7tBYP) ---

        /// <summary>トラック (464×20 r10)。</summary>
        public const float TrackW = 464f, TrackH = 20f;

        /// <summary>ノブ (24×24 r12)。トラックに重ねる。</summary>
        public const float KnobSize = 24f;

        /// <summary>値 0〜1 を表す軌跡 (ノブ中心) の x。0% で 24、100% で 464。</summary>
        public const float KnobCenterFrom = 24f, KnobCenterSpan = 440f;

        /// <summary>現在値の読み出し (0..1)。所有者は呼び出し側。</summary>
        protected Func<float> getValue;

        /// <summary>値の書き戻し。第 2 引数は操作確定 (ドラッグ終了 / 離した) か。</summary>
        protected Action<float, bool> onValue;

        protected RectTransform track;
        protected RectTransform fill;
        protected RectTransform knob;

        /// <summary>ノブ anchoredPosition.x の基準値 (トラック左端 - ノブ幅/2)。</summary>
        private float knobXBase;

        /// <summary>ノブ上端の design y (ルート左上基準)。anchoredPosition.y は -knobY。</summary>
        private float knobY;

        /// <summary>現在値 (0..1)。</summary>
        public float Value
        {
            get { return getValue != null ? Clamp01(getValue()) : 0f; }
        }

        /// <summary>
        /// スライダーを組み立てる。designTopLeft はルート左上の design 座標、
        /// rootSize はルート寸法、trackTopLeft はルート内のトラック左上
        /// (ルート左上基準の design 座標)。値ラベル等の chrome はこの部品では
        /// 作らないので、必要な画面は <see cref="AttachParts"/> を自分で呼ぶ
        /// (例: <see cref="V6VolumeSlider.Build"/>)。
        /// </summary>
        public static V6Slider Build(GameObject parent, string name, Vector2 designTopLeft,
            Vector2 rootSize, Vector2 trackTopLeft, Func<float> getValue, Action<float, bool> onValue)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent.transform, false);
            V6Layout.Place(root.GetComponent<RectTransform>(), designTopLeft, rootSize);

            var slider = root.AddComponent<V6Slider>();
            slider.getValue = getValue;
            slider.onValue = onValue;
            slider.AttachParts(root, trackTopLeft);
            slider.Refresh();
            return slider;
        }

        /// <summary>
        /// トラック / フィル / ノブを root の子として作る (GameObject 名は
        /// "Track" / "Fill" / "Knob"、Track の子に Fill)。ノブはトラックの
        /// 縦中央に重ねる。呼び出し側は getValue / onValue を先に設定すること
        /// (<see cref="Refresh"/> が値を読むため)。
        /// </summary>
        protected void AttachParts(GameObject root, Vector2 trackTopLeft)
        {
            var trackGo = V6Controls.Panel(root, "Track", trackTopLeft,
                new Vector2(TrackW, TrackH), V6Tokens.Key1, TrackH * 0.5f);
            track = trackGo.GetComponent<RectTransform>();

            // フィルも角丸 10 (design o0zh7 の Fill)。値が小さいときは幅が角丸
            // 2 個分を下回るが、そのときは 9-slice が縮むだけでレイアウトは崩れない。
            var fillGo = V6Controls.Panel(trackGo, "Fill", Vector2.zero,
                new Vector2(TrackW, TrackH), V6Tokens.Key6, TrackH * 0.5f);
            fill = fillGo.GetComponent<RectTransform>();

            knobXBase = trackTopLeft.x - KnobSize * 0.5f;
            knobY = trackTopLeft.y + TrackH * 0.5f - KnobSize * 0.5f;
            var knobGo = V6Controls.Panel(root, "Knob",
                new Vector2(trackTopLeft.x, knobY), new Vector2(KnobSize, KnobSize),
                V6Tokens.Text, KnobSize * 0.5f);
            knob = knobGo.GetComponent<RectTransform>();
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

        /// <summary>0..1 にクランプする。</summary>
        protected static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        /// <summary>現在値のフィル幅とノブ位置を更新する (レイアウトは動かさない)。</summary>
        public virtual void Refresh()
        {
            float value = Value;
            if (fill != null)
            {
                fill.sizeDelta = new Vector2(Mathf.Round(TrackW * value), TrackH);
            }

            if (knob != null)
            {
                knob.anchoredPosition = new Vector2(
                    Mathf.Round(knobXBase + KnobCenter(value)), -knobY);
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

        /// <summary>ノブ中心の x (トラック左端 = 0、検証用)。未生成なら -1。</summary>
        internal float KnobCenterForAudit
        {
            get { return knob != null ? knob.anchoredPosition.x - knobXBase : -1f; }
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
