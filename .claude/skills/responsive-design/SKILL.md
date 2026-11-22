---
name: responsive-design
description: >
  Web / application UI を異なる viewport・container・zoom・input environment に適応させるときに使用する。
  device preset の暗記ではなく、available space と content stress points を観察し、fluid layout、content-driven breakpoint、
  container query、reflow、responsive media を使い分けて実装・検証する。
---

# Responsive Design

Responsive design を「mobile / tablet / desktop の3枚を作ること」と解釈しない。

**Start fluid → Find stress points → Transform only when needed → Verify continuously**

を基本にする。

目的は特定deviceへの最適化ではなく、available space・content量・zoom・input条件が変わっても、情報構造と操作可能性を保ったまま自然に変形する2D interfaceを作ることである。

## Workflow

1. current project の shell / layout / breakpoints / tokens / container primitives を確認する。
2. 狭い幅から始め、まず query を増やさず fluid に成立させる。
3. viewport を連続的に広げ、layout / type / media / controls が不自然になる **stress point** を探す。
4. stress point ごとに、何を `preserve / wrap / stack / resize / reposition / collapse / disclose / scroll` するか決める。
5. page / shell の環境変化には media query、reusable component の局所変化には container query を優先して検討する。
6. current project の既存 components / tokens / semantics を維持して実装する。
7. preset幅だけでなく中間幅・zoom・長いcontentを含めて rendered result を確認する。

## Observe

### Information priority

- 狭くなったときも残すべき primary content / action は何か
- secondary content は移動・折りたたみ・disclosureできるか
- visual order と DOM / reading order が一致しているか
- desktopの補助情報を、理由なくmobileで削除していないか
- navigation / toolbar / filters が別surfaceへ移る場合も到達可能か

狭幅化は「削除」ではなく、まず **reflow / reposition / disclosure** として考える。

### Macro layout

- page margin / max-width / content measure
- column count / gutter
- sidebar / rail / toolbar ownership
- fixed / fluid / hybrid regions
- repeated alignment key lines
- full-widthとconstrained contentの切り替え
- wide screenで情報量を増やすのか、読みやすさのためmax-widthを維持するのか

wide viewportを単純に引き伸ばさない。
editorialなsurfaceはmeasureを維持し、dense tool / dashboardは追加spaceを情報量へ使うことがある。

### Continuous vs discrete change

まず continuous な変化で解けないか確認する。

- flex / grid intrinsic sizing
- `minmax()` / `auto-fit` / `auto-fill`
- percentages / fractional tracks
- `min()` / `max()` / `clamp()`
- max-width / min-width constraints
- content wrapping
- fluid spacing / typography

それでも構造が破綻する地点だけ discrete breakpoint を追加する。

**breakpointはdevice名から決めず、contentが変形を要求する地点から決める。**

### Query scope

**Media query** は page / viewport / environment単位の変化に使う。

- global shell
- primary navigation
- viewport-wide column structure
- orientation
- pointer / hover capability
- user preferenceなどenvironmental media features

**Container query** は、同じcomponentが配置場所によって異なるavailable widthを持つ場合に使う。

- card
- widget
- toolbar group
- reusable panel
- embedded chart / summary block

componentが「viewport幅」ではなく「自分に割り当てられた幅」で変形すべきなら container query を優先する。

### Responsive transformations

各regionについて変形の種類を明示する。

- `preserve` — サイズ・位置関係を維持
- `resize` — 比率・measure・visual weightを調整
- `wrap` — 同じflowのまま折り返す
- `stack` — rowからcolumnへ変える
- `reposition` — 別regionへ移す
- `collapse` — compact representationへ変える
- `disclose` — dialog / drawer / disclosureへ移す
- `scroll` — semantic上2D layoutが必要なsurfaceだけ内部scrollを許可

### Typography and spacing

- body text の可読性を保つ
- line measure がwide viewportで長くなりすぎないか
- headingがnarrow viewportで過度にwrapしないか
- spacing hierarchyがsmall screenで過密 / wide screenで散漫にならないか
- fixed scaleをそのまま全幅へ適用すべきか
- user zoom / font scalingでcomponentが壊れないか

font sizeを縮めてoverflowを隠すより、layoutをreflowする。

### Media

- image / video がcontainerをoverflowしないか
- raster assetに不要な巨大sourceを配信していないか
- `srcset` / `sizes` が適切か
- art direction が必要なら `<picture>` 等を検討する
- crop変更で意味のあるcontentが失われないか
- intrinsic size / aspect ratioを保持しlayout shiftを防げるか

### Input and environment

screen widthからinput methodを推測しない。

- large screenでもtouchの場合がある
- small screenでもmouse / keyboardの場合がある
- hoverを唯一のaccess pathにしない
- orientation固定を前提にしない
- long localized text / zoom / browser chrome変化をstress caseとして扱う

### Overflow

horizontal overflowを見つけたら、accidental overflow か semantic overflow かを分ける。

accidental:

- fixed width
- unbreakable text
- rigid grid
- oversized media
- min-width misuse

