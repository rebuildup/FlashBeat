# Dashboard / Data References

Last reviewed: 2026-09-10

## Goal

Dashboard は marketing page ではない。

information importance、minimum useful visualization size、resize / rearrange behavior を tile area と layout hierarchy に翻訳する。

## Primary references

### Grafana Play

Production sandbox:
https://play.grafana.org/

Product:
https://grafana.com/

Source:
https://github.com/grafana/grafana

Observe:

- dashboard canvas
- panel width / height relationships
- KPI / chart / table grouping
- row composition
- resize / rearrange behavior
- dense toolbar と data canvas の分離
- dashboard-level controls
- panel 内部の own layout
- viewport change での behavior

3 layer を分離する:

1. app shell
2. dashboard canvas
3. panel internal layout

### PostHog

Production:
https://app.posthog.com/

Product:
https://posthog.com/

Source:
https://github.com/PostHog/posthog

Observe:

- analytics tile hierarchy
- equal-size ではない insight grouping
- dashboard layout editing
- chart と textual / numeric insight の混在
- breakpoint ごとの tile layout

Implementation evidence:
PostHog の dashboard tile model は breakpoint ごとの position / size layout を JSON として保持する。

Source search starting point:
https://github.com/PostHog/posthog

## Supporting references

### IBM Carbon

https://carbondesignsystem.com/elements/2x-grid/overview/

Use for geometric consistency inside dashboard surfaces, not for deciding dashboard information hierarchy by itself.

## Extraction checklist

- dashboard canvas width behavior
- horizontal grid resolution
- row sizing
- gap
- minimum tile dimensions
- importance → area
- resize constraints
- empty-space handling
- panel title / controls placement
- scroll model
- breakpoint-specific layout

## Anti-patterns

Avoid:

- every widget equal size
- marketing-page 12-column assumptions
- chart compressed below usable size
- arbitrary card gaps unrelated to shell density
- nested scrolling without explicit ownership
- mobile = desktop tiles simply stacked in original order

Data hierarchy determines geometry.
