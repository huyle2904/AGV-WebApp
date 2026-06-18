# Plan làm lại AGV Monitor

Lý do làm lại: phiên bản Monitor trước đó đưa station như `LM1`, `LM2` lên UI quá tự tin, trong khi hệ thống chưa có cấu hình vận hành rõ ràng về vị trí, route, path topology hay target station hợp lệ. Điều này có thể làm người dùng hiểu nhầm rằng NewAGV đã sẵn sàng điều hướng theo station, dù thực tế mới chỉ đọc được một phần dữ liệu từ AGV.

## Nguyên tắc dữ liệu

1. Không bịa dữ liệu.
2. Không dùng placeholder giống dữ liệu thật như `LM1`, `AP25`, `ChargePoint`.
3. Không auto chọn station đầu tiên làm target.
4. Không coi station raw từ AGV map là route target đã được NewAGV xác thực.
5. Nếu không có path/route topology thì không hiển thị đường đi.
6. Nếu không có dữ liệu thì hiện empty state rõ ràng: `Not reported`, `No validated route map configured`, hoặc `No live robot`.
7. Command di chuyển chỉ bật khi có cấu hình target/route rõ ràng và safety gate đạt.

## Mục tiêu UIUX

Monitor phải là màn hình vận hành thật, không phải trang demo map.

Người dùng nhìn vào trang phải biết:

- AGV có đang online không.
- Robot nào đang được sync.
- Pin, e-stop, localization, control owner đang ra sao.
- Map hiện tại có được robot báo về không.
- Hệ thống NewAGV đã có route map/target station hợp lệ chưa.
- Command nào an toàn để gửi ngay.
- Command nào chưa thể gửi và vì sao.

## Layout mới

### Header

- Tên trang: `AGV Monitor`.
- Mô tả rõ: monitor dữ liệu SEER AGV thật, dữ liệu thiếu sẽ không bị đoán.
- Card trạng thái integration:
  - Online
  - Degraded
  - Offline
  - message từ API/Worker

### KPI row

Hiển thị 4 thông tin ngắn:

- Robot đang chọn.
- AGV link.
- Current map.
- Command readiness.

Nếu thiếu dữ liệu thì ghi `Not reported`, không tự sinh text.

### Cột trái

Robot state:

- Danh sách robot thật từ API.
- Mode.
- Battery.
- Connectivity.

Safety signals:

- Robot link.
- Battery.
- E-stop.
- Localization.
- Control owner.

Mỗi signal có màu:

- good: xanh.
- warn: vàng.
- bad: đỏ.
- neutral: xám.

### Khu vực giữa

Position and map layer:

- Luôn có khu vực map/canvas nếu có robot.
- Nếu chưa có validated map layer thì chỉ vẽ robot pose.
- Nếu muốn xem station raw từ AGV, người dùng phải bật toggle `Show raw AGV station records`.
- Khi bật raw station, UI ghi rõ đó là dữ liệu thô từ AGV map, chưa phải target station đã cấu hình trong NewAGV.

Không hiển thị:

- Search station.
- Selected target.
- Path/route nếu chưa có dữ liệu thật.

### Cột phải

Command panel:

- Tạm thời chỉ cho các command an toàn hơn:
  - Pause.
  - Resume.
  - Cancel.
- `GoToStation` không bật trong Monitor cho tới khi có cấu hình target/route rõ ràng.
- Hiển thị callout: `Goto station is not enabled yet`.

Alarms:

- Chỉ hiển thị alarm thật từ robot.
- Không có alarm thì ghi `No active alarm details`.

## Command policy hiện tại

Trong Monitor:

- Cho phép: pause, resume, cancel.
- Chưa cho phép: goto station, return home, teleop.

Lý do:

- Chưa có validation target station trong NewAGV.
- Chưa có route/path topology.
- Chưa có UI xác nhận khu vực vận hành an toàn.
- Teleop/open-loop rủi ro cao.

## Tiêu chí hoàn thành bản rework này

- Không còn placeholder station giả trong Monitor.
- Không còn auto target station.
- Không còn search station khi chưa có cấu hình station hợp lệ.
- Không còn hiển thị raw station như dữ liệu điều hướng đã xác thực.
- Map nói rõ khi chưa có validated route map.
- Command panel khóa goto station.
- Build Web thành công.

## File đã sửa

- `src/NewAGV.Web/Components/Pages/Map.razor`
- `src/NewAGV.Web/Components/App.razor`
- `src/NewAGV.Web/wwwroot/app.css`

## Bước tiếp theo

Muốn bật `GoToStation` đúng cách cần làm thêm:

1. Xác định station nào là station vận hành hợp lệ.
2. Lưu cấu hình station/target trong NewAGV, không chỉ dùng raw map records.
3. Xác nhận localization status nào được phép chạy ở robot hiện tại.
4. Test `3051` với một station an toàn.
5. Sau khi test thành công mới bật command target trên Monitor.
