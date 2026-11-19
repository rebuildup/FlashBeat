using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// 画面が共通フレーム (1728×972) より小さいときのコンパクト表示 (#96 / #523)。
    ///
    /// 背景: フレームは design どおり 1:1 (等倍・引き伸ばしなし) で置く契約だが、
    /// 1024×768 / 1280×720 などフレームより小さい画面では中央配置の origin が負になり、
    /// Header / Footer の操作と情報が画面外へ出る (実害のある P0)。
    ///
    /// 対応: コンパクト画面ではフレームを画面全体を覆うビューポート
    /// (<see cref="ViewportName"/>) の中に入れ、<see cref="V6FramePan"/> で
    /// ドラッグ / ホイール / PageUp/PageDown により 1:1 のまま任意の領域へ
    /// パンできるようにする。要素の大きさ・フォントは一切変えない
    /// (縮小で解決しない)。パン範囲はコンテンツ全域を必ずカバーするので、
    /// すべての操作・情報へポインタで到達できる。
    ///
    /// 通常解像度 (フレームが画面に収まる) では従来どおり
    /// <see cref="V6Frame.OriginFor"/> の中央 1:1 配置で、ビューポートは作らない。
    /// </summary>
    public static class V6CompactScreen
    {
        /// <summary>コンパクト時のビューポート GameObject 名 (監査のスクロール親判定にも使う)。</summary>
        public const string ViewportName = "V6CompactViewport";

        /// <summary>
        /// EditMode テスト用の画面サイズ差し替え (issue #748)。
        /// Screen.width/height はテストから変更できないため、コンパクト配置や
        /// モーダル用パンを検証するテストが一時的に設定する。
        /// どちらかが 0 以下なら <see cref="Place"/> の引数をそのまま使う。
        /// 製品コード・他テストへ漏れないよう、設定したテストは必ず戻すこと。
        /// </summary>
        public static Vector2Int ScreenSizeOverrideForTests;

        /// <summary>
        /// 画面がフレーム (1728×972) を内包できないか。どちらかの辺が足りなければ
        /// コンパクト (例: 1600×900 は幅不足、2000×900 は高さ不足)。
        /// </summary>
        public static bool IsCompact(int screenW, int screenH)
        {
            return screenW < Mathf.RoundToInt(V6Frame.DesignW)
                || screenH < Mathf.RoundToInt(V6Frame.DesignH);
        }

        /// <summary>
        /// パン位置 (コンテンツ左上のビューポート左上からの design px) を
        /// 表示可能範囲へクランプする。コンテンツがビューポートより小さい軸は
        /// 中央寄せ (負の隙間を作らない)。
        /// </summary>
        public static Vector2 ClampPan(float x, float y, float viewW, float viewH)
        {
            return new Vector2(
                ClampAxis(x, viewW, V6Frame.DesignW),
                ClampAxis(y, viewH, V6Frame.DesignH));
        }

        /// <summary>1 軸分のクランプ。content &lt;= view なら中央、そうでなければ [view-content, 0]。</summary>
        public static float ClampAxis(float pos, float view, float content)
        {
            if (content <= view)
            {
                return (view - content) * 0.5f;
            }

            return Mathf.Clamp(pos, view - content, 0f);
        }

        /// <summary>
        /// フレームを画面解像度に合わせて配置する。各 View の配置処理
        /// (PlaceFrame / UpdatePlacement) はこの 1 関数に集約する。
        ///
        /// - 通常解像度: ビューポートがあれば解体し、フレームをホームの親へ戻して
        ///   <see cref="V6Frame.OriginFor"/> の原点へ 1:1 配置する。
        /// - コンパクト解像度: 画面全体を覆うビューポートを (無ければ) 作り、
        ///   フレームをその中へ入れてパン位置を適用する。パン位置は
        ///   <see cref="V6FramePan.Pan"/> に保持され、再配置・リビルドをまたいで維持される。
        /// </summary>
        public static void Place(RectTransform frame, int screenW, int screenH)
        {
            if (frame == null)
            {
                return;
            }

            // EditMode テスト用の差し替え (Screen.* はテストから変えられない)。
            if (ScreenSizeOverrideForTests.x > 0 && ScreenSizeOverrideForTests.y > 0)
            {
                screenW = ScreenSizeOverrideForTests.x;
                screenH = ScreenSizeOverrideForTests.y;
            }

            var viewport = FindViewport(frame);
            var home = viewport != null ? viewport.parent : frame.parent;

            frame.localScale = Vector3.one;
            frame.anchorMin = new Vector2(0f, 1f);
            frame.anchorMax = new Vector2(0f, 1f);
            frame.pivot = new Vector2(0f, 1f);
            frame.sizeDelta = new Vector2(V6Frame.DesignW, V6Frame.DesignH);

            if (!IsCompact(screenW, screenH))
            {
                if (viewport != null)
                {
                    frame.SetParent(home, false);
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(viewport.gameObject);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(viewport.gameObject);
                    }
                }

                frame.anchoredPosition = V6Layout.ToLocal(V6Frame.OriginFor(screenW, screenH));
                return;
            }

            if (viewport == null)
            {
                viewport = CreateViewport(home);
            }

            if (frame.parent != viewport)
            {
                frame.SetParent(viewport, false);
            }

            var pan = viewport.GetComponent<V6FramePan>();
            if (pan != null)
            {
                pan.ViewportSize = new Vector2(screenW, screenH);
                pan.Content = frame;
                // 初回配置時だけ、フレーム中央がビューポート中央に来る位置へ
                // 合わせる (issue #748)。従来は (0,0) = フレーム左上が見える
                // 位置から始まり、Footer の操作へ手動パンが必要だった。
                // ユーザーがパンした後・モーダル表示中は触らない。
                pan.EnsureInitialPan();
            }

            Vector2 panPos = pan != null ? pan.Pan : Vector2.zero;
            panPos = ClampPan(panPos.x, panPos.y, screenW, screenH);
            frame.anchoredPosition = new Vector2(panPos.x, -panPos.y);
            if (pan != null)
            {
                pan.Pan = panPos;
            }
        }

        /// <summary>フレームがコンパクトビューポート内にあるか。</summary>
        public static bool IsCompactPlaced(RectTransform frame)
        {
            return FindViewport(frame) != null;
        }

        /// <summary>フレームの親がコンパクトビューポートならそれを返す。</summary>
        public static RectTransform FindViewport(RectTransform frame)
        {
            if (frame == null || frame.parent == null)
            {
                return null;
            }

            var parent = frame.parent as RectTransform;
            if (parent != null && parent.name == ViewportName)
            {
                return parent;
            }

            return null;
        }

        /// <summary>
        /// 画面全面のビューポートを作る。RectMask2D でフレームの描画と
        /// レイキャストを画面内にクリップし、<see cref="V6FramePan"/> が入力を受ける。
        /// 背景の Image は raycastTarget のみ (色は完全透明、監査の Graphic 走査では
        /// alpha 0 として除外される)。
        /// </summary>
        private static RectTransform CreateViewport(Transform home)
        {
            var go = new GameObject(ViewportName, typeof(RectTransform));
            if (home != null)
            {
                go.transform.SetParent(home, false);
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var catcher = go.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            go.AddComponent<RectMask2D>();
            go.AddComponent<V6FramePan>();
            return rt;
        }
    }

    /// <summary>
    /// コンパクトビューポート内のフレームをポインタでパンする。
    /// ビューポート (<see cref="V6CompactScreen.ViewportName"/>) に付ける。
    /// ドラッグ / ホイール (Shift+ホイールで横) / PageUp・PageDown で
    /// コンテンツ (フレーム) を動かす。範囲は <see cref="V6CompactScreen.ClampPan"/>。
    /// </summary>
    public sealed class V6FramePan : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        /// <summary>ビューポートの RectTransform (自身)。EditMode では Awake が
        /// 呼ばれないことがあるので遅延初期化する。</summary>
        public RectTransform Viewport
        {
            get
            {
                if (viewport == null)
                {
                    viewport = GetComponent<RectTransform>();
                }

                return viewport;
            }
        }

        /// <summary>パンされるコンテンツ (フレーム) の RectTransform。</summary>
        public RectTransform Content { get; set; }

        /// <summary>
        /// コンテンツ左上のビューポート左上からの design px 位置。
        /// 設定時にクランプして即座に反映する。
        /// </summary>
        public Vector2 Pan
        {
            get { return pan; }
            set
            {
                pan = V6CompactScreen.ClampPan(value.x, value.y, ViewSize.x, ViewSize.y);
                Apply();
            }
        }

        /// <summary>ビューポートのサイズ (design px = 画面 px)。未設定時は自身の rect。</summary>
        public Vector2 ViewportSize
        {
            get { return viewportSize; }
            set { viewportSize = value; }
        }

        /// <summary>ホイール 1 単位あたりのパン量 (design px)。</summary>
        public float ScrollSensitivity = 120f;

        /// <summary>PageUp/PageDown 1 回あたりのパン量 (ビューポート高さに対する比率)。</summary>
        public float PageStepRatio = 0.8f;

        /// <summary>Whether a drag pan is in progress (audit/test hook). Without
        /// IEndDragHandler this stays true after mouse release and PageUp/PageDown
        /// pan stays disabled forever (#560).</summary>
        public bool DraggingForAudit => dragging;

        /// <summary>
        /// モーダル表示中など、コンテンツのパンを止めるとき true (Issue #609)。
        /// ドラッグ / ホイール / PageUp・PageDown のすべてを無効化する。
        /// モーダルはフレームの子なので、パンを許すと Dialog ごと画面外へ
        /// 動いてしまう。既定 false で従来どおり。
        /// モーダル用の退避は <see cref="SuspendForModal"/> /
        /// <see cref="ResumeModal"/> が管理する (issue #748)。モーダルが
        /// 開いている間も true を返す (手動フラグとの論理和)。
        /// </summary>
        public bool PanSuspended
        {
            get { return panSuspended || modalRects.Count != 0; }
            set { panSuspended = value; }
        }

        /// <summary>
        /// 初回配置時のパン位置を決める (issue #748)。フレーム (1728×972) の
        /// 中央がビューポート中央に来る位置 = 非コンパクト時の中央配置と同じ
        /// 見え方になり、Header だけ / 末尾だけに偏らない。
        /// 一度でも適用したら以後はユーザーのパンを保持する
        /// (再配置・リビルド・解像度変更をまたいで初期位置へ戻さない)。
        /// </summary>
        public void EnsureInitialPan()
        {
            if (initialPanApplied || modalRects.Count != 0)
            {
                initialPanApplied = true;
                return;
            }

            var size = ViewSize;
            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            initialPanApplied = true;
            Pan = new Vector2(
                (size.x - V6Frame.DesignW) * 0.5f,
                (size.y - V6Frame.DesignH) * 0.5f);
        }

        /// <summary>
        /// モーダルを開くときに呼ぶ (issue #748)。dialogRect はフレーム
        /// ローカル座標のダイアログ矩形 (左上原点の design px)。
        /// パン入力を止め、ダイアログ中心がビューポート中心に来る位置へ
        /// 合わせる (収まらない場合は <see cref="V6CompactScreen.ClampPan"/>
        /// の範囲で最大限中央へ寄せる)。複数モーダルが重なった場合は
        /// 最後に開いたものの矩形を使い、閉じるとひとつ前へ戻る。
        /// </summary>
        public void SuspendForModal(Rect dialogRect)
        {
            if (modalRects.Count == 0)
            {
                savedPan = pan;
            }

            modalRects.Add(dialogRect);
            ApplyModalPan();
        }

        /// <summary>
        /// <see cref="SuspendForModal"/> と対になる。最後のモーダルが閉じると
        /// 保存しておいたパン位置へ戻して入力を再開する。モーダルが重なって
        /// いる場合はひとつ前の矩形のパンへ戻る。
        /// </summary>
        public void ResumeModal()
        {
            if (modalRects.Count == 0)
            {
                return;
            }

            modalRects.RemoveAt(modalRects.Count - 1);
            if (modalRects.Count == 0)
            {
                Pan = savedPan;
            }
            else
            {
                ApplyModalPan();
            }
        }

        /// <summary>モーダル用パンが有効な数 (検証・デバッグ用)。</summary>
        public int ModalPanDepthForAudit
        {
            get { return modalRects.Count; }
        }

        private void ApplyModalPan()
        {
            var rect = modalRects[modalRects.Count - 1];
            var view = ViewSize;
            if (view.x <= 0f || view.y <= 0f)
            {
                view = new Vector2(Screen.width, Screen.height);
            }

            // ダイアログ中心 (frame ローカル design px) をビューポート中心へ。
            // Pan setter が ClampPan でクランプするので、画面に収まらない
            // モーダルでも可能な範囲で最大限中央へ寄る。
            Pan = new Vector2(
                view.x * 0.5f - (rect.x + rect.width * 0.5f),
                view.y * 0.5f - (rect.y + rect.height * 0.5f));
        }

        private Vector2 pan;
        private Vector2 viewportSize;
        private RectTransform viewport;
        private Vector2 grabOffset;
        private bool dragging;
        private bool initialPanApplied;
        private bool panSuspended;
        private Vector2 savedPan;
        private readonly List<Rect> modalRects = new List<Rect>();

        private void LateUpdate()
        {
            // ビューポート矩形が変わった (解像度変更・レイアウト) ときに追従する。
            if (Viewport != null && (viewportSize.x <= 0f || viewportSize.y <= 0f))
            {
                viewportSize = Viewport.rect.size;
            }

            // Home/End は入力欄 (Result のニックネーム等) のキャレット操作と
            // 競合するので扱わない。PageUp/PageDown のみ。
            if (!dragging && !PanSuspended
                && (Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.PageDown)))
            {
                HandleKeys();
            }
        }

        private void HandleKeys()
        {
            var size = ViewSize;
            float step = size.y * PageStepRatio;
            // PageUp = 表示範囲の先頭へ (pan.y 増加)、PageDown = 末尾へ。
            // pan.y は content 上端の y_down 位置で 0 が先頭。
            if (Input.GetKeyDown(KeyCode.PageUp)) { Pan = pan + new Vector2(0f, step); }
            else if (Input.GetKeyDown(KeyCode.PageDown)) { Pan = pan + new Vector2(0f, -step); }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (PanSuspended || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            // テキスト入力上のドラッグは文字選択が優先。ここでパンを
            // 始めると選択操作がフレーム移動に化ける。Button 等の
            // Selectable は対象外: スクロール容器の上でドラッグすると
            // クリックがドラッグ扱いになるのは ScrollRect と同じ慣習。
            var press = eventData.pointerPress;
            if (press != null
                && (press.GetComponentInParent<InputField>() != null
                    || press.GetComponentInParent<TMPro.TMP_InputField>() != null))
            {
                return;
            }

            dragging = true;
            grabOffset = PointerDesignPos(eventData) - pan;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (PanSuspended || !dragging)
            {
                return;
            }

            Pan = PointerDesignPos(eventData) - grabOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            dragging = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (PanSuspended)
            {
                return;
            }

            var delta = eventData.scrollDelta;
            // pan は content 左上のビューポート内 y_down 位置 (0=先頭、
            // 負=末尾側)。ホイール上 (delta.y>0) は先頭方向、
            // ホイール右 (delta.x>0) は右方向。リスト側 OnScroll
            // (delta>0 → 先頭へ) と同じ慣習に合わせる (#550)。
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                // Shift+ホイールは横パン (一般的な慣習)。
                Pan = pan + new Vector2(delta.y * ScrollSensitivity, 0f);
                return;
            }

            Pan = pan + new Vector2(-delta.x, delta.y) * ScrollSensitivity;
        }

        /// <summary>
        /// ポインタ位置をビューポート左上原点の design 座標へ変換する。
        /// ビューポートは pivot (0.5,0.5)・画面全体なので、ローカル座標の原点は中央。
        /// </summary>
        private Vector2 PointerDesignPos(PointerEventData eventData)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Viewport, eventData.position, eventData.pressEventCamera, out local))
            {
                return pan;
            }

            var size = ViewSize;
            return new Vector2(local.x + size.x * 0.5f, size.y * 0.5f - local.y);
        }

        private Vector2 ViewSize
        {
            get
            {
                if (viewportSize.x > 0f && viewportSize.y > 0f)
                {
                    return viewportSize;
                }

                return Viewport != null ? Viewport.rect.size : Vector2.zero;
            }
        }

        private void Apply()
        {
            if (Content != null)
            {
                Content.anchoredPosition = new Vector2(pan.x, -pan.y);
            }
        }
    }
}
