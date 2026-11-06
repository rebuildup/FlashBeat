# ADR-0001: agent 環境の再整備と Skill の採否

- Status: Accepted
- Date: 2026-11-07
- Issue: #56
- Related: `constitution/CONSTITUTION.md`, `organization/profiles/release-driven-solo.md`

## Context

project-init の初期化 prompt をこの repository に適用した時点で、次のずれがあった。

- `skills-lock.json` は 19 個の Skill を記録していたが、`.agents/skills/` と `.claude/skills/` には speckit の 9 個しか無く、fresh clone では記録された Skill を起動できなかった。lock の hash も現行 upstream と 7 個で一致していなかった。
- `AGENTS.md` は 2026-08 時点の構造（3 assembly、`Assets/Scripts/`、2 個の test）を指しており、現行の 5 assembly・`Assets/Game/Scripts/`・10 test file と矛盾していた。`CLAUDE.md` にはより新しい内容があったが、両者が別々に保守されていた。
- worktree は日常的に使われている（`wt v0.81.0`、20 個以上の worktree）が、project-local の setup / teardown 入口と Worktrunk hook が無かった。
- top-level の Herdr / omp 委譲は運用されていたが、route 契約と起動 CLI が repository に無かった。
- GitHub の merge 設定は squash / rebase を許可し、`main` は保護されていなかった。profile は merge commit のみと release PR 経由の統合を要求する。
- `docs/adr/` が無く、採否の理由を残す場所が無かった。

## Decision

### 1. Skill は vendored し、hash で固定する

`bunx skills add <source> --skill <name> --agent claude-code --agent codex --copy` で `.agents/skills/` と `.claude/skills/` の両方に複製し、`skills-lock.json` と一緒に commit する。`bun evals/policy-evaluation/vendored-skills-contract.mts` を `mise run skills-check` と CI で実行し、lock と複製の一致を検査する。複製は直接編集せず、変更は upstream へ PR する。

### 2. root contract は dispatcher、技術詳細は docs

`AGENTS.md` は目的・canonical state・bootstrap / validation・work protocol・委譲境界・house rule・能力表だけを持つ。Unity の構造と罠は `docs/architecture/game-runtime.md` に移す。`CLAUDE.md` は `AGENTS.md` を指す adapter にし、rule を複製しない。

### 3. worktree lifecycle を frontend 非依存にする

`scripts/worktree/setup.sh` / `teardown.sh` を `mise run worktree:setup` / `worktree:teardown` から呼ぶ。Worktrunk は `.config/wt.toml` から同じ入口を使う。Unity の `Library/` は worktree ごとに独立させ、共有・symlink しない。port は `.worktree.env` の `PORT` で分ける。

### 4. 委譲は route 設定へ分離する

`templates/agent-routing` の CLI と route 契約を `scripts/agent-routing/` に materialize する。provider / model / 認証は dotfiles の `config/agents.toml` を正本とし、repository の `agent-routing.json` には role → route の選択だけを置く。route 不在・quota / 認証失敗は `blocked` とし、暗黙の provider / native subagent fallback をしない。Claude の native `Agent` / `Task` を禁止する hook は、`code-review` の worker 内 subagent と衝突するため導入しない。境界は root contract の policy で示す。

### 5. delivery 設定を profile に合わせる

repository の `allow_merge_commit=true` / `allow_squash_merge=false` / `allow_rebase_merge=false` を適用した。`main` は PR 必須、approving review 0、conversation resolution 必須、force push / deletion 禁止、admin にも適用する。required status check は、存在しない check 名を固定しないため設定しない。

### 6. 導入する Skill と見送る Skill

`catalog/capabilities.tsv` の `needed_when` を repository の事実で判定した。

採用:

| 能力 | 担当 | 理由 |
| --- | --- | --- |
| debugging | `diagnosing-bugs` | Unity / Web の再現できる失敗がある |
| TDD | `tdd` | EditMode / vitest の test 基盤がある。test-first を明示した時だけ起動する |
| code review | `code-review` | review 対象の diff と Issue がある |
| Japanese prose | `natural-japanese` | 日本語の Issue / docs / PR を書く |
| visual design | `design-intent` | `design/flashbeat-ui.pen` と uGUI 実装がある |
| Cloudflare | `cf-cli` + `workers-best-practices` | Worker / D1 / `cf` CLI を使う。Durable Objects は未使用なので採らない |
| naming | `code-naming` | C# の命名・rename がある |
| skill selection | `skill-selection` | 初期化・再整備の判断を再現する |

見送り:

- `linear-release-control`: release planning は GitHub Issues で行っており、Linear を採用していない。
- `vercel-react-best-practices`: `web/` は Next.js ではなく Void / Vite+ で、project 固有の `web/AGENTS.md` が既に stack と command を固定している。汎用の Next.js 前提 rule を持ち込まない。
- `durable-objects`: `web/` に Durable Object が無い。
- `skill-authoring`, `idea-discovery`, `motion-system`, `document-formats`, `orca-frontend`: 現在の repository に該当する作業・tool が無い。必要になった時点で `skill-selection` で評価する。
- `pack` 全体の router（`using-superpowers`, `poteto-mode` 等）: 入口は root contract だけが持つ。

## Consequences

- fresh clone でも Skill が揃い、lock との不一致を CI / ローカルで検出できる。
- `AGENTS.md` と `CLAUDE.md` の二重管理が無くなる。Unity の詳細は 1 つの architecture doc に集約される。
- Unity worktree の `Library/` を誤って共有する事故を、手順と setup の注意で防ぐ。ただし symlink を機械的に禁止する仕組みではない。
- native subagent の禁止は hook ではなく policy なので、強制力は弱い。Herdr / omp の実運用で逸脱を観測したら、hook か permission の再設計を行う。
- 既存の release topology（`integration/*` → `main`）は profile と異なる。この ADR では変更せず、root contract に「既知のずれ」として残した。
- `web/` の npm は残した。Bun への移行は lockfile と Void / Vite+ の動作確認を含む別 Issue とする。
- Herdr / omp / Worktrunk / Infisical は host の global mise 設定で管理されており、この repository の `mise.toml` には pin していない。`mise install --locked` だけでは agent runtime は揃わない。Windows で Herdr が使えない事情もあるため、project pin へ移す場合は platform 条件と `mise.lock` の再生成を伴う別 Issue にする。

## Re-evaluate

- `skills` CLI が lock の `computedHash` を検証して install できるようになったら、`skills-bootstrap` を簡素化する。
- Herdr / omp を別 runtime に置き換えたら、`agent-routing` と `herdr-runtime` の採否を見直す。
- `integration/*` 運用が続くなら、profile 側の release branch 表現を更新するか、`release-x-y-z` へ戻す。
- code-review / tdd が実際の作業で過剰または無視されるなら、`policy-evaluation` で比較評価して採否を変える。
