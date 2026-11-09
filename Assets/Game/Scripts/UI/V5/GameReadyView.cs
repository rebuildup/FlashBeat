using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// GameScene の Ready 状態 (design export: 03c-game-ready)。
    /// Space 待ちの間だけ出る: 右上に曲情報 (タイトル/アーティスト/BPM/Lv + ジャケット)、
    /// 中央に "Space to Start"、その下に Esc の説明。
    /// 可視の正本は <see cref="GameUiExclusivity"/> (Issue #233)。再生が始まったら
    /// GameHudController が非表示にする。レガシーの SpacetoStart も GameHudRuntime が隠す。
    /// </summary>
    public sealed class GameReadyView : MonoBehaviour
    {
        private GameObject root;
        private Canvas canvas;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI metaText;
        private Image jacketImage;
        private string loadedVideoId;

        public void Build(Canvas canvas)
        {
            this.canvas = canvas;
            var p = UISizeProfile.Current;
            float s = GameHudView.S;

            root = V5Ui.Node(canvas.gameObject, "Ready");
            var rt = root.GetComponent<RectTransform>();
            V5Ui.Fill(rt, 0f);

            // 右上: 曲情報 (タイトル/メタ + ジャケット 192x108)
            var song = V5GameUi.Row(root, "Song", 20f * s, TextAnchor.UpperRight);
            var songRt = song.GetComponent<RectTransform>();
            songRt.anchorMin = new Vector2(1f, 1f);
            songRt.anchorMax = new Vector2(1f, 1f);
            songRt.pivot = new Vector2(1f, 1f);
            songRt.anchoredPosition = new Vector2(-p.Margin, -p.Margin);
            songRt.sizeDelta = new Vector2(600f * s, 108f * s);
            song.childAlignment = TextAnchor.UpperRight;
            song.childControlWidth = true;
            song.childControlHeight = false;

            var textCol = V5GameUi.Column(song.gameObject, "Text", 8f * s, TextAnchor.UpperRight);
            textCol.childControlWidth = true;
            var textLe = textCol.gameObject.AddComponent<LayoutElement>();
            textLe.flexibleWidth = 1f;
            titleText = V5GameUi.Text(textCol.gameObject, "Title", "", p.FsL * 1.37f, V5Tokens.Text, TextAlignmentOptions.Right, FontStyles.Normal);
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            titleText.overflowMode = TextOverflowModes.Ellipsis;
            var titleLe = titleText.gameObject.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;
            metaText = V5GameUi.Text(textCol.gameObject, "Meta", "", p.FsL * 0.79f, V5Tokens.TextMuted, TextAlignmentOptions.Right);
            metaText.textWrappingMode = TextWrappingModes.NoWrap;
            metaText.overflowMode = TextOverflowModes.Ellipsis;
            var metaLe = metaText.gameObject.AddComponent<LayoutElement>();
            metaLe.flexibleWidth = 1f;

            var jacket = V5Ui.Panel(song.gameObject, "Jacket", new Vector2(192f * s, 108f * s), V5Tokens.Key2, V5Tokens.RadiusMedium);
            var jacketLe = jacket.AddComponent<LayoutElement>();
            jacketLe.preferredWidth = 192f * s;
            jacketLe.preferredHeight = 108f * s;
            jacketImage = jacket.GetComponent<Image>();
            jacketImage.raycastTarget = false;

            // 中央: [Space] to Start
            var prompt = V5GameUi.Row(root, "Prompt", 24f * s, TextAnchor.MiddleCenter);
            var promptRt = prompt.GetComponent<RectTransform>();
            promptRt.anchorMin = new Vector2(0f, 0.5f);
            promptRt.anchorMax = new Vector2(1f, 0.5f);
            promptRt.pivot = new Vector2(0.5f, 1f);
            promptRt.anchoredPosition = new Vector2(0f, 100f * s);
            promptRt.sizeDelta = new Vector2(0f, 68f * s);
            // キャップは KeyActions の現在の割当から生成する (#146/#148)
            V5Ui.KeyCap(prompt.gameObject, ActionKeyHint.Primary(KeyActions.KeyAction.Play), new Vector2(168f * s, 56f * s));
            V5GameUi.Text(prompt.gameObject, "to Start", "to Start", p.FsL * 1.79f, V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Normal);

            // 中央下: [Esc] メニュー (設定・表示・ランキング)
            var hint = V5GameUi.Row(root, "Hint", 12f * s, TextAnchor.MiddleCenter);
            var hintRt = hint.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0f, 0.5f);
            hintRt.anchorMax = new Vector2(1f, 0.5f);
            hintRt.pivot = new Vector2(0.5f, 1f);
            hintRt.anchoredPosition = new Vector2(0f, 20f * s);
            hintRt.sizeDelta = new Vector2(0f, 36f * s);
            V5GameUi.LegendItem(hint.gameObject, ActionKeyHint.Primary(KeyActions.KeyAction.Back), "メニュー (設定・表示・ランキング)");
        }

        /// <summary>
        /// 表示 / 非表示。正本は <see cref="GameUiExclusivity"/> (Issue #233)。
        /// Update が自前で可視を切り替える方式は廃止した: 後から再適用できる
        /// 単一の状態配線に集約する (Canvas ごと止めるので、隠れている間は
        /// Update / コルーチンも止まる)。
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (canvas != null && canvas.gameObject.activeSelf != visible)
            {
                canvas.gameObject.SetActive(visible);
            }
        }

        private void Update()
        {
            if (root == null || !root.activeInHierarchy) return;

            var song = GManager.GetSong(GManager.songID);
            if (song == null) return;
            titleText.text = song.title;
            metaText.text = song.musician + " · BPM " + song.bpm + " · Lv " + song.level;
            if (loadedVideoId != song.videoId)
            {
                loadedVideoId = song.videoId;
                StartCoroutine(FetchJacket(song.videoId));
            }
        }

        /// <summary>ジャケットは YouTube サムネイル (Reality: 実曲)。失敗時はプレースホルダのまま。</summary>
        private IEnumerator FetchJacket(string videoId)
        {
            jacketImage.sprite = null;
            jacketImage.type = Image.Type.Sliced;
            jacketImage.sprite = V5Ui.RoundedSprite(Mathf.RoundToInt(V5Tokens.RadiusMedium));
            jacketImage.color = V5Tokens.Key2;
            if (string.IsNullOrEmpty(videoId)) yield break;

            var url = "https://img.youtube.com/vi/" + videoId + "/hqdefault.jpg";
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;
                var tex = DownloadHandlerTexture.GetContent(req);
                if (jacketImage == null || loadedVideoId != videoId) yield break;
                jacketImage.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                jacketImage.type = Image.Type.Simple;
                jacketImage.preserveAspect = true;
                jacketImage.color = Color.white;
            }
        }
    }
}
