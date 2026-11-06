# Plan: #97

- `web/tests/e2e/lib/harness.ts`: 純粋ロジック。全 stage 関数は `fetchImpl` を注入受けにし、unit test で fetch を mock できるようにする。report entry は `{stage, expected, actual, pass}`。 schema 検証は Ajv 2020 + `contracts/schemas/chart.v1.schema.json` を直接 import (import attribute `with { type: "json" }`)。write stage は既存 `publishAndVerify` (`web/src/lib/publish/client.ts`) を import して再利用。
- `web/tests/e2e/run.ts`: CLI。flag parse (`--write`, `--song-id`, `--token-env`, `--base-url`)。既定 read-only。`--write` は `--song-id` と非空 token env が無ければ request 前に exit 2。token は印字しない。report を stdout + `last-report.json` へ。exit code は全 pass の時のみ 0。
- Node 26 の型 strip で `.ts` を直接実行 (`npm run e2e`)。vitest unit test は `tests/e2e/vitest.config.ts` (root の include は `src/**` 限定のため) で `npm run test:e2e`。`node --test tests/` に拾わせないよう spec ファイルは `*.spec.ts` 命名。
- `web/tests/e2e/.gitignore` に `last-report.json` (ownership が web/tests/e2e/** のため、親 .gitignore は編集しない)。
- 検証: unit test 全 pass → read-only モードを本番へ実走させ実値を PR に貼る。write stage は本番未実行 (flag が無いと走らない設計 + unit test で担保)。
