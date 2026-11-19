import type { Chart } from "./types";

/**
 * 白紙の譜面（issue #741）。
 *
 * エディタ（`/chart`）と公開画面（`/publish`）、新曲フローの譜面ステップが
 * それぞれ同じリテラルを持っていたのでここへ集約する。`name` は呼び出し側が
 * 曲やステップに合わせて変えられる。返すのは毎回新しいオブジェクトなので、
 * 呼び出し側が中身を書き換えても他の画面に漏れない。
 */
export function createEmptyChart(name = "無題の下書き"): Chart {
  return { name, maxBlock: 8, BPM: 120, offset: 0, notes: [] };
}
