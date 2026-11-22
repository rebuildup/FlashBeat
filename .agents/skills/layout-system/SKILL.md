---
name: layout-system
description: >
  Web UI の layout / grid / page structure を設計・実装・改善するときに使用する。
  Marketing / LP、Dashboard / Data、Application / Tool、Swiss / Editorial を分類し、
  実在する reference を browser で観察して structural rules を抽出し、
  current project に翻訳して実装・visual verification まで行う。
---

# Layout System

非自明な layout を記憶や generic UI convention だけから設計しない。

**Inspect → Extract → Translate → Implement → Verify** を順に実行する。

目的は reference product の clone ではない。
実運用で成立している layout の structural rules を観察し、current product の requirements と design language に翻訳することである。

## 1. Existing project first

外部 reference を見る前に、current project の既存 system を短く調査する。

確認対象:

- global / route layout
- design tokens
- spacing scale
- breakpoints
- container primitives
- sidebar / shell components
- typography
- established page patterns
- existing CSS variables / utilities

最初に見つけた一つの file を project convention と断定しない。
同じ責務を持つ複数箇所を確認する。

## 2. Classify the surface

最も近い family を選ぶ。

### Marketing / LP

対象:

- landing page
- product introduction
- feature page
- portfolio
- corporate website

読む:
`references/marketing.md`

### Dashboard / Data

対象:

- analytics
- monitoring
- observability
- metrics
- reporting
- admin dashboard

読む:
`references/dashboard.md`

### Application / Tool

対象:

- editor
- productivity app
- database tool
- calendar/task app
- CRM
- IDE-like UI
- management tool
- complex SaaS

読む:
`references/application.md`

### Swiss / Editorial / Typographic

対象:

- typography-driven composition
- editorial surface
- Swiss / International Typographic Style influence
- strong graphic structure

読む:
`references/swiss-editorial.md`

### Mixed surface

実際の product は複数 family を含めてよい。

例:

- SaaS homepage → Marketing
- signed-in workspace → Application
- analytics route → Dashboard
- editorial campaign page → Swiss / Editorial

route / surface ごとに分類する。
一つの global grid へ無理に統一しない。

## 3. Inspect real references before coding

non-trivial layout work では relevant な production references を原則 2 つ以上確認する。

evidence priority:

1. rendered production interface
2. responsive behavior / computed styles / DOM geometry
3. public source implementation
4. official design-system documentation

Browser / DevTools / browser automation が利用できるなら実際に page を開く。

reference 名を知っているだけで「Linear風」「Supabase風」などと想像して実装してはいけない。

### Browser inspection

可能なら最低限:

- wide desktop
- normal laptop width
- narrow/mobile width

を確認する。

必要に応じて:

- element bounding boxes
- computed width / max-width
- grid/flex definitions
- gap / padding
- sticky / fixed
- overflow
- nested containers

を見る。

source が公開されている場合も、最初に visual structure を理解してから relevant implementation を探す。
repository 内で無差別に `grid` class を検索するところから始めない。

## 4. Extract structural rules

reference ごとに以下を観察する。

### Global geometry

- viewport relationship
- outer margins
- max-width
- columns
- gutters
- spacing rhythm
- breakpoints

### Alignment

unrelated な要素間で繰り返される:

- left edge
- right edge
- center
- baseline
- width

を探す。

isolated CSS value より repeated alignment を強い evidence とする。

### Hierarchy

重要度が何によって表現されているか:

- area
- width
- height
- whitespace
- typography
- position
- grouping
- density

### Nesting

どこで grid / coordinate system が切り替わるかを確認する。

例:

```text
Viewport
→ App Shell
→ Workspace
→ Page Grid
→ Feature Grid
→ Component Layout
```

### Exceptions

以下が grid を意図的に外れていないか確認する。

- full bleed visual
- oversized heading
- edge-to-edge table
- inspector
- floating graphic
- overlay
- special callout

例外を見つけても即座に不整合扱いしない。
なぜ coherent に見えるかを調べる。

### Responsive transformation

