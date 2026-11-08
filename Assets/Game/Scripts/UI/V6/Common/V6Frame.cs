using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 共有フレーム 1728×972 (契約 §4) を構築するビルダ。
    /// design 空間 (左上原点・y_down) の座標をそのまま実ピクセル 1:1 で組み、
    /// 実機では <see cref="OriginFor"/> が返す解像度別 origin
    /// (FHD 1920×1080 → (96,54) / 2K 2560×1440 → (416,234) / Mac 2560×1664 → (416,346))
    /// に等倍のまま配置する。親側で scale を掛けて引き伸ばすことはしない
    /// (design フレームは固定解像度であり、解像度ごとに origin だけが変わる)。
    ///
    /// ゾーン (契約 §4 の座標そのもの):
    /// - Header (24,0)   1680×96
    /// - Main   (24,96)  1680×780
    /// - Footer (24,876) 1680×96
    /// - Modal  (0,0)    1728×972 (既定非表示)
    ///
    /// 座標変換は <see cref="V6Layout.ToLocal"/> に集約 (全 v6 画面共通の規約)。
    /// </summary>
    public static class V6Frame
    {
        public const float DesignW = 1728f;
        public const float DesignH = 972f;

        // ゾーン矩形 (design 空間、左上原点)。契約 §4 の写し。
        public static readonly Vector2 HeaderPos = new Vector2(24f, 0f);
        public static readonly Vector2 HeaderSize = new Vector2(1680f, 96f);
        public static readonly Vector2 MainPos = new Vector2(24f, 96f);
        public static readonly Vector2 MainSize = new Vector2(1680f, 780f);
        public static readonly Vector2 FooterPos = new Vector2(24f, 876f);
        public static readonly Vector2 FooterSize = new Vector2(1680f, 96f);
        public static readonly Vector2 ModalPos = new Vector2(0f, 0f);
        public static readonly Vector2 ModalSize = new Vector2(DesignW, DesignH);

        // Dialog (曲名入力等) の契約 §4 座標。
        public static readonly Vector2 DialogPos = new Vector2(444f, 231f);
        public static readonly Vector2 DialogSize = new Vector2(840f, 510f);

        private const string ModalNodeName = "Modal";

        /// <summary>
        /// 解像度別のフレーム origin (design の "Resolution Check" の実配置)。
        /// ((screenW - 1728) / 2, (screenH - 972) / 2) の整数切り捨て。
        /// FHD 1920×1080 → (96,54) / 2K 2560×1440 → (416,234) / Mac 2560×1664 → (416,346)。
        ///
        /// 画面がフレーム (1728×972) より小さい場合 origin は負になるが、
        /// クランプしない (クランプすると 1:1 中央配置が崩れる)。負 origin は
        /// 「フレームが画面からはみ出す」状態であり、呼び出し側は画面解像度が
        /// フレーム以上であることを前提とする。0 未満の座標はスクリーン外なので、
        /// その扱いは本関数の責務ではない (別問題として扱う)。
        /// </summary>
        public static Vector2 OriginFor(int screenW, int screenH)
        {
            return new Vector2(
                Mathf.FloorToInt((screenW - DesignW) / 2f),
                Mathf.FloorToInt((screenH - DesignH) / 2f));
        }

        /// <summary>
        /// フレームの外側を含む画面全体を不透明な暗色 (契約 §3 の <c>bg</c> =
        /// <see cref="V6Tokens.Bg"/>) で覆う。v5 等の Legacy クリーム色背景が
        /// フレーム外に透けないようにするためのヘルパ。
        /// 生成される "Backdrop" は parent 直下の全画面 stretch、
        /// 描画順を最背面 (sibling index 0) にしてフレームの下に敷く。
        /// </summary>
        public static void ApplyBackdrop(Transform parent, int screenW, int screenH, Color color)
        {
            if (parent == null)
            {
                return;
            }

            var go = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            go.transform.SetSiblingIndex(0);
        }

        /// <summary>
        /// 1728×972 の frame GameObject を作る。戻り値の RectTransform は
        /// anchor/pivot (0.5,0.5)。フレームは常に実ピクセル 1:1 (scale 1) で作られ、
        /// 呼び出し側が親 (Canvas) の中央に <see cref="OriginFor"/> の origin で
        /// 配置する。親側で scale を掛けてはならない。
        /// </summary>
        public static GameObject Create(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(DesignW, DesignH);

            CreateZone(go, "Header", HeaderPos, HeaderSize);
            CreateZone(go, "Main", MainPos, MainSize);
            CreateZone(go, "Footer", FooterPos, FooterSize);
            CreateModal(go);
            CreateOuterStroke(go);
            return go;
        }

        /// <summary>モーダル層の GameObject を返す (無い場合は null)。</summary>
        public static GameObject GetModal(GameObject frame)
        {
            return frame == null ? null : frame.transform.Find(ModalNodeName)?.gameObject;
        }

        /// <summary>モーダル層を表示する (Scrim + Dialog)。</summary>
        public static void ShowModal(GameObject frame)
        {
            var modal = GetModal(frame);
            if (modal != null)
            {
                modal.SetActive(true);
            }
        }

        /// <summary>モーダル層を非表示にする (契約「enabled: false (既定非表示)」)。</summary>
        public static void HideModal(GameObject frame)
        {
            var modal = GetModal(frame);
            if (modal != null)
            {
                modal.SetActive(false);
            }
        }

        private static void CreateZone(GameObject frame, string zoneName, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(zoneName, typeof(RectTransform));
            go.transform.SetParent(frame.transform, false);
            V6Layout.Place(go.GetComponent<RectTransform>(), pos, size);
        }

        private static void CreateModal(GameObject frame)
        {
            var modal = new GameObject(ModalNodeName, typeof(RectTransform));
            modal.transform.SetParent(frame.transform, false);
            V6Layout.Place(modal.GetComponent<RectTransform>(), ModalPos, ModalSize);

            // Scrim: 全面 #00000099。
            var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            scrim.transform.SetParent(modal.transform, false);
            var scrimImage = scrim.GetComponent<Image>();
            scrimImage.color = V6Tokens.Scrim;
            scrimImage.raycastTarget = true;
            var scrimRt = scrim.GetComponent<RectTransform>();
            scrimRt.anchorMin = Vector2.zero;
            scrimRt.anchorMax = Vector2.one;
            scrimRt.offsetMin = Vector2.zero;
            scrimRt.offsetMax = Vector2.zero;

            // Dialog (444,231) 840×510 $k1 r16 (契約 §4)。中身は各画面が載せる。
            var dialog = V6Controls.Panel(modal, "Dialog", DialogPos, DialogSize, V6Tokens.Key1, V6Tokens.RadiusLarge);
            var dialogImage = dialog.GetComponent<Image>();
            dialogImage.raycastTarget = true;

            modal.SetActive(false);
        }

        /// <summary>
        /// 外周 stroke (#EDEDED33 sw2)。uGUI に線要素はないため、4 枚の細長 Image で再現する。
        /// </summary>
        private static void CreateOuterStroke(GameObject frame)
        {
            const float sw = 2f;
            MakeStroke(frame, "StrokeTop", new Vector2(0f, 0f), new Vector2(DesignW, sw));
            MakeStroke(frame, "StrokeBottom", new Vector2(0f, DesignH - sw), new Vector2(DesignW, sw));
            MakeStroke(frame, "StrokeLeft", new Vector2(0f, 0f), new Vector2(sw, DesignH));
            MakeStroke(frame, "StrokeRight", new Vector2(DesignW - sw, 0f), new Vector2(sw, DesignH));
        }

        private static void MakeStroke(GameObject frame, string strokeName, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(strokeName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(frame.transform, false);
            var image = go.GetComponent<Image>();
            image.color = V6Tokens.Border;
            image.raycastTarget = false;
            V6Layout.Place(go.GetComponent<RectTransform>(), pos, size);
        }
    }
}
