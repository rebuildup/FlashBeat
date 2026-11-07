import { NewSongFlow } from "@/components/new-song/new-song-flow";
import { StudioShell } from "@/app/app-shell";
import type { Props } from "./new-song.server";
import "@/styles/app.css";

export default function NewSongPage(_props: Props) {
  // The guided flow is a viewport-locked page: it owns its header and its
  // inner scroll; the shell only adds the shared navigation rail.
  return (
    <StudioShell scroll={false}>
      <NewSongFlow apiOrigin={import.meta.env.VITE_FLASHBEAT_API_ORIGIN} />
    </StudioShell>
  );
}
