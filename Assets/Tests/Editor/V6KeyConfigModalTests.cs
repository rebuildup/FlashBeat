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

            for (int lane = 1; lane <= 8; lane++)
            {
                Assert.IsNotNull(FindChild(modal.gameObject, "Lane Row " + lane),
                    "8 レーンの行が揃っていない: " + lane);
            }

            Assert.IsNotNull(FindButton(modal.gameObject, "Op Reset Keys"));
            Assert.IsNotNull(FindButton(modal.gameObject, "Op Cancel Keys"));
            Assert.IsNotNull(FindButton(modal.gameObject, "Op Apply Keys"));
        }

        [Test]
        public void Rows_ShowTheRepresentativeKey_AndTheAlternates()
        {
            modal.Show();

            GameObject row1 = FindChild(modal.gameObject, "Lane Row 1");
            Assert.AreEqual("1", FindText(row1, "Key").text, "代表キーを KeyCap に出す");
            Assert.AreEqual("他: Q / A / Z", FindText(row1, "Alternates").text,
                "残りのキーを 1 行目に出す");

            GameObject row4 = FindChild(modal.gameObject, "Lane Row 4");
            Assert.AreEqual("4", FindText(row4, "Key").text);
            StringAssert.Contains("5", FindText(row4, "Alternates").text);
        }

        [Test]
        public void ClickingARow_StartsListening_AndApplySavesTheAssignment()
        {
            modal.Show();

            FindButton(modal.gameObject, "Lane Row 3").onClick.Invoke();
            Assert.IsTrue(modal.Logic.Listening, "行のクリックで待受に入る");
            Assert.AreEqual(2, modal.Logic.SelectedLane, "クリックした行が選択される");
            Assert.AreEqual(V6KeyConfigLogic.ListeningText,
                FindText(FindChild(modal.gameObject, "Lane Row 3"), "State").text,
                "待受中の文言を出す");

            Assert.AreEqual(V6KeyAssignResult.Assigned, modal.Logic.Assign(KeyCode.PageUp));
            modal.Refresh();
            Assert.AreEqual("PageUp", FindText(FindChild(modal.gameObject, "Lane Row 3"), "Key").text);

            FindButton(modal.gameObject, "Op Apply Keys").onClick.Invoke();

            Assert.IsFalse(modal.IsOpen, "適用で閉じる");
            Assert.AreEqual(KeyCode.PageUp, KeyBindings.GetKeys(2)[0], "レーン 3 の代表キーが変わる");
            Assert.IsNotNull(storage.Raw, "適用で保存する");
        }

        [Test]
        public void CancelDiscardsTheEdit()
        {
            modal.Show();
            FindButton(modal.gameObject, "Lane Row 3").onClick.Invoke();
            modal.Logic.Assign(KeyCode.PageUp);
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
            FindButton(modal.gameObject, "Lane Row 3").onClick.Invoke();
            modal.Logic.Assign(KeyCode.PageUp);
            modal.Refresh();

            FindButton(modal.gameObject, "Op Reset Keys").onClick.Invoke();

            Assert.AreEqual("3", FindText(FindChild(modal.gameObject, "Lane Row 3"), "Key").text,
                "編集内容が既定へ戻る");
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
