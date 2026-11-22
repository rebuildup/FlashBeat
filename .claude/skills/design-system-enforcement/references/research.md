# Research: design-system enforcement

Last reviewed: 2026-11-19
Status: trial-pending / method research. 本 repo の実プロジェクトでの品質改善は未検証。

## Research question

既存 Web / application UI で、機能追加を繰り返しても component treatment と token semantics が分岐しないようにするには、**何を規約として強制でき、何を観察と判断に残すべきか**。

## First-party evidence

### T3 Code — explicit ownership and layered enforcement

- [T3 Code AGENTS — Taste](https://github.com/pingdotgg/t3code/blob/main/AGENTS.md#taste): `components/ui` が自分の見た目を所有し、consumer から `className` で見た目を上書きせず、必要なら variant / size へ追加するという契約。lint `shadcn/no-restyle` による機械検査も明記
- [T3 Code Web UI policy](https://github.com/pingdotgg/t3code/blob/main/docs/internals/web-ui.md): primitive 優先、owner component の Tailwind、限定された global CSS、variant / slot ベースの合成。例外として semantic rows、resize handles、editor surface 等を明示
- [T3 Code Button implementation](https://github.com/pingdotgg/t3code/blob/main/apps/web/src/components/ui/button.tsx): `cva` の variant / size が geometry・focus・press・touch target・theme を統合。個々の consumer の ad hoc styling を減らす
- [T3 Code UI consistency agent](https://github.com/pingdotgg/t3code/blob/main/.macroscope/check-run-agents/ui-consistency.md): changed lines のみを対象とする 3 点レビュー（primitive、スタイル所有者、composition）。不明瞭なら finding 無しとする。CI実行や美観採点を要求しない
- [T3 Code UI polish PR #24776](https://github.com/pingdotgg/t3code/pull/24776): fonts / radius / icons / hierarchy / contrast を tokens と primitives 中心に修正。局所 restyle より shared owner で変える価値の production example

**観察:** design system に準拠することを単なる善意・promptの約束から、変更された行の修正位置と lint / reviewer の契約に移している。既存 style の universal correctness の証拠ではない。

### shadcn lint — mechanizable contract and limits

- [shadcn lint — design systems](https://github.com/shadcn-ui/lint/blob/main/docs/design-systems.md): component ownership、token / class policy の設定方式と scoped exceptions
- [shadcn lint — adoption](https://github.com/shadcn-ui/lint/blob/main/docs/adoption.md): new code では error、legacy は warn とする段階的導入
- [shadcn lint — rules](https://github.com/shadcn-ui/lint/blob/main/docs/rules.md): direct restyle、token / class contract 等の対象
- [Issue #5](https://github.com/shadcn-ui/lint/issues/5): descendant selector を経由する間接的な restyle は `no-restyle` が追跡できない例。**green lint ≠ complete coverage**。実際に使用する version で再評価する

**観察:** rule は所有境界を静的に指摘できるが、全経路の上書きや視覚品質は保証しない。回避策の過剰禁止は正当な feature-specific UI を壊す。

### Other system — vocabulary, not blanket restriction

- [Adobe Spectrum design tokens](https://spectrum.adobe.com/foundations/design-data/design-tokens): tokens と components / implementation guidance の組み合わせ。theme / semantic role を shared language にする具体例
- [Design Tokens Community Group 2025.10](https://www.designtokens.org/TR/2025.10/): format の interoperability についての参考。**規格への移行は既存 project の必須条件ではない**
- [GOV.UK Design System components](https://design-system.service.gov.uk/components/): component API と用途の記述を持つ、別技術系の production design system

## Comparison and decisions

| Concern | T3 Code | shadcn lint | Adobe Spectrum / GOV.UK | This Skill |
| --- | --- | --- | --- | --- |
| Visual ownership | primitive + variant + slot | syntactic restyle detection | component and token guidance | 明示済み contract を先に探し、owner に修正 |
| Enforcement | lint + narrow PR agent review | configurable static checks | reference / design-system implementation | deterministic かつ scoped なものだけ gate |
| Exceptions | editor, geometry, integration | allow / project configuration | domain-specific variants | documented exception を保存 |
| What cannot be proved | expressive quality, user comprehension | indirect overrides, rendered effect | arbitrary project intent | visual review / interaction testing に分離 |

## Failure modes to test, not prescribe away

1. **False negative:** lint は通るが child selector や wrapper を介した override が UI を壊す
2. **False positive:** intentional brand hero、feature-specific geometry、native UI、generated markup を uniform component rule が壊す
3. **Rule without owner:** CSS token 一覧だけ存在しても consumer が適切な API を知らず duplicate UI を作る
4. **Over-abstraction:** variant を無制限に増やし component 自体が不理解になる
5. **Loss of character:** style blacklist として誤用し、product の表現まで均す
6. **Heavy gate everywhere:** every PR に full visual E2E を強制し、開発速度を損なう

## Trial protocol

実プロジェクトの **同一 component を変更する held-out tasks** で、baseline と candidate を比較する。shared component 拡張、既存 style の軽微修正、intentional exception を含む負例を使う。修正前後の違反の位置・実害、lint precision、例外の妥当性、修正工数、visual / functional regression を記録する。static warning 減少だけでは採用しない。

既存の [skill-authoring evaluation protocol](https://github.com/rebuildup/design-skills/blob/main/skills/skill-authoring/references/evaluation-protocol.md) は repository 開発時の参考であり、この Skill 自体は単独 install で動作する。
