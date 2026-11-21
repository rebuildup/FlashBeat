using FlashBeat.UI.V6.Common;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #376: キー設定モーダル (design vo4kB) の組み立て・マウス操作・適用/キャンセル。
    /// 表示位置と行の内容が design と一致し、行クリック → 適用で
    /// <see cref="KeyBindings"/> が更新されることを固定する。
    /// </summary>
    public class V6KeyConfigModalTests
    {
        private KeyBindings.IStorage previousStorage;
        private MemoryStorage storage;
        private GameObject parent;
        private V6KeyConfigModal modal;

        private sealed class MemoryStorage : KeyBindings.IStorage
        {
            public string Raw;

            public string LoadRaw()
            {
                return Raw;
            }

            public void SaveRaw(string json)
            {
                Raw = json;
            }
        }

        [SetUp]
        public void SetUp()
        {
            previousStorage = null;
            storage = new MemoryStorage();
            KeyBindings.SetStorage(storage);

            parent = new GameObject("V6 Key Config Modal Test Parent", typeof(RectTransform));
            parent.GetComponent<RectTransform>().sizeDelta = new Vector2(V6Frame.DesignW, V6Frame.DesignH);
            modal = V6KeyConfigModal.Build(parent);
        }

        [TearDown]
        public void TearDown()
        {
            if (modal != null)
            {
                modal.Hide();
            }

            KeyBindings.SetStorage(previousStorage);
            Object.DestroyImmediate(parent);
        }

        [Test]
        public void Build_MatchesTheDesignFrame_AndStaysHiddenUntilShown()
        {
            Assert.IsFalse(modal.IsOpen, "生成直後は閉じている");
            Assert.IsFalse(modal.gameObject.activeSelf, "表示しない");

            modal.Show();
            Assert.IsTrue(modal.IsOpen);
            Assert.IsTrue(modal.gameObject.activeSelf);

            GameObject dialog = FindChild(modal.gameObject, "Dialog");
            Assert.IsNotNull(dialog, "Dialog が無い");
            var rt = dialog.GetComponent<RectTransform>();
            Assert.AreEqual(V6KeyConfigModal.DialogW, rt.sizeDelta.x, 0.5f);
            Assert.AreEqual(V6KeyConfigModal.DialogH, rt.sizeDelta.y, 0.5f);
            Assert.AreEqual(V6KeyConfigModal.DialogX, rt.anchoredPosition.x, 0.5f);
            Assert.AreEqual(-V6KeyConfigModal.DialogY, rt.anchoredPosition.y, 0.5f);

            GameObject scrim = FindChild(modal.gameObject, "Scrim");
            Assert.IsNotNull(scrim, "Scrim が無い");
            Assert.AreEqual(V6Frame.DesignW, scrim.GetComponent<RectTransform>().sizeDelta.x, 0.5f);

            // Issue #428: テキスト行ではなく JIS ビジュアルキーボード。
            Assert.IsNotNull(FindChild(modal.gameObject, "Key 1"), "数字キーのキャップが無い");
            Assert.IsNotNull(FindChild(modal.gameObject, "Key Q"), "QWERTY 行のキャップが無い");
            Assert.IsNotNull(FindChild(modal.gameObject, "Key ;"), "記号キーのキャップが無い");
            Assert.IsNotNull(FindChild(modal.gameObject, "Status"), "Status が無い");
            Assert.IsNotNull(FindChild(modal.gameObject, "Hint"), "Hint が無い");

            Assert.IsNotNull(FindButton(modal.gameObject, "Op Reset Keys"));
            Assert.IsNotNull(FindButton(modal.gameObject, "Op Cancel Keys"));
            Assert.IsNotNull(FindButton(modal.gameObject, "Op Apply Keys"));
        }

        [Test]
        public void KeyCaps_ShowLaneBadges_ForAssignedKeys()
        {
            modal.Show();

            // レーン 1 の代表キー 1 と残りキー Q / A / Z はすべてバッジ "1"。
            Assert.AreEqual("1", FindText(FindChild(modal.gameObject, "Key 1"), "Lane").text);
            Assert.AreEqual("1", FindText(FindChild(modal.gameObject, "Key Q"), "Lane").text);
            Assert.AreEqual("1", FindText(FindChild(modal.gameObject, "Key A"), "Lane").text);
            Assert.AreEqual("1", FindText(FindChild(modal.gameObject, "Key Z"), "Lane").text);

            // レーン 4 は代表 4 と 5 を持つ。
            Assert.AreEqual("4", FindText(FindChild(modal.gameObject, "Key 4"), "Lane").text);
            Assert.AreEqual("4", FindText(FindChild(modal.gameObject, "Key 5"), "Lane").text);
        }

        [Test]
        public void ClickingAKeyCap_SelectsTheLane_AndApplySavesTheAssignment()
        {
            modal.Show();

            // "3" はレーン 3 (index 2) の代表キー。クリックでそのレーンを選択する。
            FindButton(modal.gameObject, "Key 3").onClick.Invoke();
            Assert.AreEqual(2, modal.Logic.SelectedLane, "クリックしたキーのレーンが選択される");

            // Enter 経路は EditMode で Input を回せないので、待受開始はモデル経由。
            modal.Logic.BeginListen();
            modal.Refresh();
            Assert.IsTrue(modal.Logic.Listening, "待受に入る");
            Assert.AreEqual(V6KeyConfigLogic.ListeningText,
                FindText(FindChild(modal.gameObject, "Status"), "Status").text,
                "待受中の文言を出す");

            // 未割当の割当可能キー (') を代表キーへ。
            Assert.AreEqual(V6KeyAssignResult.Assigned, modal.Logic.Assign(KeyCode.Quote));
            modal.Refresh();
            Assert.AreEqual("3", FindText(FindChild(modal.gameObject, "Key '"), "Lane").text,
                "割り当てたキーにレーンバッジが出る");

            FindButton(modal.gameObject, "Op Apply Keys").onClick.Invoke();

            Assert.IsFalse(modal.IsOpen, "適用で閉じる");
            Assert.AreEqual(KeyCode.Quote, KeyBindings.GetKeys(2)[0], "レーン 3 の代表キーが変わる");
            Assert.IsNotNull(storage.Raw, "適用で保存する");
        }

        [Test]
        public void CancelDiscardsTheEdit()
        {
            modal.Show();
            FindButton(modal.gameObject, "Key 3").onClick.Invoke();
            modal.Logic.BeginListen();
            modal.Logic.Assign(KeyCode.Quote);
            modal.Refresh();

            FindButton(modal.gameObject, "Op Cancel Keys").onClick.Invoke();

            Assert.IsFalse(modal.IsOpen);
            Assert.AreEqual(KeyCode.Alpha3, KeyBindings.GetKeys(2)[0], "キャンセルでは変更しない");
            Assert.IsNull(storage.Raw, "キャンセルでは保存しない");
        }

        [Test]
        public void ResetButton_RestoresTheDefaults_InTheModalOnly()
        {
            modal.Show();
            FindButton(modal.gameObject, "Key 3").onClick.Invoke();
            modal.Logic.BeginListen();
            modal.Logic.Assign(KeyCode.Quote);
            modal.Refresh();

            FindButton(modal.gameObject, "Op Reset Keys").onClick.Invoke();

            Assert.AreEqual("3", FindText(FindChild(modal.gameObject, "Key 3"), "Lane").text,
                "編集内容が既定へ戻る");
            Assert.AreEqual(string.Empty,
                FindText(FindChild(modal.gameObject, "Key '"), "Lane").text,
                "割り当て直したキーのバッジは消える");
            Assert.AreEqual(KeyCode.Alpha3, KeyBindings.GetKeys(2)[0], "適用まで KeyBindings は変えない");
            Assert.IsNull(storage.Raw);
        }

        [Test]
        public void BlocksInput_IsTrueWhileOpen_AndOnTheClosingFrame()
        {
            Assert.IsFalse(V6KeyConfigModal.BlocksInput, "閉じている間は背後の操作を妨げない");
            modal.Show();
            Assert.IsTrue(V6KeyConfigModal.BlocksInput);
            modal.Hide();
            Assert.IsFalse(V6KeyConfigModal.BlocksInput, "フレームが変われば解除される");
        }

        /// <summary>
        /// Issue #862: JIS ビジュアルキーボードの全キーキャップが Dialog の
        /// 内側に収まること。最も広い行が LaneRowW を超えると中央揃えの
        /// はみ出し分がダイアログ枠の外へ描画される (マスク無し) ため、
        /// 配置側で一様縮小して収める契約を固定する。
        /// </summary>
        [Test]
        public void KeyboardKeys_StayInsideTheDialog()
        {
            modal.Show();
            GameObject dialog = FindChild(modal.gameObject, "Dialog");
            Assert.IsNotNull(dialog, "Dialog が無い");
            var dialogRt = dialog.GetComponent<RectTransform>();
            float dialogW = dialogRt.sizeDelta.x;
            float dialogH = dialogRt.sizeDelta.y;

            int count = 0;
            foreach (Transform child in dialog.transform)
            {
                if (!child.name.StartsWith("Key ")) { continue; }
                var rt = child as RectTransform;
                Assert.IsNotNull(rt, child.name + " が RectTransform でない");
                // anchor/pivot (0,1): anchoredPosition = (左, -上)。
                float left = rt.anchoredPosition.x;
                float top = -rt.anchoredPosition.y;
                float right = left + rt.sizeDelta.x;
                float bottom = top + rt.sizeDelta.y;
                Assert.GreaterOrEqual(left, -0.5f,
                    child.name + " が Dialog の左を越える (left=" + left.ToString("F1") + ")");
                Assert.LessOrEqual(right, dialogW + 0.5f,
                    child.name + " が Dialog の右を越える (right=" + right.ToString("F1") + ")");
                Assert.GreaterOrEqual(top, -0.5f,
                    child.name + " が Dialog の上を越える (top=" + top.ToString("F1") + ")");
                Assert.LessOrEqual(bottom, dialogH + 0.5f,
                    child.name + " が Dialog の下を越える (bottom=" + bottom.ToString("F1") + ")");
                count++;
            }

            Assert.Greater(count, 50, "JIS キーボードのキャップが見つからない");
        }

        // --- ヘルパ ---

        private static GameObject FindChild(GameObject root, string name)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == name)
                {
                    return children[i].gameObject;
                }
            }

            return null;
        }

        private static Button FindButton(GameObject root, string name)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].gameObject.name == name)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private static TextMeshProUGUI FindText(GameObject root, string name)
        {
            TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].gameObject.name == name)
                {
                    return texts[i];
                }
            }

            return null;
        }
    }
}
