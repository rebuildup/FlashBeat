import { useCallback } from "react";
import { css } from "styled-system/css";
import { SEARCH_INPUT_ID } from "@/app/topbar";
import { primaryCta } from "@/app/panel";

/**
 * Visitor entry of the Studio (issue #215): the first screen states the one
 * default path — 曲を選ぶ → 譜面を見る/試す — with a single primary CTA.
 * The CTA is real in every catalog state: with rows it focuses the first
 * row link; without rows (loading/empty/error) it focuses the search field
 * so the guest never lands on a dead control.
 */

const hero = css({
  bg: "gray.surface.bg",
  borderBottomColor: "gray.surface.border",
  borderBottomWidth: "1px",
  display: "flex",
  flexDirection: { base: "column", lg: "row" },
  gap: "4",
  alignItems: { base: "stretch", lg: "center" },
  justifyContent: "space-between",
  padding: { base: "4", md: "5 6" },
});

const copy = css({
  display: "grid",
  gap: "1",
  minWidth: "0",
});

const heading = css({
  color: "fg.default",
  fontSize: "lg",
  fontWeight: "semibold",
  margin: "0",
});

const steps = css({
  alignItems: "center",
  color: "fg.muted",
  display: "flex",
  flexWrap: "wrap",
  fontSize: "xs",
  gap: "2",
  margin: "0",
});

/** v5 Prefab/LegendItem: a key cap + label pair. */
const legendItem = css({
  alignItems: "center",
  display: "inline-flex",
  gap: "1.5",
});

const stepCap = css({
  bg: "gray.8",
  borderRadius: "l1",
  color: "fg.default",
  display: "inline-flex",
  fontSize: "2xs",
  justifyContent: "center",
  minWidth: "4",
  padding: "0.5 1.5",
  textAlign: "center",
  whiteSpace: "nowrap",
});

const stepNowCap = css({
  bg: "colorPalette.solid.bg",
  color: "colorPalette.solid.fg",
  colorPalette: "blue",
});

const stepArrow = css({
  color: "fg.subtle",
});

/** v5 primary CTA (Prefab/Button Primary, 56px class) — the shared class. */
const startButton = primaryCta;

/** Full width on narrow viewports, natural width from `lg`. */
const ctaWrap = css({
  display: "flex",
  "& > *": { width: { base: "100%", lg: "auto" } },
});

export function WelcomeHero() {
  const start = useCallback(() => {
    // The catalog renders cards (base) and a table (md+); only one is
    // visible. Focus the first *visible* row link — focus() on a hidden
    // element is a no-op and would leave the guest with nothing.
    const rows = Array.from(
      document.querySelectorAll<HTMLAnchorElement>("#studio-content a[data-row-link]"),
    );
    const firstRow = rows.find((row) => row.offsetParent !== null) ?? null;
    if (firstRow) {
      firstRow.focus();
      firstRow.scrollIntoView({ block: "center" });
      return;
    }
    document.getElementById(SEARCH_INPUT_ID)?.focus();
  }, []);

  return (
    <section className={hero} aria-labelledby="welcome-heading" data-testid="welcome-hero">
      <div className={copy}>
        <h2 className={heading} id="welcome-heading">
          曲を選んで、その譜面を試してみてください
        </h2>
        <p className={steps}>
          <span className={legendItem}>
            <span className={`${stepCap} ${stepNowCap}`}>1</span>
            <span>曲を選ぶ（ここから）</span>
          </span>
          <span className={stepArrow} aria-hidden="true">
            →
          </span>
          <span className={legendItem}>
            <span className={stepCap}>2</span>
            <span>譜面が開く・再生して試す</span>
          </span>
          <span className={stepArrow} aria-hidden="true">
            →
          </span>
          <span className={legendItem}>
            <span className={stepCap}>3</span>
            <span>気に入ったらスタッフに伝える（公開はスタッフが行います）</span>
          </span>
        </p>
      </div>
      <div className={ctaWrap}>
        <button type="button" className={startButton} onClick={start} data-testid="welcome-start">
          リストから曲を選ぶ
        </button>
      </div>
    </section>
  );
}
