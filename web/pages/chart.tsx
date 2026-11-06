import { savePublishDraft } from "@/lib/publish/draft";
import { useEffect, useState } from "react";
import { ChartEditor } from "@/components/chart-editor/chart-editor";
import { loadSongSession } from "@/lib/song-session/load";
import type { Chart } from "@/lib/chart/types";
import type { Props } from "./chart.server";
import "@/styles/app.css";

const emptyChart: Chart = { name: "Untitled draft", maxBlock: 8, BPM: 120, offset: 0, notes: [] };
export default function ChartPage({ songId, version }: Props) {
  const [session, setSession] = useState<Awaited<ReturnType<typeof loadSongSession>> | null>(null);
  const [editedChart, setEditedChart] = useState<Chart | null | undefined>(undefined);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  const selected = songId !== null || version !== null;
  useEffect(() => {
    let active = true;
    setSession(null); setError(""); setEditedChart(undefined);
    if (selected) loadSongSession(songId ?? "", version ?? "").then(
      (value) => { if (active) setSession(value); },
      (reason) => { if (active) setError(reason instanceof Error ? reason.message : String(reason)); },
    );
    return () => { active = false; };
  }, [songId, version, selected, attempt]);
  return <main style={{ height: "100dvh", display: "flex", flexDirection: "column" }}>
    <header style={{ padding: "8px 16px", display: "flex", gap: 24 }}>
      <a href="/">Catalog</a>
      <button disabled={(selected && !session) || editedChart === null} onClick={() => {
        try { window.location.assign(savePublishDraft({ songId, version, chart: editedChart ?? session?.chart ?? emptyChart })); }
        catch { setError("The local draft could not be saved. Keep this editor open and retry."); }
      }}>Review &amp; Publish</button>
      <span>{session ? `${session.song.title} · v${session.chartVersion} · ${session.chart.notes.length} notes` : selected ? "Published chart" : "New local draft · not published"}</span>
    </header>
    {error ? <section role="alert">{error} <button onClick={() => setAttempt(attempt + 1)}>Retry</button></section>
      : selected && !session ? <p role="status">Loading selected chart…</p>
      : <div style={{ flex: 1, minHeight: 0 }}><ChartEditor key={`${songId}:${version}:${attempt}`} initialChart={session?.chart ?? emptyChart} initialYouTubeId={session?.song.youtubeId ?? ""} onChartChange={setEditedChart} /></div>}
  </main>;
}
