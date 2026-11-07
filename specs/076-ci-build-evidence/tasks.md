# Tasks: #76

Issue #76 | Branch `76` | Base `main`

- [x] T1 `.github/workflows/build-win64.yml`: fail missing credentials with summary, remove skip gates.
- [x] T2 `.github/workflows/build-win64.yml`: check Unity/package exits and ZIP/SHA integrity before upload; depends T1.
- [x] T3 `specs/076-ci-build-evidence/check.py`: run actual PowerShell blocks for positive/negative scenarios; depends T2; attach SHA and results to Draft PR.

Verification: `python3 specs/076-ci-build-evidence/check.py`. Actual licensed Windows build is separate evidence. Rollback: revert merge commit.
