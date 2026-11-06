# FlashBeat — stacked PR execution plan (festival overnight)

Target: 2026-11-07. Contract PR: #8 (`3` → `release-0-1-0`).
Policy: **one agent = one Issue = one worktrunk worktree = one Draft PR**.
Stacking changes **PR review base**, not work ownership, release authority or evidence requirements.

## Branch / PR DAG

```text
release-0-1-0
├─ 3   contract #3  → release-0-1-0  (Draft PR #8, contract currently proposed)
│  └─ 12  Void/Vite+/Panda/Park scaffold #12 → 3
│     ├─ 14  D1 schema/migrations #14 → 12
│     │  └─ 15  read catalog/chart API #15 → 14
│     │     └─ 16  publish API #16 → 15
│     │        └─ 20  scoring API #20 → 16   [OPTIONAL]
│     └─ 18  Konva chart editor #18 → 12
│        └─ 19  Park UI publisher #19 → 18  [external runtime dependency: #16]
├─ 9   Cloudflare/Infisical permission preflight #9 → release
├─ 10  Spec Kit existing-project setup #10 → release
├─ 11  Windows baseline #11 → release
│  └─ 13  streamed yt-dlp playback #13 → 11
│     └─ 17  Unity remote catalog/chart #17 → 13   [external API dependency: #15/#16]
│        └─ 21  Unity leaderboard #21 → 17  [OPTIONAL, external #20]
└─ 22  release smoke tests/runbook #22 → release

#4/#5/#6/#7 are **integration gates**, not independent implementation-worker branches.
#23 is an intentionally UNASSIGNED UI feedback slot; only activate on concrete feedback.
```

All numeric branches above are **proposed** except already existing `3` and `release-0-1-0`; do not claim newly listed branches/PRs exist until created by workers.

## Start-time optimization

- Start **#9, #10, #11** simultaneously on independent hosts/worktrees. **#12** can start from the pushed `3` contract branch while PR #8 awaits review; do not wait for merged release.
- Once #12 HEAD is pushed and a Draft PR exists, **#14 and #18 start in parallel**, each based on `origin/12`. Neither waits for #12 to land.
- Once #14 HEAD is pushed, **#15** starts on `origin/14`; after #15 first stable contract implementation, **#16** starts on `origin/15`. PR review and implementation proceed concurrently.
- On Win, once #11 HEAD is pushed and its build is proven, #13 starts from `origin/11`. After #13 streamed gameplay proof, **#17** can start from `origin/13` against recorded API fixtures before #15/#16 are deployed. The **runtime S2 integration gate** waits for real endpoints, not the code-writing stage.
- **#19** may build the publishing UI from `origin/18` using schema/mock PUT before #16 is deployed. Only final publish verification depends on #16.
- Optional #20 and #21 are launched **only after S3 is functionally stable**; otherwise preserve the current playable build.
- **#22** maintains independent release evidence and artifacts throughout; no release version is marked passed merely because agent PRs are green.
- Keep at least one worker capacity slot free for #23 UI/UX feedback. Never fill spare capacity with speculative features.

## CLI conventions (example, from repository root)

```sh
# Example child of a pushed parent branch. No native worktrunk stack implementation.
git fetch origin
wt switch --create 14 --base origin/12
# work in issue 14 worktree; commit and publish
git push -u origin 14
gh pr create --draft --base 12 --head 14 --title 'S2: D1 schema and migration' --body 'Depends on #12; closes #14'
```

Use the same `wt switch --create <issue-number> --base origin/<parent-issue-number>` pattern. For branches with multiple logical dependencies, select **one Git parent** and use contract fixtures/mocks until the other workstream is integrated: never create a separate worker to maintain an ad-hoc cross-stack merge branch.

**Worktrunk does not natively implement automatic stack rebase.** It manages worktrees and accepts `--base`. To update stacked children after parent HEAD changes, use coordinated, reviewed `git merge origin/<parent>` into child and push (avoids forced rewriting on shared agent branches). Use stack-sync tooling only if already vetted and available; not required for tonight.

## Stacked PR landing rules

1. The **base of a Draft PR** is its immediate predecessor branch; it must show only that issue's distinct diff. PRs should open after first meaningful commit, not wait until completion.
2. Parent is pushed early; child may start before parent PR review/merge. Child PR remains Draft if its actual prerequisites are missing.
3. Integration owner lands in topological order, through **merge commits** only (preserves project-init policy). Avoid squash/rebase merge.
4. After parent lands, retarget the child's base to the parent destination (`release-0-1-0` or the next still-open predecessor) as appropriate; re-check diff and tests, do not trust auto-retarget. Never change branch base to main accidentally.
5. Cross-stack integration: #16 (cloud API) + #17 (Win player) can be separately merged to release, but Stage S2 is not released until combined Win64 remote-song demo is observed. Similarly #18+#19 and #16 must integrate before S3.
6. Keep prior proven Win64 artifact for each stage; failure of a later stage is not reason to delete S1/S2 executable.
7. Reviewer/integrator may approve the code readiness but **does not infer merge authorization** from readiness. Follow the existing authority boundary; do not idle indefinitely on avoidable tooling tasks.

## Shared interfaces, library, UI decisions

- The only shared contract is `contracts/openapi.v1.yaml` (5 routes) and `contracts/schemas/chart.v1.schema.json` (legacy format). Contract owner #3 controls changes after freeze.
- Cloud + editor: **Void** native routes, **Vite+**, **React/TypeScript**, **Panda CSS** + **Park UI / Ark UI**, **react-konva** note grid, YouTube iframe preview.
- Backend: Void built-in D1/Drizzle capabilities, no second Hono app, no bespoke ORM/auth/secret loader.
- Tokens only from existing **Infisical** to Cloudflare CLI/runtime. #9 checks environment and scopes FIRST and escalates any truly manual account/credential action once with exact scope.
- Windows runtime: existing **Unity 6 + uGUI/TMP + VideoPlayer + yt-dlp/Deno**. Only Win runtime owner mutates Scene/Prefab and one Unity Editor process.
- No Tailwind, shadcn, new design system, offline library, websocket, new chart DSL, user account, enterprise-grade anticheat.

## Specs and validation scope

Each worker may write a **short, local** feature spec/plan/tasks inside its worktree in Spec Kit format if setup from #10 is available. No retrospective specification of the entire legacy game; never overwrite project-init Constitution. Keep feature scope <=5 tasks with testable acceptance criteria.

Mac/Web: tests and API fixtures can run before parent merges. Win: editmode tests can run on an isolated checkout if Unity runtime permits, but **actual Win64 build/gameplay gate is serialized** through one Unity owner. Published work is Git SHA pinned; logs/build artifacts include chartVersion, worker branch and stage.

At handoff: report 1) branch SHA, 2) PR URL/base/head, 3) executed tests & actual results, 4) remaining blockers, 5) which next issue is unblocked. Herdr idle status is not evidence of completion.
