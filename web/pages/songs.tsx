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
      title="楽曲アーカイブ"
      subtitle="曲をアーカイブします。譜面、スコア、ランキングは保持されます。"
    >
      <SongsPanel />
    </StudioLayout>
  );
}
