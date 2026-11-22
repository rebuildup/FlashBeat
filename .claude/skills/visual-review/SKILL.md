---
name: visual-review
description: >
  Use to review and refine a rendered Web/application UI using screenshots,
  comparable baselines, independent image critique, and bounded rerendering.
  Distinguishes objective visual regressions from context-dependent aesthetics.
---

# Visual Review

UI の source、lint、成功ログから「見た目が整った」と結論しない。**実際に表示された画面**の問題を、観察可能な evidence に結び付けてレビューし、必要な部分だけ直して再表示する。

対象は Web / application UI の screenshot-driven review。ブランドの方向性、コンテンツの正しさ、機能・keyboard・animation 全体の合格判定は代替しない。静止画で証明できることとできないことを常に区別する。

## 1. Decide what this review should catch

開始前に current project の user requirements、Spec / ADR、既存 design system、既知の successful UI と、今回触れる routes / components / states を確認する。次を分ける。

- **Regression:** baseline に存在しない clip、overflow、missing content、broken alignment、unexpected theme/state
- **Visual coherence:** typography hierarchy、spacing rhythm、visual attention、density、color / icon / surface の role consistency
- **Purpose and expression:** 何を強調すべきかが伝わるか、brand / content に合うか。baseline に忠実でも不十分なことがある
- **Non-visual:** keyboard、screen reader、state transitions、scroll、motion / performance、data integrity。画像だけで pass としない

変更範囲に応じて対象を絞り、すべての PR に全画面の撮影を要求しない。大きな visual change と release candidate の review は広くする。

## 2. Capture comparable evidence

表示結果を renderer / browser / target app から取得する。スクリーンショットを実際に取得できない場合は **未実施** と記録し、想像で評価しない。

- 対象の route / component / view と **state**（default、empty、loading、error、long content、focus / selected など影響するもの）を明示
- viewport、DPR、browser / OS、theme、locale、font loading、zoom、test data、scroll position、focus / hover の条件を記録
- comparable before/after は条件を揃える。全画面図と問題箇所の **原寸 crop** を必要に応じ併用し、縮小表示の錯覚を検証
- timers、randomness、network、animation などの非決定性は fixture / deterministic capture で安定化。mask はその領域を検証対象から外すため、理由・対象を明記し最小限にする
- privacy / credentials / third-party assets を含む生画面を public PR / 外部 AI reviewer に無断送信しない。必要なら匿名・ローカル・非公開に切替

pixel / perceptual diff は **変わった領域の検出**。意図した変更の良否や美観は自動決定できない。baseline 更新は意図が確認された場合だけ行う。

## 3. Review in two passes

### Pass A — first encounter (when independent review is useful)

必要なら実装 brief や変更理由を伏せ、reviewer に artifact と対象媒体だけ渡す。自由回答で「最初に目に入るもの」「何の画面か」「理解・操作上の違和感」を聞く。仕様の適合はまだ採点しない。

### Pass B — grounded critique

次に意図・制約・expected state・baseline を開示し、**実際の表示と期待の差**を見る。少なくとも必要な観点を選ぶ。

- content hierarchy と focus / reading order の視覚的手掛かり
- recurring edges、component spacing、density、scroll area、text crop
- font weight / line height、contrast、muted vs disabled、state differentiation
- component family、radius、icon stroke、border / surface hierarchy
- responsive / theme / localization / long text 等の崩れ
- product-specific visual character と、目的のない decoration

AI image reviewers は optional。可能なら実装担当とは別の reviewer と独立 critique を使う。複数モデルの意見や 10点評価を**合格の真値にしない**。不一致は見直す場所を示す signal。特定 pixel の指摘は原寸 crop、再撮影、DOM / computed styles などで裏取りする。

## 4. Turn comments into evidence-based findings

finding の最小形式:

```text
Surface / state / viewport:
Observation (what pixels show):
Expected relation / purpose (and source):
Impact: blocker | major | minor | note
Confidence / alternative explanation:
Proposed correction owner: content | structure | token/primitive | local UI | unknown
Evidence: before / after / crop / baseline links:
```

「もっと polished」「点数が低い」は単独では修正 Issue にしない。好み、表現の違い、renderer 差を defect と断定しない。問題がなければ **no material finding** と報告し、変更を捏造しない。

## Review routing and owner decisions

静的な design system 契約は `design-system-enforcement`、primitive の設計は `ui-primitive-architecture` 相当の役割に分ける。AI reviewer は「発見と仮説」までとし、owner が明示した brand / product choice を model の多数決や score で上書きしない。T3 Code の [redesign PR #24865](https://github.com/pingdotgg/t3code/pull/24865) も council result とは別に維持する owner calls を記録し、大きな変更は off-by-default Beta toggle とした。

- 細かな修正は self-check + narrow evidence を基本とし、毎回の human review gate にしない
- 構造・ブランド等の未確定かつ consequential な選択だけ、安い実物で早い時点に人間へ判断材料を出す
- 大規模で可逆的な変更は候補 / preview / feature flag を使い、必要な human acceptance を default-on / release 前へ集約する
- 実際の権限境界と受入要件は current project を優先。`design-review-routing` 相当の review selection を用い、未実施の受入を pass としない

## 5. Repair, rerender, stop

1. 大きな意味・導線・情報の欠陥があれば表層の polish で隠さず既存要件・設計判断へ返す
2. shared visual drift は token / primitive へ、局所欠陥は owning feature へ最小修正する
3. **同じ capture 条件**で affected screen と逆状態を再表示し、問題が直ったかと新しい回帰を確認する
4. 重大な差が収束すれば終了。根拠なしの endless rating hill climb は行わず、時間・cost・見込みで停止する
5. 画像で確認できない keyboard / interaction / accessibility / performance / motion は対象に応じた別の検証を指定し、未実施を pass としない

通常 ticket は変更領域だけ短くレビューし、フル screenshot matrix / wide browser coverage は project 方針の release candidate freeze 後に集約できる。重大な視覚リスクは影響範囲を限定して早期確認する。

## 6. Report

確認した artifact / commit、capture matrix、問題ごとの観察と影響、修正・意図的維持、before/after、各未実施検証を記録。reviewer 数や点数ではなく、**再現可能な指摘と、成果物で確かめた変化**を主要 evidence とする。

参考比較・制約は [Research: screenshot-driven review](./references/research.md) を参照。品質改善の実証は **trial-pending**。単体 install でもこの手順を利用できる。
