# NewAGV — Product & System Specification

> **Tên tài liệu:** `SPEC.md`  
> **Project:** NewAGV  
> **Trạng thái:** Draft nền tảng, phiên bản 0.1  
> **Phạm vi hiện tại:** Một AGV SEER, một hệ thống NewAGV, triển khai theo từng phase  
> **Ngôn ngữ triển khai:** C# / .NET / Blazor  
> **Mục đích:** Làm tài liệu gốc để tách tiếp thành `docs/product`, user stories, API contract, database design, workflow design, deployment guide và validation plan.

---

## 1. Tóm tắt quyết định kiến trúc

Các quyết định quan trọng của tài liệu này:

1. **NewAGV cần database ngay từ MVP có chức năng lưu workflow.**
   - Không dùng database chỉ phù hợp với prototype chỉ đọc trạng thái và hiển thị map.
   - Từ khi có workflow definition, execution history, audit và cache đồng bộ, database trở thành thành phần bắt buộc.
   - Khuyến nghị MVP/pilot một máy dùng **SQLite + EF Core**.
   - Khuyến nghị production nhiều user, nhiều trạm vận hành hoặc API/Worker chạy trên các máy khác nhau dùng **PostgreSQL**; dùng **SQL Server** nếu hạ tầng doanh nghiệp đã chuẩn hóa theo Microsoft.

2. **AGV/RoboshopPRO là source of truth cho dữ liệu robot cấp thấp.**
   - Map gốc, station gốc, taskchain gốc, robot pose, robot state và trạng thái task cấp thấp đến từ AGV/RoboshopPRO.
   - NewAGV chỉ lưu cache/snapshot và metadata bổ sung; MVP không chỉnh sửa trực tiếp nội dung gốc này.

3. **Database của NewAGV là source of truth cho dữ liệu riêng của ứng dụng.**
   - Workflow definitions và versions.
   - Workflow executions, step executions và execution events.
   - Failure/retry/timeout/manual-intervention policies.
   - Audit log, app settings, quyền người dùng và metadata đồng bộ.

4. **Web không bao giờ kết nối hoặc gửi lệnh trực tiếp tới AGV.**
   - Web gọi API.
   - API xác thực, phân quyền, validate và thực hiện safety gate.
   - Worker là thành phần duy nhất giữ kết nối TCP và ghi lệnh xuống AGV.

5. **Workflow runtime chạy trong Worker; workflow rules không nằm cứng trong Worker.**
   - Domain chứa state machine và invariant thuần.
   - Application chứa use case và orchestration policy.
   - Worker host workflow runtime, command queue và SEER adapter.
   - API chỉ điều phối request, safety gate, query và realtime facade.
   - Web chỉ trình bày dữ liệu và nhận thao tác người dùng.

6. **MVP không xây một workflow engine tổng quát kiểu BPMN.**
   - Hỗ trợ sequence.
   - Retry có giới hạn.
   - Timeout.
   - Fallback sequence đơn giản.
   - Manual intervention.
   - Không hỗ trợ vòng lặp tùy ý, parallel branch hoặc expression engine trong MVP.

7. **SignalR chỉ là kênh đẩy realtime, không phải source of truth.**
   - Sau khi reconnect, Web phải gọi REST để lấy snapshot hiện tại rồi mới tiếp tục nhận event.
   - Event realtime phải có sequence number và timestamp để phát hiện mất hoặc đảo thứ tự gói tin.

8. **Một AGV chỉ có tối đa một workflow execution chủ động tại một thời điểm.**
   - Các lệnh stop/cancel/emergency/manual recovery được ưu tiên riêng.
   - Ràng buộc này được kiểm tra cả ở API và Worker để tránh race condition.

---

## 2. Product vision

NewAGV là một web app vận hành tập trung dành cho một AGV SEER, giúp người vận hành:

- Xem trạng thái kết nối và trạng thái hoạt động của AGV.
- Theo dõi vị trí, hướng, route và chuyển động trên bản đồ theo thời gian thực.
- Đồng bộ và xem danh sách taskchain có sẵn trên AGV.
- Ghép nhiều taskchain thành workflow có thứ tự và có chính sách xử lý lỗi.
- Chạy, theo dõi, dừng và xử lý workflow.
- Tra cứu lịch sử chạy, lỗi và thao tác vận hành.

NewAGV **không thay thế hoàn toàn RoboshopPRO**. RoboshopPRO tiếp tục là công cụ triển khai/cấu hình cấp thấp cho robot, map, station, route và taskchain. NewAGV tập trung vào trải nghiệm vận hành hằng ngày, workflow cấp ứng dụng, theo dõi realtime và audit.

---

## 3. Current context

### 3.1 Hiện trạng repo

Kiến trúc dự kiến hiện tại:

- `NewAGV.Web`: giao diện Blazor.
- `NewAGV.Api`: facade API và command safety gate.
- `NewAGV.Worker`: kết nối TCP tới AGV SEER.
- `NewAGV.Contracts`: DTO/model dùng chung giữa các process.

Project trước đây được phát triển nhanh nên có rủi ro:

- Business rule nằm lẫn trong UI, controller hoặc Worker.
- DTO bị dùng như domain model.
- Chưa có ownership rõ ràng cho realtime state và workflow execution.
- Chưa có quyết định database.
- Chưa có hợp đồng rõ giữa API và Worker.
- Chưa phân biệt dữ liệu gốc của AGV với dữ liệu riêng của NewAGV.
- Có thể có nhiều nơi cùng gửi command xuống robot.
- Chưa có quy tắc xử lý command timeout, mất kết nối hoặc task không xác định kết quả.

### 3.2 Giả định

- **Giả định:** Giai đoạn đầu chỉ quản lý một AGV.
- **Giả định:** AGV và server NewAGV nằm trong mạng nội bộ có độ trễ thấp.
- **Giả định:** Một taskchain trên AGV có external identifier ổn định hoặc có thể xác định bằng tổ hợp ID/name/version.
- **Giả định:** Có thể truy vấn trạng thái robot và trạng thái task qua giao thức SEER.
- **Giả định:** Có thể lấy map, station và taskchain metadata từ AGV hoặc file export từ RoboshopPRO.
- **Giả định:** Blazor có thể là Interactive Server hoặc WASM; mọi thao tác nghiệp vụ vẫn đi qua `NewAGV.Api`.
- **Giả định:** Trong MVP có ít hơn 20 người dùng đồng thời và tần suất command thấp.

### 3.3 Cần xác minh với SEER/RoboshopPRO

Trước khi khóa API adapter và acceptance criteria, phải xác minh:

1. Phiên bản controller, firmware, RoboshopPRO và Robokit/NetProtocol đang dùng.
2. Cổng TCP, request ID, message framing, encoding và heartbeat.
3. API chính xác để:
   - Lấy pose và robot state.
   - Lấy map hiện tại.
   - Lấy station/node/path.
   - Lấy planned route/current path.
   - Liệt kê taskchain.
   - Chạy taskchain.
   - Query taskchain execution.
   - Cancel/pause/resume taskchain.
   - Stop hoặc emergency stop bằng phần mềm.
4. Taskchain có execution ID riêng hay chỉ có trạng thái robot chung.
5. Command có idempotency/correlation ID do client cung cấp hay không.
6. Khi mất kết nối, taskchain trên AGV có tiếp tục chạy hay tự dừng.
7. Dữ liệu map là file ảnh, occupancy grid, JSON, binary package hay định dạng riêng.
8. Hệ tọa độ map, đơn vị đo, origin, góc quay, chiều trục Y và cách đổi world coordinate sang pixel.
9. Có version/hash/map ID ổn định hay phải tự tính checksum.
10. Taskchain đổi tên, sửa nội dung hoặc xóa có thể phát hiện bằng version/hash hay không.
11. Software stop có phải chức năng an toàn được chứng nhận hay chỉ là operational stop.
12. Tần suất status/pose tối đa an toàn mà controller hỗ trợ.
13. Cơ chế xử lý robot blocked: trạng thái riêng, error code hay chỉ là text message.

---

## 4. Goals

### 4.1 Product goals

- Cung cấp một giao diện đơn giản để người vận hành hiểu AGV đang ở đâu, đang làm gì và có lỗi gì.
- Giảm phụ thuộc vào việc mở RoboshopPRO cho các thao tác vận hành thường ngày.
- Cho phép xây dựng và lưu workflow từ các taskchain đã tồn tại.
- Chạy workflow có kiểm soát, theo dõi từng step và xử lý failure cơ bản.
- Lưu lịch sử chạy và audit để truy nguyên sự cố.
- Có kiến trúc đủ sạch để mở rộng nhưng không over-engineer.

### 4.2 System goals

- Chỉ có một đường ghi command xuống AGV.
- Domain rule có thể unit test mà không cần robot, TCP, database hoặc UI.
- Tách static map data khỏi realtime robot state.
- Tách workflow definition khỏi workflow execution.
- Khởi động lại API/Web không làm mất execution history.
- Khởi động lại Worker phải có quy trình reconcile execution đang dở.
- Có observability đủ để biết command nào được gửi, bởi ai, khi nào và kết quả ra sao.
- Có contract versioning giữa Web, API và Worker.

### 4.3 Success criteria

- Người vận hành mở Dashboard và biết trạng thái kết nối, trạng thái robot và workflow hiện tại trong tối đa 5 giây.
- Pose trên Map được cập nhật mượt, không làm UI treo.
- Một workflow sequence hợp lệ có thể tạo, lưu, chạy và theo dõi từng step.
- Không thể khởi chạy hai workflow đồng thời trên cùng AGV.
- Mọi command quan trọng đều có command ID, correlation ID, operator và audit record.
- Mất kết nối không bị diễn giải sai thành “failed” nếu trạng thái thực tế của AGV chưa biết.
- Có thể chạy E2E happy path và failure path bằng fake SEER simulator.

---

## 5. Non-goals

Không nằm trong MVP:

- Thay thế RoboshopPRO để tạo/chỉnh sửa map chi tiết.
- Thay thế RoboshopPRO để tạo/chỉnh sửa nội dung taskchain cấp thấp.
- Fleet management cho nhiều AGV.
- Traffic control, deadlock resolution giữa nhiều robot.
- Workflow BPMN đầy đủ.
- Parallel steps, arbitrary loops hoặc script do người dùng nhập.
- Safety PLC hoặc chứng nhận functional safety.
- Lưu toàn bộ pose ở tần suất cao vô thời hạn.
- Machine learning, route optimization hoặc dự đoán lỗi.
- Mobile app native.
- Multi-tenant SaaS.
- Điều khiển trực tiếp bằng joystick từ browser.
- Cho phép Web ghi tùy ý vào tham số controller.

---

## 6. User roles

