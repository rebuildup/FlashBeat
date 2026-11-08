using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Game.Playfield;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// v6 ゲーム画面 (プレイフィールド + HUD) の起動 (Issue #223 統合担当)。
    ///
    /// 描画の積層 (v6 デザイン `Uc9v6` / `nrfw8` の順):
    ///   1. 動画 (3D / VideoPlayer が描く)
    ///   2. Video Dim  #00000080  ← 別 Canvas (sorting 300)
    ///   3. プレイフィールド (レーン線 / ノーツ / 判定ライン)  ← Canvas (sorting 400)
    ///   4. HUD (グレード / スコア / コンボ / キーガイド / 歌詞)   ← Canvas (sorting 500)
    ///
    /// 旧 3D プレイフィールド (GameScene の "Lane" / "JudgeLine" / "LaneLight*") は
    /// v6 有効時は隠す (2 枚同時に描かれると崩れて見える)。
    /// 判定・入力・譜面データ (Judge / Notes / NotesManager) には触らない。
    /// </summary>
    public sealed class V6GameRuntime : MonoBehaviour
    {
        private const string GameSceneName = "GameScene";

        private static bool subscribed;

        private V6PlayfieldView playfield;
        private V6GameHudView hud;
        private GameObject dimCanvas;
        private Image dimImage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!subscribed)
            {
                subscribed = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            if (SceneManager.GetActiveScene().name == GameSceneName) Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == GameSceneName) Ensure();
        }

        private static void Ensure()
        {
            if (!V6UiMode.GameHud) return;
            if (FindObjectOfType<V6GameRuntime>() != null) return;

            var go = new GameObject("V6GameUi");
            go.AddComponent<V6GameRuntime>();
        }

        private void Start()
        {
            BuildDimLayer();
            BuildPlayfieldLayer();
            BuildHudLayer();
            HideLegacyPlayfield();
        }

        private void Update()
        {
            if (hud != null) hud.Refresh();
        }

        /// <summary>動画を減光するレイヤー (プレイフィールドより下)。</summary>
        private void BuildDimLayer()
        {
            var canvas = FlashBeat.UI.V5.V5GameUi.NewCanvas("V6GameDimCanvas", 300);
            dimCanvas = canvas.gameObject;
            dimCanvas.transform.SetParent(transform, false);

            var go = new GameObject("Video Dim", typeof(RectTransform));
            go.transform.SetParent(dimCanvas.transform, false);
            dimImage = go.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 128f / 255f); // #00000080
            dimImage.raycastTarget = false;
            var rt = dimImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void BuildPlayfieldLayer()
        {
            var canvas = FlashBeat.UI.V5.V5GameUi.NewCanvas("V6GamePlayfieldCanvas", 400);
            canvas.gameObject.transform.SetParent(transform, false);
            playfield = V6PlayfieldView.Create(canvas.GetComponent<RectTransform>());
        }

        private void BuildHudLayer()
        {
            var canvas = FlashBeat.UI.V5.V5GameUi.NewCanvas("V6GameHudCanvas", 500);
            canvas.gameObject.transform.SetParent(transform, false);
            hud = V6GameHudView.Create(canvas.GetComponent<RectTransform>());

            // HUD 側の減光は自分のレイヤー (Video Dim canvas) が出す。
            // HUD 内の同名 Image は使わない (順序が崩れるため)。
            DisableInternalDim();
        }

        private void DisableInternalDim()
        {
            if (hud == null || hud.Root == null) return;
            Transform found = FindChild(hud.Root, "Video Dim");
            if (found == null) return;
            var img = found.GetComponent<Image>();
            if (img != null) img.enabled = false;
            found.gameObject.SetActive(false);
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindChild(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>旧 3D のプレイフィールド (板 / 判定線 / レーン光) を隠す。</summary>
        private void HideLegacyPlayfield()
        {
            HideByName("Lane");
            HideByName("JudgeLine");
            for (int i = 1; i <= 8; i++)
            {
                HideByName("LaneLight" + i);
            }
        }

        private static void HideByName(string name)
        {
            var obj = GameObject.Find(name);
            if (obj != null) obj.SetActive(false);
        }
    }
}
