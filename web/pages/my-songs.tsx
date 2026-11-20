import { MySongsPanel } from "@/app/my-songs-panel";
import type { Props } from "./my-songs.server";
import "@/styles/app.css";

export default function MySongsPage(_props: Props) {
  return <MySongsPanel />;
}
