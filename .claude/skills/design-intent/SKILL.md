---
name: design-intent
description: >
  Use when a visual direction is genuinely unresolved or must be maintained across a project.
  Resolves project-specific expressive choices using context and observed references; writes or
  updates DESIGN-BRIEF.md only when durable handoff is needed, after the underlying purpose,
  content and experience have been established.
---

# Design Intent

このSkillは**視覚的な方向性の決定・共有**を担う。作品や画面の目的、必要な情報と体験の構造そのものは `design-discipline` の判断を優先する。単独インストール時も、まずそれらが確認できているかをcurrent projectとrequestから読む。

**Read existing evidence → Identify unresolved visual decisions → Inspect references → Resolve conflicts → Persist only if needed** の順で進める。briefの文書化は作業の目的ではない。

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

必要なものだけ、evidenceから決める。明示されたuser・purpose・use caseを勝手に再設定しない。

- visual direction が適用される surfaceと、既存のpurpose / audience（再確認用でありここで創作しない）
- その成果物で成立させたい experience / expressionと既存のconcept decision
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

## 6. Persist a brief only when needed

複数の画面・媒体・agent・制作回にまたがるvisual decisionを保持する必要がある場合、project rootに `DESIGN-BRIEF.md` を作成または更新する。単発・小規模なvisual workなら作成を強制せず、必要な判断を当該artifactの作業内で使う。既存のbriefがある場合は必ずそれを読む。

永続化するときの形式例:

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

`design-discipline`から渡されるpurpose・必要content・体験構造とこのSkillのvisual decisionを混同しない。表現上の好みが構造を不必要に変える場合は、なぜその構造変更が必要か検証する。

後続 Skill は `DESIGN-BRIEF.md` を preference ではなく project evidence の一つとして読む。

優先順位:

1. explicit current user requirement
2. current product requirements / established design system
3. confirmed `DESIGN-BRIEF.md`
4. external references
5. generic convention

brief が実装と矛盾した場合、古い brief を盲目的に優先しない。