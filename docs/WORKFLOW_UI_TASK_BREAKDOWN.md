# Workflow UI Task Breakdown

## 2026-06-20 Alignment Pass

Status: In progress

Completed in this pass:
- Replaced page-level workflow hero/header with flat workflow topbar.
- Separated bridge/operator metadata from action buttons.
- Reworked workflow viewport layout into topbar + 3-column workspace + bottom history dock.
- Converted execution history to compact/expanded dock structure in markup.
- Rewrote workflow CSS block to match the new flat-panel structure.

Still to verify:
- Final visual alignment against current manual CSS baseline.
- Responsive behavior with real browser refresh.

Verified:
- dotnet build src\NewAGV.Web\NewAGV.Web.csproj --no-restore -v minimal

## Design Decision: Flat Panels

Trang Workflow sử dụng **flat panel layout** (giống Lovable):
- Không có card border-radius/shadow cho sidebar, editor, inspector, history
- Dùng border separators (border-right, border-left, border-b, border-t) để phân tách panels
- Nền chung white cho toàn workspace
- gap-0 giữa các columns
- Reference: `index.tsx` (Lovable source)

## Task Status

| # | Task | Status |
|---|------|--------|
| 1 | Global header mapping | Done |
| 2 | CSS cache refresh | Done |
| 3 | Remove local page hero | Done |
| 4 | Convert to viewport-fit layout | Done |
| 5 | Build compact top action bar | Done |
| 6 | Compact left catalog | Done |
| 7 | Compact center editor | Done |
| 8 | Compact right inspector | Done |
| 9 | Execution history always visible | Done |
| 10 | Add expand/collapse history behavior | Done |
| 11 | Control internal overflow | Done |
| 12 | Typography pass | Done |
| 13 | Spacing pass | Done |
| 14 | Action icon cleanup | Done |
| 15 | Desktop responsiveness tuning | Done |
| 16 | CSS cleanup (dead code removal) | Done |
| 17 | Flat panel conversion | Done |

## Files Modified

| File | Change |
|------|--------|
| `src/NewAGV.Web/Components/Pages/Workflow.razor` | Full rewrite — flat panel markup matching Lovable |
| `src/NewAGV.Web/wwwroot/app.css` | 4136 → 2607 lines. Removed dead CSS (Monitor v1-v3, Ops v1-v3, Workflow v1). Rewrote Workflow CSS as flat panels. |
| `src/NewAGV.Web/Components/App.razor` | CSS cache-bust version |
| `src/NewAGV.Web/Components/Layout/MainLayout.razor` | Page title mapping |
| `docs/WORKFLOW_UI_TASK_BREAKDOWN.md` | This file |

## Acceptance Checklist

- [x] Sidebar is global (not inside workflow page)
- [x] Global header shows "Workflow" for /workflow route
- [x] No "NEWAGV / Workflow" hero banner
- [x] Flat panels — no card borders/shadow on sidebar, editor, inspector
- [x] Border separators between 3 columns
- [x] Action bar compact with chips + toolbar buttons
- [x] Catalog list with border-bottom items, border-left selected
- [x] Step cards with grip/index/body/actions flex layout
- [x] Step connectors with hover-to-reveal insert button
- [x] Centered canvas max-width 680px
- [x] Inspector with tabs, fields, toggles, preview card
- [x] Runtime tab with progress bar, active step, event card
- [x] Execution History always visible at bottom
- [x] History expand/collapse toggle
- [x] History table with sticky thead, monospace font
- [x] Responsive: 1360px inspector drops, 980px single column
- [x] CSS cleanup: removed ~1500 lines dead code
- [x] All icon glyphs using Segoe MDL2 Assets