desktop の列を単純に `1fr` へ stack する前提を置かない。

確認:

- column removal
- sidebar collapse
- pane → overlay
- reordered controls
- gutter reduction
- full-width transition
- hidden secondary information
- scroll ownership changes

## 5. Family-specific concerns

### Marketing

特に見る:

- repeated alignment axes
- asymmetric spans
- text / visual relationships
- section rhythm
- breakout / full bleed
- hero と subsequent sections の grid continuity

grid は section template の反復ではなく shared coordinate system として扱う。

### Dashboard

最低限 3 layer を分離する。

1. app shell
2. dashboard canvas
3. widget internal layout

見る:

- tile size
- row height
- minimum useful chart size
- KPI grouping
- importance → area mapping
- chart / table ratio
- resize behavior
- empty space
- breakpoint layouts

12-column marketing grid と同一視しない。

### Application / Tool

必ず scroll ownership を決める。

major region ごとに:

- fixed
- sticky
- independently scrollable
- viewport-sized
- content-sized
- flexible

のどれかを確認する。

settings form、table、editor、kanban、graph workspace を同じ max-width に強制しない。

### Swiss / Editorial

Swiss design を:

- Helvetica
- monochrome
- visible grid
- equal columns

へ還元しない。

見る:

- recurring alignment axes
- baseline rhythm
- asymmetric composition
- metadata placement
- scale contrast
- whitespace
- repetition
- controlled break from grid

apparent asymmetry の下にある invisible rule を探す。

## 6. Translate, do not clone

reference の arbitrary value をそのまま移植しない。

悪い例:

> Reference sidebar is 256px, therefore use 256px.

良い例:

> Reference uses a stable navigation rail and gives the primary workspace remaining fluid width. Determine the corresponding stable width from current project density, labels, and existing tokens.

current product の:

- information architecture
- typography
- brand
- components
- interaction requirements
- accessibility requirements

を保持する。

## 7. Derive project tokens

繰り返し現れる relationship は semantic token / primitive に昇格させる。

例:

```css
--layout-page-margin
--layout-content-max
--layout-gutter
--layout-sidebar-width
--layout-inspector-width
--layout-section-gap
--layout-grid-columns
--layout-grid-gap
```

reference から大量の near-identical pixel values をコピーしない。
少数の relationships / spacing family に整理する。

ただし一度しか使わない feature-specific geometry を無理に global abstraction にしない。

## 8. Implement

project の existing primitives を優先する。

新規 primitive が必要な場合:

- current design system に属する責務か
- route-local で十分か
- repeated relationship か

を判断する。

layout problem を decoration で隠さない。

## 9. Visual verification gate

compile / lint / unit test success だけでは完了ではない。

rendered result を確認する。

最低限:

- alignment
- proportions
- density
- whitespace
- content width
- section rhythm
- visual hierarchy
- clipping
- overflow
- scroll behavior
- responsive transition

可能なら reference と同じ viewport 条件で比較する。

UI が disorganized に見える場合、最初に再確認する:

- unrelated widths が増えすぎていないか
- gutter が不統一ではないか
- alignment axes が弱くないか
- nested padding が重複していないか
- scroll ownership が間違っていないか
- card / tile sizing が arbitrary ではないか
- spacing values が多すぎないか
- primary / secondary hierarchy が面積に反映されているか

## 10. Autonomous decision policy

以下は project evidence と reference から agent が自律的に決める。

- routine column count
- ordinary gutter
- standard page margin
- normal breakpoint
- reversible pane proportion
- established container use

user に戻すのは product semantics が変わる design decision に限定する。

例:

- information priority 自体を変更する
- navigation model を変更する
- mobile で機能を隠す
- major interaction pattern を変更する

通常の geometry choice を「何pxがいいですか？」と user に投げない。

## 11. Report

reference research の長いレポートは要求されない限り不要。

必要なら簡潔に:

- selected layout family
- inspected references
- extracted structural rule
- intentional deviations

だけを示す。

最終成果は reference 名ではなく rendered quality で評価する。
