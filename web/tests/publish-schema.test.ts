/**
 * Unit test of the publish request schema (issue #16).
 *
 * `SONG_PUBLISH_REQUEST_SCHEMA` is a bundled copy of the frozen contract
 * schema (`contracts/openapi.v1.yaml` → `SongPublishRequest`), because a
 * Worker has no filesystem to read the contract from at runtime. This test
 * reads the real contract file and fails when the copy drifts.
 */
import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { openapi } from "./helpers/contract.ts";
import { SONG_PUBLISH_REQUEST_SCHEMA } from "../routes/api/v1/_publish-model.ts";

const chartSchemaId = "https://flashbeat.local/contracts/schemas/chart.v1.schema.json";

describe("SONG_PUBLISH_REQUEST_SCHEMA", () => {
  it("matches the contract's SongPublishRequest schema", () => {
    const components = openapi.components as unknown as {
      schemas: Record<string, Record<string, unknown>>;
    };
    const contractSchema = components.schemas.SongPublishRequest;

    // The only intended difference: the chart reference points at the URL the
    // chart schema is registered under in the runtime Ajv instance.
    const expected = {
      ...contractSchema,
      properties: {
        ...(contractSchema.properties as Record<string, unknown>),
        chart: { $ref: chartSchemaId },
      },
    };

    assert.deepEqual(SONG_PUBLISH_REQUEST_SCHEMA, expected);
  });
});
