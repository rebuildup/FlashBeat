using System.Collections.Generic;
using FlashBeat.UI.V6.Option;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #415 (第3段階): Option 画面が Refresh / 行選択のたびに
    /// Header/Footer/タブ帯/設定行/プレビューの骨格を破棄・再生成せず、
    /// 既存 GameObject を保持したまま値と選択色だけを更新することを固定する。
    /// タブ切替では Settings / Preview パネルだけが作り直され、
    /// Header/Footer/タブ帯は同一実体のまま残る。
    /// </summary>
    public class V6OptionViewReuseTests
    {
        private GameObject parent;
        private V6OptionView view;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("Parent", typeof(RectTransform));
            view = V6OptionView.Create(parent.GetComponent<RectTransform>(), () => { });
        }

        [TearDown]
        public void TearDown()
        {
            if (view != null)
            {
                view.Dispose();
            }

            if (parent != null)
            {
                Object.DestroyImmediate(parent);
            }
        }

        private static List<GameObject> Children(Transform parent)
        {
            var list = new List<GameObject>();
            if (parent == null)
            {
                return list;
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                list.Add(parent.GetChild(i).gameObject);
            }
            return list;
        }

        private Transform Find(Transform root, string path)
        {
            return root == null ? null : root.Find(path);
        }

        private GameObject Zone(string name)
        {
            Transform t = Find(view.Root, name);
            return t != null ? t.gameObject : null;
        }

        private List<GameObject> SettingRows()
        {
            Transform panel = Find(view.Root, "Main/Settings");
            var rows = new List<GameObject>();
            if (panel == null)
            {
                return rows;
            }
            for (int i = 0; i < panel.childCount; i++)
            {
                if (panel.GetChild(i).name.StartsWith("Set "))
                {
                    rows.Add(panel.GetChild(i).gameObject);
                }
            }
            return rows;
        }

        [Test]
        public void Refresh_ReusesSameScaffoldAndRows()
        {
            Assert.AreEqual(1, view.RebuildCountForAudit, "初回構築が Rebuild として数えられていない");

            GameObject headerTitle = Find(view.Root, "Header/Title")?.gameObject;
            GameObject tabStrip = Find(view.Root, "Main/Zone Tabs")?.gameObject;
            GameObject settingsPanel = Find(view.Root, "Main/Settings")?.gameObject;
            GameObject previewPanel = Find(view.Root, "Main/Preview")?.gameObject;
            var rowsBefore = SettingRows();
            Assert.Greater(rowsBefore.Count, 0, "設定行が生成されていない");
            Assert.IsNotNull(headerTitle);
            Assert.IsNotNull(tabStrip);
            Assert.IsNotNull(settingsPanel);
            Assert.IsNotNull(previewPanel);

            view.Refresh();
            view.Refresh();

            var rowsAfter = SettingRows();
            Assert.AreEqual(rowsBefore.Count, rowsAfter.Count);
            for (int i = 0; i < rowsBefore.Count; i++)
            {
                Assert.AreSame(rowsBefore[i], rowsAfter[i],
                    "slot " + i + " の設定行が再生成されている (Issue #415)");
            }

            Assert.AreSame(headerTitle, Find(view.Root, "Header/Title")?.gameObject,
                "ヘッダーが再生成されている");
            Assert.AreSame(tabStrip, Find(view.Root, "Main/Zone Tabs")?.gameObject,
                "タブ帯が再生成されている");
            Assert.AreSame(settingsPanel, Find(view.Root, "Main/Settings")?.gameObject,
                "Settings パネルが再生成されている");
            Assert.AreSame(previewPanel, Find(view.Root, "Main/Preview")?.gameObject,
                "Preview パネルが再生成されている");

            Assert.AreEqual(1, view.RebuildCountForAudit,
                "骨格が揃っているのにゾーン全体を作り直している");
            Assert.GreaterOrEqual(view.IncrementalUpdateCountForAudit, 2,
                "差分更新が計測されていない");
        }

        [Test]
        public void RowClick_KeepsRowsAndUpdatesSelection()
        {
            var rowsBefore = SettingRows();
            Assert.Greater(rowsBefore.Count, 1, "行が 1 本しか無い");

            // 2 行目 (非アクション行) をクリックして選択を移す。
            var button = rowsBefore[1].GetComponent<Button>();
            Assert.IsNotNull(button, "行に Button が無い");
            // クリック前の選択色を記録しておく。
            var colorBefore0 = rowsBefore[0].GetComponent<Image>().color;
            var colorBefore1 = rowsBefore[1].GetComponent<Image>().color;
            button.onClick.Invoke();

            var rowsAfter = SettingRows();
            Assert.AreEqual(rowsBefore.Count, rowsAfter.Count);
            for (int i = 0; i < rowsBefore.Count; i++)
            {
                Assert.AreSame(rowsBefore[i], rowsAfter[i],
                    "slot " + i + " の設定行が再生成されている (Issue #415)");
            }

            // 選択が 0 → 1 に移ったので背景色が入れ替わっていること。
            Assert.AreEqual(colorBefore1, rowsAfter[0].GetComponent<Image>().color,
                "旧選択行の背景色が更新されていない");
            Assert.AreEqual(colorBefore0, rowsAfter[1].GetComponent<Image>().color,
                "新選択行の背景色が更新されていない");
        }

        [Test]
        public void TabSwitch_RebuildsPanelsButKeepsHeaderTabsAndFooter()
        {
            GameObject headerTitle = Find(view.Root, "Header/Title")?.gameObject;
            GameObject footerLegend = Find(view.Root, "Footer")?.GetChild(0)?.gameObject;
            GameObject tabStrip = Find(view.Root, "Main/Zone Tabs")?.gameObject;
            GameObject settingsBefore = Find(view.Root, "Main/Settings")?.gameObject;
            GameObject previewBefore = Find(view.Root, "Main/Preview")?.gameObject;
            Assert.IsNotNull(settingsBefore);
            Assert.IsNotNull(previewBefore);

            // タブを Display (1) へ切り替える。
            view.SelectTabForAudit(1);

            // Header/Footer/タブ帯は同一実体。
            Assert.AreSame(headerTitle, Find(view.Root, "Header/Title")?.gameObject,
                "タブ切替でヘッダーが再生成されている");
            Assert.AreSame(tabStrip, Find(view.Root, "Main/Zone Tabs")?.gameObject,
                "タブ切替でタブ帯が再生成されている");
            if (footerLegend != null)
            {
                Assert.AreSame(footerLegend, Find(view.Root, "Footer")?.GetChild(0)?.gameObject,
                    "タブ切替でフッターが再生成されている");
            }

            // Settings / Preview は行定義が変わるので作り直される (骨格変更扱い)。
            GameObject settingsAfter = Find(view.Root, "Main/Settings")?.gameObject;
            GameObject previewAfter = Find(view.Root, "Main/Preview")?.gameObject;
            Assert.IsNotNull(settingsAfter);
            Assert.IsNotNull(previewAfter);
            Assert.AreNotSame(settingsBefore, settingsAfter,
                "タブ切替で Settings パネルが保持されている (行定義が違うのに使い回している)");
            Assert.AreNotSame(previewBefore, previewAfter,
                "タブ切替で Preview パネルが保持されている");

            // 切替後の行数は Display タブの定義 (5 行)。
            Assert.AreEqual(5, SettingRows().Count, "Display タブの行数が想定と違う");

            // もう一度 Refresh: 今度は同一タブなのでパネルも保持される。
            view.Refresh();
            Assert.AreSame(settingsAfter, Find(view.Root, "Main/Settings")?.gameObject,
                "同一タブの Refresh で Settings パネルが再生成されている");
            Assert.AreSame(previewAfter, Find(view.Root, "Main/Preview")?.gameObject,
                "同一タブの Refresh で Preview パネルが再生成されている");
        }
    }
}
