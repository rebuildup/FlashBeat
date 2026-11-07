import { CheckIcon } from "lucide-react";
import { css } from "styled-system/css";

/**
 * Stepper of the new-song flow.
 *
 * Three steps — 曲情報 → 譜面 → 公開 — following design v5: the current step
 * is the raised control (#3A3A3A, white label), done steps keep the muted
 * list look with a check, upcoming steps stay flat. Steps up to the furthest
 * reached step are clickable, so the operator can go back without losing
 * anything.
 */

const rail = css({
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
  alignItems: "center",
});

const stepButton = css({
  alignItems: "center",
  borderRadius: "8px",
  cursor: "pointer",
  display: "flex",
  fontSize: "xs",
  gap: "2",
  minHeight: "10",
  paddingInline: "3",
  _disabled: {
    cursor: "default",
    opacity: "1",
  },
});

const currentStep = css({
  bg: "#3a3a3a",
  color: "#ffffff",
  fontWeight: "medium",
});

const doneStep = css({
  bg: "transparent",
  color: "#9ad3fa",
  _hover: { bg: "#262626" },
});

const upcomingStep = css({
  bg: "transparent",
  color: "#8c8c8c",
});

const stepNumber = css({
  alignItems: "center",
  borderColor: "currentColor",
  borderRadius: "full",
  borderStyle: "solid",
  borderWidth: "1px",
  display: "inline-flex",
  fontSize: "2xs",
  height: "5",
  justifyContent: "center",
  width: "5",
});

const stepLabel = css({
  display: "grid",
  textAlign: "left",
});

const stepLabelJa = css({
  lineHeight: "1.2",
});

const stepLabelEn = css({
  fontSize: "2xs",
  opacity: "0.7",
});

const separator = css({
  bg: "#3a3a3a",
  height: "1px",
  width: "4",
});

export interface StepperStep {
  step: 1 | 2 | 3;
  ja: string;
  en: string;
}

export const NEW_SONG_STEPPER_ITEMS: StepperStep[] = [
  { step: 1, ja: "曲情報", en: "Song info" },
  { step: 2, ja: "譜面", en: "Chart" },
  { step: 3, ja: "公開", en: "Publish" },
];

export interface StepperProps {
  current: 1 | 2 | 3;
  /** Furthest step reached; steps beyond it are not clickable. */
  maxReached: 1 | 2 | 3;
  onSelect: (step: 1 | 2 | 3) => void;
}

export function Stepper({ current, maxReached, onSelect }: StepperProps) {
  return (
    <nav aria-label="新曲登録の手順 / New song steps" className={rail}>
      {NEW_SONG_STEPPER_ITEMS.map((item, index) => {
        const state = item.step === current ? "current" : item.step < current ? "done" : "upcoming";
        const reachable = item.step <= maxReached && item.step !== current;
        return (
          <span
            key={item.step}
            className={css({ display: "flex", alignItems: "center", gap: "2" })}
          >
            {index > 0 ? <span aria-hidden className={separator} /> : null}
            <button
              type="button"
              className={`${stepButton} ${
                state === "current" ? currentStep : state === "done" ? doneStep : upcomingStep
              }`}
              disabled={!reachable}
              aria-current={state === "current" ? "step" : undefined}
              onClick={() => {
                if (reachable) {
                  onSelect(item.step);
                }
              }}
            >
              <span className={stepNumber} aria-hidden>
                {state === "done" ? <CheckIcon size={12} /> : item.step}
              </span>
              <span className={stepLabel}>
                <span className={stepLabelJa}>{item.ja}</span>
                <span className={stepLabelEn}>{item.en}</span>
              </span>
            </button>
          </span>
        );
      })}
    </nav>
  );
}
