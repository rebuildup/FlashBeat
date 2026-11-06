import { AppShell } from "@/app/app-shell";
import type { Props } from "./index.server";
import "@/styles/app.css";

export default function HomePage({ mode }: Props) {
  return <AppShell mode={mode} />;
}