### 6.1 Viewer

Quyền:

- Xem Dashboard, Map, taskchain, workflow và execution history.
- Xem lỗi và event.
- Không chạy, cancel hoặc thay đổi cấu hình.

### 6.2 Operator

Quyền:

- Tất cả quyền Viewer.
- Chạy workflow đã được publish/enable.
- Cancel workflow.
- Xác nhận manual intervention.
- Chạy quick action đã được cho phép.
- Không thay đổi safety setting hoặc AGV endpoint.

### 6.3 Supervisor

Quyền:

- Tất cả quyền Operator.
- Tạo, sửa, version và archive workflow.
- Sync map/taskchain.
- Approve hoặc enable workflow.
- Xử lý execution chờ đối soát.

### 6.4 Administrator

Quyền:

- Tất cả quyền Supervisor.
- Cấu hình endpoint, polling, retention, authentication và safety rules.
- Quản lý user/role.
- Xem dữ liệu kỹ thuật và diagnostics.

### 6.5 Service identity

Dùng cho API/Worker giao tiếp nội bộ:

- Không đăng nhập UI.
- Chỉ có quyền gọi internal API/gRPC đã định nghĩa.
- Credential phải được lưu ngoài source code.

---

## 7. Core workflows của người dùng

### 7.1 Theo dõi AGV

1. User mở Dashboard.
2. Web gọi `GET /api/agv/status`.
3. Web kết nối SignalR.
4. Dashboard hiển thị:
   - Connected/disconnected.
   - Robot mode.
   - Moving/stopped/blocked/error.
   - Battery nếu API hỗ trợ.
   - Map hiện tại.
   - Task/workflow hiện tại.
5. Khi có event mới, UI cập nhật theo sequence.

### 7.2 Đồng bộ map

1. Supervisor chọn `Sync map`.
2. API validate quyền, trạng thái kết nối và lock sync.
3. API gửi command nội bộ tới Worker.
4. Worker lấy map package/metadata từ AGV hoặc import file export.
5. Infrastructure parse tại boundary.
6. Hệ thống tính checksum/version.
7. Lưu map asset vào file storage và metadata vào database.
8. Lưu station/path snapshot gắn với map version.
9. Chỉ sau khi parse và validate thành công mới đánh dấu version là `Ready`.
10. API phát event `map.sync.completed`.
11. Web reload map snapshot.

### 7.3 Đồng bộ taskchain

1. Supervisor chọn `Sync taskchains`.
2. Worker lấy danh sách taskchain từ AGV.
3. Adapter chuyển dữ liệu thô thành `ExternalTaskchainDescriptor`.
4. Hệ thống upsert cache/snapshot.
5. Taskchain không còn trên AGV được đánh dấu `MissingFromSource`, không hard-delete.
6. Workflow validation được chạy lại cho các workflow đang active.
7. UI hiển thị workflow nào bị stale/invalid.

### 7.4 Tạo workflow

1. Supervisor mở Workflow Builder.
2. Chọn taskchain từ danh sách đã sync.
3. Thêm step theo thứ tự.
4. Cấu hình timeout, retry và failure action.
5. Web gửi draft tới API.
6. API validate schema.
7. Application validate domain invariants.
8. Lưu thành workflow draft/version.
9. Khi publish/enable, hệ thống validate lại taskchain reference và map compatibility.

### 7.5 Chạy workflow

1. Operator chọn workflow version cụ thể.
2. Web gửi `POST /api/workflows/{id}/run` kèm idempotency key.
3. API kiểm tra:
   - Quyền.
   - AGV connected.
   - AGV ở mode cho phép.
   - Không có workflow active.
   - Workflow version đang enabled.
   - Map/taskchain snapshot hợp lệ.
   - Không có sync đang chạy.
4. Worker kiểm tra lại state ngay trước khi gửi task đầu tiên.
5. Worker tạo execution và bắt đầu step 1.
6. Worker phát execution/step events.
7. API chuyển event qua SignalR.
8. UI hiển thị timeline và current step.
9. Khi hoàn thành, execution thành `Succeeded`.
10. Khi lỗi, engine áp dụng retry/fallback/manual intervention theo policy.

### 7.6 Mất kết nối giữa workflow

1. Worker phát hiện connection lost.
2. Không tự kết luận step đã failed nếu AGV có thể vẫn đang chạy.
3. Execution chuyển sang `AwaitingReconciliation`.
4. Web hiển thị cảnh báo rõ “Mất kết nối, trạng thái thực tế chưa xác định”.
5. Worker reconnect theo backoff.
6. Sau reconnect:
   - Query robot task/execution hiện tại.
   - Correlate với command/step.
   - Nếu xác định được, tiếp tục hoặc hoàn tất.
   - Nếu không xác định được, chuyển `WaitingForOperator`.
7. Operator chọn hành động cho phép: mark failed, resume after verification, cancel hoặc acknowledge.

---

## 8. Functional requirements

## 8.1 Dashboard

### FR-DASH-001 — Connection summary

Dashboard phải hiển thị:

- AGV ID/name.
- Endpoint alias; không hiển thị credential.
- Connected/disconnected/reconnecting.
- Last successful heartbeat.
- Last status timestamp.
- Worker health.
- API health.

### FR-DASH-002 — Robot state

Hiển thị tối thiểu:

- Idle.
- Moving.
- Stopped.
- Blocked.
- Error.
- Emergency stopped.
- Charging nếu có.
- Unknown.

**Cần xác minh:** mapping chính xác từ SEER status code sang internal enum.

### FR-DASH-003 — Current work

Hiển thị:

- Workflow đang chạy.
- Current step.
- Taskchain đang chạy.
- Elapsed time.
- Retry count.
- Warning/error gần nhất.

### FR-DASH-004 — Quick actions

MVP chỉ cho phép các quick action được cấu hình trước:

- Cancel active workflow.
- Request operational stop.
- Navigate to predefined home taskchain, nếu được cho phép.
- Acknowledge warning.

Không cho nhập command thô từ UI.

---

## 8.2 Map page

### FR-MAP-001 — Load static map

Map page phải load một `MapVersion` ở trạng thái `Ready`.

Static map gồm:

- Map asset.
- World coordinate metadata.
- Stations/nodes.
- Static routes/edges nếu nguồn có cung cấp.
- Version, source và sync timestamp.

### FR-MAP-002 — Realtime pose

Hiển thị robot:

- X, Y.
- Heading/yaw.
- Timestamp.
- Localization quality/confidence nếu có.
- State indicator.
- Velocity nếu có.

### FR-MAP-003 — Coordinate transform

Renderer không được trộn pixel coordinate với robot world coordinate.

Phải có một hàm transform rõ ràng:

```text
World coordinate (meter/radian)
    -> Map coordinate
    -> Pixel coordinate
    -> Viewport coordinate
```

Transform phải xử lý:

- Scale/resolution.
- Origin.
- Rotation.
- Y-axis direction.
- Zoom/pan.
- Device pixel ratio.

### FR-MAP-004 — Stations

Hiển thị station với:

- External ID.
- Name.
- Type.
- Pose.
- Map version.
- Availability/status nếu nguồn có.

### FR-MAP-005 — Current route

Phân biệt:

- Static configured route.
- Current planned path.
- Actual recent trail.
- Workflow logical route.

Không dùng một field `Path` chung cho bốn loại dữ liệu khác nhau.

### FR-MAP-006 — Realtime rendering

- Web không render lại toàn bộ map asset khi chỉ pose thay đổi.
- Static layer được cache.
- Dynamic overlay cập nhật độc lập.
- Pose event có thể được throttle ở client để tránh quá tải.

**Giả định:** Worker có thể nhận pose 5–10 lần/giây và UI chỉ cần render khoảng 5 lần/giây.  
**Cần xác minh:** tần suất an toàn và thực tế từ controller.

### FR-MAP-007 — Sync status

Map page phải hiển thị:

- Map version hiện tại trên AGV.
- Map version đang cache trên NewAGV.
- Trạng thái synced/stale/unknown.
- Last sync time.
- Sync error gần nhất.

---

## 8.3 Taskchains page

### FR-TASK-001 — List taskchains

Danh sách gồm:

- External taskchain ID.
- Name.
- Description nếu có.
- Source AGV.
- Source map nếu có.
- Source version/hash nếu có.
- Last synced time.
- Availability: available/missing/stale/unknown.
- Referenced workflow count.

### FR-TASK-002 — Sync

- Chỉ Supervisor/Admin được sync.
- Chỉ một taskchain sync chạy tại một thời điểm cho mỗi AGV.
- Sync phải có audit.
- Sync thất bại không được xóa snapshot tốt gần nhất.
- Kết quả sync phải có added/updated/missing/unchanged counts.

### FR-TASK-003 — Read-only in MVP

MVP không chỉnh sửa nội dung taskchain.

UI phải ghi rõ taskchain được quản lý từ AGV/RoboshopPRO.

### FR-TASK-004 — Search/filter

Hỗ trợ:

- Search theo ID/name.
- Filter theo available/missing/stale.
- Filter theo workflow đang tham chiếu.

---

## 8.4 Workflow builder

### FR-WF-001 — Create draft

Workflow có:

- Name.
- Description.
- Version.
- Status: Draft/Enabled/Archived.
- Compatible AGV.
- Compatible map version policy.
- Ordered steps.

### FR-WF-002 — Step configuration

Mỗi step có:

- Step ID nội bộ ổn định.
- Display name.
- Order.
- Taskchain reference.
- Timeout.
- Retry policy.
- On-failure action.
- Optional fallback plan.
- Manual intervention message.
- Continue mode sau fallback.

### FR-WF-003 — Validation

Workflow invalid nếu:

- Không có step.
- Trùng step order.
- Taskchain reference thiếu.
- Taskchain đang missing.
- Timeout không hợp lệ.
- Retry count âm hoặc vượt giới hạn hệ thống.
- Fallback rỗng nhưng action là `RunFallback`.
- Fallback tham chiếu vòng.
- Workflow đã enable nhưng không có version bất biến.
- Workflow yêu cầu map không khớp map active.
- Có control flow mà engine version hiện tại không hỗ trợ.

### FR-WF-004 — Versioning

- Mọi lần publish một workflow phải tạo `WorkflowVersion` bất biến.
- Execution luôn tham chiếu version cụ thể.
- Chỉnh draft không làm thay đổi execution cũ.
- Không hard-delete workflow/version đã có execution.
- DELETE public API thực chất archive nếu đã được tham chiếu.

### FR-WF-005 — Concurrency

PUT workflow phải dùng optimistic concurrency:

- `rowVersion`, `ETag` hoặc version number.
- Nếu hai người cùng sửa, request sau nhận `409 Conflict` hoặc `412 Precondition Failed`.

