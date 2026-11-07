import { readPublishDraft } from "@/lib/publish/draft";
import { useEffect, useState } from "react";
import { SongPublisher } from "@/components/song-publisher/song-publisher";
import { loadSongSession } from "@/lib/song-session/load";
import type { Chart } from "@/lib/chart/types";
import type { Props } from "./publish.server";
import "@/styles/app.css";

const emptyChart: Chart = { name: "Untitled draft", maxBlock: 8, BPM: 120, offset: 0, notes: [] };
export default function PublishPage({ songId, version, draftId }: Props) {
  const [session, setSession] = useState<Awaited<ReturnType<typeof loadSongSession>> | null>(null);
  const [localChart, setLocalChart] = useState<Chart | null>(null);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  const selected = songId !== null || version !== null;
  useEffect(() => {
    let active = true;
    setSession(null);
    setError("");
    try {
      setLocalChart(draftId ? readPublishDraft(draftId, songId, version) : null);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      return;
    }
    if (selected)
      loadSongSession(songId ?? "", version ?? "").then(
        (value) => {
          if (active) setSession(value);
        },
        (reason) => {
          if (active) setError(reason instanceof Error ? reason.message : String(reason));
        },
      );
    return () => {
      active = false;
    };
  }, [songId, version, draftId, selected, attempt]);
  if (error)
    return (
      <main role="alert">
        {error} <button onClick={() => setAttempt(attempt + 1)}>Retry</button>{" "}
        <a href="/">Catalog</a>
      </main>
    );
  if (selected && !session) return <p role="status">Loading selected chart…</p>;
  return (
    <div style={{ height: "100dvh", overflow: "hidden" }}>
      <SongPublisher
        key={`${songId}:${version}:${attempt}`}
        initialChart={localChart ?? session?.chart ?? emptyChart}
        initialYouTubeId={session?.song.youtubeId ?? ""}
        initialSong={session?.song}
        initialVersion={session?.chartVersion}
      />
    </div>
  );
}
