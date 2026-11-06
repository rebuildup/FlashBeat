# Plan: #76

Use the existing PowerShell workflow. Replace skip output with a failing credential preflight and summary; check native exit codes and package SHA before upload. Scope is `.github/workflows/build-win64.yml` and `specs/076-ci-build-evidence/`.

## Constitution Check
Authority and release decisions remain unchanged. Dedicated worktree owns workflow edits. Local PowerShell evidence is separate from Windows build evidence. No secrets are read or modified.

## Verification Plan
Extract and execute actual YAML PowerShell blocks with synthetic credentials and temporary package files. Missing credentials, partial credentials and invalid packages must fail; configured credentials and matching package must pass.

## Dependencies / Rollback
No dependencies; actual Unity artifact remains unverified until licensed Windows run. Revert merge commit.
