# Kế hoạch Phase 1 - AGV Monitor kiểu RDS rút gọn

Tài liệu này tách riêng Phase 1 từ roadmap RDS để đội dự án có thể đọc nhanh, biết cần làm gì trước, và biết phiên bản hiện tại đã triển khai tới đâu.

## Mục tiêu Phase 1

Xây một màn hình vận hành trung tâm cho 1 AGV thật, tương tự phần Monitor của RDS nhưng rút gọn cho NewAGV.

Người vận hành chỉ cần nhìn vào một trang để biết:

- AGV có online không.
- AGV đang ở đâu trên map.
- Map hiện tại có bao nhiêu station.
- AGV có sẵn sàng nhận lệnh không.
- Có e-stop, alarm, lỗi localization hoặc lock quyền điều khiển không.
- Có thể chọn station thật và gửi lệnh goto/pause/resume/cancel không.

## Phạm vi làm ngay

Phase này không clone toàn bộ RDS. Chỉ tập trung vào màn hình vận hành chính.

Các phần cần có:

- Top toolbar:
  - search station
  - zoom in/out
  - fit map
  - bật/tắt station, path, zone, label
  - refresh dữ liệu
- Left panel:
  - danh sách robot
  - trạng thái online/degraded/offline
  - danh sách station thật từ map
- Main map:
  - hiển thị station/path/zone
  - hiển thị robot theo pose thật
  - tự fit tọa độ âm/dương từ SEER map
  - click station để chọn target
- Right panel:
  - battery
  - e-stop
  - localization
  - confidence
  - control owner
  - task hiện tại
  - alarm/warning nổi bật
  - command panel
- Bottom status bar:
  - API health
  - Worker health
  - AGV integration status
  - số station
  - số audit entries

## Command trong Phase 1

Các command được dùng trong màn hình Monitor:

- `GoToStation` -> SEER `3051`
- `Pause` -> SEER `3001`
- `Resume` -> SEER `3002`
- `Cancel` -> SEER `3003`
- `ReturnToHome` -> SEER `3051` với home station cấu hình

Teleop/open-loop motion chưa đưa vào màn hình Monitor cho operator vì rủi ro an toàn cao.

## Safety gate

Trước khi gửi lệnh di chuyển, UI và API đều phải chặn nếu có các điều kiện sau:

- Robot offline.
- Chưa có telemetry detail.
- E-stop hoặc driver emergency đang active.
- Control owner đang bị lock bởi hệ khác.
- Có fatal/error alarm.
- Localization chưa sẵn sàng.
- Chưa chọn station khi gửi `GoToStation`.

Warning không chặn tuyệt đối, nhưng phải hiện cảnh báo để operator biết trước khi thao tác.

## Implementation đã thực hiện

Đã nâng cấp route `/map` từ Map Workspace thành `AGV Monitor`.

Các file chính:

- `src/NewAGV.Web/Components/Pages/Map.razor`
  - layout Monitor 3 cột
  - search station
  - robot list
  - station list
  - map metadata
  - readiness panel
  - alarm summary
  - command panel
  - bottom status bar
- `src/NewAGV.Web/Components/MapCanvas.razor`
  - nhận filter station/path/zone/label
  - nhận zoom
  - nhận selected robot/station
  - trả click station từ canvas về Blazor
- `src/NewAGV.Web/wwwroot/js/agvMap.js`
  - tự fit bounds theo dữ liệu thật
  - hỗ trợ tọa độ âm/dương
  - vẽ grid, axis, station, zone, path, robot heading
  - highlight station/robot được chọn
  - hit-test station khi click
- `src/NewAGV.Web/wwwroot/app.css`
  - layout Monitor
  - toolbar
  - left/right panel
  - responsive mobile/tablet
  - status/health/readiness styles
- `src/NewAGV.Web/Components/Layout/NavMenu.razor`
  - đổi menu `Map Workspace` thành `AGV Monitor`

## Cách kiểm tra hiện tại

Build Web:

```powershell
dotnet build src\NewAGV.Web\NewAGV.Web.csproj -p:OutDir=C:\Users\TD-997\Documents\NewAGV\.run-build\web\
```

Kết quả hiện tại: build thành công, không warning, không error.

Đã chạy local:

- API: `http://localhost:5222`
- Web: `http://localhost:5309`
- Monitor: `http://localhost:5309/map`

HTTP render của `/map` trả `200` và có title `AGV Monitor`.

## Việc cần test khi chuyển sang Wi-Fi AGV

Khi máy đã kết nối `IDEA_AGV_MESH`, cần kiểm tra:

1. API detail vẫn đọc được robot `AMB-01`.
2. Monitor hiển thị map `LAB_16062026`.
3. Station thật xuất hiện trong left panel và trên canvas.
4. Robot pose nằm đúng trong canvas, không bị trôi ra ngoài màn hình.
5. Click station trên canvas đổi target station.
6. Search station lọc đúng.
7. E-stop/alarm/localization/control owner hiện đúng.
8. `Pause`, `Resume`, `Cancel` gửi được qua API -> Worker -> AGV.
9. `GoToStation` chỉ test với station hợp lệ và khu vực an toàn.
10. Audit log ghi lại accepted/rejected sau mỗi command.

## Chưa làm trong Phase 1 này

Các phần sau để Phase 2 hoặc sau khi test AGV thật:

- Vẽ path topology thật giữa các station nếu chưa lấy được từ map project.
- Quản lý task record đầy đủ.
- Transport order.
- Multi-AGV.
- Teleop/open-loop motion.
- Edit map/station từ web.
- Clone toàn bộ menu RDS.

## Kết luận

Phase 1 hiện đã có khung Monitor vận hành tập trung. Bước tiếp theo là chuyển PC sang Wi-Fi AGV, chạy API/Worker/Web, rồi test với dữ liệu thật và một station an toàn trước khi cho vận hành viên dùng thường xuyên.
