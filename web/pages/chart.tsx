import { ChartEditor } from "@/components/chart-editor/chart-editor";
import { css } from "styled-system/css";
import type { Props } from "./chart.server";
import "@/styles/app.css";

const page = css({
  height: "100dvh",
  overflow: "hidden",
});

/**
 * Chart editor route (`/chart`).
 *
 * The editor is the part of the Web Studio that owns the eight lane grid. It
 * works without the API: it opens with a real chart of the Unity project and
 * imports and exports the legacy JSON format.
 */
export default function ChartPage({ chart, youtubeId }: Props) {
  return (
    <div className={page}>
      <ChartEditor initialChart={chart} initialYouTubeId={youtubeId} />
    </div>
  );
}