semantic:

- data table
- map / diagram
- timeline
- editing canvas
- presentation-like surface

semantic上2D配置が必要な領域は内部scrollを許容できるが、scrollbarを隠して存在を不可視にしない。

## Decision rules

- narrow viewportをdefaultとして考えるが、「mobile専用design」を別系統で作ることを目的にしない。
- breakpointはdevice classではなくcontent stress pointから決める。
- queryを追加する前にintrinsic / fluid CSSで解けるか確認する。
- page-level adaptationとcomponent-level adaptationを分ける。
- reusable componentは可能ならcontainer-localに自律させる。
- narrow layoutで情報・機能を消す場合は、同等の到達手段があることを確認する。
- visual reorderingによってmeaning / keyboard / screen-reader orderを壊さない。
- responsive対応をfont縮小だけで解決しない。
- breakpoint間の中間幅をfirst-class stateとして扱う。
- fixed / fluid / hybrid のどれを使うかはuser goalで決める。

## References

### Core guidance

- [MDN — Responsive web design](https://developer.mozilla.org/en-US/docs/Learn_web_development/Core/CSS_layout/Responsive_Design)
  - Observe: fluid layout、media query、mobile-first、responsive media、typography、viewportの関係。
- [web.dev — Responsive web design basics](https://web.dev/articles/responsive-web-design-basics)
  - Observe: content-driven breakpoints、viewport overflow、input capability、small-firstでbreakpointを発見する過程。
- [MDN — CSS container queries](https://developer.mozilla.org/en-US/docs/Web/CSS/Guides/Containment/Container_queries)
  - Observe: viewport queryとcontainer queryの責務差、component-local adaptation、container units。
- [W3C — Media Queries Level 5](https://www.w3.org/TR/mediaqueries-5/)
  - Observe: widthだけでなくenvironment / user-agent featuresをqueryする仕組み。

### Accessibility / reflow

- [W3C WAI — WCAG 2.2 Quick Reference: 1.4.10 Reflow](https://www.w3.org/WAI/WCAG22/quickref/#reflow)
  - Observe: 320 CSS px相当で情報・機能を失わずreflowする基準と、2D layoutが必要な例外。
- [Digital Agency Design System — Layout accessibility](https://design.digital.go.jp/dads/foundations/layout/accessibility/)
  - Observe: liquid layout、column/gutterの可変設計、不可避なhorizontal scroll時の扱い、reading order。

### Production design systems

- [Digital Agency Design System — Layout](https://design.digital.go.jp/dads/foundations/layout/)
  - Observe: margin / column / gutter、1〜12-column構成、navigation領域、breakpointの考え方。
- [GOV.UK Design System — Layout](https://design-system.service.gov.uk/styles/layout/)
  - Observe: small-screen-first、single-columnからの拡張、content measure、grid transformation。
- [GOV.UK Design System — Spacing](https://design-system.service.gov.uk/styles/spacing/)
  - Observe: small/largeでspacing scaleをどう変えるか、何をstaticに残すか。
- [Carbon Design System — 2x Grid](https://carbondesignsystem.com/elements/2x-grid/overview/)
  - Observe: fluid / fixed / hybrid behavior、screen regions、key lines、dense interfaceとeditorial surfaceの違い。

### Responsive media

- [MDN — Responsive images](https://developer.mozilla.org/en-US/docs/Web/HTML/Guides/Responsive_images)
  - Observe: resolution switching、`srcset` / `sizes`、art direction、display slotとsource selection。

## Avoid

- `375 / 768 / 1024 / 1440` のようなpresetを根拠なく増やさない。
- iPhone / iPad / desktop等のdevice名をbreakpointの理由にしない。
- desktop DOMとmobile DOMを安易に二重実装しない。
- narrow screenで重要情報を`display: none`して終わらせない。
- media queryだけをresponsive designだと考えない。
- component内部の都合をすべてviewport breakpointへ流出させない。
- wide screenで全てをstretchしない。
- fixed heightでdynamic textを閉じ込めない。
- horizontal scrollbarを機械的に隠さない。
- screenshot preset数枚だけで検証を終わらせない。

## Verify

1. browserを最小幅からwideまで**連続的に**resizeし、breakpoint直前・直後と中間幅を見る。
2. horizontal page overflowがないことを確認する。例外surfaceはoverflow ownershipを明確にする。
3. 320 CSS px相当 / 400% zoomで、情報と主要機能が失われないか確認する。
4. long title / long label / localization / dynamic dataを入れてreflowを見る。
5. page shellだけでなく、狭いsidebar等に置かれたreusable componentも確認する。
6. keyboard / reading orderとvisual orderが矛盾していないか確認する。
7. responsive imageが実表示sizeに対して不必要に巨大でないか確認する。
8. current projectが持つrepresentative desktop / mobile viewportsでもregressionがないことを確認する。

`build succeeded` や「mobile screenshotが1枚通った」を完了条件にしない。

last-reviewed: 2026-09-10
