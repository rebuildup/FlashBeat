import { DashboardPanel } from "@/app/dashboard-panel";
import { StudioLayout } from "@/app/studio-layout";
import "@/styles/app.css";

/**
 * The festival dashboard (issue #151): the ranking of every live song and
 * the statistics of the festival.
 */
export default function DashboardPage() {
  return (
    <StudioLayout
      title="Dashboard"
      subtitle="Best scores, play counts and song counts."
      active="dashboard"
    >
      <DashboardPanel />
    </StudioLayout>
  );
}
