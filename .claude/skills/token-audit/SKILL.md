---
name: token-audit
description: >
  Existing UI の hardcoded design values、token drift、semantic mismatch を監査し、
  current project に合う token system へ整理・移行するときに使用する。
  primitive → semantic → component の層を分離し、visual regression を browser で確認する。
---

# Token Audit

目的は token 数を増やすことではなく、**repeated design decisions を安定した vocabulary にすること**である。

この Skill は `dawitlabs/ui-skills` の `tokens` skill から、hardcoded-value audit、three-layer token architecture、visual verification の考え方を取り入れている。

Upstream: https://github.com/dawitlabs/ui-skills/tree/master/skills/tokens
License: MIT

## 1. Existing system first

最初に current project の token home を特定する。

確認対象:

- CSS custom properties
- Tailwind theme / CSS theme variables
- Style Dictionary / DTCG JSON
- CSS-in-JS theme objects
- platform-native theme resources
- component library tokens

既存 system がある場合は、新しい naming scheme を先に持ち込まない。

## 2. Inventory values and relationships

以下を監査する。

- color
- spacing
- typography
- radius
- border
- shadow / elevation
- z-index / layer
- motion duration / easing
- layout dimensions that are genuinely system-level

単純な unique value count だけでなく、**同じ意味なのに異なる値**と**同じ値なのに異なる意味**を区別する。

例:

- `#ef4444` が error と destructive action の両方に使われる → semantic split を検討
- `16px` が page gutter / card padding / icon size に現れる → 同値でも semantic token を共有するとは限らない

## 3. Classify candidate tokens

### Primitive

raw scale / palette。

```text
color.blue.500
space.4
radius.2
font.size.3
```

### Semantic

UI 上の意味。

```text
color.text.primary
color.surface.muted
color.border.subtle
color.action.primary
color.status.danger
space.page.gutter
```

### Component

component 固有で、global semantic token だけでは責務を表せない場合。

```text
button.primary.background
sidebar.width
popover.shadow
```

component token は必要なときだけ作る。global vocabulary を component implementation detail で汚染しない。

## 4. Promotion rules

値を token に昇格する evidence:

- multiple places で同じ role として反復する
- theme / mode で一括変換する必要がある
- brand / semantic meaning を持つ
- component contract として安定している
- arbitrary value drift を抑える価値がある

昇格しないもの:

- one-off illustration geometry
- content-dependent width / height
- feature-specific positioning
- mathematically derived values
- browser / platform workaround

「hardcoded value = bad」と決めつけない。

## 5. Normalize without flattening hierarchy

near-identical values を無条件に一つへ丸めない。

まず relationship を確認する。

悪い統合:

> 14px, 15px, 16px が近いので全部 16px。

良い判断:

> metadata / body / control label が別 role なら typography hierarchy を維持したまま scale を整理する。

spacing も同様に、pixel distance より rhythm / nesting / density を見る。

## 6. Color semantics

最低限以下を分離する。

- text hierarchy
- surface hierarchy
- border hierarchy
- interactive accent
- selected / focus
- success / warning / danger / info
- data visualization when applicable

light / dark は単純な hex inversion にしない。

各 mode で:

- perceived hierarchy
- contrast
- elevation cues
- accent prominence

が保たれるか確認する。

## 7. Migration

一度に全値を機械置換しない。

推奨順:

1. canonical token home を確定
2. duplicate / conflicting token を整理
3. highest-frequency semantic roles を移行
4. core components
5. page / feature usage
6. rare exceptions

各段階で build と rendered UI を確認する。

## 8. Verification gate

完了条件は grep で hardcoded value が 0 件になることではない。

最低限確認:

- main screens の visual regression
- light / dark mode if supported
- hover / focus / selected / disabled / error states
- component variants
- typography hierarchy
- spacing density
- border / elevation hierarchy
- contrast

migration 前後で screenshot を比較できるなら比較する。

## 9. Report

必要なら以下だけ簡潔に報告する。

- detected token architecture
- major drift / semantic conflicts
- tokens added / renamed / removed
- intentional hardcoded exceptions
- visual verification performed

大量の raw value inventory は要求されない限り出力しない。