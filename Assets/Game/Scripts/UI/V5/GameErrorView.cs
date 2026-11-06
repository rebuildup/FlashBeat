using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// YouTube の動画再生に失敗したときのダイアログ (design export: 03e)。
    ///表示の切替は GameHudRuntime (HudPlayState の観測) が行い、
    /// このクラスは見た目と Esc / R の導線だけを持つ。
    /// </summary>
    public sealed class GameErrorView : MonoBehaviour
    {
        private GameObject root;
        private CanvasGroup group;
        private Button backButton;
        private Button retryButton;
        private bool leaving;

        public void Build(Canvas canvas)
        {
            float s = GameHudView.S;
            var p = UISizeProfile.Current;

            root = V5Ui.Node(canvas.gameObject, "Error Dialog");
            root.SetActive(false);
            group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            var panel = V5Ui.Panel(root, "Panel", new Vector2(800f * s, 0f), V5Tokens.Raised2, 16f * s);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;

            var vle = panel.AddComponent<VerticalLayoutGroup>();
            vle.padding = new RectOffset(Mathf.RoundToInt(32f * s), Mathf.RoundToInt(32f * s),
                Mathf.RoundToInt(32f * s), Mathf.RoundToInt(32f * s));
            vle.spacing = 16f * s;
            vle.childControlWidth = true;
            vle.childControlHeight = false;
            vle.childForceExpandWidth = false;
            vle.childForceExpandHeight = false;
            vle.childAlignment = TextAnchor.UpperLeft;

            // Head: 警告アイコン + タイトル + サブタイトル
            var head = V5GameUi.Row(panel, "Head", 16f * s, TextAnchor.MiddleLeft);
            var headLe = head.gameObject.AddComponent<LayoutElement>();
            headLe.flexibleWidth = 1f;
            head.childControlWidth = true;
            var icon = V5GameUi.Text(head.gameObject, "Icon", "▲", 48f * s, V5Tokens.Danger, TextAlignmentOptions.Center);
            var iconLe = icon.gameObject.AddComponent<LayoutElement>();
            iconLe.minWidth = 48f * s;
            var textCol = V5GameUi.Column(head.gameObject, "Text", 4f * s, TextAnchor.MiddleLeft);
            textCol.childControlWidth = true;
            var textLe = textCol.gameObject.AddComponent<LayoutElement>();
            textLe.flexibleWidth = 1f;
            V5GameUi.Text(textCol.gameObject, "Title", "動画を再生できません", p.FsL, V5Tokens.Text, TextAlignmentOptions.Left);
            V5GameUi.Text(textCol.gameObject, "Subtitle", "YouTube エラー 403", p.FsS, V5Tokens.TextMuted, TextAlignmentOptions.Left);

            var body = V5GameUi.Text(panel, "Body",
                "通信状態を確認して、もう一度お試しください。\n譜面と音楽は読み込まれています。",
                p.FsM, V5Tokens.TextMuted, TextAlignmentOptions.Left);
            var bodyLe = body.gameObject.AddComponent<LayoutElement>();
            bodyLe.flexibleWidth = 1f;

            // Buttons (右寄せ)
            var buttonHeight = p.FsM + 28f * s;
            var buttons = V5GameUi.Row(panel, "Buttons", 12f * s, TextAnchor.MiddleRight);
            buttons.childControlWidth = false;
            var buttonsLe = buttons.gameObject.AddComponent<LayoutElement>();
            buttonsLe.flexibleWidth = 1f;
            buttonsLe.preferredHeight = buttonHeight;
            var back = V5Ui.ButtonSecondary(buttons.gameObject, "← 選曲へ", new Vector2(220f * s, buttonHeight));
            backButton = back.GetComponent<Button>();
            AddKeyCap(buttons.gameObject, "Esc", buttonHeight * 0.62f);
            var retry = V5Ui.ButtonPrimary(buttons.gameObject, "Retry", new Vector2(220f * s, buttonHeight));
            retryButton = retry.GetComponent<Button>();
            AddKeyCap(buttons.gameObject, "R", buttonHeight * 0.62f);

            if (backButton != null) backButton.onClick.AddListener(BackToSelect);
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
        }

        private static void AddKeyCap(GameObject parent, string label, float height)
        {
            var cap = V5Ui.KeyCap(parent, label, new Vector2(height * 2.2f, height));
            var le = cap.AddComponent<LayoutElement>();
            le.minWidth = height * 2.2f;
            le.preferredHeight = height;
        }

        public void Show()
        {
            leaving = false;
            if (root != null && !root.activeSelf) root.SetActive(true);
            if (group != null) group.blocksRaycasts = true;
        }

        public void Hide()
        {
            if (group != null) group.blocksRaycasts = false;
            if (root != null && root.activeSelf) root.SetActive(false);
        }

        public bool IsShown
        {
            get { return root != null && root.activeSelf; }
        }

        private void Update()
        {
            if (!IsShown) return;
            if (Input.GetKeyDown(KeyCode.Escape)) BackToSelect();
            if (Input.GetKeyDown(KeyCode.R)) Retry();
        }

        private void BackToSelect()
        {
            if (leaving) return;
            leaving = true;
            Hide();
            Initiate.Fade("SelectScene", Color.black, 1f);
        }

        private void Retry()
        {
            if (leaving) return;
            leaving = true;
            Hide();
            Initiate.Fade("GameScene", Color.black, 1f);
        }
    }
}
