# AGV Monitor – Design Spec (Blazor Implementation Guide)
> Mục tiêu: tài liệu này mô tả đầy đủ layout, design tokens, component và state của trang **AGV Monitor** để team có thể implement lại bằng **C# Blazor** (Blazor Server / Blazor WebAssembly + MudBlazor hoặc plain CSS).
---
## 1. Tổng quan layout
```
┌───────────────────────────────────────────────────────────────────────┐
│  Top Header (h = 56px)                                                │
│  [Logo] SEER AGV Control | AGV Monitor      [Role ▾] [Avatar]         │
├──────┬────────────────┬───────────────────────────────────────────────┤
│      │                │  Sticky Status Bar (h = 48px)                 │
│ Nav  │  Robot Info    ├───────────────────────────────────────────────┤
│ Side │  Sidebar       │  Alert Strip (h = 40px, optional)             │
│ bar  │  (w = 280px)   ├───────────────────────────────────────────────┤
│ w=56 │                │                                               │
│  ↔   │  - AMB-01 card │            MAP AREA (flex-1)                  │
│ w=224│  - Status grid │   [Toolbar: zoom / fit / fullscreen]          │
│      │  - Warnings    │   [Layer toggles]                             │
│      │  - Integration ├───────────────────────────────────────────────┤
│      │                │  Station Table (collapsible, 120px ↔ 320px)   │
└──────┴────────────────┴───────────────────────────────────────────────┘
```
Tỷ lệ chiều rộng (desktop ≥ 1440px):
- NavSidebar: `56px` (collapsed) / `224px` (expanded)
- RobotInfoSidebar: `280px` cố định
- Map + bảng: phần còn lại (flex-1)
---
## 2. Design Tokens
### 2.1 Màu (OKLCH, đồng bộ với `src/styles.css`)
| Token | Light | Dùng cho |
|---|---|---|
| `--background` | `oklch(0.985 0.005 200)` `#F6F8F9` | Nền trang |
| `--foreground` | `oklch(0.18 0.02 230)` `#1B2330` | Text chính |
| `--card` | `#FFFFFF` | Nền card / sidebar |
| `--border` | `oklch(0.92 0.01 220)` `#E4E8EC` | Đường kẻ |
| `--muted` | `oklch(0.96 0.005 220)` `#EEF1F3` | Nền nhạt |
| `--muted-foreground` | `oklch(0.5 0.02 230)` `#6B7480` | Text phụ |
| `--primary` (teal) | `oklch(0.62 0.11 195)` `#0E9BA8` | Accent, active nav, nút chính |
| `--primary-foreground` | `#FFFFFF` | Text trên primary |
| `--accent` | `oklch(0.93 0.04 195)` `#D6EEF1` | Hover, badge nhẹ |
| `--success` | `#16A34A` | Status OK |
| `--warning` | `#F59E0B` | Status warn / alert strip |
| `--warning-bg` | `#FEF3C7` | Nền alert strip |
| `--destructive` | `#DC2626` | Status err, E-stop |
### 2.2 Typography
- Font: **Inter** (fallback: `system-ui, sans-serif`). Trong Blazor có thể nhúng qua CDN hoặc package `@fontsource/inter` đã build sẵn.
- Sizes: `text-xs 12px / text-sm 13px / text-base 14px / text-lg 16px / text-xl 18px`.
- Heading sidebar/header: `14px` semi-bold (600).
- Body / table: `13px` regular (400).
- Number / metric (battery %): `16–18px` semi-bold.
### 2.3 Spacing / radius / shadow
- Border radius: **8px** chung (`--radius: 0.5rem`); chip/badge `6px`; nút icon `6px`.
- Spacing chuẩn: `12px` (gap card), `8px` (gap nội bộ), `16px` (padding lớn).
- Shadow: tối giản — chỉ `border` 1px. Floating toolbar trên map: `0 1px 3px rgba(0,0,0,0.08)`.
---
## 3. Khu vực chi tiết
### 3.1 NavSidebar (chuyển trang) — `AgvNavSidebar.razor`
- Width: 56px collapsed / 224px expanded; transition 150ms.
- Top: icon logo `LayoutGrid` 24px, căn giữa.
- Items (icon 18px + label 13px):
  1. Trang chủ – `Home`
  2. Giám sát – `Radar` (active)
  3. Lệnh – `Terminal`
  4. Cấu hình – `Settings`
  5. Nhật ký – `FileText`
