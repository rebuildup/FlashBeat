import { NewSongFlow } from "@/components/new-song/new-song-flow";
import type { Props } from "./new-song.server";
import "@/styles/app.css";

export default function NewSongPage(_props: Props) {
  // The guided flow is a viewport-locked page: it owns its header and its
  // inner scroll; `pages/layout.tsx` adds the shared navigation rail.
  return <NewSongFlow apiOrigin={import.meta.env.VITE_FLASHBEAT_API_ORIGIN} />;
}
