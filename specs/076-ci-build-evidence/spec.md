# Spec: truthful Win64 CI (#76)

Issue #76 | Branch `76` | Base `main` | 2026-11-07

## Objective
Missing Unity credentials produce a visible failed blocker, never a green build without an artifact.

## Owned Paths
`.github/workflows/build-win64.yml`, `specs/076-ci-build-evidence/` only. No Unity runtime, secrets, release or deployment changes.

## User Scenarios & Testing
Given no credentials, running the license step fails and writes a summary. Given a ULF or email/password pair, the gate succeeds. Given packaging failure, absent/empty ZIP or incorrect/missing checksum, upload cannot run.

## Requirements
R1: Failure is explicit without exposing secrets. R2: Native Unity/package exit codes must fail the job. R3: Upload requires one nonempty ZIP and matching SHA-256.

## Dependencies & Gate
No implementation dependency. Actual Windows CI build requires owner-managed Unity credentials; local checks do not prove artifact generation.

## Verification Evidence
Run `python3 specs/076-ci-build-evidence/check.py`; candidate SHA recorded in PR.

## Rollback
Revert this issue merge commit.

## Success Criteria
No credential-free green result; no unverified package upload.
