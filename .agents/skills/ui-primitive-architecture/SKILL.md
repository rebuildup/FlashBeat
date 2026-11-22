---
name: ui-primitive-architecture
description: >
  Use when establishing, extending, or consolidating a Web/application design system.
  Decides which controls become shared primitives, variants, composable patterns, or
  feature-local components before enforcement, without flattening product identity.
---

# UI Primitive Architecture

コンポーネントを増やすことではなく、**見た目・振る舞い・状態の決定権を適切な場所に集約すること**が目的。T3 Code の `components/ui` / variant / feature composition の責務境界を参考にするが、そのコンポーネント一覧やピクセル値は移植しない。

既存 UI の設計システム整備、新しい UI surface の基盤決定、頻発する重複の整理に使う。すべての画面を同じ見た目へ標準化するためには使わない。

## 1. Establish the intended UI language

既存 user requirements、Spec / ADR、brand / design brief、既存画面、reference と利用状況を読む。実物を観察して最初に確認する。

- 中核となる task / screen / interaction、情報密度、platform / input mode
- typography・spacing・color・radius・icon・motion の繰り返される意味と、正当な例外
- 既存の theme / token と primitive、feature / layout component、version / migration
- active / hover / focus / disabled / loading / error / selected / long content / narrow viewport の状態差
- 既存 stack の headless / accessibility primitives、選択の制約と API

初期から一式の primitive catalog を架空で設計しない。**代表的な実画面とユースケース**から必要な最小の vocabulary を確定し、別の画面で成立するか検証する。brand の重要方向性が未確定なら、安易な UI library default を承認済みデザインとみなさない。

## 2. Decide the ownership layer

次の区別を、candidate ごとに適用する。

| Layer | 所有するもの | 判定の手掛かり |
| --- | --- | --- |
| **Token** | semantic color / typography / spacing / radius / elevation / motion / theme relationship | 複数場所に意味が再出現、theme を横断する |
| **Behavior primitive** | button / input / menu / dialog 等の role、interaction、keyboard、focus、state | 他 feature でも同じ user action contract が成立する |
| **Styled UI primitive** | approved treatment と size / variant / states のまとまり | 一貫した操作系の外観を再利用する |
| **Composition / pattern** | banner / toolbar / field group / notice 等の slots・順序・組み立て | 複数部品間の関係が反復する |
| **Feature component** | feature 固有の content、workflow、特殊 editor / graph / illustration | その product context を外すと意味や geometry が変わる |
| **Layout owner** | parent の width / flex / positioning / grid / scroll | 子 primitive 内では決められない配置上の関係 |

数の固定閾値では判定しない。1回しか出ていなくても標準の意味論を持つ Button は primitive を利用し、何度出ても product 固有の図解は generic primitive に昇格させない。

## 3. Choose the smallest correct extension

既存 UI から candidate を発見し、次の順序で**判断**する。

1. 既存 primitive の API で成立する → **reuse**。layout の変更は parent へ
2. 同じ role / behavior の異なる見た目や密度 → owner の **variant / size** を追加し、all states で整合を確認
3. 同じ UI family で部品の組み合わせが変わる → **slot / composable pattern**。差分を props の爆発で隠さない
4. 意味・振る舞い・幾何が feature 固有 → **feature-local component** として維持
5. 新しい再利用可能な role / contract が必要で、既存 API が意味を誤らせる → **new primitive**。理由と共有契約を定義

例: T3 Code は `ComposerBanner` の小さな slot 群と、それらの順序・dismiss を所有する `ComposerBannerStack` を分ける。一方 `Previous agents` の行を汎用 `components/ui` variant にしない判断が記録されている。**境界は reuse 回数ではなく、意味と所有者**で決める。

## 4. Specify an observable contract

primitive を追加・昇格するときは code / types / examples に、最低限必要な範囲だけ明示する。

- semantic role、使用場面 / 使用しない場面、behavior、keyboard / pointer / touch、focus management
- `variant` = 意味 / treatment、`size` = density / geometry、`slot` = 子の構造。不要な props は増やさない
- theme token / icon / disabled / loading / error / selected / focus と responsive / platform conditions
- style ownership、consumer に許す layout props / class と、禁止する internal treatment override
- feature-local exception の理由、改修先、将来の昇格の観察条件

headless library（T3 Code は Base UI）を使う場合も、それは behavior の土台であって **完成した product design** ではない。既存採用技術と accessibility を踏まえて選ぶ。

## 5. Implement a narrow reference surface first

まず最も代表的な UI とその adjacent states で、tokens → primitive → composition → consumer の一方向の関係を検証する。必要なら story / example gallery / small fixture を用意するが、全 component に Storybook / catalog を強制しない。

確認する:

- basic states と invalid / pending / disabled / selected、keyboard、coarse pointer
- compact / normal density、long label、zoom、light / dark、responsive
- 複数 feature に再利用したときに consumer override が増えないか
- 表現上の例外が隠れていないか、component API が意味不明に肥大化していないか

変更範囲の型検査・局所テストを先に回す。全画面・実機・広範な視覚検証は project の release candidate gate に集約できるが、早期に代表画像を見ることは設計の誤方向を低コストで発見するために有用。

## 6. Decide, hand off, and avoid approval churn

agent が routine な component placement、標準 variant / density、技術選択を既存の正本と実物から決定する。重大な brand expression、navigation / information architecture、共通 vocabulary の意味を変える選択など、product に影響する**未確定の選択**だけ、人間に相違点が分かる最小の実物を提示する。承認済み方針を再質問しない。

出力は長い component inventory でなく、(1) canonical owners、(2) reusable API / examples、(3) exceptions、(4) validation、(5) 未確定の product-level decisions。実装後の逸脱防止は `design-system-enforcement` 相当、画像での品質判定は `visual-review` 相当の操作で行う。両者のインストールは必須としない。

## References and trial status

[Research: UI primitive decisions](./references/research.md) に T3 Code / Base UI / shadcn / Storybook の根拠と限界を記録。**trial-pending**：実装採用・誤抽象化・状態 coverage・変更コストを実プロジェクトで比較するまでは有効性を保証しない。