- Item active: nền `--accent`, text `--primary`, border-left 2px `--primary`.
- Item hover: nền `--muted`.
- Bottom: nút toggle “Thu gọn / Mở rộng” với icon `ChevronLeft/Right`.
- State: `bool Collapsed` (lưu localStorage nếu cần).
**Blazor parameters:**
```csharp
[Parameter] public bool Collapsed { get; set; }
[Parameter] public EventCallback<bool> CollapsedChanged { get; set; }
[Parameter] public string ActiveRoute { get; set; } = "monitor";
```
### 3.2 TopHeader — `AgvTopHeader.razor`
- Height 56px, `border-bottom 1px`, nền trắng.
- Trái: logo + breadcrumb dạng inline “SEER AGV Control | AGV Monitor”.
- Phải: dropdown chọn Role (Operator / Engineer / Admin) + Avatar tròn 32px.
### 3.3 RobotInfoSidebar — `RobotInfoPanel.razor`
Width 280px, padding 12px, gap 12px, scroll khi tràn.
Khối con:
1. **RobotCard** (`AMB-01`)
   - Tên robot 14px bold + nhãn model (12px muted).
   - Battery: progress bar h=6px, màu theo ngưỡng (≥40% primary, 20–40% warning, <20% destructive). Hiển thị `54%` ở góc phải.
   - Vị trí hiện tại: `X / Y / θ` dạng 3 dòng nhỏ.
2. **StatusGrid** — 4 hàng, mỗi hàng `[dot 8px][label][value]`
   - Link, Localization, E-stop, Control Owner.
   - Dot màu theo enum `Status { Ok, Warn, Err }` → success / warning / destructive.
3. **WarningsSummary** — nút full width: icon `Bell`, “3 cảnh báo”, mở `WarningsDrawer`.
4. **IntegrationNotice** — chip nhỏ “Tích hợp offline” + icon `AlertTriangle`, nền `--muted`.
### 3.4 StatusBar (sticky) — `RouteStatusBar.razor`
- Height 48px, sticky top trong content column, nền trắng, border-bottom.
- Items inline (gap 16px): map name `LAB_16062026`, robot `AMB-01 · Online`, battery `54%`, warnings `3`, route status `Route: Draft`.
- Mỗi item là chip nhỏ: icon 14px + text 13px.
### 3.5 AlertStrip — `AlertStrip.razor`
- Height 40px, nền `--warning-bg`, text `#92400E`.
- Icon `AlertTriangle` + message + nút `×` đóng (state `bool Dismissed`).
- Ẩn hoàn toàn nếu không có cảnh báo route.
### 3.6 MapCanvas — `MapCanvas.razor`
- Chiếm toàn bộ vùng còn lại; nền `#F1F5F7`, vẽ grid 40px bằng SVG hoặc canvas.
- Trục X/Y mảnh `#94A3B8`.
- Robot marker: chấm tròn 12px primary + đuôi hướng (tam giác nhỏ theo θ).
- **Floating toolbar** (góc phải trên, gap 4px):
  - `Plus` zoom in, `Minus` zoom out, `Maximize2` fit, `Expand` fullscreen.
  - Nút icon 32×32, nền trắng, border, radius 6px.
