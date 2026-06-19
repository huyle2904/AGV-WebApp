# CHANGELOG - NewAGV Code Changes

## 1. Contracts (`src/NewAGV.Contracts/DomainModels.cs`)

Thêm 2 records mới:

```csharp
public record SeerRelocationRequest(double X, double Y, double Angle);
public record TeleopRequest(double VelocityX, double VelocityY, double AngularVelocity);
```

---

## 2. Worker (`src/NewAGV.Worker/`)

### `Services/SeerRobotOptions.cs`
- **Xóa** `SeerRelocationRequest` record trùng (đã chuyển lên Contracts)

### `Services/SeerCommandService.cs`
- Sửa `MissionCommandType.Teleop` từ `throw` → gọi `SendTeleopAsync(request, cancellationToken)`
- Thêm method **`TeleopDriveAsync(TeleopRequest)`**: gửi TCP API `2010` (Open-loop motion) qua ControlPort với payload `{velocity_x, velocity_y, angular_velocity}`
- Thêm method **`SendTeleopAsync()`**: dispatch Teleop dùng `MissionCommandRequest.VelocityX/Y`

### `Program.cs`
- Thêm endpoint **`POST /internal/commands/teleop`**: nhận `TeleopRequest`, gọi `SeerCommandService.TeleopDriveAsync`

---

## 3. API (`src/NewAGV.Api/`)

### `Services/SeerWorkerClient.cs`
- Thêm method **`TeleopDriveAsync(TeleopRequest)`**: HTTP POST tới Worker `/internal/commands/teleop`

### `Controllers/CommandsController.cs`
- Inject thêm `SeerWorkerClient workerClient` vào constructor
- Thêm endpoint **`POST /api/commands/teleop`**: nhận `TeleopRequest`, gọi `workerClient.TeleopDriveAsync`
- Thêm endpoint **`POST /api/commands/relocate`**: nhận `SeerRelocationRequest`, gọi `workerClient.RelocateAsync`

### `Services/AgvPlantStore.cs`
- Sửa policy Teleop: `Enabled = false` → `true`, `MinimumRole = UserRole.Engineer` → `UserRole.Operator`

---

## 4. Web Frontend (`src/NewAGV.Web/`)

### `Services/AgvApiClient.cs`
- Thêm method **`TeleopDriveAsync(TeleopRequest, UserRole)`**: HTTP POST tới API `/api/commands/teleop`
- Thêm method **`RelocateAsync(SeerRelocationRequest, UserRole)`**: HTTP POST tới API `/api/commands/relocate`

### `wwwroot/js/teleop.js` (file mới)
- `window.teleop` object với methods: `start(dotNetRef)`, `stop()`
- Keyboard listeners: keydown/keyup cho W/A/S/D/Q/E/Space
  - W = tiến, S = lùi
  - A = xoay trái, D = xoay phải
  - Q = sang trái (lateral), E = sang phải (lateral)
  - Space = emergency stop (gửi velocity = 0)
- Gửi velocity vector qua `DotNet.invokeMethodAsync("OnTeleopVelocity", vx, vy, az)`

### `Components/App.razor`
- Thêm script tag: `<script src="js/teleop.js?v=20260618"></script>`

### `Components/Pages/Map.razor`

**Inject:**
- Thêm `@inject IJSRuntime JS`

**HTML - Teleop Panel (mới):**
- Panel **Teleop (WASD)** với:
  - Visual grid WASD + Q/E buttons
  - Speed slider (0.1 - 1.0)
  - Nút Bật/Tắt Teleop
  - Nút E-STOP
  - Helper text hướng dẫn phím tắt

**HTML - Relocate Panel (mới):**
- Panel **Relocation** với:
  - Hiển thị tọa độ hiện tại (X, Y, Góc)
  - Input X, Y, Góc (auto-fill từ robot pose)
  - Nút Relocate

**HTML - GoToStation (sửa):**
- Bỏ disabled callout "Chưa bật Goto station"
- Thêm `GoToStation` vào dropdown lệnh
- Thêm station picker dropdown khi chọn GoToStation

**Code-behind fields (mới):**
```csharp
private bool _teleopActive;
private double _teleopSpeed = 0.3;
private double _teleopVx, _teleopVy, _teleopAz;
private string? _teleopStatusMessage;
private Timer? _teleopTimer;
private DotNetObjectReference<Map>? _dotNetRef;
private double _relocateX, _relocateY, _relocateAngle;
private string? _relocateStatusMessage;
private string? _selectedStationId;
```

