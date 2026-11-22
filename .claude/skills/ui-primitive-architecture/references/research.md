# Research: UI primitive decisions and component boundaries

Last reviewed: 2026-11-19
Status: trial-pending. T3 Code の production source は観察済みだが、他 project への最良方式は未実証。

## Core observation

T3 Code は単に shadcn/ui default を並べるのでなく、**同じ役割を持つ UI の owner を一つに決める**。新しい feature ごとに visual treatment を再定義せず、repeatable contract と context-specific expression を分けている。

## T3 Code primary sources

- [Web UI policy](https://github.com/pingdotgg/t3code/blob/main/docs/internals/web-ui.md): `components/ui` / raw control の境界。shared primitive の role、owner-local Tailwind、slot composition、feature-specific exceptions
- [AGENTS.md — Taste](https://github.com/pingdotgg/t3code/blob/main/AGENTS.md#taste): shared appearance は `variant` / `size`、layout は parent、特定 feature の look は feature 側。明示規則から逸脱する際は owner sign-off
- [components.json](https://github.com/pingdotgg/t3code/blob/main/apps/web/components.json): shadcn schema、`base-mira`、baseColor zinc、CSS variables、Lucide、registry 選択が明示される。**shadcnの自動生成だけで全見た目が決まるわけではない**
- [Button](https://github.com/pingdotgg/t3code/blob/main/apps/web/src/components/ui/button.tsx): `class-variance-authority` で visual variants / sizes、touch target、focus、press、disabled を shared contract に含める
- [Popover](https://github.com/pingdotgg/t3code/blob/main/apps/web/src/components/ui/popover.tsx): Base UI の behavior を利用しつつ、popup variant / padding / width / touch backdrop を product 向けに包む
- [Sidebar](https://github.com/pingdotgg/t3code/blob/main/apps/web/src/components/ui/sidebar.tsx): shared shell でも width、responsive transformation、resize、interaction state を別に検討する
- [ComposerBanner](https://github.com/pingdotgg/t3code/blob/main/apps/web/src/components/chat/ComposerBanner.tsx) と [ComposerBannerStack](https://github.com/pingdotgg/t3code/blob/main/apps/web/src/components/chat/ComposerBannerStack.tsx): slots が見た目を所有、stack が順序・transition を所有。feature の責務分割の実例
- [UI redesign PR #24865](https://github.com/pingdotgg/t3code/pull/24865): "Previous agents" は panel row とし、`components/ui` variant に昇格しないと明記。layout と feature semantic を汎用化しすぎない判断

## Independent reference comparison

- [Base UI — About](https://base-ui.com/react/overview/about): accessible behavior の unstyled primitive。**headless behavior と product の見た目の決定を分ける**
- [Base UI — Composition](https://base-ui.com/react/handbook/composition): composable parts の組み立て方。T3 Code の slot 分離を理解する補助
- [shadcn/ui — components.json](https://ui.shadcn.com/docs/components-json): generator 設定と component ownership の起点。preset の選択は product 意図の代替ではない
- [shadcn/ui — Theming](https://ui.shadcn.com/docs/theming): CSS variables / theme roles を component instances の random CSS から分離
- [Storybook — What's a story?](https://storybook.js.org/docs/get-started/whats-a-story): state / variant を explicit fixture にする観察手段。Storybook 採用自体は必須ではない
- [Adobe Spectrum](https://spectrum.adobe.com/): token / components / patterns を product scale で統合した別の design system。機械的な T3 Code API のコピーを防ぐ比較対象

## Decide before creating a new primitive

| Question | Likely action | Anti-pattern |
| --- | --- | --- |
| 同じ role と interaction を別画面で使うか | existing primitive の reuse | raw `button` を再装飾 |
| behavior は同じで semantic treatment / density が変わるか | owner の variant / size | consumer が padding / focus / color を上書き |
| 複数 UI parts の arrangement が再利用されるか | slot / composition | duplicated banner markup |
| content / workflow / shape が feature に依存するか | feature-local | one-off inspector row を global primitive に移動 |
| common token への昇格価値があるか | token owner へ | 似た数値をすべて同じ semantic role に統一 |

## Trade-offs and trial

- Too few primitives → call-site drift / duplicated behavior
- Too many primitives → prop soup、巨大な variant matrix、generic house style への収束
- No state examples → default screenshot は良くても keyboard / loading / long text で破綻
- Forced universal library → native / expressive / feature-specific UI の意味を消す
- Purely source-level comparison → actual visual / interaction quality は不明

同じ UI requirement を baseline / candidate の二案で実装し、consumer overrides、repeated code、accessibility、state / mode consistency、後続変更の工数、意図的例外の保全を比較する。1 component が複数箇所で使われることだけでは品質向上の証拠にならない。
