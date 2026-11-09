// Shared hash definition for vendored Agent Skills.
//
// A vendored Skill is a directory that contains `SKILL.md`. Its identity is the
// sha256 of the concatenation of, for every regular file under the directory
// (recursively, excluding `.git` and `node_modules`):
//
//   1. the file path relative to the Skill directory, using `/` separators
//   2. the raw bytes of the file
//
// The files are visited in ascending order of their lower-cased relative path
// (byte-wise comparison), so the result does not depend on filesystem order.
//
// The same definition is used when writing `skills-lock.json` and when
// verifying it, so the two can never drift apart.
import { createHash } from 'node:crypto';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

export function walkFiles(directory: string, prefix = ''): string[] {
  const files: string[] = [];
  for (const entry of readdirSync(join(directory, prefix), { withFileTypes: true })) {
    if (entry.name === '.git' || entry.name === 'node_modules') continue;
    const relative = prefix ? prefix + '/' + entry.name : entry.name;
    if (entry.isDirectory()) files.push(...walkFiles(directory, relative));
    else if (entry.isFile()) files.push(relative);
  }
  return files;
}

export function folderHash(directory: string): string {
  const files = walkFiles(directory).sort((a, b) => {
    const left = a.toLowerCase();
    const right = b.toLowerCase();
    return left < right ? -1 : left > right ? 1 : 0;
  });
  const sha = createHash('sha256');
  for (const relative of files) {
    sha.update(relative);
    sha.update(readFileSync(join(directory, relative)));
  }
  return sha.digest('hex');
}

export const HASH_ALGORITHM = {
  algorithm: 'sha256',
  scope: 'skill directory',
  definition:
    "sha256 over, for every regular file under the skill directory (recursive, excluding .git/ and node_modules/) in ascending order of the lower-cased relative path: the relative path (with '/' separators) followed by the raw file bytes.",
} as const;