**Code-behind methods (mới):**
- `ToggleTeleop()` - bật/tắt teleop, start/stop JS listener
- `EmergencyStop()` - dừng khẩn, gửi velocity 0
- `StopTeleopAsync()` - dọn dẹp timer + JS listener
- `[JSInvokable] OnTeleopVelocity(vx, vy, az)` - nhận velocity từ JS, duy trì timer 200ms
- `SendTeleopCommand(vx, vy, az)` - gửi velocity qua API
- `RelocateAsync()` - gửi relocation request qua API
- `AvailableStations` property - lọc station từ map entities
- `SelectRobot()` - auto-fill relocate X/Y/góc + clear messages
- `EnsureSelection()` - auto-fill relocate inputs ban đầu

**DispatchAsync (sửa):**
- Dùng `_selectedStationId` và `_selectedCommand` cho GoToStation

**Dispose (sửa):**
- Dọn `_teleopTimer` và `_dotNetRef`

### `Components/Layout/NavMenu.razor` (file mới)
- Component nav sidebar với:
  - Brand logo + text "NewAGV"
  - Collapse button
  - Nav links: Trang chủ, Giám sát, Lệnh, Cấu hình, Nhật ký
  - Icons Segoe MDL2 Assets cho mỗi link
  - Active state highlight
  - `collapsed` class support khi sidebar thu gọn

### `Components/Layout/NavMenu.razor.css` (file mới)
- Scoped styles cho NavMenu:
  - `.nav-brand`, `.nav-logo`, `.nav-brand-text`
  - `.nav-collapse-btn`, `.nav-collapse-icon`
  - `.nav-menu`, `.nav-link` (hover, active, collapsed states)
  - `.nav-icon` với các ::before icons

### `Components/Layout/MainLayout.razor.css`
- Thêm `overflow-y: auto; overflow-x: hidden; flex-shrink: 0;` vào `.sidebar`
- Thêm `.sidebar-collapsed .sidebar { width: 60px; }` trong media query

### `wwwroot/app.css`

**Teleop styles (mới):**
- `.teleop-panel` - container grid
- `.teleop-header` - header với status pill
- `.pill-active`, `.pill-inactive` - trạng thái teleop
- `.wasd-grid` - grid 3 cột cho WASD layout
- `.wasd-btn` - nút WASD
- `.wasd-w`, `.wasd-a`, `.wasd-s`, `.wasd-d` - grid positions
- `.teleop-controls` - speed slider container
- `.teleop-speed` - range input
- `.teleop-actions` - grid 2 cột cho toggle + estop
- `.teleop-toggle`, `.teleop-estop` - buttons
- `.text-warn` - warning text

**Relocate styles (mới):**
- `.relocate-panel`, `.relocate-inputs`, `.relocate-field`, `.relocate-btn`
- `.current-pose` - hiển thị tọa độ hiện tại

**Sidebar + NavMenu fallback styles (mới):**
- `.page`, `.sidebar`, `.page.sidebar-collapsed .sidebar`
- `.nav-brand`, `.nav-logo`, `.nav-brand-text`
- `.nav-collapse-btn`, `.nav-collapse-icon`, `.nav-collapse-btn.collapsed .nav-collapse-icon`
- `.nav-menu`, `.nav-link` (hover, active, collapsed)
- `.nav-icon` với icons

**Icon alignment fix:**
- `.signal-icon`: `display: inline-grid !important` → `display: grid !important`
- `.signal-check`: `display: inline-grid !important` → `display: grid !important`
- `.signal-row`: `grid-template-columns` từ `1.55rem` → `1.8rem`

---

## 5. File đã xóa

- `Components/Layout/NavMenu.razor.css` (orphan cũ, không có .razor tương ứng)

---

## 6. Flow tổng thể

```
WASD key → JS teleop.js → DotNet.invokeMethodAsync 
→ Map.razor OnTeleopVelocity() → Timer 200ms → SendTeleopCommand()
→ AgvApiClient.TeleopDriveAsync() → POST /api/commands/teleop
→ CommandsController.Teleop() → SeerWorkerClient.TeleopDriveAsync()
→ Worker POST /internal/commands/teleop
→ SeerCommandService.TeleopDriveAsync() → TCP API 2010 → Robot
```

```
Relocate button → Map.razor RelocateAsync()
→ AgvApiClient.RelocateAsync() → POST /api/commands/relocate
→ CommandsController.Relocate() → SeerWorkerClient.RelocateAsync()
→ Worker POST /internal/commands/relocate
→ SeerCommandService.RelocateAsync() → TCP API 2002 → Robot
```

```
GoToStation → Map.razor DispatchAsync()
→ AgvApiClient.DispatchAsync() → POST /api/commands/dispatch
→ CommandDispatcher.DispatchAsync() → validate safety → SeerWorkerClient.DispatchAsync()
→ Worker POST /internal/commands/dispatch
→ SeerCommandService.DispatchAsync() → TCP API 3051 → Robot
```
