import { SongsPanel } from "@/app/songs-panel";
import { StudioLayout } from "@/app/studio-layout";
import "@/styles/app.css";

/**
 * The song management (issue #151): archiving a song and restoring it.
 * A song is never deleted.
 */
export default function SongsPage() {
  return (
    <StudioLayout
      title="Songs"
      subtitle="Archive a song. Its chart, scores and ranking stay."
      active="songs"
    >
      <SongsPanel />
    </StudioLayout>
  );
}
