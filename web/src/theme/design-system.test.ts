import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

/**
 * Design-system governance test (issue #802).
 *
 * The studio's visual foundation is tokens, not ad-hoc values: semantic
 * colors (`fg.*`, `border`, `canvas`, `cta.*` — `tokens/semantic-colors.ts`),
 * the typography scale (`text-styles.ts`), Park UI primitives and the shared
 * list atoms (`src/app/list.tsx`). This test keeps the governed surfaces
 * honest by rejecting the two shortcuts that bypass the tokens:
 *
 * 1. **Raw hex colors** — `#RRGGBB` / `#RRGGBBAA` literals. A color that is
 *    not a token cannot follow the light/dark semantic mapping and drifts
 *    from the Pen v5 → Panda mapping (`docs/design/web-token-mapping.md`).
 * 2. **Raw font sizes** — `fontSize` values carrying their own unit
 *    (`"13px"`, `"0.9rem"`, `13`) instead of a scale step (`"sm"`) or a
 *    semantic `textStyle`. Off-scale sizes break the typography hierarchy.
 *
 * Governed areas: the app shell and shared surfaces (`src/app`, `pages`),
 * the shell-level components (`src/components/welcome`,
 * `src/components/brand`) and the feature screens migrated to the tokens
 * in this issue (`new-song`, `song-publisher`). `chart-editor` stays out:
 * the Konva canvas resolves colors at paint time and cannot consume
 * Panda tokens. `src/components/ui` is generated Park UI output.
 *
 * Comments are stripped before scanning so issue references (`#802`) and
 * documentation hex values do not count as violations.
 */

const WEB_ROOT = fileURLToPath(new URL("../..", import.meta.url));

const GOVERNED_DIRS = [
  "src/app",
  "src/components/brand",
  "src/components/welcome",
  "src/components/new-song",
  "src/components/song-publisher",
  "pages",
];

/** `#RRGGBB` or `#RRGGBBAA`. Three-digit issue refs (`#802`) never match. */
const HEX_COLOR = /#[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?\b/;

/** `fontSize:` followed by a number or a string with its own unit. */
const RAW_FONT_SIZE =
  /fontSize\s*:\s*(?:"[^"]*(?:px|rem|em)[^"]*"|'[^']*(?:px|rem|em)[^']*'|`[^`]*(?:px|rem|em)[^`]*`|\d)/;

function governedFiles(dir: string): string[] {
  const root = join(WEB_ROOT, dir);
  const out: string[] = [];
  const walk = (current: string) => {
    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const full = join(current, entry.name);
      if (entry.isDirectory()) {
        walk(full);
      } else if (/\.(ts|tsx)$/.test(entry.name) && !entry.name.includes(".test.")) {
        out.push(full);
      }
    }
  };
  walk(root);
  return out;
}

/**
 * Strip comments so documentation (`// #802`, block-comment hex values)
 * is not scanned. `//` inside a string (URLs) is preserved by requiring
 * the `//` to be preceded by whitespace, a quote boundary or line start —
 * `https://` has `:` before the slashes and survives.
 */
function stripComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[\s"'`{}();,])\/\/[^\n]*/g, "$1");
}

describe("design system governance (issue #802)", () => {
  const files = GOVERNED_DIRS.flatMap(governedFiles);

  it("scans the governed surfaces", () => {
    // Guard against the glob silently matching nothing after a move.
    expect(files.length).toBeGreaterThan(10);
    expect(files.some((f) => f.endsWith(join("src", "app", "catalog-panel.tsx")))).toBe(true);
  });

  it("has no raw hex colors", () => {
    const violations = files.flatMap((file) => {
      const lines = stripComments(readFileSync(file, "utf8")).split("\n");
      return lines.flatMap((line, index) =>
        HEX_COLOR.test(line)
          ? [`${relative(WEB_ROOT, file)}:${index + 1}: ${line.trim().slice(0, 80)}`]
          : [],
      );
    });
    expect(violations, violations.join("\n")).toEqual([]);
  });

  it("has no raw font sizes", () => {
    const violations = files.flatMap((file) => {
      const lines = stripComments(readFileSync(file, "utf8")).split("\n");
      return lines.flatMap((line, index) =>
        RAW_FONT_SIZE.test(line)
          ? [`${relative(WEB_ROOT, file)}:${index + 1}: ${line.trim().slice(0, 80)}`]
          : [],
      );
    });
    expect(violations, violations.join("\n")).toEqual([]);
  });
});

describe("studio grid contract (issue #802)", () => {
  /**
   * The rail track literal must live exactly once — inside the shared
   * `shellColumns` of `shell-metrics.ts`. Shells compose that class; they
   * must not restate (or interpolate) the tracks themselves, because Panda
   * resolves `css()` values statically and a template literal would drop
   * the property silently.
   */
  const RAIL_TRACK = "13rem minmax(0, 1fr)";
  const SHELLS = ["src/app/app-shell.tsx", "src/app/studio-layout.tsx"];

  it("defines the rail track once, in shell-metrics", () => {
    const metrics = readFileSync(join(WEB_ROOT, "src", "app", "shell-metrics.ts"), "utf8");
    expect(metrics).toContain(RAIL_TRACK);
    expect(metrics).toContain("shellColumns");
    expect(metrics).toContain("contentArea");
  });

  it.each(SHELLS)("%s composes the shared shell grid", (file) => {
    const code = stripComments(readFileSync(join(WEB_ROOT, file), "utf8"));
    expect(code, `${file} must use shellColumns`).toContain("shellColumns");
    expect(code, `${file} must use contentArea`).toContain("contentArea");
    expect(code, `${file} must not restate the rail track`).not.toContain(RAIL_TRACK);
    expect(code, `${file} must not interpolate the rail width`).not.toContain(
      "SHELL_RAIL_WIDTH} minmax",
    );
  });
});
