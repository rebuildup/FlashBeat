import { DashboardPanel } from "@/app/dashboard-panel";
import { StudioLayout } from "@/app/studio-layout";
import "@/styles/app.css";

/**
 * The festival dashboard (issue #151): the ranking of every live song and
 * the statistics of the festival.
 */
export default function DashboardPage() {
  return (
    <StudioLayout title="ダッシュボード" subtitle="ベストスコア、プレイ数、曲数。">
      <DashboardPanel />
    </StudioLayout>
  );
}
