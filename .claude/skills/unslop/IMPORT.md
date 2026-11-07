# unslop

Vendored copy of [theclaymethod/unslop](https://github.com/theclaymethod/unslop), an
agent skill that removes AI-sounding writing from user-facing text.

- License: MIT (declared in the upstream `SKILL.md` frontmatter; upstream ships no `LICENSE` file)
- Upstream: <https://github.com/theclaymethod/unslop>
- Imported: 2026-11-07

## What it is used for

The Web Studio (`web/`) shows operator-facing copy: headings, button labels, loading and
error states. The skill defines the copy contract used there:

1. Audit the strings first, then rewrite only confirmed findings.
2. Smallest repair: keep the facts, the quantities and the register of the original.
3. No formulaic framing, no empty abstraction, no unsupported certainty, no repetition.
4. Every error and warning states the cause and the next action.

The preset used is `presets/crisp-human.md` (direct, economical, concrete).

The skill changes text only. Layout, class names, `data-testid` and the API surface are out
of its scope.
