using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Select 画面 (#121) の v5 UI。design/flashbeat-ui.pen v5 の
    /// 02 Select / 02b typing / 02c Ready / 02d History / 07 Ranking を実行時に構築する。
    ///
    /// - データと遷移は SelectSceneManager (GManager.songID / GameScene / TitleScene) が所有し、
    ///   この View は見た目と入力だけを担う。
    /// - 構築はコードのみ (シーン YAML は触らない)。Create でレガシー Canvas を非表示化する。
    /// - 寸法は design v5 の fhd (1920x1080) 実測値を u = min(W/1920, H/1080) 倍して使う
    ///   (全解像度でクリップしない。16:9 では ui-size-profiles.json の値と一致する)。
    ///   色は V5Tokens、フォントは V5Ui.Text (NotoSansJP SDF)。
    /// - 配置ヘルパの design 座標は左上原点。At() は design px を受け取り u 倍するので、
    ///   呼び出し側はすべて design px (スケール前) を渡すこと。
    /// - キー: ↑↓ 選択 / Enter 決定 (Ready へ) / Space プレイ開始 (レガシー維持) /
    ///   Esc 戻る / Tab 絞り込み (typing) / F2 履歴 / F3 ランキング / Ready 中 ←→ 値・R 開始。
    ///   絞り込み入力は Input.inputString (KeyCode に依存しない)。
    /// - データは GManager.ActiveSongs + GManager.Hiscore のみ。無いデータは
    ///   空状態または「–」で示す (デザインの数値を捏造しない)。
    /// </summary>
    public sealed class SelectView : MonoBehaviour
    {
        private const float DesignW = 1920f;
        private const float DesignH = 1080f;

        private enum ScreenState
        {
            List,
            Typing,
            Ready,
            History,
            Ranking,
        }

        // 実曲と一致するジャケットのみ Resources に同梱する (デザインの jk-* を全曲に流用しない)。
        private const string JacketResourceDir = "V5/select/jacket/";
        private static readonly Dictionary<string, string> JacketResourceNames = new Dictionary<string, string>
        {
            { "Poison", "Poison" },
            { "Calamity", "Calamity" },
            { "まにまに", "mani" },
            { "エウタナシア", "euta" },
            { "終焉逃避行", "syuen" },
            { "4th smile", "4th smile" },
        };

        // Ready の「このプレイの設定」。Key Guide / Lyrics は PlaySession 経由で
        // GameScene 側へ実値を渡す (Issue #167: 以前は private static に書くだけで
        // GameScene が読まないため、UI の値だけが変わる偽値になっていた)。
        // NoteSpeed は GManager.noteSpeed に直接反映し、保存もする。
        private static readonly string[] LyricsModes = { "Auto", "Off" };

        private SelectSceneManager owner;
        private Canvas canvas;

        private ScreenState state = ScreenState.List;
        private readonly List<int> filteredIds = new List<int>();
        private string filterText = "";
        private SelectViewLogic.SortMode sortMode = SelectViewLogic.SortMode.Recent;
        private SelectViewLogic.LevelMode levelMode = SelectViewLogic.LevelMode.All;
        private SelectViewLogic.StatusMode statusMode = SelectViewLogic.StatusMode.All;
        private int historyIndex;
        private int readyRow;
        private int rankingSegment; // 0 = Local, 1 = Online

        private readonly Dictionary<string, Sprite> jacketCache = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, Sprite> webJacketCache = new Dictionary<string, Sprite>();

        // ランキング Online セグメントの状態 (Issue #167: 実データを取得する)。
        private RankingClient.LeaderboardEntry[] onlineEntries;
        private bool onlineLoading;
        private bool onlineFetchFailed;

        private float u;

        /// <summary>View を生成し、レガシー Canvas を非表示にする。SelectSceneManager.Start から呼ぶ。</summary>
        public static SelectView Create(SelectSceneManager owner)
        {
            var root = new GameObject("V5 Select View");
            var view = root.AddComponent<SelectView>();
            view.owner = owner;

            // Issue #167: GraphicRaycaster 付きの Canvas にする (マウスクリックを効かせる)。
            var canvas = V5GameUi.NewScreenCanvas("V5 Select Canvas");
            var canvasGo = canvas.gameObject;
            canvasGo.transform.SetParent(root.transform, false);
            view.canvas = canvas;

            // レガシー (Issue #57 までのシーン配置 UI) を隠して新しい UI に置き換える。
            var legacy = GameObject.Find("Canvas");
            if (legacy != null && legacy != canvasGo) legacy.SetActive(false);

            view.RefreshFilter();
            view.Rebuild();
            // Issue #167: 矢印キー操作は SelectView 自身が担うので Unity の自動ナビゲーションは切る。
            V5GameUi.ApplySingleInputModel(canvas);
            return view;
        }

        // --- 入力 (SelectSceneManager.Update から毎フレーム呼ばれる) ---

        public void HandleInput()
        {
            switch (state)
            {
                case ScreenState.List: HandleListInput(); break;
                case ScreenState.Typing: HandleTypingInput(); break;
                case ScreenState.Ready: HandleReadyInput(); break;
                case ScreenState.History: HandleHistoryInput(); break;
                case ScreenState.Ranking: HandleRankingInput(); break;
            }
        }

        private void HandleListInput()
        {
            if (HandleWheel(filteredIds)) return;

            // メニュー操作設定 (KeyActions) に従う (0.1.9)。←→ (NavPrev/NavNext) と
            // レーンキーの左右半分 (0.1.9 のカスタム割り当て) でも選択を移動できる。
            if (Input.GetKeyDown(KeyCode.UpArrow) || KeyActions.IsDown(KeyActions.KeyAction.NavPrev) ||
                KeyBindings.IsAnyLaneKeyDown(0, 4))
            {
                Sound(); MoveSelection(-1, filteredIds);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || KeyActions.IsDown(KeyActions.KeyAction.NavNext) ||
                KeyBindings.IsAnyLaneKeyDown(4, 8))
            {
                Sound(); MoveSelection(1, filteredIds);
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { Sound(); EnterReady(); }
            else if (KeyActions.IsDown(KeyActions.KeyAction.Confirm)) { Sound(); StartSelected(); }
            else if (KeyActions.IsDown(KeyActions.KeyAction.Back)) { Sound(); ToTitle(); }
            else if (Input.GetKeyDown(KeyCode.Tab)) { Sound(); state = ScreenState.Typing; Rebuild(); }
            else if (Input.GetKeyDown(KeyCode.F2)) { Sound(); EnterHistory(); }
            else if (Input.GetKeyDown(KeyCode.F3)) { Sound(); EnterRanking(); }
        }

        private void HandleTypingInput()
        {
            string typed = Input.inputString;
            foreach (char c in typed)
            {
                if (c == '\b')
                {
                    if (filterText.Length > 0) filterText = filterText.Substring(0, filterText.Length - 1);
                }
                else if (c == '\n' || c == '\r')
                {
                    // 検索の確定: 一覧に戻る (絞り込みは維持)。
                    Sound();
                    state = ScreenState.List;
                    Rebuild();
                    return;
                }
                else if (!char.IsControl(c))
                {
                    filterText += c;
                }
            }

            if (typed.Length > 0)
            {
                Sound();
                RefreshFilter();
                Rebuild();
            }

            if (Input.GetKeyDown(KeyCode.UpArrow)) { Sound(); MoveSelection(-1, filteredIds); }
            else if (Input.GetKeyDown(KeyCode.DownArrow)) { Sound(); MoveSelection(1, filteredIds); }
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab)) { Sound(); state = ScreenState.List; Rebuild(); }
            else if (Input.GetKeyDown(KeyCode.F2)) { Sound(); EnterHistory(); }
            else if (Input.GetKeyDown(KeyCode.F3)) { Sound(); EnterRanking(); }
        }

        private void HandleReadyInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)) { Sound(); readyRow = (readyRow + 2) % 3; Rebuild(); }
            else if (Input.GetKeyDown(KeyCode.DownArrow)) { Sound(); readyRow = (readyRow + 1) % 3; Rebuild(); }
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) { Sound(); ChangeReadyValue(-1); }
            else if (Input.GetKeyDown(KeyCode.RightArrow)) { Sound(); ChangeReadyValue(1); }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.R))
            {
                Sound();
                StartSelected();
            }
            else if (Input.GetKeyDown(KeyCode.Escape)) { Sound(); state = ScreenState.List; Rebuild(); }
        }

        private void HandleHistoryInput()
        {
            var ids = HistoryIds();
            if (HandleWheel(ids)) return;

            if (Input.GetKeyDown(KeyCode.UpArrow) && ids.Count > 0)
            {
                Sound();
                historyIndex = (historyIndex + ids.Count - 1) % ids.Count;
                Rebuild();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) && ids.Count > 0)
            {
                Sound();
                historyIndex = (historyIndex + 1) % ids.Count;
                Rebuild();
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                Sound();
                SelectHistorySong(ids);
            }
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F2))
            {
                Sound();
                state = ScreenState.List;
                Rebuild();
            }
        }

        private void HandleRankingInput()
        {
            if (HandleWheel(AllIds())) return;

            if (Input.GetKeyDown(KeyCode.UpArrow)) { Sound(); MoveSelection(-1, AllIds()); }
            else if (Input.GetKeyDown(KeyCode.DownArrow)) { Sound(); MoveSelection(1, AllIds()); }
            else if (Input.GetKeyDown(KeyCode.Tab)) { Sound(); rankingSegment = 1 - rankingSegment; Rebuild(); }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) { Sound(); EnterReady(); }
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F3)) { Sound(); state = ScreenState.List; Rebuild(); }
        }

        private bool HandleWheel(List<int> ids)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0f) { Sound(); MoveSelection(-1, ids); return true; }
            if (scroll < 0f) { Sound(); MoveSelection(1, ids); return true; }
            return false;
        }

        // --- 操作 ---

        private void Sound()
        {
            if (owner != null) owner.playSound();
        }

        private void StartSelected()
        {
            // Issue #167: 再生できない曲 (placeholder / 未選択) では始めない。
            if (!HasPlayableSong()) { return; }

            // フェードが新 UI の上に来るよう、出ていくときは自分を下げる。
            if (canvas != null) canvas.sortingOrder = -100;
            // ポインタで選んだ Button の選択を解除する (Enter の二重発火を防ぐ)。
            V5GameUi.ClearPointerSelection();
            if (owner != null) owner.GameScene();
        }

        private void ToTitle()
        {
            if (canvas != null) canvas.sortingOrder = -100;
            if (owner != null) owner.TitleScene();
        }

        private void MoveSelection(int delta, List<int> ids)
        {
            if (ids == null || ids.Count == 0) return;
            int idx = ids.IndexOf(GManager.songID);
            if (idx < 0) idx = 0;
            idx = (idx + delta + ids.Count) % ids.Count;
            GManager.songID = ids[idx];
            Rebuild();
        }

        private List<int> AllIds()
        {
            var ids = new List<int>();
            for (int i = 1; i < GManager.ActiveSongs.Length; i++)
            {
                if (GManager.ActiveSongs[i] != null) ids.Add(i);
            }

            return ids;
        }

        private void EnterReady()
        {
            // Issue #167: 再生できない曲では Ready 画面へ進まない。
            if (!HasPlayableSong()) { return; }

            readyRow = 0;
            state = ScreenState.Ready;
            Rebuild();
        }

        private void EnterHistory()
        {
            var ids = HistoryIds();
            historyIndex = ids.IndexOf(GManager.songID);
            if (historyIndex < 0) historyIndex = 0;
            state = ScreenState.History;
            Rebuild();
        }

        private void EnterRanking()
        {
            rankingSegment = 0;
            state = ScreenState.Ranking;
            Rebuild();
        }

        private void SelectHistorySong(List<int> ids)
        {
            if (historyIndex < 0 || historyIndex >= ids.Count) return;
            GManager.songID = ids[historyIndex];
            RefreshFilter();
            state = ScreenState.List;
            Rebuild();
        }

        private void ChangeReadyValue(int delta)
        {
            if (readyRow == 0)
            {
                // キーガイドの表示モード (Compact / Full / Off)。ゲーム中の HUD が読む。
                PlaySession.SetKeyGuide(PlaySession.NextKeyGuide(PlaySession.KeyGuide, delta));
            }
            else if (readyRow == 1)
            {
                // 歌詞の表示可否 (Auto / Off)。ゲーム中の歌詞パネルが読む。
                int index = Array.IndexOf(LyricsModes, PlaySession.LyricsLabel(PlaySession.LyricsEnabled));
                if (index < 0) index = 0;
                index = (index + delta % LyricsModes.Length + LyricsModes.Length) % LyricsModes.Length;
                PlaySession.SetLyricsEnabled(LyricsModes[index] == "Auto");
            }
            else
            {
                int value = Mathf.RoundToInt(GManager.noteSpeed) + delta;
                GManager.noteSpeed = Mathf.Clamp(value, (int)OptionSettings.NoteSpeedMin, (int)OptionSettings.NoteSpeedMax);
                OptionSettings.Save();
            }

            Rebuild();
        }

        /// <summary>Hiscore が付いた曲 (履歴として存在するデータ) をスコア順で。</summary>
        private List<int> HistoryIds()
        {
            var ids = new List<int>();
            var songs = GManager.ActiveSongs;
            for (int i = 1; i < songs.Length; i++)
            {
                if (songs[i] != null && HiscoreAt(i) > 0) ids.Add(i);
            }

            ids.Sort((a, b) =>
            {
                int diff = HiscoreAt(b).CompareTo(HiscoreAt(a));
                return diff != 0 ? diff : a.CompareTo(b);
            });
            return ids;
        }

        private static int HiscoreAt(int songId)
        {
            return songId >= 0 && songId < GManager.Hiscore.Length ? GManager.Hiscore[songId] : 0;
        }

        // --- カタログ更新 (SelectSceneManager から呼ばれる) ---

        public void OnCatalogChanged()
        {
            RefreshFilter();
            Rebuild();
        }

        private void RefreshFilter()
        {
            filteredIds.Clear();
            filteredIds.AddRange(SelectViewLogic.FilterIds(
                GManager.ActiveSongs, filterText, sortMode, levelMode, statusMode, GManager.Hiscore));
            if (filteredIds.Count > 0 && !filteredIds.Contains(GManager.songID))
            {
                GManager.songID = filteredIds[0];
            }
        }

        // --- 構築 ---

        private void Rebuild()
        {
            u = Mathf.Min(Screen.width / DesignW, Screen.height / DesignH);
            if (u <= 0f) u = 1f;

            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(canvas.transform.GetChild(i).gameObject);
            }

            var bg = V5Ui.Node(canvas.gameObject, "BG");
            V5Ui.Fill(bg.GetComponent<RectTransform>(), 0f);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = V5Tokens.Bg;
            bgImg.raycastTarget = false;

            switch (state)
            {
                case ScreenState.History: BuildHistory(); break;
                case ScreenState.Ranking: BuildRanking(); break;
                default: BuildSelect(state == ScreenState.Ready); break;
            }
        }

        private float U(float designPx)
        {
            return designPx * u;
        }

        /// <summary>design 座標 (左上原点, design px) で配置する。</summary>
        private RectTransform At(GameObject parent, string name, float x, float yTop, float w, float h)
        {
            var node = V5Ui.Node(parent, name);
            return V5Ui.Place(node.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(U(x), -U(yTop)), new Vector2(U(w), U(h)));
        }

        /// <summary>右端基準 (右 inset, 上端 yTop, design px) で配置する。 ultrawide でも右に寄り続ける。</summary>
        private RectTransform AtRight(GameObject parent, string name, float rightInset, float yTop, float w, float h)
        {
            var node = V5Ui.Node(parent, name);
            return V5Ui.Place(node.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-U(rightInset), -U(yTop)), new Vector2(U(w), U(h)));
        }

        /// <summary>左下基準 (x, 下端からの距離, design px) で配置する。</summary>
        private RectTransform AtBottom(GameObject parent, string name, float x, float bottom, float w, float h)
        {
            var node = V5Ui.Node(parent, name);
            return V5Ui.Place(node.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(U(x), U(bottom)), new Vector2(U(w), U(h)));
        }

        /// <summary>右下基準 (右 inset, 下端からの距離, design px) で配置する。</summary>
        private RectTransform AtBottomRight(GameObject parent, string name, float rightInset, float bottom, float w, float h)
        {
            var node = V5Ui.Node(parent, name);
            return V5Ui.Place(node.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-U(rightInset), U(bottom)), new Vector2(U(w), U(h)));
        }

        /// <summary>左右を親の端からの距離 (design px) で伸縮させる上端固定の帯。</summary>
        private RectTransform Stretch(GameObject parent, string name, float left, float right, float yTop, float h)
        {
            var node = V5Ui.Node(parent, name);
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(U(left), -U(yTop) - U(h));
            rt.offsetMax = new Vector2(-U(right), -U(yTop));
            return rt;
        }

        private TextMeshProUGUI Text(GameObject parent, string name, string content, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles weight = FontStyles.Normal)
        {
            var tmp = V5Ui.Text(parent, name, content, size, color, align, weight);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        /// <summary>長い題名向け: 自動縮小 + 省略。</summary>
        private TextMeshProUGUI AutoText(GameObject parent, string name, string content, float size, Color color,
            TextAlignmentOptions align, FontStyles weight = FontStyles.Normal)
        {
            var tmp = Text(parent, name, content, size, color, align, weight);
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = size * 0.45f;
            tmp.fontSizeMax = size;
            return tmp;
        }

        private GameObject Panel(RectTransform rt, Color color, float radius)
        {
            var go = rt.gameObject;
            var img = go.AddComponent<Image>();
            img.type = Image.Type.Sliced;
            img.sprite = V5Ui.RoundedSprite(Mathf.RoundToInt(radius * u));
            img.color = color;
            return go;
        }

        private GameObject PanelAt(GameObject parent, string name, float x, float yTop, float w, float h, Color color, float radius)
        {
            var rt = At(parent, name, x, yTop, w, h);
            return Panel(rt, color, radius);
        }

        private GameObject PanelAtRight(GameObject parent, string name, float rightInset, float yTop, float w, float h, Color color, float radius)
        {
            var rt = AtRight(parent, name, rightInset, yTop, w, h);
            return Panel(rt, color, radius);
        }

        private GameObject PanelAtBottom(GameObject parent, string name, float x, float bottom, float w, float h, Color color, float radius)
        {
            var rt = AtBottom(parent, name, x, bottom, w, h);
            return Panel(rt, color, radius);
        }

        private GameObject PanelAtBottomRight(GameObject parent, string name, float rightInset, float bottom, float w, float h, Color color, float radius)
        {
            var rt = AtBottomRight(parent, name, rightInset, bottom, w, h);
            return Panel(rt, color, radius);
        }

        private static void FillRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private GameObject SmallButton(GameObject parent, string name, float x, float yTop, float w, float h, string label, Action onClick)
        {
            var go = PanelAt(parent, name, x, yTop, w, h, V5Tokens.ControlBg, V5Tokens.RadiusMedium);
            var tmp = Text(go, "Label", label, U(24), V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            FillRect(tmp.rectTransform);
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(() => onClick());
            return go;
        }

        /// <summary>右下anchored の primary ボタン (Ready / Start / この曲へ)。rightInset/bottom は design px。</summary>
        private GameObject PrimaryButton(GameObject parent, string name, float rightInset, float bottom, float w, float h, string label, Action onClick)
        {
            var go = PanelAtBottomRight(parent, name, rightInset, bottom, w, h, V5Tokens.ButtonPrimaryBg, V5Tokens.RadiusMedium);
            var tmp = Text(go, "Label", label, U(24), V5Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center, FontStyles.Bold);
            FillRect(tmp.rectTransform);
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(() => onClick());
            return go;
        }

        /// <summary>ボタンを使えない状態にする (対象曲が無いときなど)。</summary>
        private static void SetInteractable(GameObject buttonGo, bool interactable)
        {
            if (buttonGo == null) { return; }
            var button = buttonGo.GetComponent<Button>();
            if (button != null) { button.interactable = interactable; }
        }

        /// <summary>いま選んでいる曲が再生できる状態か (placeholder / 未選択は不可)。</summary>
        private static bool HasPlayableSong()
        {
            var song = GManager.GetSong(GManager.songID);
            return song != null && !song.unplayable;
        }

        /// <summary>KeyCap + 説明ラベル (design LegendItem)。x/bottom/capW は design px, 下端 anchored。</summary>
        private void LegendItem(GameObject parent, string name, float x, float bottom, float capW, string key, string label)
        {
            var cap = PanelAtBottom(parent, name + " Key " + key, x, bottom, capW, 36f, V5Tokens.ControlBg, V5Tokens.RadiusSmall);
            var capTmp = Text(cap, "Key", key, U(16), V5Tokens.TextMuted, TextAlignmentOptions.Center);
            FillRect(capTmp.rectTransform);

            var tmp = Text(parent, name + " Label", label, U(18), V5Tokens.TextMuted);
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(U(x + capW + 12f), U(bottom) + U(18));
            rt.sizeDelta = new Vector2(U(label.Length * 18f + 20f), U(24));
        }

        private static float LegendItemWidth((string key, string label, float capW) item)
        {
            return item.capW + 12f + item.label.Length * 18f + 24f;
        }

        /// <summary>右寄せの Legend 群。bottom は design px (下端からの距離)。</summary>
        private void LegendRight(GameObject parent, string name, (string key, string label, float capW)[] items, float bottom)
        {
            float right = 48f;
            for (int i = items.Length - 1; i >= 0; i--)
            {
                float labelW = items[i].label.Length * 18f + 20f;
                float capRight = right + labelW + 12f;

                var cap = PanelAtBottomRight(parent, name + i + " Key " + items[i].key, capRight, bottom, items[i].capW, 36f, V5Tokens.ControlBg, V5Tokens.RadiusSmall);
                var capTmp = Text(cap, "Key", items[i].key, U(16), V5Tokens.TextMuted, TextAlignmentOptions.Center);
                FillRect(capTmp.rectTransform);

                var labelTmp = Text(parent, name + i + " Label", items[i].label, U(18), V5Tokens.TextMuted);
                var labelRt = labelTmp.rectTransform;
                labelRt.anchorMin = new Vector2(1f, 0f);
                labelRt.anchorMax = new Vector2(1f, 0f);
                labelRt.pivot = new Vector2(1f, 0.5f);
                labelRt.anchoredPosition = new Vector2(-U(right), U(bottom) + U(18));
                labelRt.sizeDelta = new Vector2(U(labelW), U(24));

                right += LegendItemWidth(items[i]);
            }
        }

        /// <summary>絞り込みチップ (FilterButton)。ラベルは TMP rich text。</summary>
        private GameObject FilterChip(GameObject parent, string name, float x, float yTop, float w, string richLabel, Action onClick)
        {
            var go = PanelAt(parent, name, x, yTop, w, 48f, V5Tokens.ControlBg, V5Tokens.RadiusMedium);
            var tmp = Text(go, "Label", richLabel, U(20), V5Tokens.Text, TextAlignmentOptions.Center);
            FillRect(tmp.rectTransform);
            if (onClick != null)
            {
                var button = go.AddComponent<Button>();
                button.onClick.AddListener(() => onClick());
            }

            return go;
        }

        /// <summary>グレードチップ (Tag)。記録が無いときは「–」。</summary>
        private void GradeChip(GameObject parent, string name, string grade, float x, float yTop, float w, float h)
        {
            var go = PanelAt(parent, name, x, yTop, w, h, V5Tokens.ControlBg, V5Tokens.RadiusSmall);
            var tmp = Text(go, "Grade", grade ?? "–", U(16), V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            FillRect(tmp.rectTransform);
        }

        /// <summary>ジャケット画像 (cover 切り取り)。無い場合は無地タイル。</summary>
        private GameObject Jacket(GameObject parent, string name, int songId, float x, float yTop, float w, float h)
        {
            var rt = At(parent, name, x, yTop, w, h);
            rt.gameObject.AddComponent<RectMask2D>();
            var imgGo = V5Ui.Node(rt.gameObject, "Cover");
            var imgRt = imgGo.GetComponent<RectTransform>();
            imgRt.anchorMin = new Vector2(0.5f, 0.5f);
            imgRt.anchorMax = new Vector2(0.5f, 0.5f);
            imgRt.pivot = new Vector2(0.5f, 0.5f);
            imgRt.sizeDelta = new Vector2(U(w), U(h));

            var img = imgGo.AddComponent<Image>();
            img.color = V5Tokens.Base; // 無地タイル (ジャケットが無い曲)
            img.raycastTarget = false;

            var song = GManager.GetSong(songId);
            var cached = LoadJacket(song);
            if (cached != null)
            {
                ApplyCover(img, cached, new Vector2(U(w), U(h)));
            }
            else if (song != null && !string.IsNullOrEmpty(song.videoId))
            {
                StartCoroutine(FetchWebJacket(song.videoId, img, new Vector2(U(w), U(h))));
            }

            return rt.gameObject;
        }

        private Sprite LoadJacket(SongData song)
        {
            if (song == null) return null;
            string key = song.title ?? string.Empty;
            if (jacketCache.TryGetValue(key, out var cached)) return cached;

            Sprite sprite = null;
            if (JacketResourceNames.TryGetValue(key, out var name))
            {
                var tex = Resources.Load<Texture2D>(JacketResourceDir + name);
                if (tex != null)
                {
                    sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            jacketCache[key] = sprite; // null もキャッシュ (再試行しない)
            return sprite;
        }

        private IEnumerator FetchWebJacket(string videoId, Image target, Vector2 size)
        {
            if (webJacketCache.TryGetValue(videoId, out var cached))
            {
                if (cached != null && target != null) ApplyCover(target, cached, size);
                yield break;
            }

            // 既存 (Issue #57 まで) の SelectSceneManager と同じ取得元。
            string url = "https://img.youtube.com/vi/" + videoId + "/mqdefault.jpg";
            var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url);
            yield return request.SendWebRequest();
            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                webJacketCache[videoId] = null;
                yield break;
            }

            var tex = ((UnityEngine.Networking.DownloadHandlerTexture)request.downloadHandler).texture;
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            webJacketCache[videoId] = sprite;
            if (target != null) ApplyCover(target, sprite, size);
        }

        private static void ApplyCover(Image img, Sprite sprite, Vector2 size)
        {
            img.sprite = sprite;
            img.color = Color.white;
            float scale = Mathf.Max(size.x / sprite.rect.width, size.y / sprite.rect.height);
            img.rectTransform.sizeDelta = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
        }

        /// <summary>曲行 (design SongRow)。位置/寸法は design px。</summary>
        private void SongRow(GameObject parent, string name, int songId, float x, float yTop, float w, float h, bool selected)
        {
            var song = GManager.GetSong(songId);
            if (song == null) return;
            var go = PanelAt(parent, name, x, yTop, w, h, selected ? V5Tokens.Key6 : V5Tokens.Recessed2, V5Tokens.RadiusMedium);

            float thumbH = h - 12f;
            float thumbW = thumbH * (16f / 9f);
            Jacket(go, "Thumb", songId, 8f, 6f, thumbW, thumbH);

            float textX = 8f + thumbW + 12f;
            var tmp = Text(go, "Title", song.title, U(24), V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var titleRt = tmp.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(U(textX), -U(12));
            titleRt.sizeDelta = new Vector2(-U(textX + 150f), U(30));
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            var composerTmp = Text(go, "Composer", song.musician, U(16), V5Tokens.TextMuted);
            var composerRt = composerTmp.rectTransform;
            composerRt.anchorMin = new Vector2(0f, 0f);
            composerRt.anchorMax = new Vector2(1f, 0f);
            composerRt.pivot = new Vector2(0f, 0f);
            composerRt.anchoredPosition = new Vector2(U(textX), U(10));
            composerRt.sizeDelta = new Vector2(-U(textX + 150f), U(22));
            composerTmp.overflowMode = TextOverflowModes.Ellipsis;

            var levelTmp = Text(go, "Level", song.level.ToString(), U(22), V5Tokens.TextMuted, TextAlignmentOptions.Right);
            var levelRt = levelTmp.rectTransform;
            levelRt.anchorMin = new Vector2(1f, 0f);
            levelRt.anchorMax = new Vector2(1f, 1f);
            levelRt.pivot = new Vector2(1f, 0.5f);
            levelRt.anchoredPosition = new Vector2(-U(70), 0f);
            levelRt.sizeDelta = new Vector2(U(50), 0f);

            GradeChip(go, "Grade", SelectViewLogic.GradeOf(HiscoreAt(songId)), w - 52f, (h - 26f) / 2f, 44f, 26f);

            var button = go.AddComponent<Button>();
            int capturedId = songId;
            button.onClick.AddListener(() =>
            {
                Sound();
                if (GManager.songID == capturedId)
                {
                    EnterReady();
                }
                else
                {
                    GManager.songID = capturedId;
                    Rebuild();
                }
            });
        }

        /// <summary>左の曲リスト (検索/件数の間に伸縮)。rows は表示対象 id。</summary>
        private void BuildSongList(GameObject well, float top, float bottom, float rowW, List<int> rows)
        {
            var list = V5Ui.Node(well, "Song List");
            var listRt = list.GetComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0f, 0f);
            listRt.anchorMax = new Vector2(1f, 1f);
            listRt.offsetMin = new Vector2(U(24), U(bottom));
            listRt.offsetMax = new Vector2(-U(24), -U(top));

            float listH = Screen.height - U(top) - U(bottom);
            float pitch = U(74);
            int visible = Mathf.Max(1, Mathf.FloorToInt(listH / pitch));
            int selIdx = Mathf.Max(0, rows.IndexOf(GManager.songID));
            int offset = SelectViewLogic.ScrollOffset(rows.Count, selIdx, visible);
            for (int i = offset; i < Mathf.Min(offset + visible, rows.Count); i++)
            {
                SongRow(list, "Song " + rows[i], rows[i], 0f, (i - offset) * 74f, rowW, 72f, rows[i] == GManager.songID);
            }
        }

        /// <summary>左ウェル (全高, design px 幅)。</summary>
        private GameObject LeftWell(float width)
        {
            var well = V5Ui.Node(canvas.gameObject, "Left Well");
            var wellRt = well.GetComponent<RectTransform>();
            wellRt.anchorMin = new Vector2(0f, 0f);
            wellRt.anchorMax = new Vector2(0f, 1f);
            wellRt.pivot = new Vector2(0f, 0f);
            wellRt.anchoredPosition = Vector2.zero;
            wellRt.sizeDelta = new Vector2(U(width), 0f);
            var wellImg = well.AddComponent<Image>();
            wellImg.color = V5Tokens.Key0;
            wellImg.raycastTarget = false;
            return well;
        }

        // --- 02 / 02b / 02c Select ---

        private void BuildSelect(bool ready)
        {
            int total = Mathf.Max(0, GManager.ActiveSongs.Length - 1);
            var well = LeftWell(640f);

            SmallButton(well, "Back", 48f, 48f, 56f, 56f, "←", ToTitle);

            // 検索フィールド
            var search = PanelAt(well, "Search", 128f, 48f, 464f, 56f, V5Tokens.Key2, V5Tokens.RadiusMedium);
            bool hasText = filterText.Length > 0;
            string shown = hasText
                ? filterText + (state == ScreenState.Typing ? "_" : "")
                : (state == ScreenState.Typing ? "_" : "タイプして検索");
            var searchTmp = Text(search, "Query", shown, U(24), hasText || state == ScreenState.Typing ? V5Tokens.Text : V5Tokens.TextDim);
            var searchRt = searchTmp.rectTransform;
            searchRt.anchorMin = new Vector2(0f, 0f);
            searchRt.anchorMax = new Vector2(1f, 1f);
            searchRt.offsetMin = new Vector2(U(16), 0f);
            searchRt.offsetMax = new Vector2(-U(48), 0f);
            searchTmp.overflowMode = TextOverflowModes.Ellipsis;

            var clearGo = SmallButton(search, "Clear", 424f, 12f, 32f, 32f, "×", () =>
            {
                filterText = "";
                RefreshFilter();
                Rebuild();
            });
            var clearRt = clearGo.GetComponent<RectTransform>();
            clearRt.anchorMin = new Vector2(1f, 0.5f);
            clearRt.anchorMax = new Vector2(1f, 0.5f);
            clearRt.pivot = new Vector2(1f, 0.5f);
            clearRt.anchoredPosition = new Vector2(-U(8), 0f);
            clearRt.sizeDelta = new Vector2(U(32), U(32));

            // 絞り込みチップ
            string sortLabel = sortMode == SelectViewLogic.SortMode.Level ? "Level"
                : sortMode == SelectViewLogic.SortMode.Score ? "Score" : "Recent";
            string levelLabel = null;
            switch (levelMode)
            {
                case SelectViewLogic.LevelMode.Level1to5: levelLabel = "1-5"; break;
                case SelectViewLogic.LevelMode.Level6to10: levelLabel = "6-10"; break;
                case SelectViewLogic.LevelMode.Level11to15: levelLabel = "11-15"; break;
                case SelectViewLogic.LevelMode.Level16Plus: levelLabel = "16+"; break;
                default: levelLabel = "All"; break;
            }

            string statusLabel = statusMode == SelectViewLogic.StatusMode.Played ? "Played"
                : statusMode == SelectViewLogic.StatusMode.Unplayed ? "New" : "All";
            string muted = "<color=#" + ColorUtility.ToHtmlStringRGB(V5Tokens.TextMuted) + ">";

            FilterChip(well, "Sort", 48f, 120f, 184f, muted + "Sort</color> " + sortLabel + " ▼", () =>
            {
                sortMode = (SelectViewLogic.SortMode)(((int)sortMode + 1) % 3);
                RefreshFilter();
                Rebuild();
            });
            FilterChip(well, "Level", 240f, 120f, 148f, muted + "Level</color> " + levelLabel + " ▼", () =>
            {
                levelMode = (SelectViewLogic.LevelMode)(((int)levelMode + 1) % 5);
                RefreshFilter();
                Rebuild();
            });
            FilterChip(well, "Status", 396f, 120f, 160f, muted + "Status</color> " + statusLabel + " ▼", () =>
            {
                statusMode = (SelectViewLogic.StatusMode)(((int)statusMode + 1) % 3);
                RefreshFilter();
                Rebuild();
            });

            BuildSongList(well, 184f, 156f, 592f, filteredIds);

            // 件数
            var countTmp = Text(well, "Count", filteredIds.Count + " / " + total, U(22), V5Tokens.TextMuted);
            var countRt = countTmp.rectTransform;
            countRt.anchorMin = new Vector2(0f, 0f);
            countRt.anchorMax = new Vector2(0f, 0f);
            countRt.pivot = new Vector2(0f, 0f);
            countRt.anchoredPosition = new Vector2(U(48), U(126));
            countRt.sizeDelta = new Vector2(U(220), U(30));

            // レジェンド (左下)
            LegendItem(well, "L0", 24f, 44f, 64f, "A-Z", "検索");
            LegendItem(well, "L1", 24f + 64f + 12f + 2f * 18f + 24f, 44f, 64f, "↑ ↓", "曲");
            LegendItem(well, "L2", 24f + 2f * (64f + 12f + 18f + 24f) + 24f, 44f, 56f, "Tab", "絞り込み");

            // 右: ジャケット帯
            BuildJacketBand(640f, 540f);

            // 右: 題名ブロック
            BuildTitleBlock();

            // 右: 下パネル (通常 / Ready で差し替え)
            var panelRt = Stretch(canvas.gameObject, "Panel", 688f, 48f, 572f, 392f);
            Panel(panelRt, V5Tokens.Base, V5Tokens.RadiusMedium);
            if (ready)
            {
                BuildReadyPanel(panelRt.gameObject);
            }
            else
            {
                BuildScorePanel(panelRt.gameObject);
            }

            // 右下: レジェンド / ボタン
            if (ready)
            {
                LegendRight(canvas.gameObject, "RL", new (string, string, float)[]
                {
                    ("Enter", "スタート", 88f),
                    ("← →", "値", 72f),
                    ("Esc", "選曲へ", 56f),
                }, 44f);
                var startButton = PrimaryButton(canvas.gameObject, "Start", 48f, 28f, 204f, 56f, "Start", StartSelected);
                SetInteractable(startButton, HasPlayableSong());
            }
            else
            {
                LegendRight(canvas.gameObject, "RL", new (string, string, float)[]
                {
                    ("Enter", "決定", 88f),
                    ("F2", "履歴", 48f),
                    ("F3", "ランキング", 48f),
                    ("Esc", "タイトルへ", 56f),
                }, 44f);
                var readyButton = PrimaryButton(canvas.gameObject, "Ready", 48f, 28f, 180f, 56f, "Ready", EnterReady);
                SetInteractable(readyButton, HasPlayableSong());
            }
        }

        /// <summary>右側のジャケット帯 (左端 left, 高さ h, design px)。幅は画面に伸縮。</summary>
        private void BuildJacketBand(float left, float h)
        {
            float width = Screen.width - U(left);
            var bandRt = Stretch(canvas.gameObject, "Jacket", left, 0f, 0f, h);
            bandRt.gameObject.AddComponent<RectMask2D>();
            var imgGo = V5Ui.Node(bandRt.gameObject, "Cover");
            var imgRt = imgGo.GetComponent<RectTransform>();
            imgRt.anchorMin = new Vector2(0.5f, 0.5f);
            imgRt.anchorMax = new Vector2(0.5f, 0.5f);
            imgRt.pivot = new Vector2(0.5f, 0.5f);
            imgRt.sizeDelta = new Vector2(width, U(h));

            var img = imgGo.AddComponent<Image>();
            img.color = V5Tokens.Base;
            img.raycastTarget = false;

            var song = GManager.GetSong(GManager.songID);
            var cached = LoadJacket(song);
            if (cached != null)
            {
                ApplyCover(img, cached, new Vector2(width, U(h)));
            }
            else if (song != null && !string.IsNullOrEmpty(song.videoId))
            {
                StartCoroutine(FetchWebJacket(song.videoId, img, new Vector2(width, U(h))));
            }

            var dim = V5Ui.Node(bandRt.gameObject, "Dim");
            FillRect(dim.GetComponent<RectTransform>());
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.30f);
            dimImg.raycastTarget = false;
        }

        private void BuildTitleBlock()
        {
            var song = GManager.GetSong(GManager.songID);
            if (song == null) return;

            var titleTmp = AutoText(canvas.gameObject, "Title", song.title, U(64), V5Tokens.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            var titleRt = titleTmp.rectTransform;
            titleRt.anchorMin = new Vector2(1f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(1f, 1f);
            titleRt.anchoredPosition = new Vector2(-U(48), -U(300));
            titleRt.sizeDelta = new Vector2(U(900), U(80));

            var composerTmp = Text(canvas.gameObject, "Composer", song.musician, U(26), V5Tokens.TextMuted, TextAlignmentOptions.Right);
            var composerRt = composerTmp.rectTransform;
            composerRt.anchorMin = new Vector2(1f, 1f);
            composerRt.anchorMax = new Vector2(1f, 1f);
            composerRt.pivot = new Vector2(1f, 1f);
            composerRt.anchoredPosition = new Vector2(-U(48), -U(392));
            composerRt.sizeDelta = new Vector2(U(600), U(34));
            composerTmp.overflowMode = TextOverflowModes.Ellipsis;

            // タグ (No / 曲長)
            var tag1 = PanelAt(canvas.gameObject, "Tag No", 688f, 48f, 110f, 32f, new Color(0f, 0f, 0f, 0.6f), V5Tokens.RadiusSmall);
            var tag1Tmp = Text(tag1, "Text", "No." + GManager.songID, U(18), V5Tokens.Text, TextAlignmentOptions.Center);
            FillRect(tag1Tmp.rectTransform);

            string duration = SelectViewLogic.DurationText(song.duration);
            if (duration != null)
            {
                var tag2 = PanelAt(canvas.gameObject, "Tag Length", 806f, 48f, 130f, 32f, new Color(0f, 0f, 0f, 0.6f), V5Tokens.RadiusSmall);
                var tag2Tmp = Text(tag2, "Text", duration, U(18), V5Tokens.Text, TextAlignmentOptions.Center);
                FillRect(tag2Tmp.rectTransform);
            }

            // BIGSTAT (BPM / NOTES / LEVEL) 右寄せ
            BuildBigStat("BPM", song.bpm.ToString(), 2);
            BuildBigStat("NOTES", song.totalHits.ToString(), 1);
            BuildBigStat("LEVEL", song.level.ToString(), 0);
        }

        private void BuildBigStat(string label, string value, int indexFromRight)
        {
            float w = 150f;
            float gap = 40f;
            float right = 48f + indexFromRight * (w + gap);

            var valueTmp = Text(canvas.gameObject, "Stat " + label, value, U(44), V5Tokens.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            var valueRt = valueTmp.rectTransform;
            valueRt.anchorMin = new Vector2(1f, 1f);
            valueRt.anchorMax = new Vector2(1f, 1f);
            valueRt.pivot = new Vector2(1f, 1f);
            valueRt.anchoredPosition = new Vector2(-U(right), -U(440));
            valueRt.sizeDelta = new Vector2(U(w), U(56));

            var labelTmp = Text(canvas.gameObject, "StatLabel " + label, label, U(18), V5Tokens.TextMuted, TextAlignmentOptions.Right);
            var labelRt = labelTmp.rectTransform;
            labelRt.anchorMin = new Vector2(1f, 1f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.pivot = new Vector2(1f, 1f);
            labelRt.anchoredPosition = new Vector2(-U(right), -U(502));
            labelRt.sizeDelta = new Vector2(U(w), U(24));
        }

        /// <summary>02 の下パネル: ScoreRail + BEST + SONG RANKING / PLAYERS。</summary>
        private void BuildScorePanel(GameObject panel)
        {
            int hiscore = HiscoreAt(GManager.songID);

            ScoreRail(panel, "ScoreRail", 24f, 24f, 600f, hiscore);

            // BEST (右寄せ)
            var bestLabel = Text(panel, "BestLabel", "BEST", U(18), V5Tokens.TextMuted, TextAlignmentOptions.Right);
            var bestLabelRt = bestLabel.rectTransform;
            bestLabelRt.anchorMin = new Vector2(1f, 1f);
            bestLabelRt.anchorMax = new Vector2(1f, 1f);
            bestLabelRt.pivot = new Vector2(1f, 1f);
            bestLabelRt.anchoredPosition = new Vector2(-U(24), -U(20));
            bestLabelRt.sizeDelta = new Vector2(U(300), U(26));

            var bestValue = Text(panel, "BestValue", SelectViewLogic.ScoreText(hiscore), U(44), V5Tokens.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            var bestValueRt = bestValue.rectTransform;
            bestValueRt.anchorMin = new Vector2(1f, 1f);
            bestValueRt.anchorMax = new Vector2(1f, 1f);
            bestValueRt.pivot = new Vector2(1f, 1f);
            bestValueRt.anchoredPosition = new Vector2(-U(96), -U(50));
            bestValueRt.sizeDelta = new Vector2(U(300), U(56));

            GradeChipRight(panel, "BestGrade", SelectViewLogic.GradeOf(hiscore), 76f, 62f, 52f, 32f);

            // SONG RANKING (ローカルの自分の記録のみ)
            var rankingWell = PanelAt(panel, "Song Ranking", 24f, 104f, 560f, 264f, V5Tokens.Key2, V5Tokens.RadiusMedium);
            WellHead(rankingWell, "SONG RANKING");
            if (hiscore > 0)
            {
                RankRow(rankingWell, 8f, 42f, 544f, "1", "YOU", hiscore, true);
            }
            else
            {
                EmptyText(rankingWell, "スコアがまだありません", 560f, 264f);
            }

            // PLAYERS (プレイヤーごとの記録はローカルに無い)
            var playersWell = PanelAtRight(panel, "Players", 24f, 104f, 560f, 264f, V5Tokens.Key2, V5Tokens.RadiusMedium);
            WellHead(playersWell, "PLAYERS (累計)");
            EmptyText(playersWell, "ローカルモードでは\nプレイヤーごとの記録はありません", 560f, 264f);
        }

        /// <summary>右寄せのグレードチップ (panel の右 inset 基準, design px)。</summary>
        private void GradeChipRight(GameObject parent, string name, string grade, float rightInset, float yTop, float w, float h)
        {
            var go = PanelAtRight(parent, name, rightInset, yTop, w, h, V5Tokens.ControlBg, V5Tokens.RadiusSmall);
            var tmp = Text(go, "Grade", grade ?? "–", U(16), V5Tokens.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            FillRect(tmp.rectTransform);
        }

        private void WellHead(GameObject well, string label)
        {
            var head = Text(well, "Head", label, U(18), V5Tokens.TextMuted);
            var headRt = head.rectTransform;
            headRt.anchorMin = new Vector2(0f, 1f);
            headRt.anchorMax = new Vector2(1f, 1f);
            headRt.offsetMin = new Vector2(U(16), -U(36));
            headRt.offsetMax = new Vector2(-U(16), -U(8));
        }

        /// <summary>design RankRow (順位 / 名前 / 右寄せスコア + グレードチップ)。</summary>
        private void RankRow(GameObject parent, float x, float yTop, float w, string pos, string name, int score, bool highlight)
        {
            var row = PanelAt(parent, "RankRow " + pos, x, yTop, w, 56f, highlight ? V5Tokens.AccentTint : V5Tokens.Recessed1, V5Tokens.RadiusMedium);

            var posTmp = Text(row, "Pos", pos, U(22), V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var posRt = posTmp.rectTransform;
            posRt.anchorMin = new Vector2(0f, 0f);
            posRt.anchorMax = new Vector2(0f, 1f);
            posRt.pivot = new Vector2(0f, 0.5f);
            posRt.anchoredPosition = new Vector2(U(16), 0f);
            posRt.sizeDelta = new Vector2(U(40), 0f);

            var nameTmp = Text(row, "Name", name, U(22), V5Tokens.Text);
            var nameRt = nameTmp.rectTransform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(0f, 1f);
            nameRt.pivot = new Vector2(0f, 0.5f);
            nameRt.anchoredPosition = new Vector2(U(64), 0f);
            nameRt.sizeDelta = new Vector2(U(200), 0f);

            var scoreTmp = Text(row, "Score", SelectViewLogic.ScoreText(score), U(22), V5Tokens.Text, TextAlignmentOptions.Right);
            var scoreRt = scoreTmp.rectTransform;
            scoreRt.anchorMin = new Vector2(1f, 0f);
            scoreRt.anchorMax = new Vector2(1f, 1f);
            scoreRt.pivot = new Vector2(1f, 0.5f);
            scoreRt.anchoredPosition = new Vector2(-U(76), 0f);
            scoreRt.sizeDelta = new Vector2(U(280), 0f);

            GradeChip(row, "Grade", SelectViewLogic.GradeOf(score), w - 60f, 15f, 44f, 26f);
        }

        /// <summary>02c の下パネル: このプレイの設定。</summary>
        private void BuildReadyPanel(GameObject panel)
        {
            var head = Text(panel, "Head", "このプレイの設定", U(26), V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var headRt = head.rectTransform;
            headRt.anchorMin = new Vector2(0f, 1f);
            headRt.anchorMax = new Vector2(0f, 1f);
            headRt.pivot = new Vector2(0f, 0f);
            headRt.anchoredPosition = new Vector2(U(24), -U(20));
            headRt.sizeDelta = new Vector2(U(400), U(36));

            var well = PanelAt(panel, "Options", 24f, 80f, 704f, 224f, V5Tokens.Key2, V5Tokens.RadiusMedium);
            string[] labels = { "Key Guide", "Lyrics", "NoteSpeed" };
            string[] values =
            {
                PlaySession.KeyGuideLabel(PlaySession.KeyGuide),
                PlaySession.LyricsLabel(PlaySession.LyricsEnabled),
                Mathf.RoundToInt(GManager.noteSpeed).ToString(),
            };
            for (int i = 0; i < 3; i++)
            {
                int rowIdx = i;
                var row = PanelAt(well, "SettingRow " + labels[i], 8f, 8f + i * 66f, 688f, 64f,
                    i == readyRow ? V5Tokens.Key6 : V5Tokens.Recessed1, V5Tokens.RadiusMedium);

                var labelTmp = Text(row, "Label", labels[i], U(24), V5Tokens.Text);
                var labelRt = labelTmp.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(0f, 1f);
                labelRt.pivot = new Vector2(0f, 0.5f);
                labelRt.anchoredPosition = new Vector2(U(20), 0f);
                labelRt.sizeDelta = new Vector2(U(300), 0f);

                var prev = SmallButton(row, "Prev", 492f, 12f, 40f, 40f, "←", () =>
                {
                    Sound();
                    readyRow = rowIdx;
                    ChangeReadyValue(-1);
                });
                var prevRt = prev.GetComponent<RectTransform>();
                prevRt.anchorMin = new Vector2(0f, 0.5f);
                prevRt.anchorMax = new Vector2(0f, 0.5f);
                prevRt.pivot = new Vector2(0f, 0.5f);
                prevRt.anchoredPosition = new Vector2(U(492), 0f);
                prevRt.sizeDelta = new Vector2(U(40), U(40));

                var valueTmp = Text(row, "Value", values[i], U(24), V5Tokens.Text, TextAlignmentOptions.Center);
                var valueRt = valueTmp.rectTransform;
                valueRt.anchorMin = new Vector2(0f, 0f);
                valueRt.anchorMax = new Vector2(0f, 1f);
                valueRt.pivot = new Vector2(0f, 0.5f);
                valueRt.anchoredPosition = new Vector2(U(548), 0f);
                valueRt.sizeDelta = new Vector2(U(104), 0f);

                var next = SmallButton(row, "Next", 628f, 12f, 40f, 40f, "→", () =>
                {
                    Sound();
                    readyRow = rowIdx;
                    ChangeReadyValue(1);
                });
                var nextRt = next.GetComponent<RectTransform>();
                nextRt.anchorMin = new Vector2(0f, 0.5f);
                nextRt.anchorMax = new Vector2(0f, 0.5f);
                nextRt.pivot = new Vector2(0f, 0.5f);
                nextRt.anchoredPosition = new Vector2(U(648), 0f);
                nextRt.sizeDelta = new Vector2(U(40), U(40));

                var rowButton = row.AddComponent<Button>();
                rowButton.onClick.AddListener(() =>
                {
                    Sound();
                    readyRow = rowIdx;
                    Rebuild();
                });
            }

            var note = Text(panel, "Note", "演奏前に変えられる設定です。演奏中は\nEsc で停止したときだけ変更できます。", U(18), V5Tokens.TextMuted);
            var noteRt = note.rectTransform;
            noteRt.anchorMin = new Vector2(1f, 1f);
            noteRt.anchorMax = new Vector2(1f, 1f);
            noteRt.pivot = new Vector2(1f, 1f);
            noteRt.anchoredPosition = new Vector2(-U(24), -U(88));
            noteRt.sizeDelta = new Vector2(U(400), U(66));

            // Issue #167: Start ボタンは画面右下の 1 個だけにする (パネル内にもあると二重)。
        }

        // --- 02d History ---

        private void BuildHistory()
        {
            var ids = HistoryIds();
            var well = LeftWell(820f);

            SmallButton(well, "Back", 48f, 48f, 56f, 56f, "←", () => { state = ScreenState.List; Rebuild(); });

            var titleTmp = Text(well, "Title", "HISTORY", U(44), V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var titleRt = titleTmp.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(U(124), -U(44));
            titleRt.sizeDelta = new Vector2(U(400), U(64));

            var countTmp = Text(well, "Count", ids.Count + " plays", U(24), V5Tokens.TextMuted, TextAlignmentOptions.Right);
            var countRt = countTmp.rectTransform;
            countRt.anchorMin = new Vector2(1f, 1f);
            countRt.anchorMax = new Vector2(1f, 1f);
            countRt.pivot = new Vector2(1f, 1f);
            countRt.anchoredPosition = new Vector2(-U(48), -U(60));
            countRt.sizeDelta = new Vector2(U(240), U(34));

            // 履歴リスト (日付データは無いのでフラットにスコア順)
            var list = V5Ui.Node(well, "List");
            var listRt = list.GetComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0f, 0f);
            listRt.anchorMax = new Vector2(1f, 1f);
            listRt.offsetMin = new Vector2(U(24), U(180));
            listRt.offsetMax = new Vector2(-U(24), -U(196));

            if (ids.Count == 0)
            {
                EmptyText(list, "まだ記録がありません", 772f, 704f);
            }
            else
            {
                float listH = Screen.height - U(196) - U(180);
                float pitch = U(58);
                int visible = Mathf.Max(1, Mathf.FloorToInt(listH / pitch));
                int offset = SelectViewLogic.ScrollOffset(ids.Count, historyIndex, visible);
                for (int i = offset; i < Mathf.Min(offset + visible, ids.Count); i++)
                {
                    int songId = ids[i];
                    var song = GManager.GetSong(songId);
                    if (song == null) continue;
                    var row = PanelAt(list, "HistoryRow " + i, 0f, (i - offset) * 58f, 772f, 56f,
                        i == historyIndex ? V5Tokens.Key6 : V5Tokens.Recessed2, V5Tokens.RadiusMedium);

                    var nameTmp = Text(row, "Song", song.title, U(24), V5Tokens.Text);
                    var nameRt = nameTmp.rectTransform;
                    nameRt.anchorMin = new Vector2(0f, 0f);
                    nameRt.anchorMax = new Vector2(0f, 1f);
                    nameRt.pivot = new Vector2(0f, 0.5f);
                    nameRt.anchoredPosition = new Vector2(U(16), 0f);
                    nameRt.sizeDelta = new Vector2(U(380), 0f);
                    nameTmp.overflowMode = TextOverflowModes.Ellipsis;

                    GradeChip(row, "Grade", SelectViewLogic.GradeOf(GManager.Hiscore[songId]), 772f - 250f, 15f, 44f, 26f);

                    var scoreTmp = Text(row, "Score", SelectViewLogic.ScoreText(GManager.Hiscore[songId]), U(24), V5Tokens.Text, TextAlignmentOptions.Right);
                    var scoreRt = scoreTmp.rectTransform;
                    scoreRt.anchorMin = new Vector2(1f, 0f);
                    scoreRt.anchorMax = new Vector2(1f, 1f);
                    scoreRt.pivot = new Vector2(1f, 0.5f);
                    scoreRt.anchoredPosition = new Vector2(-U(16), 0f);
                    scoreRt.sizeDelta = new Vector2(U(200), 0f);

                    var button = row.AddComponent<Button>();
                    button.onClick.AddListener(() =>
                    {
                        Sound();
                        historyIndex = ids.IndexOf(songId);
                        Rebuild();
                    });
                }
            }

            LegendItem(well, "L0", 24f, 44f, 64f, "↑ ↓", "選択");
            LegendItem(well, "L1", 24f + 64f + 12f + 2f * 18f + 24f, 44f, 56f, "Esc", "戻る");

            // 右: 記録の詳細 (選択中の曲)
            if (ids.Count > 0 && historyIndex < ids.Count)
            {
                BuildHistoryDetail(ids[historyIndex]);
            }
            else
            {
                var empty = Text(canvas.gameObject, "Empty", "履歴はまだありません", U(24), V5Tokens.TextMuted, TextAlignmentOptions.Center);
                FillRect(empty.rectTransform);
            }
        }

        private void BuildHistoryDetail(int songId)
        {
            var song = GManager.GetSong(songId);
            if (song == null) return;
            int hiscore = HiscoreAt(songId);

            // ジャケット帯
            float width = Screen.width - U(820f);
            var bandRt = Stretch(canvas.gameObject, "Jacket", 820f, 0f, 0f, 320f);
            bandRt.gameObject.AddComponent<RectMask2D>();
            var imgGo = V5Ui.Node(bandRt.gameObject, "Cover");
            var imgRt = imgGo.GetComponent<RectTransform>();
            imgRt.anchorMin = new Vector2(0.5f, 0.5f);
            imgRt.anchorMax = new Vector2(0.5f, 0.5f);
            imgRt.sizeDelta = new Vector2(width, U(320));
            var img = imgGo.AddComponent<Image>();
            img.color = V5Tokens.Base;
            img.raycastTarget = false;
            var cached = LoadJacket(song);
            if (cached != null)
            {
                ApplyCover(img, cached, new Vector2(width, U(320)));
            }
            else if (!string.IsNullOrEmpty(song.videoId))
            {
                StartCoroutine(FetchWebJacket(song.videoId, img, new Vector2(width, U(320))));
            }

            var dim = V5Ui.Node(bandRt.gameObject, "Dim");
            FillRect(dim.GetComponent<RectTransform>());
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.5f);
            dimImg.raycastTarget = false;

            var titleTmp = AutoText(canvas.gameObject, "Title", song.title, U(44), V5Tokens.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            var titleRt = titleTmp.rectTransform;
            titleRt.anchorMin = new Vector2(1f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(1f, 1f);
            titleRt.anchoredPosition = new Vector2(-U(48), -U(196));
            titleRt.sizeDelta = new Vector2(U(700), U(56));

            var composerTmp = Text(canvas.gameObject, "Composer", song.musician, U(20), V5Tokens.TextMuted, TextAlignmentOptions.Right);
            var composerRt = composerTmp.rectTransform;
            composerRt.anchorMin = new Vector2(1f, 1f);
            composerRt.anchorMax = new Vector2(1f, 1f);
            composerRt.pivot = new Vector2(1f, 1f);
            composerRt.anchoredPosition = new Vector2(-U(48), -U(267));
            composerRt.sizeDelta = new Vector2(U(700), U(28));

            // グレード (大)
            string grade = SelectViewLogic.GradeOf(hiscore);
            var gradeTmp = Text(canvas.gameObject, "Grade", grade ?? "–", U(200), V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var gradeRt = gradeTmp.rectTransform;
            gradeRt.anchorMin = new Vector2(0f, 1f);
            gradeRt.anchorMax = new Vector2(0f, 1f);
            gradeRt.pivot = new Vector2(0f, 1f);
            gradeRt.anchoredPosition = new Vector2(U(880), -U(340));
            gradeRt.sizeDelta = new Vector2(U(300), U(280));

            var scoreLabel = Text(canvas.gameObject, "ScoreLabel", "SCORE", U(18), V5Tokens.TextMuted, TextAlignmentOptions.Right);
            var scoreLabelRt = scoreLabel.rectTransform;
            scoreLabelRt.anchorMin = new Vector2(1f, 1f);
            scoreLabelRt.anchorMax = new Vector2(1f, 1f);
            scoreLabelRt.pivot = new Vector2(1f, 1f);
            scoreLabelRt.anchoredPosition = new Vector2(-U(48), -U(380));
            scoreLabelRt.sizeDelta = new Vector2(U(400), U(26));

            var scoreTmp = Text(canvas.gameObject, "Score", SelectViewLogic.ScoreText(hiscore), U(64), V5Tokens.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            var scoreRt = scoreTmp.rectTransform;
            scoreRt.anchorMin = new Vector2(1f, 1f);
            scoreRt.anchorMax = new Vector2(1f, 1f);
            scoreRt.pivot = new Vector2(1f, 1f);
            scoreRt.anchoredPosition = new Vector2(-U(48), -U(414));
            scoreRt.sizeDelta = new Vector2(U(400), U(80));

            ScoreRailRight(canvas.gameObject, "ScoreRail", 48f, 590f, 600f, hiscore);

            // 判定ストリップ — 判定内訳は未記録のため「–」
            string[] judgeLabels = { "Perfect", "Great", "Bad", "Miss", "Hold", "MaxCombo" };
            Color[] judgeColors =
            {
                new Color(0xED / 255f, 0xED / 255f, 0xED / 255f),
                V5Tokens.TextMuted,
                new Color(0x7A / 255f, 0x7A / 255f, 0x7A / 255f),
                V5Tokens.Danger,
                V5Tokens.Accent,
                V5Tokens.Raised4,
            };
            for (int i = 0; i < judgeLabels.Length; i++)
            {
                var cell = AtRight(canvas.gameObject, "Judge " + judgeLabels[i], 48f + (judgeLabels.Length - 1 - i) * 165f, 690f, 165f, 96f);

                var marker = V5Ui.Node(cell.gameObject, "Marker");
                var markerRt = marker.GetComponent<RectTransform>();
                markerRt.anchorMin = new Vector2(1f, 1f);
                markerRt.anchorMax = new Vector2(1f, 1f);
                markerRt.pivot = new Vector2(1f, 1f);
                markerRt.anchoredPosition = new Vector2(-U(16), 0f);
                markerRt.sizeDelta = new Vector2(U(24), U(6));
                var markerImg = marker.AddComponent<Image>();
                markerImg.color = judgeColors[i];
                markerImg.raycastTarget = false;

                var valueTmp = Text(cell.gameObject, "Value", "–", U(40), V5Tokens.Text, TextAlignmentOptions.Center);
                var valueRt = valueTmp.rectTransform;
                valueRt.anchorMin = new Vector2(0f, 1f);
                valueRt.anchorMax = new Vector2(1f, 1f);
                valueRt.offsetMin = new Vector2(0f, -U(62));
                valueRt.offsetMax = new Vector2(0f, -U(10));

                var labelTmp = Text(cell.gameObject, "Label", judgeLabels[i], U(18), V5Tokens.TextMuted, TextAlignmentOptions.Center);
                var labelRt = labelTmp.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 0f);
                labelRt.offsetMin = new Vector2(0f, U(4));
                labelRt.offsetMax = new Vector2(0f, U(30));
            }

            // 判定比率バー — 内訳が無いので空のレールのみ
            PanelAtRight(canvas.gameObject, "JudgeRatio", 48f, 810f, 992f, 16f, V5Tokens.Key0, 4f);

            LegendRight(canvas.gameObject, "RL", new (string, string, float)[]
            {
                ("Enter", "この曲へ", 88f),
                ("Esc", "戻る", 56f),
            }, 44f);
            PrimaryButton(canvas.gameObject, "ToSong", 48f, 28f, 164f, 56f, "この曲へ", () => SelectHistorySong(HistoryIds()));
        }

        // --- 07 Ranking ---

        private void BuildRanking()
        {
            int total = Mathf.Max(0, GManager.ActiveSongs.Length - 1);
            var well = LeftWell(480f);

            SmallButton(well, "Back", 48f, 48f, 56f, 56f, "←", () => { state = ScreenState.List; Rebuild(); });

            var titleTmp = Text(well, "Title", "RANKING", U(44), V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var titleRt = titleTmp.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(U(124), -U(44));
            titleRt.sizeDelta = new Vector2(U(340), U(64));

            var list = V5Ui.Node(well, "Songs");
            var listRt = list.GetComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0f, 1f);
            listRt.anchorMax = new Vector2(1f, 1f);
            listRt.offsetMin = new Vector2(U(24), -U(136) - U(590));
            listRt.offsetMax = new Vector2(-U(24), -U(136));

            float pitch = U(74);
            int visible = Mathf.Max(1, Mathf.FloorToInt(U(590) / pitch));
            int selIdx = Mathf.Clamp(GManager.songID - 1, 0, Mathf.Max(0, total - 1));
            int offset = SelectViewLogic.ScrollOffset(total, selIdx, visible);
            for (int i = offset; i < Mathf.Min(offset + visible, total); i++)
            {
                int songId = i + 1;
                SongRow(list, "Song " + songId, songId, 0f, (i - offset) * 74f, 432f, 72f, songId == GManager.songID);
            }

            LegendItem(well, "L0", 24f, 44f, 64f, "↑ ↓", "曲");
            LegendItem(well, "L1", 24f + 64f + 12f + 18f + 24f, 44f, 56f, "Tab", "列の切替");
            LegendItem(well, "L2", 24f + (64f + 12f + 18f + 24f) + (56f + 12f + 4f * 18f + 24f) + 24f, 44f, 56f, "Esc", "戻る");

            // 中央: 曲別ランキング (ローカルは自分の記録のみ)。左ウェルと右パネルの間に伸縮。
            var songPanelRt = Stretch(canvas.gameObject, "Song Ranking", 520f, 760f, 48f, 760f);
            var songPanel = Panel(songPanelRt, V5Tokens.Base, V5Tokens.RadiusMedium);
            var songHead = Text(songPanel, "Head", "曲別ランキング", U(26), V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var songHeadRt = songHead.rectTransform;
            songHeadRt.anchorMin = new Vector2(0f, 1f);
            songHeadRt.anchorMax = new Vector2(0f, 1f);
            songHeadRt.pivot = new Vector2(0f, 0f);
            songHeadRt.anchoredPosition = new Vector2(U(24), -U(24));
            songHeadRt.sizeDelta = new Vector2(U(300), U(36));

            var song = GManager.GetSong(GManager.songID);
            // Issue #167: どちらのランキング在看かを明示する (Local / Online)。
            string songSubText = (song != null ? song.title : "")
                + (rankingSegment == 1 ? " / オンライン" : " / この端末の記録");
            var songSub = Text(songPanel, "Sub", songSubText, U(18), V5Tokens.TextMuted);
            var songSubRt = songSub.rectTransform;
            songSubRt.anchorMin = new Vector2(0f, 1f);
            songSubRt.anchorMax = new Vector2(0f, 1f);
            songSubRt.pivot = new Vector2(0f, 0f);
            songSubRt.anchoredPosition = new Vector2(U(24), -U(66));
            songSubRt.sizeDelta = new Vector2(U(340), U(26));
            songSub.overflowMode = TextOverflowModes.Ellipsis;

            // Local / Online セグメント
            var segment = PanelAtRight(songPanel, "Segment", 24f, 32f, 215f, 56f, V5Tokens.Key0, V5Tokens.RadiusMedium);
            var localBtn = SmallButton(segment, "Local", 4f, 4f, 103f, 48f, "Local", () =>
            {
                Sound();
                rankingSegment = 0;
                Rebuild();
            });
            var localBtnTmp = localBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (localBtnTmp != null)
            {
                localBtnTmp.fontStyle = rankingSegment == 0 ? FontStyles.Bold : FontStyles.Normal;
                localBtnTmp.color = rankingSegment == 0 ? V5Tokens.Text : V5Tokens.TextMuted;
            }

            // Issue #167: Online セグメントは「オンラインランキングがない曲」では出さない
            // (未接続 表示だけの列を作らない)。
            bool onlineAvailable = CanFetchOnlineRanking(song);
            if (!onlineAvailable && rankingSegment == 1)
            {
                rankingSegment = 0;
            }

            if (onlineAvailable)
            {
                var onlineBtn = SmallButton(segment, "Online", 108f, 4f, 103f, 48f, "Online", () =>
                {
                    Sound();
                    rankingSegment = 1;
                    FetchOnlineRanking(song);
                    Rebuild();
                });
                var onlineBtnTmp = onlineBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (onlineBtnTmp != null)
                {
                    onlineBtnTmp.fontStyle = rankingSegment == 1 ? FontStyles.Bold : FontStyles.Normal;
                    onlineBtnTmp.color = rankingSegment == 1 ? V5Tokens.Text : V5Tokens.TextMuted;
                }
            }

            var songWell = PanelAt(songPanel, "List Well", 24f, 107f, 592f, 629f, V5Tokens.Key2, V5Tokens.RadiusMedium);
            int hiscore = HiscoreAt(GManager.songID);
            if (rankingSegment == 1)
            {
                BuildOnlineRankRows(songWell);
            }
            else if (hiscore > 0)
            {
                RankRow(songWell, 8f, 8f, 576f, "1", "YOU", hiscore, true);
            }
            else
            {
                EmptyText(songWell, "スコアがまだありません", 592f, 629f);
            }

            // 右: プレイヤーランキング (Issue #167: 実体はこの端末の記録だけだと明記する)
            var playersPanel = PanelAtRight(canvas.gameObject, "Players", 48f, 48f, 688f, 760f, V5Tokens.Base, V5Tokens.RadiusMedium);
            var playersHead = Text(playersPanel, "Head", "この端末の記録 (ローカル)", U(26), V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var playersHeadRt = playersHead.rectTransform;
            playersHeadRt.anchorMin = new Vector2(0f, 1f);
            playersHeadRt.anchorMax = new Vector2(0f, 1f);
            playersHeadRt.pivot = new Vector2(0f, 0f);
            playersHeadRt.anchoredPosition = new Vector2(U(24), -U(24));
            playersHeadRt.sizeDelta = new Vector2(U(500), U(36));

            var playersSub = Text(playersPanel, "Sub", "オンライン未接続 / 累計 = 各曲のベストスコアの合計", U(18), V5Tokens.TextMuted);
            var playersSubRt = playersSub.rectTransform;
            playersSubRt.anchorMin = new Vector2(0f, 1f);
            playersSubRt.anchorMax = new Vector2(0f, 1f);
            playersSubRt.pivot = new Vector2(0f, 0f);
            playersSubRt.anchoredPosition = new Vector2(U(24), -U(66));
            playersSubRt.sizeDelta = new Vector2(U(500), U(26));

            var playersWell = PanelAt(playersPanel, "List Well", 24f, 107f, 640f, 629f, V5Tokens.Key2, V5Tokens.RadiusMedium);
            EmptyText(playersWell, "この端末の記録のみを\n表示しています (オンライン未接続)", 640f, 629f);

            // 下: プレイヤーサマリ (Hiscore から計算できる値のみ)。左右に伸縮。
            var profileRt = Stretch(canvas.gameObject, "Player Profile", 520f, 48f, 832f, 184f);
            var profile = Panel(profileRt, V5Tokens.Base, V5Tokens.RadiusMedium);
            var youTmp = Text(profile, "Name", "YOU", U(44), V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var youRt = youTmp.rectTransform;
            youRt.anchorMin = new Vector2(0f, 1f);
            youRt.anchorMax = new Vector2(0f, 1f);
            youRt.pivot = new Vector2(0f, 1f);
            youRt.anchoredPosition = new Vector2(U(32), -U(24));
            youRt.sizeDelta = new Vector2(U(200), U(56));

            var rankTmp = Text(profile, "Rank", "–位 / –人", U(18), V5Tokens.TextMuted);
            var rankRt = rankTmp.rectTransform;
            rankRt.anchorMin = new Vector2(0f, 1f);
            rankRt.anchorMax = new Vector2(0f, 1f);
            rankRt.pivot = new Vector2(0f, 1f);
            rankRt.anchoredPosition = new Vector2(U(32), -U(104));
            rankRt.sizeDelta = new Vector2(U(200), U(26));

            int sum = 0;
            int played = 0;
            int sPlus = 0;
            for (int i = 1; i < GManager.ActiveSongs.Length && i < GManager.Hiscore.Length; i++)
            {
                if (GManager.Hiscore[i] <= 0) continue;
                sum += GManager.Hiscore[i];
                played++;
                string grade = SelectViewLogic.GradeOf(GManager.Hiscore[i]);
                if (grade == "S" || grade == "SS") sPlus++;
            }

            BuildProfileStat(profile, "累計", SelectViewLogic.ScoreText(sum), 0);
            BuildProfileStat(profile, "プレイ曲", played + " / " + total, 1);
            BuildProfileStat(profile, "S 以上", sPlus.ToString(), 2);
        }

        /// <summary>オンラインランキングを取得できる曲か (cloud 曲のみ)。</summary>
        private static bool CanFetchOnlineRanking(SongData song)
        {
            if (song == null || !song.isRemote) { return false; }
            if (string.IsNullOrEmpty(song.remoteId)) { return false; }
            if (song.chartVersion <= 0) { return false; }
            return RankingClient.IsValidSongId(song.remoteId);
        }

        /// <summary>オンラインランキングを取得する (RankingClient の公共 API を呼ぶだけ)。</summary>
        private void FetchOnlineRanking(SongData song)
        {
            onlineEntries = null;
            onlineFetchFailed = false;
            onlineLoading = true;
            StartCoroutine(RankingClient.GetLeaderboard(song.remoteId, song.chartVersion, result =>
            {
                onlineLoading = false;
                if (result != null && result.ok && result.leaderboard != null && result.leaderboard.entries != null)
                {
                    onlineEntries = result.leaderboard.entries;
                }
                else
                {
                    onlineEntries = null;
                    onlineFetchFailed = true;
                }

                Rebuild();
            }));
        }

        /// <summary>オンライン順位の行を描く (取得前 / 失敗 / 取得済みの 3 状態)。</summary>
        private void BuildOnlineRankRows(GameObject songWell)
        {
            if (onlineLoading)
            {
                EmptyText(songWell, "取得しています…", 592f, 629f);
                return;
            }

            if (onlineFetchFailed)
            {
                EmptyText(songWell, "ランキングを\nとりにいけませんでした", 592f, 629f);
                return;
            }

            if (onlineEntries == null || onlineEntries.Length == 0)
            {
                EmptyText(songWell, "この曲はまだ\n順位がありません", 592f, 629f);
                return;
            }

            float y = 8f;
            int count = Mathf.Min(onlineEntries.Length, 9);
            for (int i = 0; i < count; i++)
            {
                var entry = onlineEntries[i];
                if (entry == null) { continue; }
                RankRow(songWell, 8f, y, 576f, entry.rank.ToString(),
                    string.IsNullOrEmpty(entry.nickname) ? "YOU" : entry.nickname, entry.score, false);
                y += 64f;
            }
        }

        private void BuildProfileStat(GameObject parent, string label, string value, int index)
        {
            float x = 220f + index * 300f;
            var valueTmp = Text(parent, "Stat " + label, value, U(40), V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var valueRt = valueTmp.rectTransform;
            valueRt.anchorMin = new Vector2(0f, 1f);
            valueRt.anchorMax = new Vector2(0f, 1f);
            valueRt.pivot = new Vector2(0f, 1f);
            valueRt.anchoredPosition = new Vector2(U(x), -U(44));
            valueRt.sizeDelta = new Vector2(U(280), U(52));

            var labelTmp = Text(parent, "StatLabel " + label, label, U(18), V5Tokens.TextMuted);
            var labelRt = labelTmp.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 1f);
            labelRt.anchorMax = new Vector2(0f, 1f);
            labelRt.pivot = new Vector2(0f, 1f);
            labelRt.anchoredPosition = new Vector2(U(x + 4), -U(104));
            labelRt.sizeDelta = new Vector2(U(280), U(26));
        }

        /// <summary>右寄せの ScoreRail (rightInset は design px)。</summary>
        private void ScoreRailRight(GameObject parent, string name, float rightInset, float yTop, float w, int hiscore)
        {
            var rt = AtRight(parent, name, rightInset, yTop, w, 50f);
            BuildScoreRail(rt.gameObject, w, hiscore);
        }

        /// <summary>design ScoreRail: C/B/A/S/SS/+ の帯 + フィル + BEST マーカー。</summary>
        private void ScoreRail(GameObject parent, string name, float x, float yTop, float w, int hiscore)
        {
            var rail = At(parent, name, x, yTop, w, 50f);
            BuildScoreRail(rail.gameObject, w, hiscore);
        }

        private void BuildScoreRail(GameObject rail, float w, int hiscore)
        {
            var track = V5Ui.Node(rail.gameObject, "Track");
            var trackRt = track.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0f, 1f);
            trackRt.anchorMax = new Vector2(1f, 1f);
            trackRt.offsetMin = new Vector2(0f, -U(14));
            trackRt.offsetMax = new Vector2(0f, 0f);

            // セグメント (design 実測: 120/120/120/120/40/70, 間 2px)
            float[] seg = { 120f, 120f, 120f, 120f, 40f, 70f };
            string[] labels = { "C", "B", "A", "S", "SS", "+" };
            float total = 0f;
            for (int i = 0; i < seg.Length; i++) total += seg[i] + (i < seg.Length - 1 ? 2f : 0f);
            float scale = w / total; // design px 比
            float cx = 0f;
            for (int i = 0; i < seg.Length; i++)
            {
                var segGo = V5Ui.Node(track, "Seg " + labels[i]);
                var segRt = segGo.GetComponent<RectTransform>();
                segRt.anchorMin = new Vector2(0f, 0f);
                segRt.anchorMax = new Vector2(0f, 1f);
                segRt.pivot = new Vector2(0f, 0.5f);
                segRt.anchoredPosition = new Vector2(U(cx), 0f);
                segRt.sizeDelta = new Vector2(U(seg[i] * scale), 0f);
                var segImg = segGo.AddComponent<Image>();
                segImg.color = i == 5 ? new Color(0x3A / 255f, 0x32 / 255f, 0x20 / 255f) : V5Tokens.Key0;
                segImg.raycastTarget = false;

                var labelTmp = Text(rail.gameObject, "Label " + labels[i], labels[i], U(16), V5Tokens.TextMuted, TextAlignmentOptions.Center);
                var labelRt = labelTmp.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 1f);
                labelRt.anchorMax = new Vector2(0f, 1f);
                labelRt.pivot = new Vector2(0.5f, 0f);
                labelRt.anchoredPosition = new Vector2(U(cx + seg[i] * scale / 2f), -U(20));
                labelRt.sizeDelta = new Vector2(U(seg[i] * scale), U(24));

                cx += seg[i] * scale + 2f;
            }

            // フィル (スコア比) + BEST マーカー
            float ratio = Mathf.Clamp01((float)hiscore / SelectViewLogic.ScoreMax);
            float fillW = ratio * U(w);
            if (ratio > 0f)
            {
                var fillGo = V5Ui.Node(rail.gameObject, "Fill");
                var fillRt = fillGo.GetComponent<RectTransform>();
                fillRt.anchorMin = new Vector2(0f, 1f);
                fillRt.anchorMax = new Vector2(0f, 1f);
                fillRt.pivot = new Vector2(0f, 0.5f);
                fillRt.anchoredPosition = new Vector2(0f, -U(7));
                fillRt.sizeDelta = new Vector2(fillW, U(14));
                var fillImg = fillGo.AddComponent<Image>();
                fillImg.color = new Color(0xED / 255f, 0xED / 255f, 0xED / 255f);
                fillImg.raycastTarget = false;

                var markerGo = V5Ui.Node(rail.gameObject, "Best");
                var markerRt = markerGo.GetComponent<RectTransform>();
                markerRt.anchorMin = new Vector2(0f, 1f);
                markerRt.anchorMax = new Vector2(0f, 1f);
                markerRt.pivot = new Vector2(0.5f, 0.5f);
                markerRt.anchoredPosition = new Vector2(fillW, -U(7));
                markerRt.sizeDelta = new Vector2(U(4), U(22));
                var markerImg = markerGo.AddComponent<Image>();
                markerImg.color = V5Tokens.Warn;
                markerImg.raycastTarget = false;
            }
        }

        private void EmptyText(GameObject parent, string content, float w, float h)
        {
            var tmp = Text(parent, "Empty", content, U(20), V5Tokens.TextMuted, TextAlignmentOptions.Center);
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(U(w) * 0.9f, U(h) * 0.9f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
        }
    }
}
