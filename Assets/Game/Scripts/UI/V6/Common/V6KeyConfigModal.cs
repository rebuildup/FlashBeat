using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// キー設定モーダル (design <c>vo4kB</c>、設計判断
    /// <c>docs/design/v6/review-2026-11-11-decisions.md</c> §4)。
    /// Option の Play タブと Pause の Play タブにある「キー設定」行から開く。
    ///
    /// - 8 レーンの代表キーを一覧し、行を選んで Enter / クリックで待受に入り、
    ///   次に押されたキーを代表キーへ割り当てる。残りのキーは保持する。
    /// - 他のレーンが使用中のキーと予約キー (UI 操作・F キーなど) は確定しない。
    /// - 「既定に戻す」「キャンセル」「適用」。保存は「適用」だけ。
    /// - 表示中は背後の画面へキーを渡さない (<see cref="BlocksInput"/> を各画面が参照する)。
    ///
    /// 編集モデルは <see cref="V6KeyConfigLogic"/> (UI 非依存・テスト対象)。
    /// </summary>
    public sealed class V6KeyConfigModal : MonoBehaviour
    {
        // --- design vo4kB ---
        public const float DialogX = 444f, DialogY = 124f;
        public const float DialogW = 840f, DialogH = 724f;
        public const float LaneRowW = 784f, LaneRowH = 52f;
        public const float LaneRowStep = 60f; // 行高 52 + 行間 8
        public const float LaneRowX = 28f, LaneRowY = 112f;
        public const float FooterY = 612f;

        /// <summary>モーダルを開いている間 true。閉じたフレームも 1 回だけ true
        /// (同じキーが背後の画面へ二重に届かないようにする)。</summary>
        public static bool BlocksInput
        {
            get
            {
                V6KeyConfigModal current = active;
                return (current != null && current.isOpen) || Time.frameCount == consumedFrame;
            }
        }

        private static V6KeyConfigModal active;
        private static int consumedFrame = -1;
        private static KeyCode[] captureKeys;

        private GameObject rootGo;
        private GameObject dialogGo;
        private V6KeyConfigLogic logic;
        private bool isOpen;
        private readonly List<KeyCap> keyCaps = new List<KeyCap>();
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI hintText;

        /// <summary>閉じたあとに呼ばれる (適用・キャンセルどちらでも)。</summary>
        public Action Closed;

        /// <summary>キーボード上の 1 キーキャップ (Issue #428)。</summary>
        private sealed class KeyCap
        {
            public V6JisKeyboard.Key Def;
            public Image Background;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI LaneBadge;
        }

        /// <summary>開いているか。</summary>
        public bool IsOpen
        {
            get { return isOpen; }
        }

        /// <summary>編集中のモデル (テスト用)。</summary>
        public V6KeyConfigLogic Logic
        {
            get { return logic; }
        }

        /// <summary>モーダルを (非表示で) 組み立てる。parent は design 1728×972 の画面フレーム。</summary>
        public static V6KeyConfigModal Build(GameObject parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var go = new GameObject("V6 Key Config Modal", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = V6Layout.ToLocal(V6Frame.ModalPos);
            rt.sizeDelta = V6Frame.ModalSize;

            var modal = go.AddComponent<V6KeyConfigModal>();
            modal.BuildContent();
            go.SetActive(false);
            return modal;
        }

        /// <summary>開く。現在のキー割り当てを読み直し、編集は適用まで反映しない。</summary>
        public void Show()
        {
            if (rootGo == null)
            {
                return;
            }

            logic = new V6KeyConfigLogic(KeyBindings.Current);
            isOpen = true;
            active = this;
            rootGo.SetActive(true);
            rootGo.transform.SetAsLastSibling();
            Refresh();
        }

        /// <summary>閉じる (編集内容は破棄)。</summary>
        public void Hide()
        {
            bool wasOpen = isOpen;
            isOpen = false;
            if (ReferenceEquals(active, this))
            {
                active = null;
            }

            if (rootGo != null)
            {
                rootGo.SetActive(false);
            }

            if (wasOpen && Closed != null)
            {
                Closed();
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(active, this))
            {
                active = null;
            }
        }

        // --- 構築 ---

        private void BuildContent()
        {
            rootGo = gameObject;

            var scrim = V6Controls.Panel(rootGo, "Scrim", V6Frame.ModalPos, V6Frame.ModalSize,
                V6Tokens.Scrim, 0f);
            scrim.GetComponent<Image>().raycastTarget = true; // 背後のクリックを遮る

            dialogGo = V6Controls.Panel(rootGo, "Dialog", new Vector2(DialogX, DialogY),
                new Vector2(DialogW, DialogH), V6Tokens.Key1, V6Tokens.RadiusLarge);

            V6Controls.Text(dialogGo, "Head", "キー設定", new Vector2(28f, 28f),
                new Vector2(784f, 40f), 32f, V6Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            V6Controls.Text(dialogGo, "Description", "キーをクリックしてレーンを選び、キーを押して割り当てます。",
                new Vector2(28f, 72f), new Vector2(784f, 24f), 22f, V6Tokens.TextDim);

            // Issue #428: テキスト一覧ではなく JIS 配列のビジュアルキーボード。
            // 割当済みキーにはレーン番号バッジを出し、クリックでレーン選択 /
            // 待受中の割当に使う。
            BuildKeyboard();

            // 選択レーンの状態 (衝突・待受など) と操作ヒント。
            statusText = V6Controls.Text(dialogGo, "Status", string.Empty,
                new Vector2(28f, 420f), new Vector2(784f, 26f), 20f, V6Tokens.TextMuted);
            hintText = V6Controls.Text(dialogGo, "Hint",
                "クリック: レーン選択 / Enter: キー待受 / ←→: レーン移動 / Esc: 閉じる",
                new Vector2(28f, 452f), new Vector2(784f, 22f), 18f, V6Tokens.TextDim);

            BuildFooter();
        }

        /// <summary>JIS キーボードを構築する (Issue #428)。</summary>
        private void BuildKeyboard()
        {
            float keyboardX = 28f;
            float keyboardY = 116f;

            for (int r = 0; r < V6JisKeyboard.Rows.Length; r++)
            {
                V6JisKeyboard.Key[] row = V6JisKeyboard.Rows[r];
                // 行ごとに中央揃え (JIS 配列の段差をそのまま見せる)。
                float x = keyboardX + (LaneRowW - V6JisKeyboard.RowWidth(row)) * 0.5f;
                for (int i = 0; i < row.Length; i++)
                {
                    V6JisKeyboard.Key def = row[i];
                    float w = def.Width * V6JisKeyboard.KeyW;
                    var capGo = V6Controls.Panel(dialogGo, "Key " + def.Label,
                        new Vector2(x, keyboardY + r * (V6JisKeyboard.KeyH + V6JisKeyboard.RowGap)),
                        new Vector2(w, V6JisKeyboard.KeyH), V6Tokens.Key3, V6Tokens.RadiusSmall);
                    var cap = new KeyCap { Def = def };
                    cap.Background = capGo.GetComponent<Image>();
                    cap.Label = V6Controls.Text(capGo, "Label", def.Label, Vector2.zero,
                        new Vector2(w, V6JisKeyboard.KeyH), 20f, V6Tokens.Text, TextAlignmentOptions.Center);
                    cap.LaneBadge = V6Controls.Text(capGo, "Lane", string.Empty,
                        new Vector2(w - 22f, 2f), new Vector2(20f, 16f), 14f, V6Tokens.TextMuted,
                        TextAlignmentOptions.Right);

                    if (def.Assignable)
                    {
                        var button = capGo.AddComponent<Button>();
                        button.transition = Selectable.Transition.None;
                        var nav = button.navigation;
                        nav.mode = Navigation.Mode.None;
                        button.navigation = nav;
                        button.onClick.AddListener(() => OnKeyCapClicked(cap));
                    }
                    else
                    {
                        cap.Background.color = new Color(0f, 0f, 0f, 0.001f);
                        cap.Label.color = V6Tokens.TextDim;
                    }

                    keyCaps.Add(cap);
                    x += w + V6JisKeyboard.KeyGap;
                }
            }
        }

        /// <summary>
        /// キーキャップのクリック (Issue #428)。
        /// 待受中は物理キーと同じ検証経路 (<see cref="V6KeyConfigLogic.Assign"/>) で
        /// 割り当てる。それ以外は割当済みキーからレーンを選択する。
        /// </summary>
        private void OnKeyCapClicked(KeyCap cap)
        {
            if (logic == null || !isOpen || cap == null || !cap.Def.Assignable)
            {
                return;
            }

            if (logic.Listening)
            {
                logic.Assign(cap.Def.Code);
                Consume();
                Refresh();
                return;
            }

            int lane = LaneOfKeyCap(cap.Def.Code);
            if (lane >= 0)
            {
                logic.Select(lane);
                Consume();
                Refresh();
            }
        }

        /// <summary>
        /// キーキャップのキーが属するレーン。Shift 付き記号 (Colon/Less など) も
        /// 同じ物理キーとして拾うためエイリアスを併せて調べる。
        /// </summary>
        private int LaneOfKeyCap(KeyCode code)
        {
            int lane = logic.LaneOfKey(code);
            if (lane >= 0)
            {
                return lane;
            }

            KeyCode alias = AliasOf(code);
            return alias != KeyCode.None ? logic.LaneOfKey(alias) : -1;
        }

        /// <summary>同じ物理キーが出す別 KeyCode (Shift 記号)。</summary>
        private static KeyCode AliasOf(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.Semicolon: return KeyCode.Colon;
                case KeyCode.Comma: return KeyCode.Less;
                case KeyCode.Period: return KeyCode.Greater;
                case KeyCode.Slash: return KeyCode.Question;
                case KeyCode.Minus: return KeyCode.Underscore;
                case KeyCode.Equals: return KeyCode.Plus;
                case KeyCode.LeftBracket: return KeyCode.LeftCurlyBracket;
                case KeyCode.RightBracket: return KeyCode.RightCurlyBracket;
                default: return KeyCode.None;
            }
        }

        private void BuildFooter()
        {
            var footer = V6Controls.Panel(dialogGo, "Buttons", new Vector2(28f, FooterY),
                new Vector2(784f, 56f), new Color(0f, 0f, 0f, 0f), 0f);
            footer.GetComponent<Image>().raycastTarget = false;

            var reset = V6Controls.ButtonSecondary(footer, "既定に戻す",
                new Vector2(108f, 0f), new Vector2(220f, 56f));
            reset.name = "Op Reset Keys";
            reset.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (logic == null)
                {
                    return;
                }

                logic.ResetToDefaults();
                Refresh();
            });

            var cancel = V6Controls.ButtonSecondary(footer, "キャンセル",
                new Vector2(340f, 0f), new Vector2(180f, 56f));
            cancel.name = "Op Cancel Keys";
            cancel.GetComponent<Button>().onClick.AddListener(Hide);

            var apply = V6Controls.ButtonPrimary(footer, "適用",
                new Vector2(532f, 0f), new Vector2(252f, 56f));
            apply.name = "Op Apply Keys";
            apply.GetComponent<Button>().onClick.AddListener(Apply);
        }

        /// <summary>編集内容を保存して閉じる。</summary>
        public void Apply()
        {
            if (logic == null)
            {
                Hide();
                return;
            }

            if (logic.HasChanges() && !logic.Commit())
            {
                // 保存に失敗しても閉じない (編集を失わない)。空レーンなどはモデルが作らない。
                return;
            }

            Hide();
        }

        // --- 表示の更新 ---

        /// <summary>モデルの内容をキーボード全体へ反映する (Issue #428)。</summary>
        public void Refresh()
        {
            if (logic == null)
            {
                return;
            }

            int selected = logic.SelectedLane;
            bool listening = logic.Listening;
            for (int i = 0; i < keyCaps.Count; i++)
            {
                KeyCap cap = keyCaps[i];
                if (!cap.Def.Assignable)
                {
                    continue;
                }

                int lane = LaneOfKeyCap(cap.Def.Code);
                bool bound = lane >= 0;
                bool isSelectedLaneKey = bound && lane == selected;

                // 選択レーンのキー > 他レーンのキー > 未割当 の順で見せる。
                // 待受中は割当可能なキーを一段明るくして「押せる」ことを示す。
                Color bg = isSelectedLaneKey ? V6Tokens.Key5
                    : bound ? V6Tokens.Key4
                    : listening ? V6Tokens.Key2
                    : V6Tokens.Key3;
                cap.Background.color = bg;
                cap.Label.color = bound || listening ? V6Tokens.Text : V6Tokens.TextMuted;
                cap.LaneBadge.text = bound ? (lane + 1).ToString() : string.Empty;
                cap.LaneBadge.color = isSelectedLaneKey ? V6Tokens.Text : V6Tokens.TextMuted;
            }

            if (statusText != null)
            {
                statusText.text = logic.StateText(selected);
                statusText.color = logic.ConflictLane >= 0
                    ? V6Tokens.DangerLight
                    : V6Tokens.TextMuted;
            }

            if (hintText != null)
            {
                hintText.fontStyle = listening ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        // --- 入力 (モーダルが最前面で消費する) ---

        private void Update()
        {
            if (!isOpen || logic == null)
            {
                return;
            }

            if (logic.Listening)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    logic.CancelListen();
                    Consume();
                    Refresh();
                    return;
                }

                KeyCode pressed = PollPressedKey();
                if (pressed != KeyCode.None)
                {
                    logic.Assign(pressed);
                    Consume();
                    Refresh();
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Consume();
                Hide();
                return;
            }

            // 適用へのキーボード経路 (PR #387 レビュー指摘)。Enter は行の待受開始に
            // 使っているため、修飾キー付きの Enter を「適用」に割り当てる。
            // Tab でフッターへフォーカスを移す方式は、EventSystem の選択と
            // Update の Enter 処理が二重に走る危険があるので採らない。
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                 || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand))
                && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                Consume();
                Apply();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                logic.BeginListen();
                Consume();
                Refresh();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                logic.Move(-1);
                Consume();
                Refresh();
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                logic.Move(1);
                Consume();
                Refresh();
            }
        }

        private void Consume()
        {
            consumedFrame = Time.frameCount;
        }

        /// <summary>待受中に押されたキーを 1 つ返す (無ければ None)。</summary>
        private static KeyCode PollPressedKey()
        {
            if (captureKeys == null)
            {
                var keys = new List<KeyCode>();
                KeyCode[] all = (KeyCode[])Enum.GetValues(typeof(KeyCode));
                for (int i = 0; i < all.Length; i++)
                {
                    string name = all[i].ToString();
                    if (name.StartsWith("Mouse") || name.StartsWith("Joystick") || name.StartsWith("Axis"))
                    {
                        continue;
                    }

                    keys.Add(all[i]);
                }

                captureKeys = keys.ToArray();
            }

            for (int i = 0; i < captureKeys.Length; i++)
            {
                if (Input.GetKeyDown(captureKeys[i]))
                {
                    return captureKeys[i];
                }
            }

            return KeyCode.None;
        }
    }
}
