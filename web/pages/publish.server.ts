import { defineHandler, type InferProps } from "void";
import { SAMPLE_CHART, SAMPLE_YOUTUBE_ID } from "@/lib/chart/fixtures";

export type Props = InferProps<typeof loader>;

/**
 * The publish screen is a browser only tool.
 *
 * The chart editor draws into a canvas, so the route is not server rendered.
 * The loader still runs on the first request and hands the initial chart to
 * the page.
 */
export const ssr = false;

export const loader = defineHandler(async () => {
  return {
    chart: SAMPLE_CHART,
    youtubeId: SAMPLE_YOUTUBE_ID,
  };
});
