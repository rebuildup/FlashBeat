using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// 契約 §7「選択中の曲パネル」(<c>dRztp</c> 536×780) の Controls 行部品。
    /// skip-back 56×56 $k4 r28 (icon 32) / play 72×72 #EDEDED r36 (icon 36 #1B1B1B) /
    /// skip-forward 56×56 $k4 r28 (icon 32) を gap20 で中央寄せした Row コンテナ。
    /// すべて design 空間 (左上原点・y_down)。
    /// </summary>
    public static class V6Panel
    {
        public const float PanelW = 536f;
        public const float PanelH = 780f;

        // 契約 §7 Controls 行: (12,668) 512×88、gap20 中央。
        public const float ControlsW = 512f;
        public const float ControlsH = 88f;
        public const float ControlsGap = 20f;
        public const float SkipSize = 56f;
        public const float SkipRadius = 28f;
        public const float SkipIconSize = 32f;
        public const float PlaySize = 72f;
        public const float PlayRadius = 36f;
        public const float PlayIconSize = 36f;

        /// <summary>Controls 行の 3 ボタンの design x オフセット (コンテナ左端基準、中央寄せ済み)。</summary>
        public static float[] ControlsOffsets()
        {
            float total = SkipSize + ControlsGap + PlaySize + ControlsGap + SkipSize;
            float x = (ControlsW - total) * 0.5f;
            return new[]
            {
                x,
                x + SkipSize + ControlsGap,
                x + SkipSize + ControlsGap + PlaySize + ControlsGap,
            };
        }

        /// <summary>
        /// 選択中の曲パネルの Controls 行を作る。designTopLeft はパネル ローカルの
        /// (12,668) など契约座標を呼び出し側から渡す。押下時の挙動は呼び出し側で
        /// Button.onClick を購読する。
        /// </summary>
        public static GameObject ControlsRow(GameObject parent, Vector2 designTopLeft)
        {
            var row = V6Layout.Row(parent, "Controls", designTopLeft, new Vector2(ControlsW, ControlsH), ControlsGap);

            float[] offsets = ControlsOffsets();
            V6Controls.IconButton(row, "skip-back", new Vector2(offsets[0], (ControlsH - SkipSize) * 0.5f),
                new Vector2(SkipSize, SkipSize), V6Tokens.Key4, SkipRadius, SkipIconSize, V6Tokens.TextMuted);
            V6Controls.IconButton(row, "play", new Vector2(offsets[1], (ControlsH - PlaySize) * 0.5f),
                new Vector2(PlaySize, PlaySize), V6Tokens.ButtonPrimaryBg, PlayRadius, PlayIconSize,
                V6Tokens.ButtonPrimaryLabel);
            V6Controls.IconButton(row, "skip-forward", new Vector2(offsets[2], (ControlsH - SkipSize) * 0.5f),
                new Vector2(SkipSize, SkipSize), V6Tokens.Key4, SkipRadius, SkipIconSize, V6Tokens.TextMuted);
            return row;
        }
    }
}
