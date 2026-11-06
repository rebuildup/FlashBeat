# CLAUDE.md

この repository の正本は [`AGENTS.md`](AGENTS.md)。Claude Code も最初に `AGENTS.md` を読み、そこから Skill と docs へ進む。

- 追加の agent instruction をここへ複製しない。rule を変えるときは `AGENTS.md` か該当 Skill を更新する。
- Claude 固有の設定は `.claude/settings.json`（project-local hook）と `.claude/skills/` にだけ置く。
- Unity / LFS / build / test の詳細は [`docs/architecture/game-runtime.md`](docs/architecture/game-runtime.md) と [`web/AGENTS.md`](web/AGENTS.md)。
