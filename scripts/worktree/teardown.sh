#!/usr/bin/env bash
# Release resources that live outside the worktree directory before the
# worktree is removed. Every frontend calls this through
# `mise run worktree:teardown`. Safe to run repeatedly and when setup never ran.
set -euo pipefail

root=$(git rev-parse --show-toplevel)
cd "$root"

key=""
if [ -f .worktree.env ]; then
  key=$(sed -n 's/^WORKTREE_KEY=//p' .worktree.env)
fi
: "${key:=$(git branch --show-current)}"

# Project-specific teardown goes below, keyed by "$key". Stop only processes
# that recorded this key; never kill a shared Unity editor or dev server
# belonging to another worktree.

rm -f "$root/.worktree.env"
echo "worktree:teardown: $root (WORKTREE_KEY=$key)"
