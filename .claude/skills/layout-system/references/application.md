# Application / Tool References

Last reviewed: 2026-09-10

## Goal

Tool UI では column count より:

- shell responsibilities
- fixed / fluid regions
- nested layouts
- scroll ownership
- content-specific width

を観察する。

## Primary references

### Supabase Studio

Production:
https://supabase.com/dashboard

Source:
https://github.com/supabase/supabase/tree/master/apps/studio

Observe:

- global navigation
- project navigation
- workspace
- route header / toolbar
- editor / table / detail surfaces
- fixed vs flexible widths
- sticky regions
- nested scroll containers
- route ごとの content width 差
- data-dense UI の spacing

Supabase Studio は hosted platform でも使用される実 product implementation なので、rendered UI と source の往復に向く。

### Plane

Production:
https://app.plane.so/

Product:
https://plane.so/

Source:
https://github.com/makeplane/plane

Observe:

- sidebar → workspace hierarchy
- list / board / cycle / detail の layout switching
- navigation density
- view-level controls
- same shell 内で異なる working surface を成立させる方法

### Twenty

Product:
https://twenty.com/

Source:
https://github.com/twentyhq/twenty

Observe:

- CRM table / kanban / record detail
- contextual side panel
- dense data surface
- primary workspace を維持したまま detail を表示する方法
- panel width と content minimum width の balance

### Linear application

Production:
https://linear.app/

Observe:

- dense list layout
- sidebar hierarchy
- command-oriented tool density
- detail views
- consistent alignment across route types

Marketing homepage と application UI を混同しない。

## Supporting reference

### Supabase Design System / source conventions

source を追う場合、visual structure を把握してから relevant layout component を探す。

## Mandatory questions

major region ごとに決める:

- fixed?
- sticky?
- independently scrollable?
- viewport-sized?
- content-sized?
- flexible?
- resizable?
- overlay at narrow widths?

## Nested model

```text
Viewport
→ App Shell
   → Navigation
   → Workspace
      → Route Layout
         → Feature Layout
            → Component
```

一つの global grid に flatten しない。

## Content width

以下を同じ max-width に強制しない:

- settings form
- data table
- code editor
- kanban
- graph
- calendar
- record detail

information shape が width を決める。

## Anti-patterns

Avoid:

- application content を理由なく常に centered max-width にする
- outer page と inner pane の両方で同じ padding を重ねる
- scrollable region が不明
- side panel open で primary content が unusable width になる
- narrow viewport で desktop pane をそのまま圧縮する