---

## 8.5 Workflow execution

### FR-EXEC-001 — Single active execution

Mỗi `AgvId` chỉ có tối đa một execution ở nhóm trạng thái active:

- Pending.
- Starting.
- Running.
- Pausing.
- Paused.
- WaitingForOperator.
- AwaitingReconciliation.
- Cancelling.

Ràng buộc được kiểm tra:

1. Trong transaction database.
2. Tại API safety gate.
3. Tại Worker ngay trước khi start.

### FR-EXEC-002 — Step lifecycle

Mỗi step execution có:

- Attempt number.
- Start/end time.
- Status.
- Command ID.
- External task execution ID nếu có.
- Result/error code.
- Error detail.
- Timeout deadline.
- Snapshot taskchain metadata.

### FR-EXEC-003 — Retry

Retry chỉ được thực hiện khi:

- Policy cho phép.
- Số attempt chưa vượt giới hạn.
- Step được đánh dấu retry-safe.
- Worker đã xác minh task cũ không còn active hoặc protocol hỗ trợ idempotency.
- AGV đang ở trạng thái phù hợp.

Không được retry mù một command có side effect như nâng/hạ nếu chưa biết command trước có hoàn thành hay không.

### FR-EXEC-004 — Timeout

Timeout có hai lớp:

- **Transport timeout:** không nhận response/ack.
- **Execution timeout:** taskchain không hoàn thành trong thời gian cho phép.

Transport timeout không tự động đồng nghĩa task chưa chạy. Nó có thể dẫn tới `AwaitingReconciliation`.

### FR-EXEC-005 — Fallback

MVP hỗ trợ `FallbackPlan` là một sequence giới hạn:

- Tối đa một cấp fallback.
- Không nested fallback trong fallback.
- Không loop.
- Có giới hạn số step.
- Sau fallback có `ContinueNext`, `RetryPrimary`, `FailWorkflow` hoặc `WaitForOperator`.

Ví dụ:

```text
Primary step: GoToB
On failure:
  1. GoToC
  2. GoToB
After fallback: ContinueNext
```

### FR-EXEC-006 — Manual intervention

Khi policy là `WaitForOperator`:

- Execution chuyển `WaitingForOperator`.
- UI hiển thị reason và hướng dẫn.
- Chỉ Operator/Supervisor được acknowledge.
- Các action được phép phải được engine tính sẵn, không để UI gửi chuỗi tùy ý.
- Mọi action có audit.

### FR-EXEC-007 — Cancel

Cancel gồm:

1. API nhận request và tạo cancel command.
2. Execution chuyển `Cancelling`.
3. Worker gửi cancel/stop tới AGV nếu protocol hỗ trợ.
4. Query lại trạng thái robot/task.
5. Chỉ chuyển `Cancelled` khi đã xác nhận hoặc operator xác nhận theo policy.
6. Nếu không xác định, chuyển `AwaitingReconciliation`.

### FR-EXEC-008 — Pause/resume

Pause/resume chỉ bật nếu API SEER hỗ trợ semantics rõ ràng.

Nếu không hỗ trợ:

- Ẩn action.
- Không giả lập pause bằng cách ngắt kết nối.
- Có thể dùng `WaitingForOperator` ở ranh giới giữa hai step, nhưng không gọi đó là pause taskchain đang chạy.

---

## 8.6 Settings

### FR-SET-001 — AGV connection

Lưu:

- AGV logical ID.
- Display name.
- Host/IP.
- Port/profile.
- Protocol version/profile.
- Connection timeout.
- Heartbeat interval.
- Reconnect policy.

Credential/secret không lưu plain text trong database hoặc source control.

### FR-SET-002 — Sync settings

- Auto-sync map on startup: mặc định off.
- Auto-sync taskchain on startup: cấu hình được.
- Scheduled sync interval: optional.
- Stale threshold.
- Asset directory.

### FR-SET-003 — Safety settings

- Allowed robot modes.
- Require map synced.
- Require taskchain available.
- Max retry count.
- Max workflow duration.
- Allow software stop.
- Allow pause/resume.
- Require Supervisor approval cho workflow enable.
- Manual intervention timeout.

### FR-SET-004 — Settings audit

Mọi thay đổi safety/endpoint phải ghi:

- Old value.
- New value.
- User.
- Timestamp.
- Correlation ID.

Secret value phải được redact.

---

## 8.7 Logs/history

### FR-LOG-001 — Execution history

Filter theo:

- Time range.
- Workflow.
- Version.
- Status.
- Operator.
- Taskchain.
- Error code.

### FR-LOG-002 — Product events

Lưu event có ý nghĩa nghiệp vụ:

- Workflow started/completed/failed/cancelled.
- Step started/completed/failed/retried.
- Fallback entered/completed/failed.
- Connection lost/restored.
- Robot blocked/unblocked.
- Sync completed/failed.
- Manual intervention requested/resolved.

### FR-LOG-003 — Technical logs

Technical log dùng structured logging, không nhất thiết lưu toàn bộ trong business database.

Khuyến nghị:

- Console/file sink cho MVP.
- Centralized logging cho production.
- Correlation bằng `CorrelationId`, `CommandId`, `ExecutionId`, `AgvId`.

---

## 9. Non-functional requirements

## 9.1 Reliability

- Worker phải reconnect bằng exponential backoff có jitter.
- Command queue phải serialize command cho một AGV.
- Không gửi hai command điều hướng cạnh tranh.
- Execution transition phải transactional.
- Event xử lý lặp phải idempotent.
- Sync dùng staging rồi atomic activate version mới.
- Shutdown Worker phải graceful; không bỏ dở DB transaction.
- Sau restart phải reconcile execution active.

## 9.2 Performance

**Mục tiêu MVP, cần đo thực tế:**

- `GET /api/agv/status`: p95 dưới 300 ms trong LAN.
- API query thông thường: p95 dưới 500 ms.
- Realtime event từ Worker tới UI: p95 dưới 1 giây sau khi Worker nhận state.
- Map static asset được cache bằng hash/version.
- UI không nhận pose nhanh hơn tốc độ render cấu hình.
- History query bắt buộc có pagination.

## 9.3 Availability

- Web/API lỗi không được làm Worker mất quyền kiểm soát command đang chạy nếu được deploy riêng.
- Worker mất kết nối không được làm API báo robot idle.
- Health check tách:
  - Process alive.
  - Database ready.
  - Worker connected.
  - AGV responsive.
- “API healthy” không đồng nghĩa “AGV connected”.

## 9.4 Security

- Authentication bắt buộc ngoài môi trường development.
- RBAC theo role.
- TLS cho Web/API và internal gRPC nếu qua mạng.
- Không expose Worker endpoint ra mạng người dùng.
- Validate mọi input tại boundary.
- Không deserialize type tùy ý từ client.
- Không log password/token/secret.
- Chống CSRF theo hosting model của Blazor.
- Rate limit command endpoint.
- Audit các command quan trọng.
- Settings nhạy cảm dùng environment variable, secret store hoặc OS credential store.

## 9.5 Maintainability

- Domain không phụ thuộc framework.
- Không dùng DTO làm entity.
- Mỗi integration có adapter/interface.
- Protocol parser có test fixtures.
- Magic number của SEER phải nằm trong protocol profile, không rải trong code.
- Mọi state transition đi qua một service/state machine thống nhất.
- Migrations được version control.

## 9.6 Observability

Metrics tối thiểu:

- `agv_connection_state`.
- `agv_reconnect_total`.
- `agv_status_age_seconds`.
- `agv_command_total`.
- `agv_command_failed_total`.
- `workflow_execution_total`.
- `workflow_execution_duration_seconds`.
- `workflow_step_retry_total`.
- `signalr_connected_clients`.
- `map_sync_total`.
- `taskchain_sync_total`.

## 9.7 Time and ordering

- Lưu timestamp bằng UTC.
- UI hiển thị theo timezone cấu hình.
- Event có `OccurredAtUtc` và `Sequence`.
- Không dùng clock của browser làm nguồn quyết định timeout.
- Timeout được tính ở Worker/Application runtime.
- Nếu controller có timestamp riêng, lưu cả source timestamp và received timestamp.

---

## 10. System architecture

## 10.1 Logical architecture

```text
┌─────────────────────────────────────────────────────────────┐
│ NewAGV.Web                                                  │
│ Blazor pages/components, client state, map renderer         │
└───────────────────────┬─────────────────────────────────────┘
                        │ HTTPS REST + SignalR
┌───────────────────────▼─────────────────────────────────────┐
│ NewAGV.Api                                                  │
│ Auth, RBAC, REST, SignalR, validation, safety gate          │
│ Public facade; no direct TCP                                │
└───────────────────────┬─────────────────────────────────────┘
                        │ Internal gRPC/IPC
┌───────────────────────▼─────────────────────────────────────┐
│ NewAGV.Worker                                               │
│ Single AGV connection owner, command queue, state polling,  │
│ workflow runtime host, reconnect/reconcile                  │
└───────────────────────┬─────────────────────────────────────┘
                        │ SEER TCP/Robokit protocol
┌───────────────────────▼─────────────────────────────────────┐
│ SEER AGV                                                    │
└─────────────────────────────────────────────────────────────┘

Shared implementation libraries:
- NewAGV.Domain
- NewAGV.Application
- NewAGV.Infrastructure
- NewAGV.Contracts

Persistence:
- Relational database
- Map/file asset storage
- Structured logs
```

## 10.2 Recommended solution structure

```text
src/
  NewAGV.Web/
  NewAGV.Api/
  NewAGV.Worker/
  NewAGV.Contracts/
  NewAGV.Domain/
  NewAGV.Application/
  NewAGV.Infrastructure/

tests/
  NewAGV.Domain.Tests/
  NewAGV.Application.Tests/
  NewAGV.Api.IntegrationTests/
  NewAGV.Worker.Tests/
  NewAGV.Contracts.Tests/
  NewAGV.E2E.Tests/
  NewAGV.Testing.SeerSimulator/

docs/
  product/
  architecture/
  api/
  database/
  workflow/
  validation/
  adr/
```

Nếu chưa muốn tăng số project ngay, có thể tạo các folder `Domain`, `Application`, `Infrastructure` trong một assembly tạm thời. Tuy nhiên dependency rule phải giữ như khi đã tách project.

## 10.3 Dependency rule

```text
Domain
  ↑
Application
  ↑
Infrastructure
  ↑
Api / Worker

Contracts
  ← Api / Worker / Web
```

Quy tắc:

