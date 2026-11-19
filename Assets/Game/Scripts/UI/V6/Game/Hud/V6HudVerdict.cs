using System.Collections.Generic;
using FlashBeat.Gameplay;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Game.Playfield;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V6.Game.Hud
{
    /// <summary>
    /// V6GameHudView のレーン別判定行と、キーガイド / 判定行をレーン中心へ
    /// 合わせるレーン格子の部分 (Issue #385 Phase 2.2 で分割)。
    /// partial class のためフィールド・メソッドは V6GameHudView 本体と共有する。
    /// レーン格子は判定テキストとキーキャップ (V6HudChrome) の両方を動かす。
    /// </summary>
    public sealed partial class V6GameHudView
    {
        private readonly List<TextMeshProUGUI> verdictTexts = new List<TextMeshProUGUI>();

        // --- 毎フレームの状態 (判定行) ---
        private int lastJudged = -1;
        private readonly string[] laneVerdicts = new string[V6LaneGeometry.LaneCount];
        private readonly float[] laneVerdictUntil = new float[V6LaneGeometry.LaneCount];
        /// <summary>判定テキストを出した時刻 (出現アニメーションの基準)。</summary>
        private readonly float[] laneVerdictStart = new float[V6LaneGeometry.LaneCount];
        /// <summary>
        /// 判定テキストの行ローカル基準 y (anchoredPosition.y)。
        /// Issue #731: タイムラインの原点 = 判定線の位置。実際の表示位置は
        /// 毎フレーム <see cref="V6HudLogic.VerdictPoseAt"/> の OffsetPx を
        /// 足した値 (判定線 → 上昇 → 保持 → 下方退場)。
        /// </summary>
        private float verdictBaseY;


        /// <summary>v6 のレーン別判定行を使うか (既定は false = 元の 3D 判定エフェクト)。</summary>
        private static bool VerdictRowEnabled
        {
            get { return FlashBeat.UI.V6.V6UiMode.VerdictRow; }
        }

        private void BuildVerdictRow()
        {
            // 既定 (VerdictRow=false) では判定のフィードバックは元の 3D エフェクトが担う。
            if (!VerdictRowEnabled) return;

            var row = V5Ui.Node(gameObject, "Verdict Row");
            V5Ui.PinBottom(V6Layout.RectOf(row), m.VerdictH, m.VerdictBottom);

            for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
            {
                var text = V5Ui.Text(row, "Verdict " + lane, "", m.VerdictFs, V5Tokens.Text,
                    TextAlignmentOptions.Center);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                var rt = text.rectTransform;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(m.KeyColumnCenter(lane), 0f);
                rt.sizeDelta = new Vector2(m.VerdictW, m.VerdictH);
                SetAlpha(text, 0f);
                verdictTexts.Add(text);
            }
        }

        /// <summary>
        /// キーガイド / 判定行の 8 列の x を実際の画面へ合わせる (Issue #233 実機指摘 +
        /// 契約 §8 整合 + Owner 指示 2026-11-08「キーガイドをレーン中心に一致させる」)。
        ///
        /// - v6 プレイフィールド有効時: 契約 §6 のレーン中心線 (判定線 y=792 上の下端
        ///   x = 339 + 150·lane) を <see cref="V6HudMetrics.LaneCenterScreenX"/>
        ///   (= <see cref="V6LaneGeometry.DesignToScreen"/>: 高さ合わせ + 水平中央寄せ) で
        ///   実画面 x へ写す。プレイフィールドと同じ写像なので、キーガイド / 判定行は
        ///   レーン中心と必ず一致する (契約 §8 の KeyGuide 格子 310.. / 529.. / 464.. は
        ///   design サンプル値で、レーン中心 (FHD 376.7 / 2K 502.2 / Mac 381.1) とはずれる)。
        /// - レガシー 3D プレイフィールド時: レーン判定位置 (y=0.76, z=0.15) を
        ///   Camera.Main で投影してローカル x へ写す (契約 §8 ⚠ の「レーン中心への追従」)。
        ///   投影できない (カメラ無し等) ときは build 時の design px のまま。
        ///
        /// 判定行も同じ格子に載せる (契約 §8: 判定行サンプルは KeyGuide 格子と同位置)。
        ///
        /// 変換式は純ロジック (<see cref="V6HudLogic.ScreenXToTemplateX"/> /
        /// <see cref="V6HudMetrics.LaneCenterScreenX"/>) として EditMode テスト可能。
        /// </summary>
        private void UpdateLaneGrids()
        {
            // Issue #402: レーン格子は「解像度・曲・レガシーカメラの投影」でしか
            // 変わらない。以前は毎フレーム anchoredPosition を書いていた。
            // 入力 (どのキーが押されたか) や判定には影響しないので、
            // 依存値が変わったときだけ計算する。
            float rootScale = transform.lossyScale.x;
            if (rootScale <= 0f) return;

            Vector2 canvasSize = CanvasSize();
            if (!LaneGridInputsChanged(canvasSize, rootScale))
            {
                return;
            }

            lastLaneGridCanvas = canvasSize;
            lastLaneGridScale = rootScale;
            lastLaneGridPlayfield = FlashBeat.UI.V6.V6UiMode.Playfield;
            NoteLaneGridCamera();
            UpdateVerdictAnchor(canvasSize);

            if (FlashBeat.UI.V6.V6UiMode.Playfield)
            {
                for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
                {
                    float x = V6HudMetrics.LaneCenterScreenX(lane, canvasSize.x, canvasSize.y);
                    SetLaneGridX(lane, V6HudLogic.ScreenXToTemplateX(x, rootScale));
                }
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;
            for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
            {
                Vector3 world = new Vector3(Judge.LaneWorldX(lane), 0.76f, 0.15f);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;
                SetLaneGridX(lane, V6HudLogic.ScreenXToTemplateX(screen.x, rootScale));
            }
        }

        /// <summary>
        /// 判定ポップのタイムライン原点 = 判定線の実位置から行ローカル y を求める
        /// (Issue #432 / #731)。原点は判定線そのもの (オフセット 0)。
        /// レーン格子と同じ入力 (解像度・カメラ) が変わったときだけ呼ばれる。
        /// </summary>
        private void UpdateVerdictAnchor(Vector2 canvasSize)
        {
            if (verdictTexts.Count == 0 || m == null)
            {
                return;
            }

            float judgeLineFromBottom = -1f;
            if (FlashBeat.UI.V6.V6UiMode.Playfield)
            {
                // v6 プレイフィールド: 判定線は design y=792 (画面上端原点)。
                Vector2 lineTop = V6LaneGeometry.DesignToScreen(
                    new Vector2(0f, V6LaneGeometry.JudgeLineY), canvasSize);
                judgeLineFromBottom = canvasSize.y - lineTop.y;
            }
            else
            {
                // レガシー 3D: 描画される判定線 (JudgeLineWorldY/Z = 0.48/0、
                // GameScene JudgeLine の実ワールド位置) を投影して画面 y
                // (下原点) を取る。判定エフェクト出現点 (0.76/0.15) ではない
                // — design §10.3 の「判定線」は描画される線を指す (Issue #715)。
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 sp = cam.WorldToScreenPoint(V6HudLogic.LegacyJudgeLineAnchor());
                    if (sp.z > 0f)
                    {
                        judgeLineFromBottom = sp.y;
                    }
                }
            }

            if (judgeLineFromBottom < 0f)
            {
                return;
            }

            // テキストは PinBottom された行の中に (0, 0.5) アンカーで置かれている。
            // anchoredPosition.y = 原点 (判定線、画面下原点) − 行中心 (画面下原点)。
            // Issue #731: verdictBaseY は判定線上の原点。実際の表示位置は
            // 毎フレーム VerdictPoseAt(elapsed).OffsetPx を足した値で、
            // 上昇 → 保持 → 下方退場のタイムラインに従う。
            // レイアウトは動かさず y だけを時間で変える。
            float rowCenterFromBottom = m.VerdictBottom + m.VerdictH * 0.5f;
            verdictBaseY = judgeLineFromBottom - rowCenterFromBottom;
            for (int i = 0; i < verdictTexts.Count; i++)
            {
                var rt = verdictTexts[i].rectTransform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, verdictBaseY);
            }
        }

        /// <summary>
        /// レーン格子の計算入力が前回から変わったか (Issue #402)。
        ///
        /// 変わらない限り <see cref="SetLaneGridX"/> を呼ばない。カメラの変換行列も
        /// 入力の 1 つなので、カメラが動いた場合 (v5 プレイフィールドの揺れ等) は
        /// 追従する。位置・回転の値を直接追跡する (Transform.hasChanged は
        /// 読んでもリセットされないので使わない)。
        /// </summary>
        private bool LaneGridInputsChanged(Vector2 canvasSize, float rootScale)
        {
            bool playfield = FlashBeat.UI.V6.V6UiMode.Playfield;

            if (!V6LayoutWrites.NearlyEqual(lastLaneGridCanvas.x, canvasSize.x, V6LayoutWrites.PositionEpsilon)
                || !V6LayoutWrites.NearlyEqual(lastLaneGridCanvas.y, canvasSize.y, V6LayoutWrites.PositionEpsilon))
            {
                return true;
            }

            if (!V6LayoutWrites.NearlyEqual(lastLaneGridScale, rootScale, 0.0001f))
            {
                return true;
            }

            if (lastLaneGridPlayfield != playfield)
            {
                return true;
            }

            if (!playfield)
            {
                var cam = Camera.main;
                if (cam == null)
                {
                    // カメラが無くなった場合だけ作り直す（記録が残っているかを見る）。
                    return lastLaneGridCamera != null;
                }

                if (!ReferenceEquals(lastLaneGridCamera, cam)) return true;

                var t = cam.transform;
                if (!NearlyEqualVector(lastLaneGridCameraPosition, t.position)
                    || !NearlyEqualRotation(lastLaneGridCameraRotation, t.rotation))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Tracked カメラの位置・回転を更新する (UpdateLaneGrids の適用後に呼ぶ)。</summary>
        private void NoteLaneGridCamera()
        {
            if (FlashBeat.UI.V6.V6UiMode.Playfield)
            {
                lastLaneGridCamera = null;
                return;
            }

            var cam = Camera.main;
            lastLaneGridCamera = cam;
            if (cam == null) return;

            lastLaneGridCameraPosition = cam.transform.position;
            lastLaneGridCameraRotation = cam.transform.rotation;
        }

        private static bool NearlyEqualVector(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude <= 0.000001f; // 位置 0.001 単位の揺れ
        }

        private static bool NearlyEqualRotation(Quaternion a, Quaternion b)
        {
            return Quaternion.Angle(a, b) <= 0.01f;
        }

        private Vector2 lastLaneGridCanvas = new Vector2(float.NaN, float.NaN);
        private float lastLaneGridScale = float.NaN;
        private bool lastLaneGridPlayfield;
        private Camera lastLaneGridCamera;
        private Vector3 lastLaneGridCameraPosition;
        private Quaternion lastLaneGridCameraRotation;

        /// <summary>
        /// レーン格子の第 lane 列へキーキャップと判定テキストを載せる。
        /// x は canvas (root) 左原点。子は PinBottom された行の中に (0, 0.5) アンカーで
        /// 置かれているので anchoredPosition は行左端基準 → 行の inset (offsetMin.x) を
        /// 引いてから載さないと行 inset 分 (Mac KeyGuide 45px / Verdict 98px) 右にずれる。
        /// キーキャップはラベル (TMP, stretch アンカー) ではなくキャップ (親) を動かす。
        /// </summary>
        private void SetLaneGridX(int lane, float canvasX)
        {
            if (lane < verdictTexts.Count)
            {
                var rt = verdictTexts[lane].rectTransform;
                // Issue #402: 同値代入をしない (判定行の固定位置)。
                V6LayoutWrites.AnchoredPositionIfChanged(rt,
                    canvasX - ((RectTransform)rt.parent).offsetMin.x, rt.anchoredPosition.y);
            }
            if (lane < keyCaps.Count)
            {
                var cap = (RectTransform)keyCaps[lane].transform.parent;
                V6LayoutWrites.AnchoredPositionIfChanged(cap,
                    canvasX - ((RectTransform)cap.parent).offsetMin.x, cap.anchoredPosition.y);
            }
        }

        /// <summary>
        /// レーン別判定行。判定の検出は Judge が確定時に積むイベントキュー
        /// (<see cref="Judge.TryDequeueVerdict"/>, Issue #680) をドレインする。
        /// シーンルートの判定クローン走査・クローン名の規約には依存しない。
        /// 判定行が有効な間は 3D 判定エフェクトの Instantiate を
        /// <see cref="Judge.SuppressJudgementEffects"/> で止める
        /// (キューへのイベントは止めない)。
        /// </summary>
        private void UpdateLaneVerdicts()
        {
            // 判定行の有無に合わせて 3D エフェクトの抑制を同期する。
            // モードが実行中に切り替わっても毎フレーム追従する。
            Judge.SuppressJudgementEffects = VerdictRowEnabled;

            int judged = GManager.perfect + GManager.great + GManager.bad + GManager.miss;
            if (judged != lastJudged)
            {
                if (judged < lastJudged)
                {
                    // Retry 等でカウンタが戻った: レーン判定をクリア
                    for (int lane = 0; lane < laneVerdicts.Length; lane++)
                    {
                        laneVerdicts[lane] = null;
                        laneVerdictUntil[lane] = 0f;
                        laneVerdictStart[lane] = 0f;
                        if (lane < verdictTexts.Count)
                        {
                            SetAlpha(verdictTexts[lane], 0f);
                            verdictTexts[lane].rectTransform.localScale = Vector3.one;
                        }
                    }

                    // 前のプレイで積まれた未消費イベントも捨てる。
                    Judge.ClearPendingVerdicts();
                }

                lastJudged = judged;
            }

            DrainPendingVerdicts();

            float now = Time.unscaledTime;
            for (int lane = 0; lane < verdictTexts.Count; lane++)
            {
                float remain = laneVerdictUntil[lane] - now;
                if (remain <= 0f)
                {
                    if (laneVerdicts[lane] != null)
                    {
                        laneVerdicts[lane] = null;
                        SetAlpha(verdictTexts[lane], 0f);
                        verdictTexts[lane].rectTransform.localScale = Vector3.one;
                    }

                    continue;
                }

                // Issue #731: 判定は判定線の位置 (オフセット 0) に即時出現し、
                // VerdictPoseAt の純粋タイムラインに従って上昇 → 頂点で保持 →
                // 判定線を越えて下へ加速落下しながらフェードアウトする。
                // 本編と Option プレビューは同じ関数・同じ定数を使う。
                float elapsed = now - laneVerdictStart[lane];
                V6HudLogic.VerdictPose pose = V6HudLogic.VerdictPoseAt(elapsed, m);
                SetAlpha(verdictTexts[lane], pose.Alpha);

                var rt = verdictTexts[lane].rectTransform;
                // レイアウトは触らず anchoredPosition.y だけを時間で変える。
                // verdictBaseY は判定線上の原点なので、pose.OffsetPx
                // (正 = 判定線より上) をそのまま足す。
                V6LayoutWrites.AnchoredPositionIfChanged(rt, rt.anchoredPosition.x,
                    verdictBaseY + pose.OffsetPx);
            }
        }

        /// <summary>
        /// Judge が判定確定時に積んだイベントをすべて取り出してレーン別判定行へ
        /// 反映する (Issue #680)。キューは確定順なので、同じレーンに同フレームで
        /// 複数イベント (同時押し・HOLD 中断など) があれば後のものが残る。
        /// </summary>
        private void DrainPendingVerdicts()
        {
            // 判定行が無い (VerdictRow=false) ときは表示先が無いので溜めない。
            if (verdictTexts.Count != V6LaneGeometry.LaneCount)
            {
                Judge.ClearPendingVerdicts();
                return;
            }

            while (Judge.TryDequeueVerdict(out int judgement, out int lane))
            {
                if (lane < 0 || lane >= laneVerdicts.Length) continue;

                string label = V6HudLogic.VerdictLabel((LongNote.Judgement)judgement);
                if (label.Length == 0) continue;

                laneVerdicts[lane] = label;
                // Issue #731: 判定の見え時間は VerdictTotalSeconds (1.00s)。
                // 判定線上に出現 → 上昇 → 保持 → 下方退場 (§10.3)。
                // レーンごとに独立した start/until を持つので、同時押しや
                // 高速連打で他レーン・同レーンの再判定がタイミングを
                // 壊さない (同レーンの再判定は新しいイベントで上書き)。
                laneVerdictUntil[lane] = Time.unscaledTime + V6HudLogic.VerdictTotalSeconds;
                laneVerdictStart[lane] = Time.unscaledTime;
                verdictTexts[lane].text = label;
                verdictTexts[lane].color = V6HudLogic.VerdictColor(label);
            }
        }

        private static void SetAlpha(TMP_Text text, float alpha)
        {
            // Issue #402: フェード中は値が変わるので毎フレーム書く必要があるが、
            // 同じアルファのフレーム (フェード開始前・終了後) は書かない。
            V6LayoutWrites.AlphaIfChanged(text, alpha);
        }
    }
}
