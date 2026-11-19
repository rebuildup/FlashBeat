using FlashBeat.Gameplay;
using UnityEngine;
using FlashBeat.UI.V6.Game.Playfield;

namespace FlashBeat.UI.V6.Game.Hud
{
    /// <summary>
    /// 上部シェード (Top Shade) がレーン帯を暗くしないための幾何 (Issue #675)。
    ///
    /// Top Shade は Screen Space Overlay の Image なので 3D の深度テストを通らず、
    /// アプローチ中のノーツ (画面上部を通過する) の上に α≈0.65 の黒グラデーションを
    /// そのまま重ねてしまう。flat-rendering の深度保証は 3D レンダラ間の話で、
    /// uGUI overlay は対象外 (docs/architecture/flat-rendering.md 参照)。
    ///
    /// 対処としてシェードのテクスチャに「レーン帯の抜き」を入れる。抜きの形は
    /// ノーツが実際に通る領域 = レーン境界線 (<see cref="PlayfieldLaneGuides.LaneBoundaryX"/>)
    /// が作る帯に、ノーツ半分幅 + 余白を足したもの。
    ///
    /// レーン帯の画面座標はプレイフィールドのモードで取り方を変える:
    /// - 3D プレイフィールド (既定): GameScene のカメラでレーン面
    ///   (y = <see cref="MediaTimeline.NotePlaneY"/>) との交点を各行について解き、
    ///   ワールドのレーン境界を <see cref="Camera.WorldToScreenPoint"/> で投影する。
    ///   カメラが取れない環境 (EditMode テスト等) では v6 の設計座標写像
    ///   (<see cref="V6LaneGeometry.DesignToScreen"/>) にフォールバックする。
    /// - v6 遠近法プレイフィールド: ノーツは uGUI Image で
    ///   <see cref="V6LaneGeometry"/> の台形に沿って置かれるので、同じ写像で帯を出す。
    ///
    /// Issue #760: 帯の幾何は画面上端のシェード専用ではなく、
    /// <see cref="CorridorRows(float, float, float, float, int, float[], float[])"/>
    /// で任意の縦位置・高さの HUD 帯 (歌詞パネル等) にも適用できる。
    /// </summary>
    public static class V6TopShadeHole
    {
        /// <summary>シェードテクスチャの幅 (px)。高さはシェード高さと同じ px 数。</summary>
        public const int TextureWidth = 512;

        /// <summary>レーン帯の左右へ足す余白 (画面 px)。ノーツのはみ出しとガイド線を含む。</summary>
        public const float CorridorMarginPx = 12f;

        /// <summary>
        /// 画面上端に接する高さ <paramref name="shadeH"/> の帯について、
        /// レーン帯の画面 x 範囲を埋める (<see cref="CorridorRows(float, float, float, float, int, float[], float[])"/>
        /// の領域上端 0 の場合)。
        /// </summary>
        public static void CorridorRows(float screenW, float screenH, float shadeH,
            int rows, float[] left, float[] right)
        {
            CorridorRows(screenW, screenH, 0f, shadeH, rows, left, right);
        }

        /// <summary>
        /// 画面上端から <paramref name="regionTopPx"/> px・高さ
        /// <paramref name="regionHeightPx"/> px の任意の帯 (上部シェード、
        /// 歌詞パネル等) について、各行のレーン帯の画面 x 範囲
        /// (左原点 px、<paramref name="left"/> &lt; <paramref name="right"/>) を埋める。
        /// 行 index はテクスチャ行 (0 = 帯の下端、<paramref name="rows"/>-1 = 帯の上端)。
        /// Issue #760: 歌詞パネルのような画面途中の帯でも同じ抜きを使えるよう、
        /// 領域の上端オフセットを取れる形に一般化した。
        /// </summary>
        public static void CorridorRows(float screenW, float screenH,
            float regionTopPx, float regionHeightPx, int rows, float[] left, float[] right)
        {
            Camera cam = Camera.main;
            bool use3D = !V6UiMode.Playfield && cam != null;
            if (use3D)
            {
                // 描画時と同じ aspect で投影する。PlayfieldLaneGuides.Update が
                // 毎フレーム掛ける水平 fit (非 16:9 画面で画面全体を横に圧縮する)
                // が HUD 構築より後に初めて走ると、レーン帯の抜きが実際の
                // ノーツ位置から横にずれ、シェードがノーツの縁を暗くする。
                // fit は冪等なのでここで先に掛けておく。
                PlayfieldCameraFit.Apply(cam, PlayfieldCameraFit.ScreenAspect());
            }

            for (int row = 0; row < rows; row++)
            {
                // テクスチャ行 → 帯内の y (上端原点 px)。行 0 = 帯下端。
                float yFromTop = regionTopPx
                    + regionHeightPx * (1f - row / Mathf.Max(1, rows - 1));
                bool ok = use3D
                    && CorridorRow3D(cam, screenW, screenH, yFromTop, out left[row], out right[row]);
                if (!ok)
                {
                    CorridorRowDesign(screenW, screenH, yFromTop, out left[row], out right[row]);
                }
            }
        }