- **Layer toggles** (góc phải dưới): checkbox dạng pill: `Stations`, `Path`, `Obstacles`, `Heatmap`.
- Trong Blazor: dùng `<svg>` thuần hoặc tích hợp `BlazorPanZoom` / `SkiaSharp.Views.Blazor` nếu cần performance.
### 3.7 StationTable (collapsible) — `StationTable.razor`
- Default height 120px (chỉ thấy header + 1 hàng), expanded 320px. Toggle bằng nút `ChevronUp/Down` ở header.
- Header sticky bên trong, nền `--muted`.
- Cột: `#`, `Tên`, `X`, `Y`, `θ`, `Status`, hành động (`⋯`).
- Hàng cao 32px, font 13px, hover nền `--accent` 30%.
- Status cell: chip nhỏ Ok/Warn/Err.
### 3.8 WarningsDrawer — `WarningsDrawer.razor`
- Cố định bên phải, width 360px, full-height, slide-in 200ms.
- Header: “Cảnh báo” + nút `X`.
- Danh sách card cảnh báo: icon severity, tiêu đề, mô tả, timestamp, nút “Xác nhận”.
- Overlay mờ phía sau, click để đóng.
---
## 4. Data model (gợi ý C#)
```csharp
public enum Status { Ok, Warn, Err }
public enum RouteState { Draft, Validated, Running }
public record Robot(
    string Id,
    string Name,
    string Model,
    bool Online,
    int BatteryPercent,
    double X, double Y, double Theta,
    Status Link, Status Localization, Status EStop,
    string ControlOwner);
public record Station(
    int Index, string Name,
    double X, double Y, double Theta,
    Status Status);
public record Warning(
    string Id, Status Severity,
    string Title, string Message,
    DateTime At, bool Acknowledged);
public record MapInfo(string Name, RouteState RouteState);
```
State page chính (`AgvMonitor.razor` hoặc qua `IAgvMonitorState` scoped service):
```csharp
bool NavCollapsed;
bool WarningsOpen;
bool StationsExpanded;
bool AlertDismissed;
Robot CurrentRobot;
MapInfo CurrentMap;
List<Station> Stations;
List<Warning> Warnings;
```
---
## 5. Tương tác / hành vi
| Hành động | Kết quả |
|---|---|
| Click toggle NavSidebar | `NavCollapsed` đảo, animate width 150ms |
| Click WarningsSummary | `WarningsOpen = true`, drawer slide-in |
| Click `×` AlertStrip | `AlertDismissed = true`, ẩn strip |
| Click `ChevronUp/Down` StationTable | đảo `StationsExpanded`, animate height |
| Click nút zoom map | `Scale *= 1.2 / 0.8`, clamp `[0.25, 4]` |
| Click `Fit` | reset transform về fit-to-screen |
| Click layer toggle | bật/tắt visibility layer tương ứng |
| Robot mất kết nối | `Link = Err`, dot đỏ, badge “Offline” trong StatusBar |
| Battery < 20% | thanh battery đổi destructive + thêm warning vào list |
---
## 6. Responsive
- ≥ 1440px: layout đầy đủ như trên.
- 1024–1439px: RobotInfoSidebar 240px, ẩn label phụ trong StatusBar.
- < 1024px: NavSidebar mặc định collapsed; RobotInfoSidebar trở thành drawer trái (mở bằng nút trên header). Map chiếm full-width.
- < 768px: StationTable chuyển sang dạng card list dọc.
---
## 7. Mapping component React ↔ Blazor
| React (hiện tại) | Blazor đề xuất |
|---|---|
| `<AgvMonitor />` page | `Pages/AgvMonitor.razor` |
| Sidebar điều hướng | `Shared/AgvNavSidebar.razor` |
| Robot panel | `Shared/RobotInfoPanel.razor` (+ `RobotCard`, `StatusRow`) |
| Status bar trên cùng map | `Shared/RouteStatusBar.razor` |
| Alert strip | `Shared/AlertStrip.razor` |
| Map | `Shared/MapCanvas.razor` (SVG) |
| Station table | `Shared/StationTable.razor` |
| Warnings drawer | `Shared/WarningsDrawer.razor` |
| `lucide-react` icons | Dùng [Blazor.LucideIcons](https://www.nuget.org/packages?q=lucide) hoặc `MudBlazor` icons; có thể fallback SVG inline với cùng tên |
---
## 8. CSS variables cho Blazor (copy vào `wwwroot/css/app.css`)
```css
:root {
  --background: #F6F8F9;
  --foreground: #1B2330;
  --card: #FFFFFF;
  --border: #E4E8EC;
  --muted: #EEF1F3;
  --muted-foreground: #6B7480;
  --primary: #0E9BA8;
  --primary-foreground: #FFFFFF;
  --accent: #D6EEF1;
  --success: #16A34A;
  --warning: #F59E0B;
  --warning-bg: #FEF3C7;
  --destructive: #DC2626;
  --radius: 8px;
  --space: 12px;
  --font-sans: "Inter", system-ui, sans-serif;
}
.chip { display:inline-flex; align-items:center; gap:6px;
  padding:2px 8px; border-radius:6px; font-size:12px;
  background:var(--muted); color:var(--muted-foreground); }
.chip--ok   { background:#DCFCE7; color:#15803D; }
.chip--warn { background:#FEF3C7; color:#92400E; }
.chip--err  { background:#FEE2E2; color:#991B1B; }
.status-dot { width:8px; height:8px; border-radius:9999px; display:inline-block; }
.status-dot--ok   { background:var(--success); }
.status-dot--warn { background:var(--warning); }
.status-dot--err  { background:var(--destructive); }
```
---
## 9. Checklist khi port sang Blazor
- [ ] Tạo layout 3 cột với `display:flex` (không dùng Grid để dễ resize).
- [ ] Tách NavSidebar và RobotInfoPanel thành 2 component riêng — KHÔNG gộp.
- [ ] Lưu `NavCollapsed` vào `ProtectedLocalStorage`.
- [ ] Render map bằng SVG để dễ binding marker/path theo dữ liệu realtime (SignalR).
- [ ] Đảm bảo StatusBar sticky `position: sticky; top: 0; z-index: 10` trong content column.
- [ ] WarningsDrawer dùng `@if (WarningsOpen)` + class `drawer--open` để animate.
- [ ] Tất cả màu lấy từ CSS variables, không hardcode hex trong markup.
- [ ] Test responsive 1280 / 1440 / 1920.
```