using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Issue #120: TitleScene の v5 UI (design 01 Title)。
    /// コードでレイアウトを構築し、既存 TitleSceneManager の振る舞い
    /// (SelectScene / OptionScene / EndGame / 効果音) をコールバックで再利用する。
    ///
    /// レイアウトは <see cref="TitleLayout"/> (design 実測値の画面比率) に従い、
    /// 解像度変化時に ApplyLayout で張り直す。16:9 / 16:10 / 4:3 でロゴが完全に見え、
    /// フレーム線が画面右下まで伸びる (フィードバック対応)。
    ///
    /// メニューは GameStart / Ranking / Option / Exit。Ranking は
    /// <see cref="TitleLinks.RankingRequested"/> が設定されている間だけ表示する。
    /// History は遷移先が未実装のため表示しない。
    /// ナビゲーションはレイアウト非依存: ↑↓ (W/S) で選択、Enter/Space で決定、マウスクリック。
    /// 左下 (F2 ヒント) と右上 (更新通知) は他コンポーネントの領域なので何も描かない。
    /// メニューアイコン (design の ▷🏆⚙⏏) は NotoSansJP 静的アトラスに字形がなく
    /// 豆腐になるため描かない (フィードバック「cheap な場合のみ」を見送り)。
    /// </summary>
    public sealed class TitleView : MonoBehaviour
    {
        /// <summary>カタログ状態バッジのラベル (デザイン表記: オンライン · 42曲)。</summary>
        public static string CatalogLabel(bool remoteCatalog, int songCount)
        {
            return (remoteCatalog ? "オンライン" : "ローカル") + " · " + songCount + "曲";
        }

        private sealed class Item
        {
            public readonly string Label;
            public readonly Action Run;

            /// <summary>表示条件を毎フレーム判定する動的項目 (Ranking) か。</summary>
            public readonly bool IsDynamic;
            public GameObject Row;
            public Image Background;

            public Item(string label, Action run, bool isDynamic = false)
            {
                Label = label;
                Run = run;
                IsDynamic = isDynamic;
            }
        }

        private readonly List<Item> items = new List<Item>();
        private int selected;
        private int lastWidth;
        private int lastHeight;
        private bool rankingShown;

        private RectTransform logo;
        private RectTransform barH;
        private RectTransform barV;
        private RectTransform menu;
        private RectTransform catalog;
        private RectTransform legend;
        private TextMeshProUGUI decideCap;

        /// <summary>凡例「決定」のキャプ。入力はメニュー操作設定 (KeyActions.Start) の設定値に従う。</summary>
        public static string DecideCap()
        {
            return ActionKeyHint.Primary(KeyActions.KeyAction.Start);
        }

        /// <summary>
        /// Canvas (ScreenSpaceOverlay, Constant Pixel Size x1) ごと新規に構築する。
        /// レガシー UI は呼び出し側が非アクティブ化すること。
        /// </summary>
        public static TitleView Build(Action gameStart, Action option, Action exit, Action sound)
        {
            var canvasGo = new GameObject("V5 Title Canvas", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasConfigurator.Configure(canvas);

            var root = V5Ui.Node(canvasGo, "Root");
            V5Ui.Fill(root.GetComponent<RectTransform>(), 0f);
            var view = root.AddComponent<TitleView>();
            view.items.Add(new Item("GameStart", gameStart));
            view.items.Add(new Item("Ranking", InvokeRanking, isDynamic: true));
            view.items.Add(new Item("Option", option));
            view.items.Add(new Item("Exit", exit));
            view.onSound = sound;

            V5Decor.BuildBackdrop(root);
            V5Decor.BuildEdgeDeco(root);
            view.BuildFrame();
            view.BuildLogo();
            view.BuildMenu();
            view.BuildCatalog();
            view.BuildLegend();
            view.ApplyLayout();
            view.SetSelected(0, silent: true);
            return view;
        }

        private static void InvokeRanking()
        {
            if (TitleLinks.RankingRequested != null)
            {
                TitleLinks.RankingRequested();
            }
        }

        private Action onSound;

        private void Update()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight)
            {
                ApplyLayout();
            }

            UpdateRankingVisibility();

            int visible = VisibleCount();
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                SetSelected((selected + visible - 1) % visible);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                SetSelected((selected + 1) % visible);
                return;
            }

            // 決定 (Space/Enter) はメニュー操作設定 (KeyActions.Start) に従う (0.1.9)。
            if (KeyActions.IsDown(KeyActions.KeyAction.Start))
            {
                Activate(selected);
                return;
            }

            // 旧レター短縮キー (O/E) もメニュー操作設定に従う (Space は GameStart 扱いなので除外)。
            if (KeyActions.IsDown(KeyActions.KeyAction.Option))
            {
                ActivateLabel("Option");
                return;
            }

            if (KeyActions.IsDown(KeyActions.KeyAction.Exit))
            {
                ActivateLabel("Exit");
            }
        }

        private int VisibleCount()
        {
            int n = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Row != null && items[i].Row.activeSelf)
                {
                    n++;
                }
            }

            return Mathf.Max(1, n);
        }

        /// <summary>表示中の index 番目の項目。</summary>
        private Item VisibleItem(int index)
        {
            int k = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Row == null || !items[i].Row.activeSelf)
                {
                    continue;
                }

                if (k == index)
                {
                    return items[i];
                }

                k++;
            }

            return items[0];
        }

        private void ActivateLabel(string label)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Label == label && items[i].Row.activeSelf)
                {
                    SetSelected(VisibleIndexOf(i));
                    Activate(selected);
                    return;
                }
            }
        }

        private int VisibleIndexOf(int itemsIndex)
        {
            int k = 0;
            for (int i = 0; i < itemsIndex; i++)
            {
                if (items[i].Row != null && items[i].Row.activeSelf)
                {
                    k++;
                }
            }

            return k;
        }

        private void UpdateRankingVisibility()
        {
            bool show = TitleLinks.RankingRequested != null;
            if (show == rankingShown)
            {
                return;
            }

            rankingShown = show;
            if (items[1].Row != null)
            {
                items[1].Row.SetActive(show);
                ApplyLayout();
                SetSelected(Mathf.Clamp(selected, 0, VisibleCount() - 1), silent: true);
            }
        }

        private void Activate(int index)
        {
            if (onSound != null)
            {
                onSound();
            }

            var item = VisibleItem(index);
            if (item != null && item.Run != null)
            {
                item.Run();
            }
        }

        private void SetSelected(int index, bool silent = false)
        {
            selected = Mathf.Clamp(index, 0, VisibleCount() - 1);
            if (!silent && onSound != null)
            {
                onSound();
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Background != null)
                {
                    items[i].Background.color =
                        (items[i].Row.activeSelf && VisibleIndexOf(i) == selected)
                            ? V5Tokens.Key6 : V5Tokens.Key3;
                }
            }
        }

        // --- 構築 ---

        /// <summary>フレーム線 (Bar H / Bar V)。位置と寸法は ApplyLayout で設定する。</summary>
        private void BuildFrame()
        {
            barH = CreateLine("Bar H");
            barV = CreateLine("Bar V");
        }

        private RectTransform CreateLine(string name)
        {
            var go = V5Ui.Node(gameObject, name);
            var img = go.AddComponent<Image>();
            img.color = V5Tokens.ButtonPrimaryBg;
            img.raycastTarget = false;
            return go.GetComponent<RectTransform>();
        }

        /// <summary>FlashBeat ロゴ (13.png)。位置と寸法は ApplyLayout で設定する。</summary>
        private void BuildLogo()
        {
            var go = V5Ui.Node(gameObject, "Logo");
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var sprite = V5Decor.LoadSprite("13");
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
            }

            logo = go.GetComponent<RectTransform>();
        }

        /// <summary>メニュー行 (選択 Key6 / 非選択 Key3)。位置と寸法は ApplyLayout で設定する。</summary>
        private void BuildMenu()
        {
            menu = V5Ui.Node(gameObject, "Menu").GetComponent<RectTransform>();
            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                var row = V5Ui.Panel(menu.gameObject, "Menu " + items[i].Label,
                    new Vector2(100f, 40f), V5Tokens.Key3, V5Tokens.RadiusMedium);
                var rowRt = row.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);

                var label = V5Ui.Text(row, "Label", items[i].Label, UISizeProfile.Current.FsM, V5Tokens.Text,
                    TextAlignmentOptions.Left, FontStyles.Bold);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.offsetMin = new Vector2(RowPad(), 0f);
                labelRt.offsetMax = new Vector2(-RowPad(), 0f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;

                var button = row.AddComponent<Button>();
                button.onClick.AddListener(() =>
                {
                    SetSelected(VisibleIndexOf(index));
                    Activate(selected);
                });

                items[i].Row = row;
                items[i].Background = row.GetComponent<Image>();
                if (items[i].IsDynamic)
                {
                    row.SetActive(false); // RankingRequested が設定されてから表示
                }
            }
        }

        private float RowPad()
        {
            return V5Decor.S(24f);
        }

        /// <summary>カタログ状態バッジ (design: オンライン · 42曲)。</summary>
        private void BuildCatalog()
        {
            var tmp = V5Ui.Text(gameObject, "Catalog",
                CatalogLabel(GManager.HasRemoteCatalog, GManager.totalSong),
                V5Decor.S(22f), V5Tokens.TextMuted);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            catalog = tmp.rectTransform;
        }

        /// <summary>凡例行: [↑][↓] 選択  [決定キー] 決定 (決定キーは KeyActions.Start の設定値)。</summary>
        private void BuildLegend()
        {
            string decide = DecideCap();
            var legendGo = V5Decor.BuildLegend(gameObject, Vector2.zero, new[]
            {
                ("↑", V5Decor.S(46f), (string)"選択"),
                ("↓", V5Decor.S(46f), (string)null),
                (decide, V5Decor.S(96f), (string)"決定"),
            });
            legend = legendGo.GetComponent<RectTransform>();
            var capTransform = legendGo.transform.Find("KeyCap " + decide);
            if (capTransform != null)
            {
                decideCap = capTransform.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        private void OnEnable()
        {
            KeyActions.Changed += RefreshDecideCap;
        }

        private void OnDisable()
        {
            KeyActions.Changed -= RefreshDecideCap;
        }

        /// <summary>キー設定が変わったら凡例の決定キャップを描き直す。</summary>
        private void RefreshDecideCap()
        {
            if (decideCap != null)
            {
                decideCap.text = DecideCap();
            }
        }

        // --- レイアウト適用 (解像度に依存する部分はここだけ) ---

        private void ApplyLayout()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            int visible = VisibleCount();
            var r = TitleLayout.Compute(lastWidth, lastHeight, visible);

            // ロゴ: 左右中央、上端は画面高さに比例 (design y 115..340 of 1080)。
            V5Ui.Place(logo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -r.Logo.y), new Vector2(r.Logo.width, r.Logo.height));

            // フレーム線: 太さはロゴと同じ「画面高さに比例」の規則。
            // 縦バーは横バーの右端に揃え、画面下端まで伸ばす。
            PlaceTopLeft(barH, r.BarH);
            PlaceTopLeft(barV, r.BarV);

            // メニュー: 上端基準 (design 先頭行の上端 y 603.5)。
            menu.sizeDelta = new Vector2(r.Menu.width, r.Menu.height);
            V5Ui.Place(menu, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(r.Menu.x, -r.Menu.y), r.Menu.size);

            int k = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Row == null || !items[i].Row.activeSelf)
                {
                    continue;
                }

                var rowRt = items[i].Row.GetComponent<RectTransform>();
                rowRt.anchoredPosition = new Vector2(0f, -k * (r.RowH + r.RowGap));
                rowRt.sizeDelta = new Vector2(0f, r.RowH);
                k++;
            }

            PlaceTopLeft(catalog, r.Catalog);
            PlaceTopLeft(legend, r.Legend);
        }

        /// <summary>Rect (y は上端基準) を「上端左ピボット」で張る。</summary>
        private static void PlaceTopLeft(RectTransform rt, Rect rect)
        {
            V5Ui.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(rect.x, -rect.y), rect.size);
        }
    }
}