- `NewAGV.Domain` không reference Web, API, Worker, EF Core, SignalR hoặc SEER SDK.
- `NewAGV.Application` reference Domain và định nghĩa port/interface.
- `NewAGV.Infrastructure` implement repository, SEER adapter, file storage.
- `NewAGV.Api` gọi Application use case, không truy cập TCP.
- `NewAGV.Worker` host Application runtime và Infrastructure adapter.
- `NewAGV.Web` chỉ dùng public API contract/client model.
- `NewAGV.Contracts` chứa DTO/event contract, không chứa EF entity hoặc mutable domain entity.
- Mapping giữa DTO và Domain phải explicit.
- Input không tin cậy được parse/validate ở boundary.

## 10.4 Vai trò từng layer

### Web UI layer

Chịu trách nhiệm:

- Routing.
- Page/component.
- Form validation mức UX.
- Map rendering.
- SignalR client.
- Client-side cache ngắn hạn.
- Hiển thị permissions và action availability.

Không chịu trách nhiệm:

- Quyết định workflow transition.
- Safety rule cuối cùng.
- Command retry.
- Kết nối TCP.
- Ghi database trực tiếp.

### API layer

Chịu trách nhiệm:

- Authentication/authorization.
- REST/SignalR contract.
- Request validation.
- Command safety gate.
- Query projection.
- Idempotency key.
- Rate limiting.
- Problem Details.
- Internal Worker client.

Không chứa:

- Protocol parsing.
- Workflow engine chi tiết.
- UI state.
- Long-running TCP loop.

### Application layer

Use cases dự kiến:

- `GetAgvStatus`.
- `SyncMap`.
- `SyncTaskchains`.
- `CreateWorkflow`.
- `UpdateWorkflow`.
- `PublishWorkflowVersion`.
- `ValidateWorkflow`.
- `RunWorkflow`.
- `CancelWorkflowExecution`.
- `ResolveManualIntervention`.
- `ReconcileExecution`.
- `SendAllowedAgvCommand`.

### Domain layer

Chứa:

- Aggregate.
- Value object.
- Enum.
- Invariant.
- State transition.
- Failure policy.
- Domain event.

Không chứa:

- HTTP status code.
- SQL.
- JSON-specific attribute.
- SignalR hub.
- Socket.
- File path tuyệt đối.

### Infrastructure layer

Chứa:

- EF Core DbContext/repository.
- SQLite/PostgreSQL/SQL Server provider.
- SEER TCP adapter.
- Protocol framing/parser.
- Map file storage.
- Clock.
- Structured logging adapter.
- Internal gRPC implementation.
- Authentication integration.

### Worker

Chịu trách nhiệm:

- Sở hữu connection session.
- Poll/subscribe state.
- Chuẩn hóa raw status.
- Single-writer command queue.
- Gửi command và correlate response.
- Host workflow execution loop.
- Persist execution state theo ownership rule.
- Emit normalized event.
- Reconnect/reconcile.

Worker không:

- Render UI.
- Tự quyết định text hiển thị.
- Bỏ qua safety gate.
- Nhận command tùy ý từ browser.

## 10.5 API–Worker communication

### Khuyến nghị MVP

Dùng **internal gRPC** hoặc typed internal HTTP API:

- API gọi Worker cho command.
- Worker cung cấp health/state snapshot.
- Worker stream normalized events về API hoặc API subscribe internal stream.
- Endpoint chỉ bind localhost hoặc internal network.
- Dùng service authentication nếu khác máy.

Không khuyến nghị thêm RabbitMQ/Kafka trong MVP.

### Khi cần durable messaging

Bổ sung outbox/inbox khi:

- API và Worker chạy khác máy.
- Command không được phép mất khi process restart.
- Có nhiều AGV/Worker.
- Cần retry delivery độc lập.

Trong MVP local pilot, database record + synchronous internal call + idempotency đủ đơn giản.

## 10.6 Ownership ghi dữ liệu

Để tránh hai process ghi chồng nhau:

- API sở hữu:
  - Workflow definition/version.
  - User settings.
  - User-triggered command request.
  - Audit request.
- Worker sở hữu:
  - AGV current state projection.
  - Workflow execution.
  - Step execution.
  - Execution event.
  - Sync result do Worker thực hiện.
- Cả hai chỉ append audit thông qua một service thống nhất.

---

## 11. Domain model

## 11.1 Aggregate overview

```text
Agv
├── AgvConnectionState
├── AgvOperationalState
├── ActiveMapReference
└── CurrentTaskReference

MapDefinition
└── MapVersion
    ├── MapAsset
    ├── Station[]
    └── StaticRoute[]

ExternalTaskchain
└── TaskchainSnapshot[]

WorkflowDefinition
└── WorkflowVersion
    └── WorkflowStep[]
        ├── TaskchainReference
        ├── RetryPolicy
        ├── TimeoutPolicy
        └── FailurePolicy
            └── FallbackPlan

WorkflowExecution
└── WorkflowStepExecution[]
    └── StepAttempt[]
        └── ExecutionEvent[]
```

## 11.2 WorkflowDefinition

Đại diện identity và metadata dài hạn của workflow.

```text
WorkflowDefinition
- Id: Guid
- Name: string
- Description: string?
- Status: Draft | Enabled | Archived
- CurrentDraftVersionNumber: int
- LatestPublishedVersionId: Guid?
- CompatibleAgvId: string
- CreatedAtUtc: DateTimeOffset
- CreatedBy: UserId
- UpdatedAtUtc: DateTimeOffset
- UpdatedBy: UserId
- ConcurrencyToken: string/rowversion
```

Quy tắc:

- Name bắt buộc, giới hạn độ dài.
- Archive thay vì hard-delete nếu có published version hoặc execution.
- Chỉ published version được run.
- `Enabled` không có nghĩa mọi version đều runnable; API run phải chỉ rõ version hoặc lấy latest enabled version.

## 11.3 WorkflowVersion

Snapshot bất biến của workflow khi publish.

```text
WorkflowVersion
- Id: Guid
- WorkflowDefinitionId: Guid
- VersionNumber: int
- EngineSchemaVersion: int
- DefinitionJson: optional canonical snapshot
- ValidationStatus: Valid | Invalid | Stale
- ValidationErrors: collection
- CompatibleMapPolicy
- PublishedAtUtc
- PublishedBy
- ContentHash
```

Khuyến nghị lưu normalized relational rows và có thêm canonical JSON snapshot để audit/migration dễ hơn. JSON snapshot không thay thế hoàn toàn relational model trong MVP nếu cần query step/taskchain reference.

## 11.4 WorkflowStep

```text
WorkflowStep
- Id: Guid
- WorkflowVersionId: Guid
- Order: int
- Name: string
- TaskchainReference: TaskchainReference
- TimeoutPolicy: TimeoutPolicy
- RetryPolicy: RetryPolicy
- FailurePolicy: FailurePolicy
- IsEnabled: bool
```

Invariant:

- Order duy nhất trong version.
- Step không được sửa sau publish.
- Taskchain reference không null.
- Policy phải pass validation.

## 11.5 TaskchainReference

Workflow không copy logic taskchain cấp thấp. Nó chỉ tham chiếu nguồn ngoài và lưu snapshot metadata.

```text
TaskchainReference
- SourceAgvId
- ExternalTaskchainId
- ExpectedName
- ExpectedSourceVersion?
- ExpectedContentHash?
- RequiredMapExternalId?
- BindingMode:
    ExactVersion
    LatestCompatible
    ByExternalId
```

Khuyến nghị MVP dùng:

- `ByExternalId` nếu SEER không cung cấp version/hash ổn định.
- Lưu `ExpectedName` và snapshot để cảnh báo thay đổi.
- Trước mỗi lần run, resolve reference sang taskchain snapshot hiện tại.

Nếu taskchain bị đổi/xóa:

- Xóa: workflow validation thành `Invalid` hoặc `Stale`; không được run.
- Đổi name nhưng ID giữ nguyên: cảnh báo metadata changed; có thể vẫn run sau khi revalidate.
- Đổi content/version: `ExactVersion` thì block; `LatestCompatible` thì yêu cầu revalidation.
- Không tự động sửa workflow im lặng.

## 11.6 RetryPolicy

```text
RetryPolicy
- MaxAttempts: int           // gồm cả lần đầu hoặc quy ước rõ
- Delay: TimeSpan
- BackoffMode: Fixed | Linear | Exponential
- MaxDelay: TimeSpan
- RetryOnErrorCodes: string[]
- RetrySafetyClass:
    Never
    ReconcileThenRetry
    SafeIdempotent
```

Giới hạn MVP:

- MaxAttempts cấu hình hệ thống, ví dụ tối đa 3.
- Không cho custom script.
- Retry decision phải dựa trên normalized error category.

## 11.7 TimeoutPolicy

```text
TimeoutPolicy
- StartAckTimeout
- ExecutionTimeout
- ReconciliationTimeout
- OnTimeout:
    Fail
    Retry
    RunFallback
    WaitForOperator
```

Không dùng một timeout duy nhất cho mọi giai đoạn.

## 11.8 FailurePolicy

```text
FailurePolicy
- OnFailure:
    FailWorkflow
    RetryThenFail
    RunFallback
    WaitForOperator
- RetryPolicy
- FallbackPlan?
- AfterFallback:
    ContinueNext
    RetryPrimary
    FailWorkflow
    WaitForOperator
```

## 11.9 FallbackPlan

```text
FallbackPlan
- Id
- Steps: FallbackStep[]
- MaxDuration
- ContinueMode
```

`FallbackStep` cũng tham chiếu taskchain nhưng MVP không chứa fallback lồng nhau.

Validation:

- Không tham chiếu chính nó theo cách tạo cycle.
- Không quá giới hạn step.
- Taskchain phải available.
- Map compatibility phải hợp lệ.

## 11.10 WorkflowExecution

Đại diện một lần chạy cụ thể.

```text
WorkflowExecution
- Id: Guid
- WorkflowDefinitionId
- WorkflowVersionId
- AgvId
- Status: WorkflowStatus
- StatusReason
- RequestedBy
- RequestedAtUtc
- StartedAtUtc?
- EndedAtUtc?
- CurrentStepExecutionId?
- CorrelationId
- IdempotencyKey
- WorkerInstanceId
- LastHeartbeatAtUtc
- ConcurrencyToken
```

Khác biệt:

- Definition/version mô tả **sẽ chạy gì**.
- Execution mô tả **một lần đã/đang chạy**, với thời gian, trạng thái và kết quả.
- Sửa definition không được thay đổi execution.
- Execution phải giữ snapshot cần thiết để điều tra sau này.

## 11.11 WorkflowStepExecution

```text
WorkflowStepExecution
- Id
- WorkflowExecutionId
- WorkflowStepId
- StepOrder
- StepNameSnapshot
- TaskchainSnapshotJson
- Status: StepStatus
- AttemptCount
- StartedAtUtc?
- EndedAtUtc?
- ErrorCategory?
- ErrorCode?
- ErrorMessage?
- IsFallback
- ParentPrimaryStepExecutionId?
```

