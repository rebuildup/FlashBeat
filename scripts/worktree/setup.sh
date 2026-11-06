#!/usr/bin/env bash
# Prepare the current worktree. Every frontend (Worktrunk, plain git, an IDE)
# calls this through `mise run worktree:setup`, so the state does not depend on
# which tool created the worktree. Safe to run repeatedly.
set -euo pipefail

root=$(git rev-parse --show-toplevel)
cd "$root"

# Derive per-worktree values from the branch, not from a frontend template.
branch=$(git branch --show-current)
key=${branch:-$(git rev-parse --short HEAD)}
checksum=$(printf '%s' "$key" | cksum | cut -d' ' -f1)
port=$((20000 + checksum % 10000))

# Write atomically so an interrupted or concurrent run never leaves a partial file.
tmp=$(mktemp "$root/.worktree.env.XXXXXX")
printf 'WORKTREE_KEY=%s\nPORT=%s\n' "$key" "$port" > "$tmp"
mv "$tmp" "$root/.worktree.env"

# Materialize the project toolchain when mise is available.
if [ -f mise.toml ] && command -v mise >/dev/null 2>&1; then
  if [ -f mise.lock ]; then
    mise install --locked
  else
    mise install
  fi
fi

# Unity keeps Library/, Temp/ and Logs/ inside the project root. They must stay
# per-worktree: sharing or symlinking them corrupts concurrent editors. Nothing
# to create here - just do not share them.

# LFS assets are smudged by `git worktree add` unless the checkout skipped it.
# In `git lfs ls-files`, `-` marks a pointer whose object is not downloaded.
if command -v git-lfs >/dev/null 2>&1; then
  if git lfs ls-files | awk '$2 == "-" { found = 1 } END { exit !found }'; then
    echo "worktree:setup: some LFS objects are missing; run 'git lfs pull' before Unity validation"
  else
    echo "worktree:setup: LFS objects are present"
  fi
fi

echo "worktree:setup: $root (WORKTREE_KEY=$key PORT=$port)"