        /// <summary>
        /// v6 設計座標系のレーン帯を画面 px へ写す (カメラ不要のフォールバック経路)。
        /// レーン境界は 下端 x=264+150·i / 上端 x=792+18·i (中心線 ± レーン幅/2)。
        /// </summary>
        private static void CorridorRowDesign(float screenW, float screenH,
            float yFromTop, out float left, out float right)
        {
            var screen = new Vector2(screenW, screenH);
            float scale = V6LaneGeometry.ScreenScale(screen);
            float designY = yFromTop / scale;
            float t01 = Mathf.Clamp01(1f - designY / V6LaneGeometry.JudgeLineY);

            float laneWidth = V6LaneGeometry.LaneWidthAt(t01);
            float bottomStep = V6LaneGeometry.LaneCentreBottomStep;
            float topStep = V6LaneGeometry.LaneCentreTopStep;
            int last = V6LaneGeometry.LaneCount - 1;

            // レーン 0 の左境界とレーン 7 の右境界 (中心線 ± レーン幅/2)。
            float leftDesign = Mathf.Lerp(
                V6LaneGeometry.LaneCentreBottomX - bottomStep * 0.5f,
                V6LaneGeometry.LaneCentreTopX - topStep * 0.5f, t01);
            float rightDesign = Mathf.Lerp(
                V6LaneGeometry.LaneCentreBottomX + bottomStep * 0.5f + last * bottomStep,
                V6LaneGeometry.LaneCentreTopX + topStep * 0.5f + last * topStep, t01);

            Vector2 ls = V6LaneGeometry.DesignToScreen(new Vector2(leftDesign, designY), screen);
            Vector2 rs = V6LaneGeometry.DesignToScreen(new Vector2(rightDesign, designY), screen);
            left = Mathf.Clamp01(ls.x / screenW) * screenW - CorridorMarginPx;
            right = Mathf.Clamp01(rs.x / screenW) * screenW + CorridorMarginPx;
            if (right - left < 1f)
            {
                float mid = (left + right) * 0.5f;
                left = mid - 0.5f;
                right = mid + 0.5f;
            }
        }

        /// <summary>
        /// 3D プレイフィールド: 画面上端から <paramref name="yFromTop"/> px の行が
        /// レーン面 (y = NotePlaneY) と交わる z をカメラのレイから求め、
        /// レーン境界 (x = LaneBoundaryX(0..8)) をスクリーン px へ投影する。
        /// 投影できない行は false (呼び出し側が設計座標フォールバックを使う)。
        /// </summary>
        private static bool CorridorRow3D(Camera cam, float screenW, float screenH,
            float yFromTop, out float left, out float right)
        {
            left = 0f;
            right = 0f;
            if (cam == null) return false;

            // 画面座標 (上端原点) → ビューポート (左下原点)。
            var viewport = new Vector3(0.5f, 1f - yFromTop / Mathf.Max(1f, screenH), 0f);
            Ray ray = cam.ViewportPointToRay(viewport);
            if (ray.direction.y >= -1e-6f) return false;

            float t = (MediaTimeline.NotePlaneY - ray.origin.y) / ray.direction.y;
            if (t <= 0f) return false;

            // レーンガイドが伸びる z 範囲にクランプ (画面上端より奥は先端の帯)。
            float z = ray.origin.z + ray.direction.z * t;
            float frontZ = -PlayfieldLaneGuides.FrontDepth(cam);
            z = Mathf.Clamp(z, frontZ, PlayfieldLaneGuides.GuideDepth);

            Vector3 wl = cam.WorldToScreenPoint(new Vector3(
                PlayfieldLaneGuides.LaneBoundaryX(0), MediaTimeline.NotePlaneY, z));
            Vector3 wr = cam.WorldToScreenPoint(new Vector3(
                PlayfieldLaneGuides.LaneBoundaryX(PlayfieldLaneGuides.GuideCount - 1),
                MediaTimeline.NotePlaneY, z));
            if (wl.z <= 0f || wr.z <= 0f) return false;

            left = Mathf.Min(wl.x, wr.x) - CorridorMarginPx;
            right = Mathf.Max(wl.x, wr.x) + CorridorMarginPx;
            return true;
        }

        /// <summary>
        /// テクスチャ 1 画素のアルファ。グラデーション値
        /// (<paramref name="gradientAlpha01"/>) をレーン帯の内側では 0 にする。
        /// <paramref name="screenX"/> は画面 px (左原点)、帯は
        /// [<paramref name="corridorLeft"/>, <paramref name="corridorRight"/>]。
        /// </summary>
        public static float ShadeAlpha(float screenX, float corridorLeft,
            float corridorRight, float gradientAlpha01)
        {
            if (screenX >= corridorLeft && screenX <= corridorRight)
            {
                return 0f;
            }

            return gradientAlpha01;
        }
    }
}
