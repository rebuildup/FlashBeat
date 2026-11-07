import { NewSongFlow } from "@/components/new-song/new-song-flow";
import type { Props } from "./new-song.server";
import "@/styles/app.css";

export default function NewSongPage(_props: Props) {
  return <NewSongFlow apiOrigin={import.meta.env.VITE_FLASHBEAT_API_ORIGIN} />;
}
