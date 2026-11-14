using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Result
{

    /// <summary>
    /// V6ResultView の段階表示 (reveal) / アニメーション部分
    /// (Issue #385 Phase 2.1 で分割)。partial class のため
    /// フィールド・メソッドは V6ResultView 本体と共有する。
    /// </summary>
    public sealed partial class V6ResultView
    {
        // 段階表示 (Issue #384)。要素は CanvasGroup のアルファで出し入れする
        // (レイアウトは動かさない)。
        private Image trophyIcon;
        private RectTransform trophyRect;
        private CanvasGroup trophyGroup;
        private CanvasGroup gradeGroup;
        private RectTransform gradeRect;
        private CanvasGroup scoreLabelGroup;
        private CanvasGroup scoreValueGroup;
        private CanvasGroup bestLabelGroup;
        private CanvasGroup bestValueGroup;
        private CanvasGroup gaugeGroup;
        private readonly CanvasGroup[] judgeGroups = new CanvasGroup[5];
        private readonly List<CanvasGroup> lateGroups = new List<CanvasGroup>();
        private float revealElapsed;
        private bool revealComplete;
        private bool registerShown;

        // --- 段階表示 (Issue #384) ---
        // 時間割は V6ResultRevealLogic (design 決定 §7.1) が持つ。ここは
        // その時刻を要素へ適用するだけにして、判定可能なロジックを純関数に保つ。

        private void AdvanceReveal()
        {
            if (revealComplete)
            {
                return;
            }

            // 隠れている間は進めない (シーン非表示中に演出を消費しない)。
            revealElapsed += Time.unscaledDeltaTime;
            ApplyReveal(revealElapsed);

            if (!registerShown && V6ResultRevealLogic.ShouldShowRegister(revealElapsed))
            {
                // 2000ms で登録欄 (05b モーダル) を出し、名前欄へフォーカスする
                // (design 決定 §7.1 / §7.3)。
                registerShown = true;
                OpenModal();
            }

            if (V6ResultRevealLogic.IsComplete(revealElapsed))
            {
                revealComplete = true;
            }
        }

        /// <summary>
        /// 段階表示を打ち切って全要素を最終状態で表示する。openRegister が false の
        /// ときは登録モーダルを自動で開かない (Space スキップ / 監査用)。
        /// </summary>
        private void CompleteReveal(bool openRegister)
        {
            revealElapsed = V6ResultRevealLogic.TotalSeconds;
            revealComplete = true;
            ApplyFinalRevealState();

            if (openRegister && !registerShown)
            {
                registerShown = true;
                OpenModal();
            }
            else if (!openRegister)
            {
                // スキップ後に自動で登録欄を開かない (以降の段はもう無い)。
                registerShown = true;
            }
        }

        private void ApplyReveal(float elapsed)
        {
            float trophyAlpha = V6ResultRevealLogic.Alpha(elapsed,
                V6ResultRevealLogic.TrophyStart, V6ResultRevealLogic.TrophyDuration);
            SetAlpha(trophyGroup, trophyAlpha);
            SetScale(trophyRect, V6ResultRevealLogic.TrophyScale(elapsed));

            SetAlpha(gradeGroup, V6ResultRevealLogic.Alpha(elapsed,
                V6ResultRevealLogic.GradeStart, V6ResultRevealLogic.GradeDuration));
            SetScale(gradeRect, V6ResultRevealLogic.GradeScale(elapsed));

            float scoreAlpha = V6ResultRevealLogic.Alpha(elapsed,
                V6ResultRevealLogic.ScoreLabelStart, V6ResultRevealLogic.ScoreLabelDuration);
            SetAlpha(scoreLabelGroup, scoreAlpha);
            SetAlpha(scoreValueGroup, scoreAlpha);
            scoreValue.text = V6ResultLogic.FormatScore(
                V6ResultRevealLogic.CountedScore(runScore, elapsed));

            float bestAlpha = V6ResultRevealLogic.Alpha(elapsed,
                V6ResultRevealLogic.BestStart, V6ResultRevealLogic.BestDuration);
            SetAlpha(bestLabelGroup, bestAlpha);
            SetAlpha(bestValueGroup, bestAlpha);

            for (int i = 0; i < judgeGroups.Length; i++)
            {
                SetAlpha(judgeGroups[i], V6ResultRevealLogic.JudgementAlpha(elapsed, i));
            }

            float gauge = V6ResultRevealLogic.GaugeProgress(elapsed);
            SetAlpha(gaugeGroup, gauge);
            float fill = V6ResultLogic.GaugeFillWidth(runScore);
            gaugeFill.rectTransform.sizeDelta = new Vector2(fill * gauge, GaugeTrackH);

            float lateAlpha = V6ResultRevealLogic.Alpha(elapsed,
                V6ResultRevealLogic.SongInfoStart, V6ResultRevealLogic.SongInfoDuration);
            for (int i = 0; i < lateGroups.Count; i++)
            {
                SetAlpha(lateGroups[i], lateAlpha);
            }
        }

        /// <summary>全要素を最終状態にする (演出の終点とスキップ後)。</summary>
        private void ApplyFinalRevealState()
        {
            SetAlpha(trophyGroup, 1f);
            SetScale(trophyRect, 1f);
            SetAlpha(gradeGroup, 1f);
            SetScale(gradeRect, 1f);
            SetAlpha(scoreLabelGroup, 1f);
            SetAlpha(scoreValueGroup, 1f);
            SetAlpha(bestLabelGroup, 1f);
            SetAlpha(bestValueGroup, 1f);
            for (int i = 0; i < judgeGroups.Length; i++)
            {
                SetAlpha(judgeGroups[i], 1f);
            }

            SetAlpha(gaugeGroup, 1f);
            gaugeFill.rectTransform.sizeDelta = new Vector2(V6ResultLogic.GaugeFillWidth(runScore), GaugeTrackH);
            for (int i = 0; i < lateGroups.Count; i++)
            {
                SetAlpha(lateGroups[i], 1f);
            }

            // カウントアップ途中の値を残さない (RefreshResult と同じ最終表示)。
            scoreValue.text = V6ResultLogic.FormatScore(runScore);
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = alpha;
            }
        }

        /// <summary>段階表示の対象に CanvasGroup を付け、非表示 (alpha 0) で作る。</summary>
        private static CanvasGroup FadeGroup(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = go.AddComponent<CanvasGroup>();
            }

            group.alpha = 0f;
            return group;
        }

        private static void SetScale(RectTransform rect, float scale)
        {
            if (rect != null)
            {
                rect.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
