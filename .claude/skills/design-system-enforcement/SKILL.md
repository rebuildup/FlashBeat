---
name: design-system-enforcement
description: >
  Use when implementing or reviewing an established Web/application UI and preventing
  design-system drift through component ownership, tokens, static rules, and focused
  PR checks. Separates enforceable contracts from contextual visual judgment.
---

# Design System Enforcement

既存の UI が変更されるたびに新しい見た目の流儀が生まれる問題を、**正本となる component / token contract と実行可能な検査**で抑制する。目標は一様な UI でも lint warning 0 でもなく、意図した design language を壊さずに機能追加すること。

この Skill は既存 Web / application UI の実装・変更・レビューに使う。新規 brand direction、content / information architecture、視覚の魅力そのものは決定しない。正本にない規則を T3 Code や shadcn/ui の好みとして導入しない。

## 1. Inspect the actual contract

コード変更前に current project の authoritative source を特定する。

- 明示された user requirement、現行 Spec / ADR / design brief、既存 component library、theme / token source、CSS architecture、lint / CI、既存の例外
- 同じ役割を果たす複数 screen / state / component の実装。単一例や legacy drift を正本と誤認しない
- component API（variant、size、slot、state、theme）と consumer が所有する layout（width、flex、placement）を分ける
- product 固有の例外（hero、brand expression、custom editor、native controls、platform integration）を把握する

未確定の design direction を恣意的な lint rule に変換しない。契約の変更が必要なら component / token の正本と決定理由を先に更新する。

## 2. Classify constraints before enforcing

| 分類 | 例 | 検証・処遇 |
| --- | --- | --- |
| **Enforceable contract** | 登録済み primitive を consumer が無断で restyle、意味のある token の迂回、許可されない raw control 再実装、同一 contract の複製 | 静的検査 / focused review。明確な違反のみ error |
| **Project-specific contract** | approved spacing / radius scale、state treatment、layer order、feature 固有の slot / variant 使用 | project の正本と適用範囲がある場合だけ check |
| **Contextual judgment** | 余白が美しい、重要性が伝わる、個性的、アニメーションが適切、card 数、gradient の有無 | レンダリングと設計意図で判断。数値 lint で美観を断定しない |
| **Authorized exception** | feature 固有 geometry、brand asset、illustration、OS workaround、特殊 editor surface | 用途・範囲・理由を記録。無条件禁止しない |

**静的ルールは検出できる性質だけを強制する。** 「code に直書き値がある」だけでは違反ではない。同じ値でも semantic role が異なり得る。

## Responsibility boundary: decide before enforcing

この Skill は **定義済みの contract を検査する**。どれを primitive にするか、`variant` と feature-local のどちらが適切かの設計そのものは `ui-primitive-architecture` 相当の検討を先に行う。新しい screen の UI 方向が決まらない段階で、他製品の primitive 一覧や暫定配置を lint で永久固定しない。

shared component の contract に重大な product intent / brand の衝突が生じる場合、人間へ戻す前に既存 decision を読む。新たな承認や例外が必要なときの timing は `design-review-routing` 相当の判断に委ね、routine な variant choice の承認待ちを作らない。

## 3. Place the rule at its owner

- **Tokens:** 色・space・type・radius・motion 等の意味論を canonical token source に置く。設計判断の昇格基準は repeated relationship / theme role であり、全値の token 化ではない
- **Primitives:** 視覚的・操作的 treatment は component 内に保持する。必要な variation は variant / size / slot / typed prop へ追加する
- **Consumers:** 配置・文脈・content は consumer が担当する。consumer から primitive の内部装飾、focus、touch target、状態色を無断上書きしない
- **Composition:** 再利用すべき row / notice / banner は markup と class のコピーではなく shared composition にする。局所固有の表現は feature component にとどめる
- **Runtime styling:** runtime 値、generated markup、shadow DOM、animation、Electron / native integration は適切な例外として扱う

「新規 primitive を増やす」よりも既存 API で表現できるかを先に見る。variant が肥大化するなら本当に同一 component の責務か見直す。

## 4. Implement checks at proportional cost

1. 既存 linter / compiler / tests と tool availability を確認。adapter は repository に合わせて選択し、外部ツールを暗黙に導入しない
2. 最初は **changed files / affected components** の範囲で、契約を破る具体的な行と正しい修正場所を検出
3. 同種違反が繰り返され、false positive を評価できた場合だけ deterministic rule / custom lint / test に昇格する
4. 既存 codebase に drift がある場合は new / changed code の error と legacy の warning / scoped backlog を分ける。全体機械置換をしない
5. 例外は必要な file / line / scenario に限定し、理由と owner を残す。global disable や制限回避の selector / inline style を作らない

React + Tailwind の場合は [shadcn lint](https://github.com/shadcn-ui/lint) の `no-restyle` 等が候補だが、**import / wrapper 等の解析範囲と見落としを確認してから採用**する。native / non-React ではそのまま適用せず同じ責務を別の検証手段へ翻訳する。

## 5. Review the changed surface

PR review で変更箇所に次を確認する。

- existing primitive / token / variant に同じ役割がないか
- 変更元が consumer、primitive、token のどこに属すべきか
- focus、hover、disabled、selected、error、light / dark、responsive の state treatment が消えていないか
- intentional exception と accidental drift が区別されているか
- lint 違反を回避するための opaque wrapper / descendant selector / global CSS が増えていないか

finding は **location → observable rule / contract → impact → owning layer → minimal remediation** で記録する。diff から支持できない style preference を violation として報告しない。視覚的な妥当性は独立した rendered review に渡す。

## 6. Verification and handoff

- 通常の ticket では変更範囲に応じた lint / typecheck / scoped contract test を優先する。各 ticket に全画面 screenshot / E2E / build を暗黙に加えない
- 広い theme / primitive 変更の視覚回帰、cross-mode、cross-surface の包括確認は release candidate の freeze 後など project の定義した検証時点に集約する。重大な早期 risk は最小範囲で早期確認する
- report には checked scope、violations、waivers、変更した contract、実行した検証、未検証の視覚面を区別して記録する
- compliance pass は **良い UI の証明ではない**。render / contrast / interaction / intent の評価を別に残す

## Trial / research

手法と適用範囲の比較は [Research: design-system enforcement](./references/research.md) を参照する。採用前後の実プロジェクトで、契約逸脱の再発率、誤検出、必要な例外、変更時間、表現の損失を比較する。現段階は **trial-pending** であり、適合性の改善を一般化しない。
