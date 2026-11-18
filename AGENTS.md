# AGENTS.md

This file is the root contract (dispatcher) for AI agents working in this repository. Keep it short — project details live behind the pointers below.

## Project

FlashBeat is a Unity 6 (6000.5.6f1) self-made rhythm game (`音ゲー`) that plays music from YouTube videos and judges 8-lane keyboard input against falling notes. Japanese-language project; comments and many identifiers are in Japanese. `web/` is the festival / Cloudflare frontend — its rules live in `web/AGENTS.md`.

## Canonical state

- source of truth: git — `main` is the released state, `release-x-y-z` is the current release integration line
- durable work scope / acceptance criteria / dependencies: GitHub Issues; review / candidate integration / validation evidence: GitHub Pull Requests
- secrets: Infisical (self-hosted) — see "Cloud deployment credentials" below
- Unity runtime structure, gameplay loop, song data format, tests: [`docs/architecture/game-runtime.md`](docs/architecture/game-runtime.md)
- flat (unlit) playfield rendering: [`docs/architecture/flat-rendering.md`](docs/architecture/flat-rendering.md)

## Bootstrap / validation

- toolchain: `mise install` (project-local pins in `mise.toml` + `mise.lock`); Unity 6000.5.6f1 via Unity Hub; after a fresh clone run `git lfs pull`
- EditMode tests (batch mode — do **not** pass `-quit`, it exits before the test runner starts):
  `Unity -batchmode -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml`
- Unity-less C# typecheck: `script/compile-check.sh`
- builds: Editor menu `Build → Build Windows 64` / `Build → Build macOS`, or batch `-executeMethod BuildScript.BuildStandaloneWindows64` / `BuildStandaloneMacOS`; release/update contract and packaging: [`docs/operations/update-release.md`](docs/operations/update-release.md)
- agent environment check: `mise run skills-check` (vendored Skills vs `skills-lock.json`)

## Work protocol

- top-level durable work is a GitHub Issue; ticket branch / PR lifecycle, stacked PRs, release PRs and `main` protection follow the `github-delivery` Skill (capability table below)
- publish the first meaningful commit, push, and open a PR early; details in `github-delivery`
- which decisions stay with the agent vs. go back to the user: `engineering-decisions`
- persistent prose (docs, ADR, Issue/PR, commits): `writing-discipline` (+ `natural-japanese` for Japanese reader-facing text)
- resuming after interruption: `agent-recovery` — resume from Issue / PR / branch / checkpoint, not conversation history
- worktrees: `mise run worktree:setup` / `mise run worktree:teardown` (`.config/wt.toml`, `scripts/worktree/`); Worktrunk, plain git and IDE-created worktrees all use the same entry points
- top-level delegation uses the host's existing agent router (dotfiles `agent` CLI); this repository intentionally has no project-local route config — see ADR-0001 decision 4
- implementation / CI / release quality contract (what must be verified at each stage): [`docs/operations/development-convergence.md`](docs/operations/development-convergence.md)

## House rules

- source code (identifiers, comments, developer-facing logs) and commit messages are English; internal documentation, Issues, PRs and review discussion are Japanese. Localization resources are the exception.
- commit message format: `<work-prefix>: <short title>` (e.g. `fix(#96): ...`, `docs: ...`)
- JS/TS package manager is Bun — except `web/` and `design/`, which currently use npm (`package-lock.json`); the Bun migration is tracked separately (ADR-0001 consequences)
- text search: `rg` / `rg --files`
- do not add new `.py` scripts — use the project language or shell (existing `script/*.py` are grandfathered)
- new container definitions are `Containerfile`
- project rules live in this repository, not only in global config / home directory / agent-private memory

## Cloud deployment credentials

- Infisical (self-hosted `https://secrets.rebuildup.dev`) is the only secrets source of truth. FlashBeat has its own project (`0645e78f-ae7a-48b3-8597-4986be983496`, env `dev`); never read, copy or reuse another project's Infisical secrets or Cloudflare tokens.
- Binding: `.infisical.json` (tracked) + `config/infisical.sh` (instance metadata). Inject secrets per command with `./script/with-secrets <command>` (Windows: `pwsh script/with-secrets.ps1 <command>`). Never commit `.env` files or secret values.
- The official `cf` CLI and `void` are pinned in `mise.toml` (project-local, never install globally); run `mise install` first and invoke them as `mise exec -- cf ...` / `mise exec -- void ...`, always under `script/with-secrets` so `CLOUDFLARE_API_TOKEN` / `CLOUDFLARE_ACCOUNT_ID` are injected noninteractively. `wrangler` is not used.
- State, required token scopes, rollback and safe boundaries: [`docs/operations/cloudflare-bootstrap.md`](docs/operations/cloudflare-bootstrap.md).
- Festival D1 schema, migrations and the remote apply record: [`docs/operations/d1-migrations.md`](docs/operations/d1-migrations.md).

