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
      subtitle="Archive a song to take it out of the catalog — its history stays."
      active="songs"
    >
      <SongsPanel />
    </StudioLayout>
  );
}
