using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Issue #120: TitleScene の v5 UI (design 01 Title, fhd 1920x1080 実測値)。
    /// コードでレイアウトを構築し、既存 TitleSceneManager の振る舞い
    /// (SelectScene / OptionScene / EndGame / 効果音) をコールバックで再利用する。
    /// メニューは GameStart / Option / Exit (History / Ranking は遷移先未実装のため非表示)。
    /// ナビゲーションはレイアウト非依存: ↑↓ (W/S) で選択、Enter/Space で決定、マウスクリック。
    /// 左下 (F2 ヒント) と右上 (更新通知) は他コンポーネントの領域なので何も描かない。
    /// 寸法は fhd 実測 px を S(px) = px * FsM/30 でプロファイル倍率に合わせる。
    /// </summary>
    public sealed class TitleView : MonoBehaviour
    {
        private static readonly string[] MenuLabels = { "GameStart", "Option", "Exit" };

        private Action onGameStart;
        private Action onOption;
        private Action onExit;
        private Action onSound;

        private Image[] rowBackgrounds;
        private int selected;

        /// <summary>カタログ状態バッジのラベル (デザイン表記: オンライン · 42曲)。</summary>
        public static string CatalogLabel(bool remoteCatalog, int songCount)
        {
            return (remoteCatalog ? "オンライン" : "ローカル") + " · " + songCount + "曲";
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
            view.onGameStart = gameStart;
            view.onOption = option;
            view.onExit = exit;
            view.onSound = sound;

            V5Decor.BuildBackdrop(root);
            V5Decor.BuildEdgeDeco(root);
            view.BuildBars();
            view.BuildLogo();
            view.BuildMenu();
            view.BuildCatalog();
            view.BuildLegend();
            view.SetSelected(0, silent: true);
            return view;
        }

        private float S(float fhdPx)
        {
            return fhdPx * (UISizeProfile.Current.FsM / 30f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                SetSelected((selected + MenuLabels.Length - 1) % MenuLabels.Length);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                SetSelected((selected + 1) % MenuLabels.Length);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space))
            {
                Activate(selected);
                return;
            }

            // 旧レター短縮キー (O/E) も維持 (Space は GameStart 扱いなので除外)。
            if (Input.GetKeyDown(KeyCode.O))
            {
                SetSelected(1);
                Activate(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                SetSelected(2);
                Activate(2);
            }
        }

        private void Activate(int index)
        {
            if (onSound != null)
            {
                onSound();
            }

            switch (index)
            {
                case 0:
                    if (onGameStart != null) onGameStart();
                    break;
                case 1:
                    if (onOption != null) onOption();
                    break;
                case 2:
                    if (onExit != null) onExit();
                    break;
            }
        }

        private void SetSelected(int index, bool silent = false)
        {
            selected = Mathf.Clamp(index, 0, MenuLabels.Length - 1);
            if (!silent && onSound != null)
            {
                onSound();
            }

            for (int i = 0; i < rowBackgrounds.Length; i++)
            {
                rowBackgrounds[i].color = i == selected ? V5Tokens.Key6 : V5Tokens.Key3;
            }
        }

        // --- レイアウト ---

        /// <summary>Bar H (0,424 / 1500x16) + Bar V (1484,440 / 16x640)、#EDEDED。</summary>
        private void BuildBars()
        {
            var barH = V5Ui.Node(gameObject, "Bar H");
            V5Ui.Place(barH.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -S(424f)), new Vector2(S(1500f), S(16f)));
            var barHImg = barH.AddComponent<Image>();
            barHImg.color = V5Tokens.ButtonPrimaryBg;
            barHImg.raycastTarget = false;

            var barV = V5Ui.Node(gameObject, "Bar V");
            V5Ui.Place(barV.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(S(1484f), -S(440f)), new Vector2(S(16f), S(640f)));
            var barVImg = barV.AddComponent<Image>();
            barVImg.color = V5Tokens.ButtonPrimaryBg;
            barVImg.raycastTarget = false;
        }

        /// <summary>Logo (280,108.5 / 1280x253, 13.png contain)。</summary>
        private void BuildLogo()
        {
            V5Decor.BuildLogo(gameObject, new Vector2(S(280f), -S(108.5f)),
                new Vector2(S(1280f), S(253f)));
        }

        /// <summary>Menu (256,603.5 / 行 332x64, gap 10, 選択 Key6 / 非選択 Key3)。</summary>
        private void BuildMenu()
        {
            var menu = V5Ui.Node(gameObject, "Menu");
            V5Ui.Place(menu.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 0f),
                new Vector2(S(256f), -S(603.5f)), new Vector2(S(332f), S(212f)));

            rowBackgrounds = new Image[MenuLabels.Length];
            for (int i = 0; i < MenuLabels.Length; i++)
            {
                int index = i;
                var row = V5Ui.Panel(menu, "Menu " + MenuLabels[i],
                    new Vector2(S(332f), S(64f)), V5Tokens.Key3, V5Tokens.RadiusMedium);
                var rowRt = row.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -i * S(74f));
                rowRt.sizeDelta = new Vector2(0f, S(64f));

                var label = V5Ui.Text(row, "Label", MenuLabels[i], UISizeProfile.Current.FsM, V5Tokens.Text,
                    TextAlignmentOptions.Left, FontStyles.Bold);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.offsetMin = new Vector2(S(24f), 0f);
                labelRt.offsetMax = new Vector2(-S(24f), 0f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;

                var button = row.AddComponent<Button>();
                button.onClick.AddListener(() =>
                {
                    SetSelected(index);
                    Activate(index);
                });

                rowBackgrounds[i] = row.GetComponent<Image>();
            }
        }

        /// <summary>カタログ状態バッジ (652.5,695.5 / fs22 muted)。</summary>
        private void BuildCatalog()
        {
            var catalog = V5Ui.Text(gameObject, "Catalog",
                CatalogLabel(GManager.HasRemoteCatalog, GManager.totalSong),
                S(22f), V5Tokens.TextMuted);
            V5Ui.Place(catalog.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f),
                new Vector2(S(652.5f), -S(695.5f)), new Vector2(S(400f), S(32f)));
            catalog.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>Legend (659,775.5): [↑][↓] 選択  [Enter] 決定。</summary>
        private void BuildLegend()
        {
            V5Decor.BuildLegend(gameObject, new Vector2(S(659f), -S(775.5f)), new[]
            {
                ("↑", S(46f), (string)"選択"),
                ("↓", S(46f), (string)null),
                ("Enter", S(96f), (string)"決定"),
            });
        }
    }
}
