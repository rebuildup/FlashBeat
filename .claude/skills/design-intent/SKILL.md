---
name: design-intent
description: >
  新規 UI の visual direction が未確定なとき、既存 project context・実在 reference・user intent を整理し、
  後続の layout / color / typography / motion skill が共有できる DESIGN-BRIEF.md を作る。
  vague な「clean / modern / minimal」のまま実装を始めず、矛盾と未確定事項を解消するために使用する。
---

# Design Intent

実装前に visual direction を固定する。ただし interview 自体を目的にしない。

**Read existing evidence → Identify unresolved design decisions → Resolve contradictions → Inspect references → Write brief** の順で進める。

この Skill は `dawitlabs/ui-skills` の `design-grill` から、design brief・contradiction detection・reference specificity の考え方を取り入れている。

Upstream: https://github.com/dawitlabs/ui-skills/tree/master/skills/design-grill
License: MIT

## 1. Read existing context first

質問する前に current project を読む。

最低限確認する:

- README / product description
- existing design docs
- existing tokens / global CSS
- established components
- primary screens / routes
- screenshots / Figma references if available
- existing `DESIGN-BRIEF.md`

すでに code / design から明らかなことを user に再質問しない。

## 2. Resolve only decisions that materially affect design

以下を evidence から埋める。

- product category / primary surface
- target user and usage context
- defining screen or interaction
- desired emotional register
- information density
- visual references and what specifically matters in each
- explicitly rejected references / patterns
- color direction
- typography direction
- component character
- motion level
- light / dark mode requirements

routine な geometry や token 値を user に決めさせない。

user に戻すのは、複数の plausible direction があり、選択によって product semantics / brand perception が大きく変わる場合に限定する。

## 3. Ban vague direction words as final evidence

以下の語だけで decision を正当化しない:

- clean
- modern
- minimal
- professional
- premium
- sleek
- intuitive

これらが出たら observable characteristics に変換する。

例:

> "minimal" → low surface count, weak decoration, few accent colors, high whitespace, reduced border/shadow vocabulary

> "technical" → dense information, restrained radius, strong alignment, tabular numerics, low decorative motion

## 4. References must be specific

「Linear っぽく」「Apple っぽく」で止めない。

reference ごとに最低限:

- what surface / screen is relevant
- what behavior or visual relationship matters
- what should NOT be copied

を決める。

可能なら production interface を browser で観察する。

良い記述:

> Linear の issue view にある compact typography と pane hierarchy を参考にする。dark palette や brand expression 自体はコピーしない。

悪い記述:

> Linear-like UI.

## 5. Detect contradictions

brief を書く前に、direction 同士の矛盾を探す。

典型例:

- dense UI + generous whitespace everywhere
- minimal motion + highly expressive transition on routine actions
- serious / precise + oversized playful pill controls
- low visual noise + many competing accent colors
- mobile parity + hiding core functionality on mobile

矛盾が product surface ごとの使い分けで解決できるなら分離する。

例:

- marketing: expressive motion
- application: subtle functional motion

## 6. Write `DESIGN-BRIEF.md`

project root に以下の形式で書く。

既存の `DESIGN-BRIEF.md` がある場合は再生成して全置換しない。confirmed decisions と、その判断を支える evidence を保持したまま差分更新する。current user requirement、current product requirements、または established design system と矛盾する項目だけを更新し、変更した decision には理由と新しい evidence を記録する。

```markdown
# Design Brief — <product>

## Product context
- Category:
- Primary user:
- Usage context:
- Defining screen / interaction:

## Design intent
- Emotional register:
- Density:
- Visual character:

## References
- <reference>: <specific property to learn from>

## Avoid
- <reference / pattern>: <what and why>

## Color direction
- Default mode:
- Temperature / saturation:
- Accent strategy:
- Semantic constraints:

## Typography direction
- Role split:
- Density / scale:
- Numeric / metadata requirements:

## Component character
- Radius:
- Elevation:
- Border strategy:
- Control density:

## Motion
- Level:
- Where motion is useful:
- Where motion should be absent:

## Decisions made
- <decision>: <reason / evidence>

## Explicitly unresolved
- <only true product-level open questions>
```

## 7. Handoff

後続 Skill は `DESIGN-BRIEF.md` を preference ではなく project evidence の一つとして読む。

優先順位:

1. explicit current user requirement
2. current product requirements / established design system
3. confirmed `DESIGN-BRIEF.md`
4. external references
5. generic convention

brief が実装と矛盾した場合、古い brief を盲目的に優先しない。