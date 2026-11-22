# Marketing / LP References

Last reviewed: 2026-09-10

## Goal

Marketing surface では、同じ section template を繰り返すのではなく、異なる composition を一つの page 上で coherent に見せる shared coordinate system を観察する。

## Primary references

### Linear

Production:
https://linear.app/

Observe:

- hero と subsequent sections で再利用される vertical axes
- centered composition から asymmetric split への移行
- heading / copy / product visual の span relationship
- section ごとに構成が変わっても page 全体が崩れない理由
- visual が container を超える箇所
- responsive で何を stack / crop / simplify するか

Do not copy Linear-specific dark styling or product imagery.

### Stripe

Production:
https://stripe.com/

Observe:

- asymmetric text / visual layouts
- repeated content edges across different sections
- dense product information と large whitespace の切替
- background / visual breakout が base grid とどう接続されるか

### Vercel

Production:
https://vercel.com/

Observe:

- visible borders によって追いやすい alignment
- container boundary
- large feature region と smaller repeated units
- section boundary と grid boundary の一致 / 不一致
- dense technical content の width control

### Dub

Production:
https://dub.co/

Source:
https://github.com/dubinc/dub

Observe:

- 実用的な SaaS marketing page の container / section composition
- hero / logo / feature / testimonial / CTA の width hierarchy
- rendered structure と source implementation の対応

特に visual inspection 後に source を追う教材として使う。

## Supporting system references

### IBM 2x Grid / Carbon

https://www.ibm.com/design/language/2x-grid/
https://carbondesignsystem.com/elements/2x-grid/overview/
https://carbondesignsystem.com/elements/2x-grid/usage/

Use for:

- base unit
- divisions
- gutter / margin relationships
- grid as geometric foundation

Production reference の代替にはしない。

### USWDS Layout Grid

https://designsystem.digital.gov/utilities/layout-grid/

Use for:

- conventional 12-column model
- container / row / column basics
- simple responsive baseline

## Extraction checklist

- common outer container
- 3–6 strongest repeated vertical axes
- section-specific spans
- text measure
- visual breakout rules
- horizontal gutter family
- vertical section rhythm
- breakpoint transformation

「全部 12 column に収まっているか」ではなく、異種 section がどの alignment rule を共有しているかを見る。
