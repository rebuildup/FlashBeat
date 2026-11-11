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
        private readonly List<LaneRow> rows = new List<LaneRow>();

        /// <summary>閉じたあとに呼ばれる (適用・キャンセルどちらでも)。</summary>
        public Action Closed;

        private sealed class LaneRow
        {
            public Image Background;
            public TextMeshProUGUI LaneLabel;
            public TextMeshProUGUI KeyLabel;
            public TextMeshProUGUI State;
            public TextMeshProUGUI Alternates;
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
            V6Controls.Text(dialogGo, "Description", "レーンごとに代表キーを変更します。",
                new Vector2(28f, 72f), new Vector2(784f, 24f), 22f, V6Tokens.TextDim);

            var list = V6Controls.Panel(dialogGo, "Lane List", new Vector2(LaneRowX, LaneRowY),
                new Vector2(LaneRowW, 472f), new Color(0f, 0f, 0f, 0f), 0f);
            list.GetComponent<Image>().raycastTarget = false;

            for (int lane = 0; lane < V6KeyConfigLogic.LaneCount; lane++)
            {
                rows.Add(BuildLaneRow(list, lane));
            }

            BuildFooter();
        }

        private LaneRow BuildLaneRow(GameObject list, int lane)
        {
            var rowGo = V6Controls.Panel(list, "Lane Row " + (lane + 1),
                new Vector2(0f, LaneRowStep * lane), new Vector2(LaneRowW, LaneRowH),
                V6Tokens.Key2, V6Tokens.RadiusMedium);

            var row = new LaneRow();
            row.Background = rowGo.GetComponent<Image>();
            row.LaneLabel = V6Controls.Text(rowGo, "Lane", "Lane " + (lane + 1),
                new Vector2(16f, 16f), new Vector2(120f, 32f), 22f, V6Tokens.TextDim);

            // 代表キーの KeyCap (design: 120×40 r4 fs24)。
            var cap = V6Controls.Panel(rowGo, "KeyCap", new Vector2(152f, 6f),
                new Vector2(120f, 40f), V6Tokens.Key4, V6Tokens.RadiusSmall);
            row.KeyLabel = V6Controls.Text(cap, "Key", string.Empty, Vector2.zero,
                new Vector2(120f, 40f), 24f, V6Tokens.Text, TextAlignmentOptions.Center);

            row.State = V6Controls.Text(rowGo, "State", string.Empty,
                new Vector2(288f, 2f), new Vector2(464f, 26f), 20f, V6Tokens.TextMuted);
            row.Alternates = V6Controls.Text(rowGo, "Alternates", string.Empty,
                new Vector2(288f, 32f), new Vector2(480f, 20f), 16f, V6Tokens.TextMuted);

            var button = rowGo.AddComponent<Button>();
            button.targetGraphic = row.Background;
            int captured = lane;
            button.onClick.AddListener(() =>
            {
                if (logic == null || !isOpen)
                {
                    return;
                }

                logic.Select(captured);
                logic.BeginListen();
                Refresh();
            });

            return row;
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

        /// <summary>モデルの内容を全行へ反映する。</summary>
        public void Refresh()
        {
            if (logic == null)
            {
                return;
            }

            for (int lane = 0; lane < rows.Count; lane++)
            {
                LaneRow row = rows[lane];
                bool selected = lane == logic.SelectedLane;
                bool listeningHere = selected && logic.Listening;

                row.Background.color = selected ? V6Tokens.Key6 : V6Tokens.Key2;
                row.KeyLabel.text = V6KeyConfigLogic.KeyLabel(logic.Representative(lane));
                row.Alternates.text = logic.AlternatesText(lane);
                row.State.text = logic.StateText(lane);
                row.State.color = (selected && logic.ConflictLane >= 0)
                    ? V6Tokens.DangerLight
                    : selected ? V6Tokens.Text : V6Tokens.TextMuted;
                row.LaneLabel.color = selected ? V6Tokens.Text : V6Tokens.TextDim;

                if (listeningHere)
                {
                    row.State.fontStyle = FontStyles.Bold;
                }
                else
                {
                    row.State.fontStyle = FontStyles.Normal;
                }
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
