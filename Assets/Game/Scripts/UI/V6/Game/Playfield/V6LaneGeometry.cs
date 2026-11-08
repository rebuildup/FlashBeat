using UnityEngine;

namespace FlashBeat.UI.V6.Game.Playfield
{
    /// <summary>
    /// v6 プレイフィールドの幾何 (Issue #219)。
    ///
    /// 一次データは design/flashbeat-ui.pen のノード <c>nrfw8</c>
    /// (07 Game · v6 · プレイフィールド (共通フレーム))。設計座標系は 1728×972、
    /// y は下向き (原点 = 画面左上)。
    ///
    /// レーン分離線 8 本は Pencil 上「Lane 0..7」と名前が付いた **レーン中心線** である
    /// (根拠: デザインに置かれた 8 個のサンプルノーツの中心 x が、
    /// その深度での 8 本の線の x とすべて一致する。境界説だと 8 レーンに 9 本必要)。
    ///
    ///   - 下端 (y=792): x = 339 + 150·i   (レーン幅 150px)
    ///   - 上端 (y=0):   x = 801 + 18·i    (レーン幅 18px)
    ///
    /// 線形補間 (デザインの SVG パスが直線なので t に対して線形)。
    ///
    /// 設計座標 → 実画面の写像は **高さ合わせ + 水平中央寄せ**
    /// (scale = 画面高さ / 972、設計フレーム中心を画面中心へ):
    /// 1920×1080 と 2560×1440 は 16:9 なので設計通りそのまま、
    /// 2560×1664 でもプレイフィールド (下端幅 1200 設計px) は画面内に収まる。
    ///
    /// 契約照合 (2026-11-08 / docs/design/v6/flashbeat-v6-contract.md §6):
    /// 下列の定数はすべて **契約値 = 実装値** であることを項目ごとに確認済み。
    ///   判定線 y=792 → JudgeLineY / 判定線 h=4 → JudgeLineHeightDesignPx
    ///   判定グロウ y=796 h=2 → JudgeGlowY / JudgeGlowHeightDesignPx
    ///   判定線色 #EDEDED → JudgeLineColor / グロウ色 #FFFFFF4D → JudgeGlowColor
    ///   レーン線色 #FFFFFF2E sw2 → SeparatorColor / SeparatorStrokeDesignPx
    ///   ノーツ色 #EDEDED → NoteColor
    ///   レーン中心線 下端 x=339 step=150 → LaneCentreBottomX / LaneCentreBottomStep
    ///   レーン中心線 上端 x=801 step=18 → LaneCentreTopX / LaneCentreTopStep
    /// </summary>
    public static class V6LaneGeometry
    {
        /// <summary>設計座標系の幅 (design frame nrfw8)。</summary>
        public const float DesignWidth = 1728f;

        /// <summary>設計座標系の高さ。</summary>
        public const float DesignHeight = 972f;

        /// <summary>判定線の y (設計座標、下向き)。</summary>
        public const float JudgeLineY = 792f;

        /// <summary>レーン数 (Judge.LaneKeys / NotesManager.block と同一)。</summary>
        public const int LaneCount = 8;

        /// <summary>レーン中心線の下端 x (lane 0、y=792)。</summary>
        public const float LaneCentreBottomX = 339f;

        /// <summary>レーン中心線の下端間隔 (= 判定線上のレーン幅)。</summary>
        public const float LaneCentreBottomStep = 150f;

        /// <summary>レーン中心線の上端 x (lane 0、y=0)。</summary>
        public const float LaneCentreTopX = 801f;

        /// <summary>レーン中心線の上端間隔 (= 奥のレーン幅)。</summary>
        public const float LaneCentreTopStep = 18f;

        /// <summary>レーン線の線幅 (design: stroke #FFFFFF2E sw2)。</summary>
        public const float SeparatorStrokeDesignPx = 2f;

        /// <summary>判定ラインの高さ (design: (0,792) 1728×4)。</summary>
        public const float JudgeLineHeightDesignPx = 4f;

        /// <summary>判定グロウの高さ (design: (0,796) 1728×2)。</summary>
        public const float JudgeGlowHeightDesignPx = 2f;

        /// <summary>判定グロウの上端 y (design: 判定ラインの下端 792+4 = 796)。</summary>
        public const float JudgeGlowY = JudgeLineY + JudgeLineHeightDesignPx;

        /// <summary>ノーツ幅 = レーン幅への比率 (デザイン実測: y=640 のノーツ 97px / レーン幅 124.7px)。</summary>
        public const float NoteWidthRatio = 0.78f;

