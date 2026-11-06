import { css } from "styled-system/css";
import { StateBlock, panelBody, panelFrame, panelHeader, panelSubtitle, panelTitle } from "./panel";

const roadmap = css({
  color: "fg.muted",
  display: "grid",
  fontSize: "xs",
  gap: "2",
  margin: "0",
  padding: "0",
});

const roadmapItem = css({
  alignItems: "baseline",
  display: "flex",
  gap: "2",
});

const roadmapKey = css({
  color: "fg.subtle",
  fontVariantNumeric: "tabular-nums",
  minWidth: "2.5rem",
});

export function EditorPanel() {
  return (
    <section className={panelFrame}>
      <header className={panelHeader}>
        <div>
          <h2 className={panelTitle}>Chart editor</h2>
          <p className={panelSubtitle}>Eight-lane Konva grid</p>
        </div>
      </header>

      <div className={panelBody}>
        <StateBlock
          title="The grid is not wired yet"
          description="The eight-lane react-konva grid lands with issue #18 on top of this shell. Everything it needs — tokens, Park UI components and the app frame — is already in place."
        />

        <ul className={roadmap}>
          <li className={roadmapItem}>
            <span className={roadmapKey}>#14</span>
            <span>D1 schema and migrations</span>
          </li>
          <li className={roadmapItem}>
            <span className={roadmapKey}>#16</span>
            <span>Publish API</span>
          </li>
          <li className={roadmapItem}>
            <span className={roadmapKey}>#18</span>
            <span>Konva chart editor</span>
          </li>
        </ul>
      </div>
    </section>
  );
}
