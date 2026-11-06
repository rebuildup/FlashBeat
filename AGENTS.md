# AGENTS.md

FlashBeat の agent 入口。ここには routing と project 固有の不変条件だけを置き、判断の詳細は Skill へ渡す。

## Project

Unity 6 (`6000.5.6f1`) の自作音ゲー。YouTube の映像・音源に合わせて 8 lane の keyboard 入力と falling note を判定する。Windows 64 の展示ビルドを GitHub Releases で配布する。

- ゲーム本体: `Assets/`, `Packages/`, `ProjectSettings/`
- Web Studio (React。Unity とは独立): `web/`
- 配布・運用手順: `docs/operations/`, `release/`
- 技術的な全体像: [`docs/architecture/game-runtime.md`](docs/architecture/game-runtime.md), [`web/AGENTS.md`](web/AGENTS.md)

`Assets/`, `web/`, `constitution/`, `organization/` は異なる workstream が同時に触る。`Owned Paths` の外を無断で横断しない。

## Canonical state

| Fact | Authority |
| --- | --- |
| リリース済み source | `main` |
| 進行中の release 統合 | `release-<major>-<minor>-<patch>`（現在は `integration/*` を使う運用。下の「既知のずれ」） |
| 実装 scope / acceptance / dependency | GitHub Issue |
| 変更候補と検証 evidence | Pull Request |
| 復旧 | Issue / PR / branch / committed docs。会話履歴は canonical ではない |
| secret value | Infisical (self-hosted `https://secrets.rebuildup.dev`) |

## Bootstrap / validation

```sh
mise install --locked                  # repository-pinned toolchain (bun, uv, cf, void)
git lfs pull                          # binary assets; LFS 未取得は complete checkout ではない
```

Unity Editor は Hub で `6000.5.6f1` を開く。CLI の入口:

```sh
# EditMode tests（-quit を付けない。付けると test が走る前に editor が終了する）
Unity -batchmode -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml

# Windows 64 build
Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildStandaloneWindows64
```

`web/` は `web/AGENTS.md` の command に従う（`vp install`, `vp check`, `npx tsc --noEmit`, `npm test`, `vp build`）。agent 環境自体の検証は `mise run skills-check`。

## Work protocol

1. 作業前に Issue を確認する。durable な作業は GitHub Issue を先に作る。
2. branch は Issue 番号だけ（例: Issue #56 → branch `56`）。base は active な release 統合 branch。hard dependency がある stacked ticket だけ immediate predecessor branch を base にできる。
3. 最初の meaningful commit の直後に remote へ push し、Draft PR を作る。`base` / `head` / Issue link / 検証 evidence を PR に書く。
4. review と validation が current SHA で揃ったら、ticket-class PR は追加承認を待たずに dependency 順で release trunk へ landing し、linked Issue を close する。candidate freeze 中は候補外の landing を保留する。release-class PR (`head` が current release branch、`base` が `main`) だけは、その interaction での明示的な user authorization を必要とする。通常の ticket → main は landing 対象外。分類・承認は [ADR-0027](docs/adr/ADR-0027.md) と [ADR-0012](docs/adr/ADR-0012.md) に従う。
5. landing は **merge commit のみ**。squash / rebase merge は使わない。branch 整理の `git rebase` は別扱い。
6. CI が緑でも、current SHA 以外の evidence を green として再利用しない。

詳細: `github-delivery` Skill。release batching と freeze は同 Skill の `references/adaptive-release-batching.md`。

## 判断を user に返す条件

実装方針・tool 選定・設計の優先順位は `engineering-decisions` Skill に従い、agent が決められるものは決める。user に返すのは、product scope、不可逆な操作、release 公開、権限・secret・外部契約の変更など、agent に authority がない判断だけ。

質問・blocker・進捗の見せ方は `interaction-discipline` Skill。

## 文章

README / ADR / Issue / PR / commit / code comment など残る文章は、書く前に `writing-discipline` Skill を読む。日本語の読みやすさは `natural-japanese` Skill を重ねる。commit message は英語、internal documentation / Issue / PR は日本語。

## Worktree と委譲

worktree を作ったら `mise run worktree:setup`、削除前に `mise run worktree:teardown`。Worktrunk (`wt`) が使える環境では `wt switch --create <issue> --base=<base>`。frontend が違っても同じ entrypoint を通す。

- Unity の `Library/` は worktree ごとに独立させる。symlink で共有しない（同時に開くと破損する）。
- port など共有 mutable state は `.worktree.env` の `PORT` を基準に分ける。
- worktree は runtime isolation の証明ではない。worker ごとの DB / port / build output は別に分ける。

