import { AppShell } from "@/app/app-shell";
import type { Props } from "./index.server";
import "@/styles/app.css";

export default function HomePage({ mode, query, level }: Props) {
  return <AppShell mode={mode} query={query} level={level} />;
}
