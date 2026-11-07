# Tasks: #97

1. [x] `web/tests/e2e/lib/harness.ts` — catalog/chart/schema/write stage の純粋ロジック + report entry 型。
2. [x] `web/tests/e2e/run.ts` — CLI (read-only 既定, `--write` は 3 flag 揃いでしか走らない) + JSON report 出力。
3. [x] `web/tests/e2e/harness.spec.ts` — vitest unit test (fetch mock, 比較ロジック・失敗検出)。
4. [x] `web/tests/e2e/{vitest.config.ts,.gitignore,README.md}` + `web/package.json` script entries (`e2e`, `test:e2e`)。
5. [x] unit test 実行 + 本番 read-only 実走、実値を PR へ記載。
