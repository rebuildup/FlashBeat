using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// GameScene のロード表示 (design export: 03d-game-loading)。
    /// 全画面を暗くし、中央に "Loading youtube video ..." とインジケータを出す。
    ///
    /// 進捗 API がパイプラインに無い (MusicManager は準備状態しか公開しない) ので
    /// 不定インジケータ (往復するバー)。origin/113 (PR #117) の LoadingOverlay が
    /// trunk に入ったら <see cref="LoadingOverlayHook"/> をそちらへ差し替えること。
    /// </summary>
    public sealed class GameLoadingView : MonoBehaviour
    {
        private const float CycleSeconds = 1.6f;

        private GameObject root;
        private RectTransform fill;
        private float trackWidth;

        public void Build(Canvas canvas)
        {
            var p = UISizeProfile.Current;
            float s = GameHudView.S;

            root = V5Ui.Node(canvas.gameObject, "Loading");
            V5Ui.Fill(root.GetComponent<RectTransform>(), 0f);
            var bg = root.AddComponent<Image>();
            bg.color = V5Tokens.Bg;

            // Edge Deco (design: 380px 幅の左右装飾, 縦は全面)
            AddEdgeDeco(root, "Edge Left", "V5/Game/edge-left", new Vector2(0f, 0f), new Vector2(0f, 1f));
            AddEdgeDeco(root, "Edge Right", "V5/Game/edge-right", new Vector2(1f, 0f), new Vector2(1f, 1f));

            var col = V5GameUi.Column(root, "Loading", 24f * s, TextAnchor.MiddleCenter);
            var colRt = col.GetComponent<RectTransform>();
            colRt.anchorMin = new Vector2(0.5f, 0.5f);
            colRt.anchorMax = new Vector2(0.5f, 0.5f);
            colRt.pivot = new Vector2(0.5f, 0.5f);
            colRt.anchoredPosition = new Vector2(0f, -40f * s);
            colRt.sizeDelta = new Vector2(600f * s, 120f * s);
            col.childControlWidth = true;

            var row = V5GameUi.Row(col.gameObject, "Row", 16f * s, TextAnchor.MiddleCenter);
            var rowLe = row.gameObject.AddComponent<LayoutElement>();
            rowLe.preferredHeight = p.FsL;
            V5GameUi.Text(row.gameObject, "Label", "Loading youtube video ...", p.FsL, V5Tokens.Text, TextAlignmentOptions.Left);

            var track = V5Ui.Panel(col.gameObject, "Track", new Vector2(600f * s, 8f * s), V5Tokens.Well, V5Tokens.RadiusSmall);
            var trackLe = track.AddComponent<LayoutElement>();
            trackLe.preferredHeight = 8f * s;
            trackWidth = 600f * s;
            fill = V5Ui.Panel(track, "Fill", new Vector2(trackWidth * 0.5f, 8f * s), V5Tokens.Text, V5Tokens.RadiusSmall)
                .GetComponent<RectTransform>();
            fill.anchorMin = new Vector2(0f, 0.5f);
            fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
        }

        private void AddEdgeDeco(GameObject parent, string name, string resource, Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var go = V5Ui.Panel(parent, name, Vector2.zero, V5Tokens.Bg, 1f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            bool left = anchorMax.x < 1f;
            rt.offsetMin = left ? new Vector2(0f, 0f) : new Vector2(-380f * GameHudView.S, 0f);
            rt.offsetMax = left ? new Vector2(380f * GameHudView.S, 0f) : Vector2.zero;
            var sprite = V5GameUi.LoadSprite(resource);
            V5GameUi.SetOrFallback(go, sprite, V5Tokens.Bg, 1f);
        }

        private void Update()
        {
            if (fill == null) return;
            // 不定インジケータ: 幅が 0.15〜0.6 を往復するバー
            float t = Mathf.PingPong(Time.unscaledTime / CycleSeconds, 1f);
            fill.sizeDelta = new Vector2(trackWidth * (0.15f + 0.45f * t), fill.sizeDelta.y);
        }

        public void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible) root.SetActive(visible);
        }
    }

    /// <summary>
    /// ロード表示の差し替えポイント (明示的なフック)。
    /// origin/113 (PR #117) が LoadingOverlay を trunk に入れたら、このクラスを
    /// 削除して呼び出し側をそちらへ向ける。今は <see cref="GameLoadingView"/> が正体。
    /// </summary>
    public static class LoadingOverlayHook
    {
        private static GameLoadingView view;

        public static void Bind(GameLoadingView loadingView)
        {
            view = loadingView;
        }

        public static void Show()
        {
            if (view != null) view.SetVisible(true);
        }

        public static void Hide()
        {
            if (view != null) view.SetVisible(false);
        }
    }
}
