# Research: screenshot-driven visual review

Last reviewed: 2026-11-19
Status: trial-pending / method research. Screenshot critique の効果を本 repo で検証したわけではない。

## Research question

AI agent が既存 UI を改修するとき、**コード上は正しくても表示が悪い**問題をどう発見・修正するか。visual regression と visual critique の責務、画像だけの限界、model score の不安定性を区別する。

## Source comparisons

### T3 Code — iterative human/AI critique

- [UI polish PR #24776](https://github.com/pingdotgg/t3code/pull/24776): font weights、radius scale、icon stroke、hierarchy、control treatment、contrast を shared token / primitive で改善。複数 UI state の before / after screenshot と 3 review rounds（Claude、Muse、Codex の自己記録）を掲載
- [T3 Code Web UI](https://github.com/pingdotgg/t3code/blob/main/docs/internals/web-ui.md): component owners、theme、composition の実装規約。画像の指摘をどこで直すかの手掛かり
- [Agents Sucked At UI (Until Today)](https://www.youtube.com/watch?v=xq0eB8vxeqk): 製作者による multi-model screenshot feedback の実演。**9/10** は実験内の rubric であり普遍的な品質閾値ではない

PRには downscaled screenshot による composer placeholder の誤判定を原寸 crop で訂正した経緯もある。screen context / crop を見ずに「画像に欠陥がある」と断定すると false positive になる。

**観察:** 独立 reviewer の複数視点は見落としの発見に役立ち得るが、同じ artifact の主観 score は prompt / context / reviewer に依存する。反復数そのものは品質の evidence ではない。

### Playwright — controlled, repeatable visual regression

- [Visual comparisons](https://playwright.dev/docs/test-snapshots): `toHaveScreenshot()` は安定した screenshot を取得して baseline と比較。OS / browser / font / renderer 差に注意
- [Page assertions](https://playwright.dev/docs/api/class-pageassertions): animations、caret、mask、style、scale、threshold 等の撮影オプション。mask は「見えないようにした領域の誤差がない」証明ではない

**観察:** regression test は fixed environment の意図しない表示変化を拾う。baseline がもともと悪い場合、画像が同一でも良いデザインにはならない。

### Storybook / Chromatic — state coverage and approval

- [Storybook visual testing](https://storybook.js.org/docs/writing-tests/visual-testing): isolated stories を visual tests に変換でき、component states の網羅に向く
- [Chromatic snapshots](https://www.chromatic.com/docs/snapshots/): animation 中の capture によるノイズ、interaction完了後 capture 等の注意
- [Chromatic interaction tests](https://www.chromatic.com/docs/interactions/): interaction test の後で screenshot を撮る。static image と actual state への到達の区別

**観察:** component variant / state library があるなら screenshot matrix を人手で全列挙するより story-based capture が合理的。complex task flow 全体の成立は別検証。

### Percy — baseline-driven change review

- [Percy visual testing basics](https://www.browserstack.com/docs/percy/overview/visual-testing-basics): approved baseline に対する visual diff と reviewer の判断
- [Baseline management](https://www.browserstack.com/docs/percy/visual-testing-workflows/baseline-management/overview): branch / approved snapshots 間の比較対象の運用
- [Percy Visual Review Agent](https://www.browserstack.com/docs/percy/ai-agents/visual-review-agent/overview): AI による差分 classification の例。外部 service や agent の導入を必須とはしない

**観察:** 「何が変わったか」の検出と「変化を承認すべきか」の判断は分離する。baseline approval を無条件自動化しない。

## What each method can / cannot establish

| Method | Strong evidence | Cannot establish alone |
| --- | --- | --- |
| Static lint / component contracts | unauthorized override、known API misuse | rendered quality / experience |
| Pixel / perceptual visual diff | changed regions / regression candidates | intentionality, beauty, semantics |
| Screenshot critique | hierarchy、alignment、density、color、crop、visual attention | keyboard、screen reader、scroll / gesture、animation chronology |
| Multi-model council | diverse hypotheses and missed areas | objective 9/10 quality、independence の保証 |
| Browser / interaction test | reachable state、task action、focus / scroll の挙動 | visual purpose / aesthetic success |
| Human / cold-read review | first impression と目的のずれ | all environments coverage |

## Reproducibility and false-positive controls

- 同じ viewport / DPR / rendering engine / OS / zoom / font / locale / theme / fixture / state / scroll / focus で撮る
- fixed clock、deterministic fixture、animation settle を使う。mask の領域と理由を明記し、肝心な area を隠さない
- full image で画面全体の階層を見て、細部は **1:1 crops** で確認する
- before/after は画面領域の差分以外に、修正が他 state / locale / width を壊していないか確認する
- uploaded image、private workspace、secrets、third-party imagery を public evaluator / PR に無断公開しない
- external services、CI full matrix、multi-model calls は scope と cost が見合う場合だけ使う

## Trial protocol

同じ Web UI task を skillなし / current / candidate で比較し、実際の screenshots と interactions から次を記録する。

1. **Detection:** 有効な finding が増えたか。虚偽の指摘・見落としは何か
2. **Correction:** 表示問題を実際に直し、原状の動作・brand・content を保持したか
3. **Stability:** capture条件を固定しても差分・score の不一致が起きるか
4. **Cost:** captures、reviewers、修正回数、elapsed time、token / service cost
5. **Negative controls:** intentional typography / illustration / high density、no-change task、重い interaction を含むが静止画では判定不能な画面

固定の「全モデル9点」や「ゼロ diff」は gate にしない。製作側が知らない brief を blind reviewer に見せることと、要件を開示した適合性評価を別に実施する。画像レビューで実施できなかった検証は `not tested` と書く。結果が不十分なら **insufficient evidence** とし、Skill の一般的な有効性を主張しない。