        /// <summary>ノーツ厚み = ノーツ幅への比率 (デザイン実測: 13px / 97px。奥 4px → 手前 13px の範囲を再現する)。</summary>
        public const float NoteThicknessRatio = 13f / 97f;

        /// <summary>
        /// 判定線を通過したノーツが画面下端 (設計 y = 972) を出きる t01。
        /// y = 792·(1 - t) が 972 になる t = -180/792。
        /// </summary>
        public const float ExitT01 = (JudgeLineY - DesignHeight) / JudgeLineY;

        /// <summary>レーン分離線の色 (design: #FFFFFF2E)。</summary>
        public static readonly Color SeparatorColor = FromHex("#FFFFFF2E");

        /// <summary>判定ラインの色 (design: #EDEDED)。</summary>
        public static readonly Color JudgeLineColor = FromHex("#EDEDED");

        /// <summary>判定グロウの色 (design: #FFFFFF4D)。</summary>
        public static readonly Color JudgeGlowColor = FromHex("#FFFFFF4D");

        /// <summary>ノーツの色 (design: #EDEDED)。</summary>
        public static readonly Color NoteColor = FromHex("#EDEDED");

        /// <summary>
        /// 動画減光の色 (design §8: Video Dim #00000080 全画面)。
        /// 動画より手前・プレイフィールドより後ろのレイヤで敷く。
        /// #00000080 = 黒 + アルファ 0x80/255 (明示的な数値で持つ)。
        /// </summary>
        public static readonly Color VideoDimColor = new Color(0f, 0f, 0f, 0x80 / 255f);

        /// <summary>
        /// レーン <paramref name="lane"/> 上の t01 位置の点を返す。
        ///
        /// <paramref name="t01"/> は 0 = 判定線 (y=792)、1 = 奥 (y=0)。
        /// 範囲外は**クランプせず線形外挿する** (判定線を通過したノーツを
        /// 画面下へ流れ出させるのに使う。見た目の打ち切りは呼び出し側で行う)。
        /// <paramref name="scale"/> はノーツサイズ係数 (判定線上 = 1、奥に向かって縮む)。
        /// <paramref name="position"/> は設計座標 (y 下向き)。
        /// </summary>
        public static bool TryLanePoint(int lane, float t01, out Vector2 position, out float scale)
        {
            if (lane < 0 || lane >= LaneCount)
            {
                position = default;
                scale = 0f;
                return false;
            }

            float bottomX = LaneCentreBottomX + LaneCentreBottomStep * lane;
            float topX = LaneCentreTopX + LaneCentreTopStep * lane;
            position = new Vector2(Mathf.LerpUnclamped(bottomX, topX, t01), JudgeLineY * (1f - t01));
            scale = LaneWidthAt(t01) / LaneCentreBottomStep;
            return true;
        }

        /// <summary>t01 におけるレーン幅 (設計px)。判定線上 150px → 奥 18px の線形補間。</summary>
        public static float LaneWidthAt(float t01)
        {
            return Mathf.LerpUnclamped(LaneCentreBottomStep, LaneCentreTopStep, t01);
        }

        /// <summary>t01 におけるノーツ幅 (設計px)。レーン幅 × <see cref="NoteWidthRatio"/>。</summary>
        public static float NoteWidthAt(float t01)
        {
            return LaneWidthAt(t01) * NoteWidthRatio;
        }

        /// <summary>t01 におけるノーツ厚み (設計px)。ノーツ幅 × <see cref="NoteThicknessRatio"/>。</summary>
        public static float NoteThicknessAt(float t01)
        {
            return NoteWidthAt(t01) * NoteThicknessRatio;
        }

        /// <summary>実画面 (px) における 設計px → px の倍率 (高さ合わせ)。</summary>
        public static float ScreenScale(Vector2 screenSize)
        {
            return screenSize.y / DesignHeight;
        }

        /// <summary>
        /// 設計座標 → 実画面座標 (原点 = 画面左上、y 下向き)。
        /// 高さ合わせ + 水平中央寄せ。横方向のはみ出し (2560×1664) はそのまま
        /// 負のオフセットになり、画面端でクリップされる (フィールド自体は収まる)。
        /// </summary>
        public static Vector2 DesignToScreen(Vector2 design, Vector2 screenSize)
        {
            float s = ScreenScale(screenSize);
            float offsetX = (screenSize.x - DesignWidth * s) * 0.5f;
            return new Vector2(design.x * s + offsetX, design.y * s);
        }

        private static Color FromHex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