## 11.12 StepAttempt

Nên có nếu retry là chức năng chính:

```text
StepAttempt
- Id
- WorkflowStepExecutionId
- AttemptNumber
- CommandId
- ExternalExecutionId?
- Status
- SentAtUtc?
- AckAtUtc?
- StartedAtUtc?
- EndedAtUtc?
- ResultCode?
- RawResponseReference?
```

Không overwrite attempt cũ khi retry.

## 11.13 ExecutionEvent

Append-only:

```text
ExecutionEvent
- Id
- WorkflowExecutionId
- StepExecutionId?
- Sequence
- EventType
- OccurredAtUtc
- ReceivedAtUtc
- Source: Api | Worker | Agv | Operator
- Severity
- Message
- DataJson
- CorrelationId
```

Event table dùng cho timeline/audit nghiệp vụ, không phải event sourcing hoàn chỉnh trong MVP.

## 11.14 WorkflowStatus

Public statuses bắt buộc:

- `Pending`
- `Running`
- `Succeeded`
- `Failed`
- `Cancelled`
- `Paused`

Internal/transient statuses nên có:

- `Starting`
- `Pausing`
- `WaitingForOperator`
- `AwaitingReconciliation`
- `Cancelling`

Terminal:

- Succeeded.
- Failed.
- Cancelled.

Không terminal:

- Các trạng thái còn lại.

## 11.15 StepStatus

- Pending.
- Dispatching.
- Running.
- Retrying.
- RunningFallback.
- WaitingForOperator.
- AwaitingReconciliation.
- Succeeded.
- Failed.
- Skipped.
- Cancelled.

## 11.16 State transition examples

### Workflow happy path

```text
Pending -> Starting -> Running -> Succeeded
```

### Failure không fallback

```text
Running -> Failed
```

### Retry

```text
Running
  -> Step Failed
  -> Retrying
  -> Running
  -> Succeeded/Failed
```

### Manual intervention

```text
Running -> WaitingForOperator -> Running/Failed/Cancelled
```

### Connection lost

```text
Running -> AwaitingReconciliation
  -> Running
  -> WaitingForOperator
  -> Failed/Cancelled
```

Mọi transition không hợp lệ phải bị reject và ghi technical warning.

---

## 12. Map model

## 12.1 MapDefinition

Identity logic của một map.

```text
MapDefinition
- Id
- AgvId
- ExternalMapId?
- Name
- Source: Agv | RoboshopExport | ManualImport
- ActiveVersionId?
- CreatedAtUtc
```

## 12.2 MapVersion

Bất biến sau khi sync/import thành công.

```text
MapVersion
- Id
- MapDefinitionId
- ExternalVersion?
- ContentHash
- SourceTimestamp?
- SyncedAtUtc
- Status: Staging | Ready | Failed | Superseded
- Format
- Resolution
- Width/Height
- OriginX/OriginY
- OriginYaw
- YAxisDirection
- AssetStorageKey
- MetadataJson
```

## 12.3 MapAsset

Khuyến nghị lưu file ngoài database:

```text
MapAsset
- StorageKey
- FileName
- ContentType
- Size
- Sha256
- CreatedAtUtc
```

MVP:

- File system local.
- Thư mục theo `map/{mapId}/{versionId}/`.
- Ghi file tạm, validate checksum, sau đó rename atomic.
- DB chỉ lưu path tương đối/storage key.

Production:

- Shared file storage hoặc object storage nếu nhiều instance.
- Không lưu absolute path phụ thuộc máy.

Không khuyến nghị lưu map binary lớn dạng BLOB trong SQLite ở MVP, trừ khi file rất nhỏ và deployment yêu cầu một file duy nhất.

## 12.4 Station

```text
Station
- Id
- MapVersionId
- ExternalStationId
- Name
- Type
- X
- Y
- Yaw
- MetadataJson
```

Station thuộc `MapVersion`, không chỉ thuộc `MapDefinition`, vì vị trí có thể thay đổi giữa version.

## 12.5 StaticRoute

Nếu nguồn cung cấp:

```text
StaticRoute
- Id
- MapVersionId
- ExternalRouteId?
- FromStationId?
- ToStationId?
- GeometryJson/Polyline
- Directionality
- MetadataJson
```

## 12.6 RobotPose

Realtime state, mặc định không persist từng sample:

```text
RobotPose
- AgvId
- MapExternalId?
- X
- Y
- Yaw
- LinearVelocity?
- AngularVelocity?
- LocalizationQuality?
- SourceTimestamp?
- ReceivedAtUtc
- Sequence
```

Lưu trong memory/state store và broadcast.

Nếu cần lịch sử:

- Sampling 1 Hz hoặc theo distance threshold.
- Retention 7–30 ngày.
- Partition/cleanup job.
- Đây là phase sau, không bật mặc định.

## 12.7 PlannedPath

```text
PlannedPath
- AgvId
- MapVersionId?
- ExternalTaskExecutionId?
- Points[]
- GeneratedAtUtc
- Sequence
- Source
```

Dữ liệu ephemeral. Chỉ persist nếu cần điều tra.

## 12.8 CoordinateTransform

```text
CoordinateTransform
- Resolution
- OriginX
- OriginY
- OriginYaw
- FlipY
- ImageHeight
```

Viết một module test được với các fixture:

- World origin.
- Góc 0/90/180.
- Y flip.
- Zoom/pan.
- Station và robot overlay trùng điểm kỳ vọng.

---

## 13. Data ownership and source of truth

| Dữ liệu | Source of truth | NewAGV lưu gì | Cho phép sửa ở NewAGV MVP |
|---|---|---|---|
| Robot pose | AGV | Current projection in-memory | Không |
| Robot operational state | AGV | Current projection + product events | Không |
| Active low-level task | AGV | Projection/reference | Không |
| Map content | AGV/RoboshopPRO | Versioned cache + asset | Không |
| Station/node | AGV/RoboshopPRO | Snapshot theo map version | Không |
| Static route | AGV/RoboshopPRO | Snapshot nếu lấy được | Không |
| Taskchain logic | AGV/RoboshopPRO | Reference + metadata snapshot | Không |
| Taskchain availability | AGV | Cache/sync status | Không |
| Workflow definition | NewAGV DB | Toàn bộ | Có |
| Workflow version | NewAGV DB | Immutable snapshot | Publish mới, không sửa version cũ |
| Workflow execution | NewAGV DB + AGV event | Execution/step/attempt/event | Chỉ qua command hợp lệ |
| Retry/fallback policy | NewAGV DB | Toàn bộ | Có theo role |
| App settings | NewAGV DB/config | Toàn bộ | Có theo role |
| AGV secret | Secret store | Reference | Không hiển thị |
| Operator audit | NewAGV DB | Append-only | Không sửa |
| Technical log | Logging system | Structured log | Không qua UI bình thường |
| Telemetry history | Không có trong MVP | Optional sampled data | Không |

Nguyên tắc:

- Cache không được giả vờ là nguồn gốc.
- Mọi cache record cần `SyncedAtUtc`, source ID và hash/version nếu có.
- Khi không biết dữ liệu còn khớp nguồn hay không, trạng thái là `Unknown/Stale`, không phải `Synced`.

---

## 14. Database recommendation

## 14.1 Kết luận

**Có, NewAGV nên dùng database từ đầu.**

Lý do:

- Workflow phải được lưu và version.
- Execution phải tồn tại qua restart.
- Retry/fallback cần state bền vững.
- Audit là yêu cầu vận hành thực tế.
- Taskchain/map cache cần biết phiên bản và thời điểm sync.
- Không có DB sẽ dẫn tới file JSON rời rạc, race condition, mất lịch sử và khó migration; đó chỉ là database tự chế nhưng kém hơn.

## 14.2 Nếu không có database, hệ thống mất gì

- Workflow mất sau deploy/restart nếu chỉ ở memory.
- Không có execution history đáng tin cậy.
- Không biết taskchain/map cache nào đã sync lúc nào.
- Không có optimistic concurrency khi nhiều user sửa workflow.
- Không có audit đầy đủ.
- Khó đảm bảo một workflow active duy nhất.
- Khó reconcile sau Worker restart.
- Không thể query lịch sử hoặc phân tích lỗi ổn định.
- Dễ phát sinh nhiều file JSON không có transaction.
- Khó migration schema và backup.

Không DB chỉ chấp nhận cho prototype Phase 2 chỉ đọc status/map, không lưu workflow.

## 14.3 SQLite

Phù hợp khi:

- Một server/máy vận hành.
- API và Worker cùng máy.
- Một AGV.
- Ít user.
- Tần suất ghi thấp.
- Cần cài đặt đơn giản.

Ưu điểm:

- Không cần DB server.
- Backup đơn giản.
- EF Core hỗ trợ tốt.
- Đủ cho pilot.

Nhược điểm:

- Single-writer locking.
- Không phù hợp nhiều instance.
- Khó HA.
- Concurrent write giữa API/Worker cần cẩn thận.
- Một số constraint/index nâng cao hạn chế hơn PostgreSQL.

Cấu hình:

- Bật WAL mode.
- Transaction ngắn.
- Không ghi pose tần suất cao.
- Có retry cho `database is locked`.
- Backup theo snapshot an toàn, không copy bừa file đang ghi.

## 14.4 PostgreSQL

Khuyến nghị mặc định cho production mới khi:

- Nhiều user/trạm.
- API và Worker tách máy.
- Có nhiều AGV tương lai.
- Cần robust concurrency.
- Cần partial index, JSONB, monitoring và backup tốt.

Ưu điểm:

- Concurrency tốt.
- Open source.
- Constraint/index mạnh.
- JSONB hữu ích cho metadata/protocol payload.
- Dễ containerize.

Nhược điểm:

- Cần vận hành DB server.
- Backup/monitoring phức tạp hơn SQLite.

## 14.5 SQL Server

Chọn khi:

- Công ty đã có SQL Server.
- Team vận hành quen Microsoft stack.
- Có yêu cầu AD, backup và monitoring theo chuẩn hiện hữu.

Không chọn chỉ vì project viết bằng C#; ngôn ngữ không phải lý do đủ để trả license và công vận hành.

## 14.6 Khuyến nghị theo phase

| Phase | Database |
|---|---|
| Phase 1–3 local prototype | SQLite |
| Phase 4–6 single-machine pilot | SQLite vẫn chấp nhận |
| Production nhiều user hoặc tách host | PostgreSQL |
| Doanh nghiệp chuẩn hóa Microsoft | SQL Server |
| Multi-AGV/multi-site | PostgreSQL hoặc SQL Server, không SQLite |

