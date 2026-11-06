import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { CONTRACT_DIR, readRepositoryChart } from "./repository-charts";

/**
 * The contract is frozen by issue #3. The web app ships a copy of the chart
 * schema and of one real chart so the browser bundle stays self contained;
 * these tests fail as soon as the copies drift away from the repository.
 */
describe("contract copies", () => {
  it("keeps the chart schema identical to contracts/schemas/chart.v1.schema.json", () => {
    const contract = readFileSync(path.join(CONTRACT_DIR, "chart.v1.schema.json"), "utf8");
    const copy = readFileSync(path.join(import.meta.dirname, "..", "chart.v1.schema.json"), "utf8");

    expect(copy).toBe(contract);
  });

  it("keeps the sample chart identical to the repository chart", () => {
    const repository = readRepositoryChart("チュートリアル.json");
    const fixture = readFileSync(
      path.join(import.meta.dirname, "..", "fixtures", "sample-chart.json"),
      "utf8",
    );

    expect(fixture).toBe(repository);
  });
});
