import { css } from "styled-system/css";
import { styled } from "styled-system/jsx";
import { button } from "styled-system/recipes";
import { StateBlock, panelBody, panelFrame, panelHeader, panelSubtitle, panelTitle } from "./panel";

/**
 * Pointer to the publish screen.
 *
 * The screen itself lives on its own route (`/publish`), because it holds the
 * chart editor next to the metadata form and needs the whole window.
 */
const callToAction = css({
  alignItems: "center",
  display: "flex",
  flexDirection: "column",
  gap: "3",
  justifyContent: "center",
  minHeight: "12rem",
  textAlign: "center",
});

const accent = css({ colorPalette: "teal" });

/**
 * The link itself looks like a button.
 *
 * A link around a button would nest two interactive elements, which is
 * invalid HTML and confuses the keyboard order.
 */
const PublishLink = styled("a", button);

export function PublishPanel() {
  return (
    <section className={panelFrame}>
      <header className={panelHeader}>
        <div>
          <h2 className={panelTitle}>Publish</h2>
          <p className={panelSubtitle}>One atomic PUT of metadata and chart</p>
        </div>
      </header>

      <div className={panelBody}>
        <div className={callToAction}>
          <StateBlock
            title="The publish screen has its own route"
            description="Metadata, chart and the staff token of the operator live on one screen. Open it to edit a song and publish it."
          />
          <PublishLink className={accent} href="/publish">
            Open the publish screen
          </PublishLink>
        </div>
      </div>
    </section>
  );
}