## 14.7 Entity/table đề xuất

- `Agvs`
- `AgvStateSnapshots`
- `MapDefinitions`
- `MapVersions`
- `MapAssets`
- `Stations`
- `StaticRoutes`
- `TaskchainSnapshots`
- `WorkflowDefinitions`
- `WorkflowVersions`
- `WorkflowSteps`
- `FallbackPlans`
- `FallbackSteps`
- `WorkflowExecutions`
- `WorkflowStepExecutions`
- `StepAttempts`
- `ExecutionEvents`
- `CommandRequests`
- `AuditLogs`
- `AppSettings`
- `SyncRuns`

## 14.8 Constraint/index quan trọng

- Unique `(AgvId, ExternalTaskchainId, SourceVersion/ContentHash)`.
- Unique `(WorkflowDefinitionId, VersionNumber)`.
- Unique `(WorkflowVersionId, StepOrder)`.
- Unique `(WorkflowExecutionId, Sequence)` cho execution event.
- Unique idempotency key theo command scope.
- Index history theo `(AgvId, RequestedAtUtc)`.
- Index execution theo `(Status, StartedAtUtc)`.
- Index taskchain theo `(AgvId, Availability)`.
- FK delete restricted cho version/execution.
- Concurrency token cho mutable aggregate.
- Một active execution/AGV:
  - PostgreSQL: partial unique index.
  - SQLite: transaction + lock row/application guard.

## 14.9 Retention

MVP:

- Workflow execution: giữ vô thời hạn hoặc ít nhất 1 năm theo chính sách.
- Audit: tối thiểu 1 năm, cần xác nhận yêu cầu doanh nghiệp.
- Product event: 90–365 ngày.
- Technical log: 14–30 ngày.
- Realtime pose: không persist.
- Optional telemetry: 7–30 ngày.

Retention phải cấu hình và có cleanup job; không để file/database lớn dần như một thí nghiệm xã hội.

---

## 15. API design

## 15.1 Convention

- Base: `/api`.
- JSON UTF-8.
- Date/time ISO 8601 UTC.
- Error theo RFC 7807 `ProblemDetails`.
- Pagination: `pageSize`, `cursor` hoặc `page`.
- Command endpoint nhận `Idempotency-Key`.
- Response có `Correlation-Id`.
- Dùng `ETag`/`If-Match` cho update workflow.
- Không expose raw SEER packet ở public API.

## 15.2 Query endpoints

| Method | Endpoint | Loại | Mô tả |
|---|---|---|---|
| GET | `/api/agv/status` | Query | Current normalized AGV status |
| GET | `/api/agv/health` | Query | Worker/connection readiness |
| GET | `/api/maps` | Query | Map definitions/versions |
| GET | `/api/maps/{id}` | Query | Map metadata/version |
| GET | `/api/maps/{id}/asset` | Query | Static map asset |
| GET | `/api/maps/{id}/stations` | Query | Stations |
| GET | `/api/taskchains` | Query | Taskchain snapshots |
| GET | `/api/taskchains/{id}` | Query | Taskchain metadata |
| GET | `/api/workflows` | Query | Workflow list |
| GET | `/api/workflows/{id}` | Query | Definition/draft |
| GET | `/api/workflows/{id}/versions` | Query | Version list |
| GET | `/api/workflow-versions/{id}` | Query | Immutable version |
| GET | `/api/workflows/{id}/validation` | Query | Validation result |
| GET | `/api/workflow-executions` | Query | Execution history |
| GET | `/api/workflow-executions/{id}` | Query | Execution detail |
| GET | `/api/workflow-executions/{id}/events` | Query | Timeline |
| GET | `/api/audit-logs` | Query | Audit history |
| GET | `/api/settings` | Query | Sanitized settings |

## 15.3 Command endpoints

| Method | Endpoint | Loại | Mô tả |
|---|---|---|---|
| POST | `/api/maps/sync` | Command | Start map sync |
| POST | `/api/maps/import` | Command | Import approved export file |
| POST | `/api/taskchains/sync` | Command | Sync taskchain |
| POST | `/api/workflows` | Command | Create draft |
| PUT | `/api/workflows/{id}` | Command | Update draft |
| DELETE | `/api/workflows/{id}` | Command | Archive/delete draft |
| POST | `/api/workflows/{id}/validate` | Command | Validate draft/version |
| POST | `/api/workflows/{id}/publish` | Command | Create immutable version |
| POST | `/api/workflows/{id}/enable` | Command | Enable runnable workflow |
| POST | `/api/workflows/{id}/run` | Command | Run published version |
| POST | `/api/workflow-executions/{id}/cancel` | Command | Request cancel |
| POST | `/api/workflow-executions/{id}/pause` | Command | Pause nếu supported |
| POST | `/api/workflow-executions/{id}/resume` | Command | Resume nếu supported |
| POST | `/api/workflow-executions/{id}/interventions/{interventionId}/resolve` | Command | Resolve manual action |
| POST | `/api/agv/commands/stop` | Command | Operational stop |
| POST | `/api/agv/commands/emergency-stop` | Command | Chỉ nếu được xác minh an toàn |
| PUT | `/api/settings/{key}` | Command | Update setting |

## 15.4 Run workflow request

```json
{
  "workflowVersionId": "guid",
  "agvId": "agv-01",
  "requestedStartMode": "Immediate",
  "note": "Optional operator note"
}
```

Header:

```text
Idempotency-Key: 7d...
```

Response `202 Accepted` hoặc `201 Created`:

```json
{
  "executionId": "guid",
  "status": "Pending",
  "statusUrl": "/api/workflow-executions/{id}"
}
```

## 15.5 Safety gate response

Ví dụ `409 Conflict`:

```json
{
  "type": "https://newagv/errors/active-workflow-exists",
  "title": "AGV đang có workflow hoạt động",
  "status": 409,
  "code": "ACTIVE_WORKFLOW_EXISTS",
  "correlationId": "...",
  "details": {
    "activeExecutionId": "..."
  }
}
```

## 15.6 Status codes

- `200 OK`: query/update đồng bộ thành công.
- `201 Created`: tạo resource.
- `202 Accepted`: command dài đã được nhận.
- `400 Bad Request`: parse/schema invalid.
- `401 Unauthorized`.
- `403 Forbidden`.
- `404 Not Found`.
- `409 Conflict`: state conflict/safety gate.
- `412 Precondition Failed`: ETag mismatch.
- `422 Unprocessable Entity`: domain validation fail.
- `429 Too Many Requests`.
- `503 Service Unavailable`: Worker/AGV unavailable.
- `504 Gateway Timeout`: internal call timeout, không khẳng định robot chưa nhận command.

## 15.7 Command envelope nội bộ

```text
AgvCommandEnvelope
- CommandId
- CorrelationId
- IdempotencyKey
- AgvId
- CommandType
- RequestedBy
- RequestedAtUtc
- ExpiresAtUtc
- ExpectedAgvStateSequence?
- ExecutionId?
- StepExecutionId?
- Payload
```

Worker phải revalidate:

- Command chưa hết hạn.
- State sequence không quá stale nếu được cung cấp.
- AGV connected.
- Không có conflicting command.
- Command type được allowlist.

## 15.8 Emergency stop endpoint

Không mặc định gọi nút Web là “Emergency Stop” trừ khi đã xác minh:

- Controller hỗ trợ command.
- Đường truyền và phần mềm đáp ứng yêu cầu safety.
- Hệ thống được đánh giá/certify phù hợp.

Trong MVP nên dùng tên **Operational Stop** hoặc **Request Stop**. Physical E-stop vẫn là cơ chế ưu tiên và độc lập.

---

## 16. Realtime design

## 16.1 SignalR hub

Endpoint:

```text
/hubs/realtime
```

Group:

- `agv:{agvId}`
- `execution:{executionId}`
- `map:{mapId}` nếu cần

## 16.2 Event types

```text
agv.connection.updated
agv.status.updated
agv.pose.updated
agv.path.updated
workflow.execution.updated
workflow.step.updated
workflow.intervention.requested
execution.event.appended
map.sync.updated
taskchain.sync.updated
system.alert.raised
```

## 16.3 Event envelope

```json
{
  "eventType": "agv.pose.updated",
  "schemaVersion": 1,
  "eventId": "guid",
  "sequence": 12345,
  "agvId": "agv-01",
  "occurredAtUtc": "2026-06-25T08:00:00Z",
  "receivedAtUtc": "2026-06-25T08:00:00.080Z",
  "correlationId": null,
  "payload": {}
}
```

## 16.4 Reconnect behavior

Client không giả định SignalR giao mọi event đúng một lần.

Khi reconnect:

1. Lấy `GET /api/agv/status`.
2. Nếu đang xem execution, lấy execution detail/events mới nhất.
3. So sánh sequence.
4. Tiếp tục subscribe.
5. Nếu sequence gap, reload snapshot.

## 16.5 Throttling

- Worker giữ state mới nhất.
- Pose event có thể coalesce.
- Status transition và error không được drop.
- API/Web có separate channel:
  - High-frequency pose.
  - Low-frequency critical state.
- Không ghi mỗi pose vào DB.

## 16.6 Backpressure

Nếu client chậm:

- Bỏ intermediate pose, giữ latest.
- Không bỏ workflow state transition.
- Giới hạn queue per connection.
- Disconnect client quá chậm nếu cần, client tự resync.

---

## 17. UI/UX pages

## 17.1 Dashboard/Home

### Thành phần

- Connection card.
- Robot state card.
- Current workflow card.
- Map thumbnail.
- Latest alerts.
- Quick actions.
- Worker/database health indicator cho Admin.

### UX rule

- Trạng thái quan trọng dùng text + icon, không chỉ màu.
- Disconnected phải nổi bật.
- `Unknown` không được hiển thị như Idle.
- Command button disabled kèm lý do cụ thể.

## 17.2 Map page

Layout đề xuất:

```text
┌────────────────────────────────────────────────────────────┐
│ Toolbar: map version | sync state | zoom | layers          │
├───────────────────────────────────────┬────────────────────┤
│                                       │ Robot details      │
│ Map canvas                            │ Current workflow    │
│ - static map                          │ Selected station    │
│ - stations                            │ Alerts              │
│ - route                               │                    │
│ - robot pose                          │                    │
└───────────────────────────────────────┴────────────────────┘
```

Layers bật/tắt:

- Map.
- Stations.
- Static route.
- Planned path.
- Recent trail.
- Labels.

## 17.3 Taskchains page

- Table/list.
- Sync button.
- Last sync summary.
- Search/filter.
- Drawer chi tiết metadata.
- Badge referenced/missing/stale.
- Link đến workflow đang dùng taskchain.

