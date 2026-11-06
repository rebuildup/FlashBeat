---
description: "FlashBeat lean per-issue implementation plan template (project-local override of the Spec Kit core plan template)"
---

# Plan: [FEATURE / ISSUE TITLE]

**Issue**: #<issue-number> | **Branch**: `<issue-number>` | **Base**: `release-0-1-0` | **Date**: [DATE]
**Spec**: [`specs/<spec-dir>/spec.md`](spec.md)

## Summary

[What will be built, in one short paragraph, and the approach taken. Reuse the
architecture that already exists; do not introduce a second convention.]

## Owned Paths

```text
<concrete paths this issue creates or modifies>
```

**Structure Decision**: [one sentence on why these paths and not others]

## Dependencies & Gate

- **Depends on**: #<issue-number> (branch `<n>`) — [what is consumed; what is stubbed/faked until it lands]
- **Blocks**: #<issue-number>
- **Gate**: [integration gate: the evidence required before landing]

## Exclusions

- [work explicitly deferred to another issue]

## Constitution Check

*GATE: must pass before implementation.*

- Authority: [`constitution/CONSTITUTION.md`](../../../constitution/CONSTITUTION.md) and
  [`organization/profiles/release-driven-solo.md`](../../../organization/profiles/release-driven-solo.md)
  are unchanged by this issue and remain the top-level contract.
- Mutable Ownership Safety: [how concurrent workers are kept off the same mutable state]
- Evidence Integrity: [what evidence is produced and how it is bound to the SHA]
- Violations: [none, or the deviation and its justification]

## Verification Plan

| # | Check | Command / Method | Evidence to attach |
|---|-------|-------------------|--------------------|
| V1 | [test/lint/build/smoke] | [exact command] | [log/artifact/SHA] |

## Rollback

[Single-issue rollback: the exact revert/delete action and why it cannot affect
other branches.]