## Constitution / operating profile

- Top-level contract: [`constitution/CONSTITUTION.md`](constitution/CONSTITUTION.md)
- Current Operating Model: [`organization/profiles/release-driven-solo.md`](organization/profiles/release-driven-solo.md)
- Unity/LFS/build/test specifics: [`docs/architecture/game-runtime.md`](docs/architecture/game-runtime.md) remains project-specific authority while preserving the Constitution.

## Skills (能力ごとの担当)

Skills are vendored into `.agents/skills/` and `.claude/skills/` and pinned by `skills-lock.json`. Do not edit a vendored copy; send the change upstream instead. The adoption decision and its reasons are recorded in [`docs/adr/ADR-0001-agent-environment.md`](docs/adr/ADR-0001-agent-environment.md) section 6.

| 能力 | 担当 | 使う場面 |
| --- | --- | --- |
| routing | この `AGENTS.md` | 常に |
| delivery | `github-delivery` | Issue / branch / PR / release |
| recovery | `agent-recovery` | 中断・crash・sandbox 再作成からの再開 |
| orchestration | `parallel-orchestration` | subagent / 複数 worker への分解と統合 |
| supervisor runtime | `herdr-runtime` | Herdr pane で worker を起動・監視する |
| execution isolation | `sandbox-runtime` | worker ごとの mutable runtime 分離 |
| workspace lifecycle | `worktree-workflow` | worktree の作成・掃除・build cache |
| quality gate | `quality-gate` | verification level、test / CI / build の設計 |
| design-before-build | `design-refinement` | 非自明な feature / architecture / product design |
| engineering decisions | `engineering-decisions` | 実装判断と user への escalation 境界 |
| debugging | `diagnosing-bugs` | 再現する失敗・性能劣化の原因調査 |
| TDD | `tdd` | test-first / red-green-refactor を明示した時 |
| code review | `code-review` | branch / PR / WIP の diff review |
| correctness | `correctness-assurance` | 不変条件と最小十分な保証手段 |
| naming | `code-naming` | 新規命名・rename・命名 review |
| security advisory | `security-maintenance` | dependency / runtime の脆弱性判断 |
| security audit | `security-audit` | 未知の security defect の能動探索 |
| secrets | `secrets-management` | Infisical の project / path / 権限 / 注入 |
| estimation | `agent-delivery-estimation` | 中長期の release / capacity 見積もり |
| onboarding | `onboarding` | fresh clone から開発・復旧できる docs |
| prose | `writing-discipline` | 残る文章を書く |
| Japanese prose | `natural-japanese` | 日本語の自然さ・読みやすさ |
| interaction | `interaction-discipline` | 質問・blocker・進捗の見せ方 |
| policy evaluation | `policy-evaluation` | Constitution / Skill / 環境自体の評価 |
| skill selection | `skill-selection` | 導入 Skill の選定・見直し |
| visual design | `design-intent` | UI の visual direction が未確定な時 |
| Cloudflare | `cf-cli` + `workers-best-practices` | API / D1 / Worker の操作と review（multi） |
| React / frontend | `web/AGENTS.md` | `web/` の実装。汎用 React Skill は使わない |
| release planning | GitHub Issues | Linear は採用していない |
| skill authoring | なし | 変更は upstream (`rebuildup/project-init` 等) へ PR |
| ideation / document formats / motion | なし | 必要になった時点で `skill-selection` で評価する |

Linear 系 Skill (`linear-release-control`) と pack 全体の router (`using-superpowers`, `poteto-mode` 等) は不採用。入口は root contract だけが持つ。

Spec Kit の `speckit-*` Skill（`.agents/skills/`, `.claude/skills/`）は、`docs/agent/spec-kit-workflow.md` の spec / plan / tasks workflow で使う。project-init の能力表とは別の layer として扱う。

外部 Skill が pack 内の未導入 Skill を参照する場合は、この repository の担当で読み替える。`tdd` の `codebase-design` は `design-refinement` を、`code-review` の `/setup-matt-pocock-skills` は GitHub Issue / PR を tracker として使う（setup command は実行しない）。

### Skill の管理

```sh
bun evals/policy-evaluation/vendor-skills.mts            # upstream から再取得して lock を更新
bun evals/policy-evaluation/vendor-skills.mts --check    # lock が最新か確認（network 必要）
mise run skills-check                                     # vendored と lock の hash 検査（offline）
```

`skills-lock.json` の各 entry は upstream の commit SHA で pin され、`computedHash` は skill directory 配下の全通常ファイル（`.git/` と `node_modules/` を除く）を lower-case 相対パス昇順で「パス + 生バイト列」の連結として sha256 したもの。`mise run skills-check` はこの定義を実装した eval script であり、CI でも同じ検査を走らせる。