## 17.4 Workflow builder

MVP ưu tiên danh sách tuần tự hơn canvas graph.

Layout:

- Bên trái: taskchain catalog.
- Giữa: ordered step list.
- Bên phải: step policy editor.
- Footer: validation errors, save draft, publish.

Drag/drop là tiện ích, không phải yêu cầu lõi. Phải có nút Add/Move Up/Move Down để dễ test và accessibility.

## 17.5 Workflow run/execution page

- Workflow name/version.
- Status banner.
- Current step.
- Timeline.
- Attempt/retry count.
- Fallback branch.
- Error detail.
- Cancel/pause/resume nếu allowed.
- Manual intervention panel.
- Link tới map với current route.

## 17.6 Settings page

Tabs:

- Connection.
- Sync.
- Safety.
- Storage/database.
- Realtime.
- Authentication.
- Diagnostics.

Các setting nguy hiểm yêu cầu:

- Role Admin.
- Confirm.
- Audit.
- Có thể yêu cầu restart rõ ràng.

## 17.7 Logs/history page

Tabs:

- Workflow executions.
- Product events.
- Operator audit.
- Sync history.
- Diagnostics cho Admin.

Không hiển thị raw packet mặc định. Raw protocol diagnostics chỉ bật trong debug mode và phải redact.

---

## 18. Safety rules

## 18.1 Hai lớp safety gate

### API gate

Kiểm tra:

- Auth/RBAC.
- Request validation.
- Worker available.
- AGV connection state không stale.
- Workflow valid.
- No active execution.
- Map/taskchain sync state.
- Rate limit.
- Idempotency.

### Worker gate

Kiểm tra lại ngay trước khi send:

- Connection thật sự active.
- Robot mode.
- E-stop/fault.
- Command queue state.
- Active low-level task.
- Expected state sequence.
- Execution ownership.
- Command expiry.

API gate tốt không loại bỏ Worker gate; state có thể thay đổi giữa hai thời điểm.

## 18.2 Command priority

Đề xuất:

1. Physical E-stop: ngoài hệ thống.
2. Verified emergency/software stop.
3. Cancel/operational stop.
4. Recovery/manual intervention.
5. Workflow task command.
6. Sync/query.

Command queue phải ưu tiên stop/cancel nhưng không chen packet làm hỏng protocol framing.

## 18.3 Start workflow preconditions

Không start khi:

- AGV disconnected/reconnecting.
- Status quá stale.
- AGV đang error/e-stop/manual mode không cho phép.
- Map active không xác định.
- Workflow invalid/stale.
- Taskchain missing.
- Có execution active.
- Worker đang reconcile.
- Sync làm thay đổi snapshot.
- DB không writable.
- Safety setting chưa load.

## 18.4 One active workflow

Tại một AGV:

- Chỉ một workflow active.
- Quick command di chuyển không được chạy cạnh workflow.
- Read-only query vẫn được phép.
- Stop/cancel được phép theo priority.

## 18.5 Audit

Audit bắt buộc cho:

- Run/cancel/pause/resume.
- Operational stop/emergency stop request.
- Resolve intervention.
- Sync.
- Workflow publish/enable/archive.
- Safety/endpoint changes.

## 18.6 Safety disclaimer

NewAGV là lớp vận hành phần mềm. Nó không tự động trở thành hệ thống safety-rated. Các chức năng dừng khẩn, vùng an toàn, bumper, lidar safety và PLC phải tuân theo thiết kế/chứng nhận của hệ thống robot thực tế.

---

## 19. Error and failure handling

## 19.1 Error categories

Chuẩn hóa lỗi thành:

- `ConnectionLost`
- `TransportTimeout`
- `ProtocolError`
- `CommandRejected`
- `RobotBusy`
- `RobotBlocked`
- `RobotFault`
- `EmergencyStopped`
- `LocalizationLost`
- `MapMismatch`
- `TaskchainMissing`
- `TaskExecutionFailed`
- `ExecutionTimeout`
- `ValidationFailed`
- `PersistenceFailed`
- `UnknownExternalError`

Raw SEER code được lưu thêm, không dùng trực tiếp làm domain enum.

## 19.2 Failure matrix

| Tình huống | Hành vi |
|---|---|
| AGV disconnected trước run | Reject command |
| AGV disconnect giữa step | AwaitingReconciliation |
| Send timeout, không biết AGV nhận chưa | Không retry mù; reconcile |
| Taskchain trả failed rõ ràng | Áp dụng failure policy |
| Robot blocked | Giữ Running/Blocked theo policy; timeout nếu kéo dài |
| Map mismatch | Block start hoặc pause trước step |
| Taskchain bị xóa | Workflow Invalid/Stale |
| Worker restart | Load active execution, reconnect, reconcile |
| API restart | Web reconnect; Worker tiếp tục nếu deploy riêng |
| DB write fail trước send | Không send |
| DB write fail sau send | Critical alert + reconcile; không giả định rollback robot |
| SignalR mất | UI fetch snapshot |
| Duplicate run request | Trả execution cũ theo idempotency key |
| Cancel timeout | AwaitingReconciliation |
| Fallback fail | Fail hoặc WaitForOperator theo policy |

## 19.3 Write-before-send rule

Trước command có side effect:

1. Tạo command/attempt record.
2. Commit trạng thái `Dispatching`.
3. Gửi command.
4. Ghi ack/result.

Nếu database không ghi được, không gửi command mới.

Nếu gửi được nhưng ghi kết quả thất bại:

- Raise critical event.
- Không resend tự động.
- Reconcile từ AGV.

## 19.4 Raw payload retention

- Không lưu mọi status packet.
- Lưu raw request/response cho command quan trọng có giới hạn kích thước.
- Có retention.
- Redact secret.
- Có thể lưu file diagnostics riêng thay vì business DB.

---

## 20. Validation rules

## 20.1 Workflow validation levels

### Schema validation

- Required fields.
- Type/range.
- String length.
- Enum.

### Domain validation

- Step order.
- Policy consistency.
- No cycles.
- Version immutable.
- Retry limits.

### External reference validation

- Taskchain exists.
- Map compatible.
- Source sync fresh.
- AGV supports required action.

### Runtime validation

- Current connection.
- Robot state.
- No active execution.
- Worker ownership.
- Command not expired.

## 20.2 Sync freshness

Đề xuất:

```text
Synced: source version/hash matches
Stale: known mismatch or older than configured threshold
Unknown: cannot query source
Missing: source confirms item absent
```

Tuổi cache một mình không chứng minh mismatch, nhưng có thể buộc re-sync theo safety policy.

---

## 21. MVP phases

## Phase 1 — Stabilize architecture and docs

### Deliverables

- `SPEC.md`.
- ADR cho database, Worker ownership và API–Worker transport.
- Domain project/application boundary.
- Shared contract rule.
- Error model.
- Initial EF migrations.
- Basic CI build/test.
- Remove direct Web-to-AGV code.

### Acceptance criteria

- Dependency graph không có Domain -> Infrastructure.
- Chỉ Worker reference SEER adapter.
- Có architecture tests.
- Có database decision được triển khai.
- Có fake clock và fake AGV interface.

## Phase 2 — AGV status and realtime map

### Deliverables

- Worker TCP connection.
- Reconnect/heartbeat.
- Normalized AGV status.
- `GET /api/agv/status`.
- SignalR.
- Map import/sync basic.
- Station and pose rendering.
- Connection/status history event.

### Acceptance criteria

- Disconnect/reconnect hiển thị đúng.
- Pose overlay khớp fixture.
- UI resync sau SignalR reconnect.
- Không ghi pose vào DB liên tục.
- Unknown không bị hiển thị thành Idle.

## Phase 3 — Taskchain sync

### Deliverables

- List/sync adapter.
- Taskchain snapshot/cache.
- Taskchains page.
- Sync history.
- Reference validation.

### Acceptance criteria

- Added/updated/missing được phân biệt.
- Snapshot tốt cũ không mất khi sync fail.
- Workflow reference validator có test.

## Phase 4 — Workflow definition

### Deliverables

- CRUD draft.
- Version/publish.
- Workflow builder.
- Sequence.
- Timeout/retry/failure config.
- Database persistence.
- Validation UI.

### Acceptance criteria

- Published version immutable.
- Concurrency conflict được xử lý.
- Missing taskchain block publish/run.
- Execution cũ không đổi khi draft sửa.

## Phase 5 — Workflow execution

### Deliverables

- Run sequence.
- Worker runtime.
- Step execution/attempt/event.
- Execution page.
- Cancel basic.
- History.
- Idempotent run request.

### Acceptance criteria

- Happy path E2E.
- Không chạy hai workflow cùng AGV.
- Restart API không làm mất execution.
- Worker restart đưa execution vào reconciliation.
- Duplicate request không tạo execution thứ hai.

## Phase 6 — Failure policy/fallback

### Deliverables

- Retry safety classification.
- Fallback sequence.
- Manual intervention.
- Timeout/reconcile.
- Failure path UI.

### Acceptance criteria

- Retry không vượt giới hạn.
- Non-idempotent uncertain command không retry mù.
- Fallback path hiển thị đúng.
- Manual action có audit.
- Cycle invalid.

## Phase 7 — Hardening

### Deliverables

- Auth/RBAC.
- Logs/metrics.
- Backup/restore.
- Deployment package.
- Load/soak tests.
- Security review.
- Production DB migration.
- Runbook.
- Multi-AGV readiness assessment.

### Acceptance criteria

- Restore backup thành công.
- Soak test ổn định.
- Failure injection pass.
- Operator runbook được nghiệm thu.
- Không còn secret trong repo/log.

---

## 22. Testing strategy

## 22.1 Domain unit tests

Test:

- Workflow transition.
- Step ordering.
- Retry count.
- Backoff calculation.
- Timeout decision.
- Fallback validation.
- No-cycle rule.
- Manual intervention.
- Terminal state immutability.
- One active execution invariant.

Không dùng database hoặc network.

## 22.2 Application unit tests

Dùng mock/fake port:

- `IAgvGateway`
- `IWorkflowRepository`
- `IExecutionRepository`
- `IClock`
- `IUnitOfWork`
- `IEventPublisher`
- `ICurrentUser`

Test use case:

- Run accepted/rejected.
- Idempotency.
- Safety precondition.
- Write-before-send.
- Reconciliation.

## 22.3 Protocol/Worker tests

- Packet framing.
- Partial packet.
- Multiple packet trong một read.
- Invalid length/checksum.
- Unknown message.
- Timeout.
- Reconnect.
- Out-of-order response.
- Duplicate response.
- Correlation.
- Command serialization.
- Stop priority.

Dùng fake TCP server hoặc SEER simulator, không chỉ mock method.

