import { SongPublisher } from "@/components/song-publisher/song-publisher";
import { css } from "styled-system/css";
import type { Props } from "./publish.server";
import "@/styles/app.css";

const page = css({
  height: "100dvh",
  overflow: "hidden",
});

/**
 * Publish route (`/publish`).
 *
 * The screen shows the metadata of one song next to the chart editor and
 * sends both in one request. It needs no backend: without an API origin it
 * publishes against the bundled fixture of the contract.
 */
export default function PublishPage({ chart, youtubeId }: Props) {
  const apiOrigin = import.meta.env.VITE_FLASHBEAT_API_ORIGIN ?? "";

  return (
    <div className={page}>
      <SongPublisher initialChart={chart} initialYouTubeId={youtubeId} apiOrigin={apiOrigin} />
    </div>
  );
}