委譲は既存の dotfiles `agent` CLI を使用する。`~/.dotfiles/config/agents.toml` を provider / model / quota scope の唯一の正本とし、`agent routes --json` で設定を確認する。明示的な `agent <harness> <provider>` で応答とツール実行を検証してから割り当てる。設定済みであることを利用可能の証拠にしない。

- プロジェクト独自の router / supervisor 基盤を追加しない。唯一の統括監督と active Worker の担当・候補SHA・検証・quota状態は Issue #41 へ記録する。
- 402/429 は同じ quota scope のルートをまとめて停止し、無限再試行や暗黙の有料 fallback をしない。再割当は明示的なルート選択で行う。
- Herdr は既存の terminal 管理に使う。pane の running / done / idle だけで進捗や所有権を判断しない。

詳細: `parallel-orchestration`, `herdr-runtime`, `sandbox-runtime`, `worktree-workflow`。

## 復旧

会話を再開するのではなく、Issue → branch → PR → committed docs の順に状態を再構成する。active ticket は published head と PR を持つ。欠けていれば PR を作り直す。外部 side effect（Release, deploy, D1 migration）は、再実行前に何が起きたかを確認する。

詳細: `agent-recovery` Skill。局限された handoff は `docs/ops/` に残す。

## この repository の house rule

- source code（identifier, comment, developer log）と commit message は英語。internal documentation / Issue / PR / review discussion は日本語。
- commit message: `<work-prefix>: <ごく短い title>`（例: `fix(judge): ...`）。
- 新しい JavaScript / TypeScript の package manager は Bun。`web/` だけは既存の npm lockfile と Vite+ / Void の都合で npm を使う（移行は別 Issue）。
- text search は `rg` / `rg --files`。
- 新しい script を `.py` で追加しない。project の言語か shell で書く。
- 新しい container definition は `Containerfile`。
- project 固有 rule を global 設定・home directory・agent の私的 memory にだけ置かない。repository に残す。

## Skills（能力ごとの担当）

Skill は `.agents/skills/` と `.claude/skills/` に vendored。直接編集せず、`skills-lock.json` と hash を一致させる。

| 能力 | 担当 | 使う場面 |
| --- | --- | --- |
| routing | この `AGENTS.md` | 常に |
| delivery | `github-delivery` | Issue / branch / PR / release / `main` 保護 |
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

Spec Kit の `speckit-*` Skill（`.agents/skills/`, `.claude/skills/`, `.omp/commands/`）は、`docs/agent/spec-kit-workflow.md` の spec / plan / tasks workflow で使う。project-init の能力表とは別の layer として扱う。

`github-delivery` は Linear 連携の箇所で `linear-release-control` を参照するが、この repository は Linear を採用していないため導入しない。GitHub Issues の範囲ではその参照を適用しない。

外部 Skill が pack 内の未導入 Skill を参照する場合は、この repository の担当で読み替える。`tdd` の `codebase-design` は `design-refinement` を、`code-review` の `/setup-matt-pocock-skills` は GitHub Issue / PR を tracker として使う（setup command は実行しない）。

## Skill の管理

```sh
bunx skills add <source> --skill <name> --agent claude-code --agent codex --copy -y
bunx skills update -p -y
mise run skills-check          # vendored と skills-lock.json の hash 検査
```

`--global` は使わない。導入した複製は編集しない。直したい点は upstream へ PR する。

## 既知のずれ（解消するまで事実として扱う）

- profile は `release-x-y-z` を統合線とするが、現在の release 0.1.1 は `integration/exhibit-v2` → `main` の PR #55 で進んでいる。新しい ticket の base は、作業時点の active な release / integration branch を Issue と PR で確認して決める。
- `web/` は npm lockfile を使う。house rule の Bun との差は上に書いた理由による。
- `Assets/Resources/` と `Assets/Game/Resources/` の両方に chart JSON がある。新しい曲は `Assets/Game/Resources/` を正とし、legacy 側を更新しない。
- Unity の実 version は Hub に `6000.6.8f1` も入っているが、project の基準は `ProjectSettings/ProjectVersion.txt` の `6000.5.6f1`。
- Herdr / omp / Worktrunk / Infisical は host の global mise 設定で管理している。`mise install --locked` は bun / uv / cf / void だけを用意する。agent runtime が無い host では、dotfiles の `agent` と Herdr を先に用意する（[ADR-0001](docs/adr/ADR-0001-agent-environment.md)）。
