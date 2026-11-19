using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V6;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// 設定 1 行の共有ビュー (Issue #733)。Option / Pause の設定パネルが
    /// <see cref="V6SettingsCatalog"/> の行定義から同じ見た目・同じ操作で
    /// 組み立てるための部品。
    ///
    /// design: 行 496×56 $k1 r8 (選択行 $k6) / Label w200 fs24 /
    /// 値はステッパーの間に中央揃え (アクション行は右寄せ) /
    /// Prev・Next は 40×40 $k4 r8 (icon 24 $text-muted)。
    /// 行クリックは「選択 + アクション行なら決定」をホスト側の
    /// コールバックで行う (選択状態の持ち主は各ビュー)。
    /// </summary>
    public sealed class V6SettingRowView
    {
        // --- design px (Option / Pause 共通の設定行) ---
        public const float Width = 496f;
        public const float Height = 56f;
        public const float LabelWidth = 200f;
        public const float LabelFontSize = 24f;
        public const float ValueFontSize = 24f;
        /// <summary>編集値を中央揃えで置く領域 (Prev 300-340 / Next 444-484 の間)。</summary>
        public const float ValueCenterX = 344f, ValueCenterW = 96f;
        /// <summary>アクション行の値領域 (右寄せ)。</summary>
        public const float ValueActionX = 260f, ValueActionW = 120f;
        /// <summary>ステッパー (40×40) の x とアイコンサイズ。</summary>
        public const float StepperPrevX = 300f, StepperNextX = 444f;
        public const float StepperSize = 40f, StepperIconSize = 24f;
        public const float StepperIconY = 8f;
        /// <summary>値の右端に出す導線アイコン (chevron-right 等) の寸法と位置。</summary>
        public const float ValueIconSize = 24f;
        public const float ValueIconX = 460f, ValueIconY = 16f;

        /// <summary>行の上端 y (design: 60 + step*k)。</summary>
        public const float FirstY = 60f;
        /// <summary>行の高さ。</summary>
        public const float RowH = Height;
        /// <summary>行間 (行数が少ないタブ)。</summary>
        public const float StepLoose = 76f;
        /// <summary>行間 (行がパネルからあふれるタブ)。</summary>
        public const float StepTight = 60f;

        public GameObject Root { get; private set; }
        public Image Background { get; private set; }
        public TextMeshProUGUI LabelText { get; private set; }
        public TextMeshProUGUI ValueText { get; private set; }

        private V6SettingRow row;

        /// <summary>
        /// 行を組み立てる。
        /// onAdjust: (delta, shift) — ホストが選択をこの行へ移してから
        /// 自身の AdjustSelected 経路へ流す。null ならステッパーを出さない。
        /// onClick: 行パネルのクリック (ホストが選択 + アクション行の決定を行う)。
        /// </summary>
        public static V6SettingRowView Build(GameObject parent, V6SettingRow row, int index,
            Vector2 designTopLeft, bool selected,
            Action<int, bool> onAdjust, Action onClick)
        {
            var view = new V6SettingRowView { row = row };

            view.Root = V6Controls.Panel(parent, "Set " + row.Label, designTopLeft,
                new Vector2(Width, Height),
                selected ? V6Tokens.Key6 : V6Tokens.Key1, V6Tokens.RadiusMedium);
            view.Background = view.Root.GetComponent<Image>();

            var rowButton = view.Root.AddComponent<Button>();
            rowButton.transition = Selectable.Transition.None;
            var nav = rowButton.navigation;
            nav.mode = Navigation.Mode.None;
            rowButton.navigation = nav;
            V6ButtonPressFeedback.Attach(view.Root);
            rowButton.onClick.AddListener(() =>
            {
                if (onClick != null)
                {
                    onClick();
                }
            });

            view.LabelText = V6Controls.Text(view.Root, "Label", row.Label,
                new Vector2(12f, 0f), new Vector2(LabelWidth, Height), LabelFontSize,
                row.Danger ? V6Tokens.Danger : V6Tokens.Text);
            view.LabelText.textWrappingMode = TextWrappingModes.NoWrap;
            view.LabelText.overflowMode = TextOverflowModes.Ellipsis;

            // 編集値は左右の矢印の「間」に中央揃えで置く
            // (Prev 300-340 / Next 444-484 → 値は 344-440 の中央 392)。
            // アクション行 (キー設定等) は従来どおり右寄せの値を残す。
            bool centered = !row.IsAction;
            view.ValueText = V6Controls.Text(view.Root, "Value",
                row.Value != null ? row.Value() : string.Empty,
                centered ? new Vector2(ValueCenterX, 0f) : new Vector2(ValueActionX, 0f),
                centered ? new Vector2(ValueCenterW, Height) : new Vector2(ValueActionW, Height),
                ValueFontSize, V6Tokens.Text,
                centered ? TextAlignmentOptions.Center : TextAlignmentOptions.Right);
            view.ValueText.textWrappingMode = TextWrappingModes.NoWrap;
            view.ValueText.overflowMode = TextOverflowModes.Ellipsis;
            if (centered)
            {
                view.ValueText.enableAutoSizing = true;
                view.ValueText.fontSizeMin = 16f;
                view.ValueText.fontSizeMax = ValueFontSize;
            }

            // 導線アイコン (「開く ›」の › をスプライト化したもの等)。
            if (!string.IsNullOrEmpty(row.ValueIcon))
            {
                var icon = V6Icons.Create(view.Root, row.ValueIcon, ValueIconSize,
                    V6Tokens.TextMuted, "Value Icon");
                if (icon != null)
                {
                    var rt = icon.rectTransform;
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(ValueIconX, -ValueIconY);
                    rt.sizeDelta = new Vector2(ValueIconSize, ValueIconSize);
                }
            }

            // ステッパーは「値を持ち Adjust で書き込める行」だけに出す。
            // 読み取り専用の行 (Pause Data タブの統計等) に死んだ ◀ ▶ を出さない。
            if (!row.IsAction && row.Adjust != null && onAdjust != null)
            {
                var prev = V6Controls.IconButton(view.Root, "chevron-left",
                    new Vector2(StepperPrevX, StepperIconY),
                    new Vector2(StepperSize, StepperSize),
                    V6Tokens.Key4, V6Tokens.RadiusMedium, StepperIconSize, V6Tokens.TextMuted);
                prev.GetComponent<Button>().onClick.AddListener(() => onAdjust(-1, false));

                var next = V6Controls.IconButton(view.Root, "chevron-right",
                    new Vector2(StepperNextX, StepperIconY),
                    new Vector2(StepperSize, StepperSize),
                    V6Tokens.Key4, V6Tokens.RadiusMedium, StepperIconSize, V6Tokens.TextMuted);
                next.GetComponent<Button>().onClick.AddListener(() => onAdjust(1, false));
            }

            return view;
        }

        /// <summary>選択状態の背景色を更新する。</summary>
        public void SetSelected(bool selected)
        {
            if (Background != null)
            {
                Background.color = selected ? V6Tokens.Key6 : V6Tokens.Key1;
            }
        }

        /// <summary>値テキストを現在値で書き換える (レイアウトは動かさない)。</summary>
        public void RefreshValue()
        {
            if (ValueText != null && row != null && row.Value != null)
            {
                ValueText.text = row.Value();
            }
        }

        /// <summary>
        /// 行の行間。design は行数が少ないタブでは 76px、
        /// 行がパネル (panelHeight) からあふれるタブでは 60px に詰める。
        /// </summary>
        public static float RowStep(int rowCount, float panelHeight)
        {
            if (rowCount <= 0)
            {
                return StepLoose;
            }

            float needed = FirstY + StepLoose * (rowCount - 1) + RowH;
            return needed <= panelHeight ? StepLoose : StepTight;
        }
    }
}