## 22.4 API integration tests

Dùng test host + temporary database:

- Auth/RBAC.
- Validation.
- ProblemDetails.
- ETag.
- Idempotency.
- Run/cancel.
- Query pagination.
- SignalR connection basic.

Test SQLite và ít nhất CI contract với production provider trước release.

## 22.5 Database tests

- Migration up.
- Constraint.
- Transaction.
- Active execution lock.
- Concurrent workflow update.
- Cleanup/retention.
- Backup/restore smoke test.

## 22.6 UI tests

- Workflow builder add/reorder/remove.
- Validation errors.
- Disabled actions and reason.
- Execution timeline.
- Reconnect/resync.
- Map transform fixture.

Có thể dùng bUnit cho component và Playwright cho E2E.

## 22.7 E2E happy path

```text
Start simulator
-> connect Worker
-> sync map
-> sync taskchains
-> create workflow
-> publish
-> run
-> task 1 succeeds
-> task 2 succeeds
-> execution succeeds
-> history/audit available
```

## 22.8 E2E failure path

```text
Run workflow
-> primary task fails with RobotBlocked/timeout
-> engine retries according to policy
-> fallback via C executes
-> retry/continue to B
-> workflow continues
-> timeline shows all attempts
```

## 22.9 Failure injection

- Kill API.
- Kill Worker.
- Drop TCP.
- Delay ACK.
- Return malformed packet.
- DB temporarily unavailable.
- Duplicate SignalR event.
- Taskchain removed after publish.
- Map changed before run.

## 22.10 Contract tests

- JSON serialization snapshots.
- Enum compatibility.
- Required field compatibility.
- Event schema version.
- API–Worker gRPC contract.
- Web API client generated/manual contract.

Rule: thêm field optional tương thích; rename/remove field cần version mới.

---

## 23. Validation plan

## 23.1 Product validation

Với operator thật:

- Có hiểu trạng thái robot trong 10 giây không.
- Có phân biệt disconnected, idle, blocked và error không.
- Có tạo workflow sequence mà không cần biết protocol không.
- Khi failure xảy ra, có hiểu hệ thống đang retry/fallback/chờ xác nhận không.
- Có biết hành động nào an toàn và vì sao button bị disabled không.

## 23.2 Technical validation

Trên robot thật:

1. Ghi lại firmware/software version.
2. Capture protocol flow của từng command.
3. Xác minh response/timeout.
4. Xác minh task completion correlation.
5. Xác minh behavior khi obstacle.
6. Xác minh behavior khi Wi-Fi mất.
7. Xác minh map coordinate.
8. Xác minh cancel/stop.
9. Xác minh lift up/down retry safety.
10. Xác minh taskchain changed/deleted detection.

## 23.3 Pilot exit criteria

- Chạy liên tục ít nhất 8 giờ không memory leak rõ rệt.
- 100 workflow happy path liên tiếp theo kịch bản test.
- Disconnect/reconnect không tạo duplicate command.
- Cancel và stop behavior được operator chấp nhận.
- Backup/restore DB pass.
- Audit truy nguyên được ai chạy/cancel.
- Không có critical bug về trạng thái giả.

---

## 24. Deployment recommendation

## 24.1 Single-machine pilot

```text
Windows/Linux host
├── NewAGV.Web/Api
├── NewAGV.Worker
├── SQLite database
├── Map asset directory
└── Log directory
```

- Internal Worker endpoint bind localhost.
- Service auto-start.
- Health dashboard.
- Scheduled backup.
- Map directory và DB cùng nằm trên disk có backup.

## 24.2 Production

```text
Reverse proxy
├── Web/API instance
├── Worker instance per AGV/site
├── PostgreSQL/SQL Server
├── Shared/object map storage
├── Central logs/metrics
└── Secret store
```

Không scale nhiều Worker cùng điều khiển một AGV nếu chưa có lease/leader election.

## 24.3 Worker lease

Multi-instance future:

- Worker phải acquire lease theo `AgvId`.
- Chỉ lease holder được connect/send command.
- Lease có heartbeat và fencing token.
- Đây không thuộc MVP nhưng schema nên có `WorkerInstanceId`.

---

## 25. Risks

| Risk | Mức độ | Giảm thiểu |
|---|---:|---|
| Protocol SEER khác theo firmware | Cao | Protocol profile + test trên version thật |
| Không correlate được task execution | Cao | Reconciliation, không retry mù |
| Map format/coordinate không rõ | Cao | Fixture và calibration test |
| API và Worker cùng gửi command | Critical | Single connection owner |
| Hai workflow chạy đồng thời | Critical | DB constraint + double gate |
| Software stop bị hiểu là safety E-stop | Critical | Naming/disclaimer/xác minh |
| SQLite lock khi ghi nhiều | Trung bình | WAL, không lưu pose, migrate production |
| Taskchain thay đổi ngoài NewAGV | Cao | Snapshot/hash/stale validation |
| SignalR mất event | Trung bình | REST snapshot + sequence |
| Worker restart giữa step | Cao | Durable execution + reconciliation |
| Retry command side-effect | Critical | RetrySafetyClass |
| UI map render quá nặng | Trung bình | Static/dynamic layers + throttle |
| Scope phình thành fleet/BPMN | Cao | Giữ non-goals và phase gate |
| Secret/config lọt log | Cao | Redaction + secret store |
| Operator hiểu nhầm Unknown là Idle | Cao | Explicit state/UX rule |

---

## 26. Open questions

### SEER/RoboshopPRO

1. Taskchain list/query API cụ thể là gì?
2. Taskchain có immutable ID không?
3. Có version/hash không?
4. Có execution instance ID không?
5. Cancel/pause/resume semantics?
6. Map retrieval format?
7. Planned route API?
8. Robot blocked/error code mapping?
9. Lift command có trạng thái phản hồi đáng tin cậy?
10. Software stop có mức an toàn nào?

### Product

1. Có bắt buộc login/AD ngay từ pilot không?
2. Ai được publish workflow?
3. Có yêu cầu approval 2 người cho workflow nguy hiểm không?
4. Execution history giữ bao lâu?
5. Operator có được chạy taskchain đơn lẻ ngoài workflow không?
6. Có cần schedule workflow theo giờ không?
7. Có cần lock workflow theo ca vận hành không?
8. Có cần localization quality threshold để block run không?

### Deployment

1. API và Worker cùng máy hay khác máy?
2. Hệ điều hành production?
3. Có PostgreSQL/SQL Server sẵn không?
4. Có reverse proxy/TLS nội bộ không?
5. Backup được lưu ở đâu?
6. Có centralized logging không?

### Map

1. Một AGV có nhiều map active theo khu vực không?
2. Có map switch command không?
3. Station ID có ổn định qua map version không?
4. Có cần custom display name/tag riêng trong NewAGV không?
5. Có cần recent trail và retention không?

---

## 27. Recommended ADRs

Tạo các file:

- `ADR-001-database-for-mvp.md`
- `ADR-002-worker-is-single-agv-connection-owner.md`
- `ADR-003-workflow-runtime-hosted-by-worker.md`
- `ADR-004-api-worker-communication.md`
- `ADR-005-map-assets-outside-relational-database.md`
- `ADR-006-workflow-versioning.md`
- `ADR-007-signalr-is-not-source-of-truth.md`
- `ADR-008-no-blind-retry-for-uncertain-side-effects.md`

---

## 28. Definition of done

Một feature liên quan AGV chỉ được coi là done khi:

- Có requirement/acceptance criteria.
- Có domain/application test.
- Có integration test hoặc simulator scenario.
- Có authorization.
- Có audit nếu là command.
- Có error handling.
- Có telemetry/log.
- Có behavior khi Worker/AGV disconnected.
- Không bypass API/Worker boundary.
- Cập nhật API contract và tài liệu.
- Đã kiểm tra trên protocol version mục tiêu nếu feature phụ thuộc SEER.

---

## 29. Glossary

### AGV

Automated Guided Vehicle. Trong tài liệu dùng chung cho robot SEER được điều khiển.

### RoboshopPRO

Phần mềm triển khai/cấu hình của hệ sinh thái SEER, dùng để kết nối robot, map, station, route, model và taskchain.

### Taskchain

Chuỗi hành động cấp thấp được định nghĩa trên AGV/RoboshopPRO. NewAGV MVP chỉ tham chiếu và chạy, không chỉnh logic bên trong.

### Workflow

Chuỗi logic cấp ứng dụng do NewAGV quản lý, ghép nhiều taskchain và failure policy.

### Workflow definition

Identity và draft metadata của workflow.

### Workflow version

Snapshot bất biến, đã publish, dùng để chạy.

### Workflow execution

Một lần chạy cụ thể của một workflow version.

### Step execution

Một lần thực thi một workflow step trong execution.

### Attempt

Một lần thử cụ thể của step; retry tạo attempt mới.

### Fallback

Sequence thay thế khi primary step thất bại.

### Reconciliation

Quá trình đối chiếu state trong DB với state thực tế trên AGV sau timeout, mất kết nối hoặc restart.

### Safety gate

Tập kiểm tra trước khi command được phép xuống AGV.

### Operational stop

Lệnh dừng vận hành qua phần mềm; không mặc định tương đương physical emergency stop.

### Source of truth

Nguồn dữ liệu có thẩm quyền cuối cùng.

### Snapshot

Bản chụp metadata tại một thời điểm để audit/validate, không thay thế source gốc.

### Stale

Dữ liệu cache có khả năng không còn khớp nguồn hoặc đã vượt freshness policy.

### Unknown

Không đủ dữ liệu để kết luận; không được tự đổi thành trạng thái “an toàn” như Idle.

### Idempotency key

Khóa giúp request lặp không tạo thêm một execution/command mới.

### Correlation ID

ID dùng nối log, command, execution và event trong toàn hệ thống.

---

## 30. Kết luận triển khai

NewAGV nên bắt đầu bằng một kiến trúc nhỏ nhưng có ranh giới cứng:

- Database ngay từ đầu; SQLite cho pilot.
- Worker là chủ sở hữu duy nhất của AGV connection.
- API là public facade và safety gate.
- Domain/Application giữ workflow rule.
- SignalR chỉ đẩy realtime.
- Map/taskchain từ AGV là read-only source data trong MVP.
- Workflow/audit/execution là dữ liệu riêng của NewAGV.
- Không retry command có side effect khi trạng thái chưa xác định.
- Không gọi software stop là emergency stop nếu chưa được xác minh/certify.

Thiết kế này đủ cho một AGV hiện tại và không khóa đường mở rộng lên nhiều AGV. Phần mở rộng chỉ nên làm sau khi single-AGV runtime, reconciliation và safety behavior đã ổn định.
