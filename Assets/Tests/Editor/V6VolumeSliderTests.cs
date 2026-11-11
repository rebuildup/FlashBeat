using FlashBeat.Persistence;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Game.Pause;
using FlashBeat.UI.V6.Option;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #375: 共通音量スライダ (design J7tBYP) の値写像と、
    /// Option / Pause が同じ値モデル (GManager) を編集することの検証。
    ///
    /// スライダーの見た目 (フィル幅 / ノブ位置) は値から決まるので、
    /// 写像を固定すれば画面上の値表示と操作結果が一致する。
    /// ドラッグそのものは EditMode では再現しない (ポインタ座標が要る) ため、
    /// 写像 (<see cref="V6VolumeSlider.ValueAt"/>) とキー操作の刻みを検証する。
    /// </summary>
    public class V6VolumeSliderTests
    {
        // --- 値写像 (design: ノブ中心 24 → 464 が 0 → 100%) ---

        [Test]
        public void ValueAt_MapsTrackEnds_AndClampsOutside()
        {
            Assert.AreEqual(0f, V6VolumeSlider.ValueAt(0f), 1e-4f, "トラック左端より外は 0");
            Assert.AreEqual(0f, V6VolumeSlider.ValueAt(V6VolumeSlider.KnobCenterFrom), 1e-4f,
                "ノブ中心の左端 = 0%");
            Assert.AreEqual(0.5f, V6VolumeSlider.ValueAt(244f), 1e-4f, "中央 = 50%");
            Assert.AreEqual(1f, V6VolumeSlider.ValueAt(464f), 1e-4f, "ノブ中心の右端 = 100%");
            Assert.AreEqual(1f, V6VolumeSlider.ValueAt(600f), 1e-4f, "トラック右端より外は 100%");
        }

        [Test]
        public void KnobCenter_FollowsTheDesignFormula_AndRoundTripsThroughValueAt()
        {
            Assert.AreEqual(24f, V6VolumeSlider.KnobCenter(0f), 1e-4f);
            Assert.AreEqual(464f, V6VolumeSlider.KnobCenter(1f), 1e-4f);

            float[] values = { 0f, 0.07f, 0.25f, 0.42f, 0.5f, 0.93f, 1f };
            for (int i = 0; i < values.Length; i++)
            {
                float x = V6VolumeSlider.KnobCenter(values[i]);
                Assert.AreEqual(values[i], V6VolumeSlider.ValueAt(x), 1e-4f,
                    "ノブ位置から値を戻せる (" + values[i] + ")");
            }
        }

        // --- キー操作の刻み (決定 §3.2) ---

        [Test]
        public void Nudge_MovesOnePercent_OrTenPercentWithShift()
        {
            Assert.AreEqual(0.43f, V6VolumeSlider.Nudge(0.42f, 1, false), 1e-4f, "通常は 1%");
            Assert.AreEqual(0.41f, V6VolumeSlider.Nudge(0.42f, -1, false), 1e-4f);
            Assert.AreEqual(0.52f, V6VolumeSlider.Nudge(0.42f, 1, true), 1e-4f, "Shift で 10%");
            Assert.AreEqual(0.32f, V6VolumeSlider.Nudge(0.42f, -1, true), 1e-4f);
        }

        [Test]
        public void Nudge_ClampsTo0And1_AndStaysOnThePercentGrid()
        {
            Assert.AreEqual(0f, V6VolumeSlider.Nudge(0f, -1, false), 1e-4f, "0% 未満にならない");
            Assert.AreEqual(0f, V6VolumeSlider.Nudge(0.05f, -1, true), 1e-4f);
            Assert.AreEqual(1f, V6VolumeSlider.Nudge(1f, 1, false), 1e-4f, "100% を超えない");
            Assert.AreEqual(1f, V6VolumeSlider.Nudge(0.95f, 1, true), 1e-4f);

            // ドラッグ由来の端数値でも、キー操作後は % の格子へ乗る (表示と値がずれない)。
            Assert.AreEqual(0.33f, V6VolumeSlider.Nudge(0.3349f, 0, false), 1e-4f);
        }

        [Test]
        public void Format_ShowsWholePercent()
        {
            Assert.AreEqual("0%", V6VolumeSlider.Format(0f));
            Assert.AreEqual("42%", V6VolumeSlider.Format(0.42f));
            Assert.AreEqual("100%", V6VolumeSlider.Format(1f));
            Assert.AreEqual("100%", V6VolumeSlider.Format(1.4f), "範囲外もクランプして表示する");
            Assert.AreEqual("42%", V6OptionLogic.FormatVolume(0.42f), "行の表示とスライダーの表示は同じ");
        }

        // --- Option: 行とスライダーが同じ値モデルを編集する ---

        [Test]
        public void OptionLogic_NudgeVolume_UsesTheSliderSteps()
        {
            Assert.AreEqual(0.51f, V6OptionLogic.NudgeVolume(0.5f, 1, false), 1e-4f);
            Assert.AreEqual(0.6f, V6OptionLogic.NudgeVolume(0.5f, 1, true), 1e-4f);
            Assert.AreEqual(0.6f, V6OptionLogic.AdjustVolume(0.5f, 1), 1e-4f,
                "旧ステッパー経路も同じ刻み (Shift 相当) を使う");
        }

        [Test]
        public void OptionSoundTab_BuildsTheSliderOverTheLiveValue_AndKeepsItClearOfTheDescription()
        {
            float original = GManager.mainVolume;
            var parent = new GameObject("V6 Volume Slider Option Parent", typeof(RectTransform));
            V6OptionView view = null;
            try
            {
                GManager.mainVolume = 0.42f;
                view = V6OptionView.Create(parent.GetComponent<RectTransform>(), () => { });
                ClickButton(view.Root.gameObject, "Tab Sound");

                GameObject slider = FindChild(view.Root.gameObject, "Volume Slider");
                Assert.IsNotNull(slider, "Sound タブのプレビューに共通スライダーが載る");

                TextMeshProUGUI value = FindText(slider, "Value");
                Assert.AreEqual("42%", value.text, "スライダーは GManager の現在値を表示する");
                Assert.IsNotNull(FindText(slider, "Hint"), "操作ヒントを出す");

                // 行の ▶ でもスライダーが追従する (同じ値モデル)。
                ClickButton(FindChild(view.Root.gameObject, "Set Main Volume"), "IconButton chevron-right");
                Assert.AreEqual(0.43f, GManager.mainVolume, 1e-4f, "行の ▶ は 1% 動かす");
                Assert.AreEqual("43%", FindText(slider, "Value").text, "スライダー表示が追従する");

                // 説明ブロックはスライダーの下に置く (design の更新に合わせた配置)。
                Assert.LessOrEqual(V6OptionLogic.VolumeSliderY + V6VolumeSlider.Height,
                    V6OptionLogic.SoundDescDividerY, "スライダーと説明の区切りが重ならない");
            }
            finally
            {
                GManager.mainVolume = original;
                if (view != null)
                {
                    view.Dispose();
                }
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void OptionTabsWithoutVolume_HaveNoSlider()
        {
            var parent = new GameObject("V6 Volume Slider Option Tabs Parent", typeof(RectTransform));
            V6OptionView view = null;
            try
            {
                view = V6OptionView.Create(parent.GetComponent<RectTransform>(), () => { });

                ClickButton(view.Root.gameObject, "Tab Data");
                Assert.IsNull(FindChild(view.Root.gameObject, "Volume Slider"), "Data タブにスライダーは無い");

                ClickButton(view.Root.gameObject, "Tab Play");
                Assert.IsNull(FindChild(view.Root.gameObject, "Volume Slider"), "Play タブにスライダーは無い");

                ClickButton(view.Root.gameObject, "Tab Sound");
                Assert.IsNotNull(FindChild(view.Root.gameObject, "Volume Slider"), "Sound タブにだけ載る");
            }
            finally
            {
                if (view != null)
                {
                    view.Dispose();
                }
                Object.DestroyImmediate(parent);
            }
        }

        // --- Pause: Sound タブはキー一覧の代わりに同じスライダーを出す ---

        [Test]
        public void PauseSoundTab_SwapsTheKeyListForTheSlider_AndEditsTheSelectedRow()
        {
            float main = GManager.mainVolume;
            float se = GManager.effectVolume;
            bool hadMain = PlayerPrefs.HasKey(OptionSettings.MainVolumeKey);
            float savedMain = PlayerPrefs.GetFloat(OptionSettings.MainVolumeKey, 0f);
            bool hadSe = PlayerPrefs.HasKey(OptionSettings.EffectVolumeKey);
            float savedSe = PlayerPrefs.GetFloat(OptionSettings.EffectVolumeKey, 0f);

            var parent = new GameObject("V6 Volume Slider Pause Parent", typeof(RectTransform));
            V6PauseView view = null;
            try
            {
                view = V6PauseView.Create(parent.GetComponent<RectTransform>());
                Assert.IsNotNull(view, "v6 ポーズが生成される");
                view.Open();
                try
                {
                    GameObject slider = FindChild(view.Root.gameObject, "Volume Slider");
                    GameObject keyList = FindChild(view.Root.gameObject, "Key List");
                    Assert.IsNotNull(slider, "共有プレビューパネルにスライダーがある");
                    Assert.IsNotNull(keyList, "キー一覧のコンテナがある");

                    view.SelectTab(2); // Sound
                    Assert.IsTrue(slider.activeInHierarchy, "Sound タブはスライダーを出す");
                    Assert.IsFalse(keyList.activeInHierarchy, "Sound タブはキー一覧を隠す");
                    Assert.AreEqual("音量", FindText(view.Root.gameObject, "Desc Title").text,
                        "Sound タブの説明題は「音量」");

                    view.SelectTab(0); // Play
                    Assert.IsFalse(slider.activeInHierarchy, "Play タブはスライダーを隠す");
                    Assert.IsTrue(keyList.activeInHierarchy, "Play タブはキー一覧を出す");

                    // Main 行の ←→ がスライダーと GManager の両方へ届く。
                    view.SelectTab(2);
                    GManager.mainVolume = 0.5f;
                    view.Refresh();
                    Assert.AreEqual("50%", FindText(slider, "Value").text, "スライダーは選択行の値を表示する");
                    view.AdjustSelected(1);
                    Assert.AreEqual(0.51f, GManager.mainVolume, 1e-4f, "←→ は 1% 動かす");
                    Assert.AreEqual("51%", FindText(slider, "Value").text, "スライダー表示が追従する");

                    // SE 行へ移ると同じスライダーが SE の値を表示する。
                    GManager.effectVolume = 0.24f;
                    view.MoveSelection(1);
                    Assert.AreEqual("24%", FindText(slider, "Value").text, "選択行が変わると表示値も変わる");
                }
                finally
                {
                    view.Close();
                }
            }
            finally
            {
                GManager.mainVolume = main;
                GManager.effectVolume = se;
                RestoreFloat(OptionSettings.MainVolumeKey, hadMain, savedMain);
                RestoreFloat(OptionSettings.EffectVolumeKey, hadSe, savedSe);
                if (view != null)
                {
                    view.Dispose();
                }
                Object.DestroyImmediate(parent);
            }
        }

        // --- ヘルパ ---

        private static void RestoreFloat(string key, bool existed, float value)
        {
            if (existed)
            {
                PlayerPrefs.SetFloat(key, value);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
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

        private static void ClickButton(GameObject root, string name)
        {
            Button button = FindButton(root, name);
            Assert.IsNotNull(button, "ボタンが見つからない: " + name);
            button.onClick.Invoke();
        }
    }
}
