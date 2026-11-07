import { useCallback } from "react";
import { css } from "styled-system/css";
import { SEARCH_INPUT_ID } from "@/app/topbar";

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

const stepNow = css({
  bg: "#2B4457",
  borderRadius: "full",
  color: "#9AD3FA",
  paddingBlock: "0.5",
  paddingInline: "2",
});

const stepArrow = css({
  color: "fg.subtle",
});

/** v5 primary CTA: solid #EDEDED, dark label, 56px-class height. */
const startButton = css({
  alignItems: "center",
  bg: "#EDEDED",
  borderRadius: "l2",
  color: "#1B1B1B",
  display: "inline-flex",
  fontSize: "sm",
  fontWeight: "medium",
  gap: "2",
  minHeight: "12",
  paddingInline: "6",
  textDecoration: "none",
  whiteSpace: "nowrap",
  width: { base: "100%", lg: "auto" },
  justifyContent: "center",
  _hover: { opacity: "0.9" },
  _focusVisible: { outline: "2px solid #3FA9F5", outlineOffset: "2px" },
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
          <span className={stepNow}>1. 曲を選ぶ（ここから）</span>
          <span className={stepArrow} aria-hidden="true">
            →
          </span>
          <span>2. 譜面が開く・再生して試す</span>
          <span className={stepArrow} aria-hidden="true">
            →
          </span>
          <span>3. 気に入ったらスタッフに伝える（公開はスタッフが行います）</span>
        </p>
      </div>
      <div>
        <button type="button" className={startButton} onClick={start} data-testid="welcome-start">
          リストから曲を選ぶ
        </button>
      </div>
    </section>
  );
}
