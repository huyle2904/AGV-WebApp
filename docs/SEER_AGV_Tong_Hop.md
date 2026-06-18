# Tài liệu tổng hợp kết nối SEER AGV cho dự án NewAGV

## 1. Mục đích của tài liệu này

Tài liệu này dùng để gom lại phần cốt lõi của bộ TCP/IP API của SEER AGV theo cách dễ đọc hơn.

Mục tiêu:

- Giúp người mới vào dự án hiểu nhanh AGV đang giao tiếp như thế nào.
- Giúp đội dự án biết nên bắt đầu tích hợp từ đâu.
- Giúp phân biệt phần nào là "bắt buộc phải hiểu", phần nào là "nâng cao".

Tài liệu này không thay thế 100% tài liệu gốc của SEER. Nó là bản tóm tắt có giải thích, để dễ bắt đầu hơn.

## 2. Bức tranh tổng thể

Trong dự án này, web app không nên nói chuyện trực tiếp với AGV từ browser.

Kiến trúc hợp lý là:

```text
Người dùng
-> NewAGV.Web
-> NewAGV.Api
-> lớp tích hợp SEER TCP
-> Robot AGV
```

Nói ngắn gọn:

- `NewAGV.Web` là giao diện.
- `NewAGV.Api` là backend trung gian.
- Backend sẽ mở kết nối TCP đến robot.
- Robot nhận request TCP, xử lý, rồi trả response TCP.

Ngoài ra robot còn có chế độ **push dữ liệu chủ động** về client, nên backend có thể nhận trạng thái realtime mà không phải hỏi liên tục.

## 3. Hai cách backend có thể lấy dữ liệu từ AGV

### 3.1. Polling

Backend chủ động gửi request để hỏi robot:

- vị trí hiện tại
- pin
- trạng thái điều hướng
- cảnh báo
- bản đồ đang dùng

Ưu điểm:

- dễ hiểu
- dễ debug
- dễ làm phiên bản đầu

Nhược điểm:

- nếu hỏi quá nhiều sẽ tốn tài nguyên
- dữ liệu không realtime bằng push

### 3.2. Push

Robot có API `19301` để **tự đẩy trạng thái** cho client đã kết nối.

Backend có thể:

1. mở kết nối TCP với robot
2. cấu hình push bằng API `9300`
3. robot tự gửi gói push định kỳ
4. backend cập nhật state trong hệ thống
5. web đọc lại state đó qua API hoặc SignalR

Ưu điểm:

- realtime hơn
- đỡ phải polling quá nhiều

Nhược điểm:

- phức tạp hơn một chút ở phần parser và quản lý kết nối

### 3.3. Khuyến nghị cho dự án này

Nên dùng **hybrid**:

- dùng polling để bootstrap, test kết nối, fallback
- dùng push để realtime

## 4. Robot SEER giao tiếp bằng gì

SEER không dùng HTTP API chính cho phần điều khiển robot.

Phần điều khiển cốt lõi dùng:

- **TCP**
- request/response dạng nhị phân
- phần data là JSON serialize

Điều này rất quan trọng. Nghĩa là:

- browser không nên nói chuyện trực tiếp với robot
- backend mới là nơi cầm kết nối TCP

## 5. Cấu trúc một gói tin TCP SEER

Mỗi gói tin gồm 2 phần:

1. **Header** cố định 16 byte
2. **Data area** là JSON serialize

### 5.1. Header

```c
struct ProtocolHeader {
    uint8_t  m_sync;
    uint8_t  m_version;
    uint16_t m_number;
    uint32_t m_length;
    uint16_t m_type;
    uint8_t  m_reserved[6];
};
```

Ý nghĩa:

- `m_sync`: luôn bắt đầu bằng `0x5A`
- `m_version`: version protocol
  - RBK 3.4: `0x01`
  - RBK 3.5: `0x02`
- `m_number`: số thứ tự request/response, do client tự gán
- `m_length`: độ dài phần data JSON
- `m_type`: mã API
- `m_reserved[6]`: luôn phải có đủ 6 byte, có thể điền `0x00`

### 5.2. Quy tắc request/response

- request có mã API ví dụ `1004`
- response tương ứng thường là `request + 10000`
- ví dụ:
  - request `1004`
  - response `11004`

Robot **không tự động trả dữ liệu lung tung**. Mỗi request hợp lệ sẽ có response tương ứng.

Ngoại lệ quan trọng:

- API push `19301` là gói robot chủ động gửi

### 5.3. Data area

Data area là JSON serialize.

Ví dụ request relocation:

```json
{"x":10.0,"y":3.0,"angle":0}
```

Lưu ý:

- dùng ASCII an toàn nhất
- tránh tên map chứa tiếng Trung hoặc ký tự lạ
- thứ tự key trong JSON response không cố định
- nếu có field lạ ngoài tài liệu, có thể bỏ qua

## 6. Port của từng nhóm API

Theo tài liệu đã thu thập:

- Robot Status API: `19204`
- Robot Control API: `19205`
- Robot Navigation API: `19206`
- Robot Configuration API: `19207`
- Other APIs: `19210`

Điều này có nghĩa là backend có thể cần làm việc với nhiều cổng TCP khác nhau, tùy nhóm chức năng.

## 7. Những nhóm API cốt lõi cần hiểu

Không cần học toàn bộ tài liệu ngay. Với dự án này, phần cốt lõi là:

### 7.1. Robot Status API

Dùng để đọc trạng thái robot:

- thông tin robot
- vị trí
- tốc độ
- blocked
- pin
- estop
- I/O
- trạng thái điều hướng
- trạng thái định vị
- alarm
- map đang load
- danh sách station

### 7.2. Robot Control API

Dùng để điều khiển cơ bản:

- stop open-loop motion
- relocation
- cancel relocation
- open-loop motion
- switch map

### 7.3. Robot Navigation API

Dùng để điều hướng:

- pause
- resume
- cancel
- path navigation
- designated path navigation
- dịch chuyển / quay / quay cung tròn
- query task chain
- query task chain list
- execute pre-stored task

### 7.4. Robot Push API

Dùng để robot chủ động đẩy trạng thái về backend.

Đây là phần rất đáng dùng cho realtime dashboard.

## 8. Danh sách API quan trọng nhất cho phase 1

Phần dưới đây là những API đủ để làm phiên bản đầu có thể chạy thật.

### 8.1. Đọc trạng thái robot

- `1000` Query Robot Information
- `1004` Query robot location
- `1005` Query robot speed
- `1006` Query blocked status
- `1007` Query battery status
- `1011` Query area status
- `1012` Query estop status
- `1013` Query I/O status
- `1019` Query obstacle information
- `1020` Query navigation status
- `1021` Query localization status
- `1050` Query alarm status
- `1060` Query current control owner
- `1110` Query robot task status package
- `1300` Query loaded map and stored map
- `1301` Query station information of current map

### 8.2. Điều khiển robot

- `2000` Stop open-loop motion
- `2002` Relocation
- `2004` Cancel relocation
- `2010` Open-loop motion
- `2022` Switch loaded map

### 8.3. Điều hướng robot

- `3001` Pause navigation
- `3002` Resume navigation
- `3003` Cancel navigation
- `3051` Path navigation
- `3053` Get navigation path
- `3055` Translation
- `3056` Rotation
- `3057` Tray rotation
- `3058` Circular motion
- `3059` Enable / disable paths
- `3066` Designated path navigation
- `3067` Clear specified navigation path
- `3068` Clear specified navigation path by task ID
- `3101` Query task chain
- `3106` Execute pre-stored tasks
- `3115` Query task chain list

### 8.4. Realtime push

- `9300` Set robot push config
- `19301` Robot push

## 9. Những API nào người mới nên hiểu trước

Nếu mới vào dự án, nên đọc theo thứ tự này:

1. `1004` vị trí
2. `1007` pin
3. `1012` estop
4. `1050` alarm
5. `1021` localization status
6. `1300` current map
7. `1301` station list
8. `2002` relocation
9. `3051` path navigation
10. `3001`, `3002`, `3003`
11. `1020` navigation status
12. `1110` task status package
13. `19301` robot push

Nếu hiểu 13 mục này, đã có thể nắm gần hết luồng chính của hệ thống.

## 10. Những khái niệm quan trọng cần hiểu thật chắc

### 10.1. Relocation là gì

Relocation là bước xác nhận robot đang ở đâu trên bản đồ.

Robot không thể điều hướng đúng nếu localization chưa ổn.

API liên quan:

- `2002`: gửi lệnh relocation
- `2004`: hủy relocation
- `1021`: hỏi trạng thái relocation

`1021` trả:

- `0 = RELOC_INIT`
- `1 = RELOC_SUCCESS`
- `2 = RELOC_RELOCING`

Ý nghĩa thực tế:

- trước khi chạy điều hướng, nên chắc chắn robot đã `RELOC_SUCCESS`

### 10.2. Path navigation là gì

API `3051` là điều hướng từ điểm nguồn tới điểm đích, robot tự tính đường đi.

Ví dụ cơ bản:

```json
{
  "source_id": "LM2",
  "id": "LM1"
}
```

Ý nghĩa:

- `source_id`: điểm bắt đầu
- `id`: điểm đích

Điểm rất quan trọng:

- gửi `3051` mới có thể làm task hiện tại bị hủy
- `task_id` không được trùng
- tài liệu khuyến cáo API này phù hợp single vehicle / test / task chain, không nên dùng bừa trong hệ nhiều xe có scheduler

### 10.3. Designated path navigation là gì

API `3066` là gửi hẳn một chuỗi điểm cho robot chạy theo đúng thứ tự đó.

Ví dụ:

```json
{
  "move_task_list": [
    {
      "id": "LM2",
      "source_id": "LM1",
      "task_id": "12344321"
    },
    {
      "id": "AP1",
      "source_id": "LM2",
      "task_id": "12344322"
    }
  ]
}
```

Khác với `3051`:

- `3051` là robot tự tìm đường đến đích
- `3066` là backend chỉ rõ từng đoạn path

### 10.4. Task chain và task status

Có 2 nhóm dễ nhầm:

- `1020`: trạng thái điều hướng hiện tại
- `1110`: trạng thái task theo `task_id`
- `3101`: trạng thái task chain
- `3115`: danh sách task chain

Nếu chỉ cần biết robot đang chạy tới đâu, thường xem:

- `1020`
- `1110`

### 10.5. Push API quan trọng ở đâu

`19301` có thể chứa gần như toàn bộ trạng thái hữu ích:

- vị trí
- tốc độ
- pin
- estop
- alarm
- IO
- task status
- localization
- current map
- obstacle / blocked

Nghĩa là nếu backend parse tốt `19301`, web có thể hiển thị realtime khá đầy đủ.

## 11. Luồng điều khiển robot cơ bản

Đây là luồng đơn giản, thực tế nhất cho phase 1.

### 11.1. Khởi động và kiểm tra kết nối

1. Backend mở TCP tới robot
2. Query `1000` để xác nhận robot trả lời
3. Query `1300` để biết map hiện tại
4. Query `1301` để lấy danh sách station

### 11.2. Kiểm tra an toàn trước khi chạy

1. Query `1012` estop
2. Query `1050` alarm
3. Query `1021` localization status
4. Query `1060` current control owner

Nếu:

- estop đang kích hoạt
- localization chưa ổn
- robot đang bị hệ khác giữ quyền

thì không nên gửi lệnh chạy.

### 11.3. Relocation nếu cần

1. Gửi `2002`
2. Poll `1021`
3. đợi đến khi `reloc_status = 1`

### 11.4. Điều hướng tới một điểm

1. Gửi `3051`
2. Poll `1020` hoặc nghe `19301`
3. theo dõi:
   - `task_status`
   - `target_id`
   - `finished_path`
   - `unfinished_path`

### 11.5. Tạm dừng / chạy tiếp / hủy

- `3001`: pause
- `3002`: resume
- `3003`: cancel

### 11.6. Theo dõi task

- `1110`: theo dõi theo `task_id`
- `3101`: theo dõi task chain hiện tại
- `3115`: lấy danh sách task chain

## 12. Những trạng thái quan trọng cần hiển thị trên web

Màn hình giám sát tối thiểu nên có:

- tên robot
- current map
- vị trí `x`, `y`, `angle`
- battery level
- charging
- estop
- blocked
- current task status
- target station
- finished path / unfinished path
- alarm hiện tại
- localization confidence / reloc status
- current control owner

Nếu có thêm:

- DI / DO
- area_ids
- obstacle geometry

thì giao diện vận hành sẽ mạnh hơn.

## 13. Mapping đơn giản từ tài liệu sang tính năng web

### 13.1. Dashboard

Dùng:

- `1000`
- `1004`
- `1005`
- `1007`
- `1012`
- `1020`
- `1050`
- `19301`

### 13.2. Điều khiển điều hướng

Dùng:

- `2002`
- `3001`
- `3002`
- `3003`
- `3051`
- `3066`

### 13.3. Cấu hình map / station

Dùng:

- `1300`
- `1301`
- `2022`

### 13.4. Giám sát an toàn

Dùng:

- `1006`
- `1011`
- `1012`
- `1019`
- `1050`
- `1060`

## 14. Những điểm cần cực kỳ cẩn thận khi tích hợp thật

### 14.1. Không gửi lệnh mở vòng bừa bãi

`2010 Open-loop motion` rất mạnh, nhưng nguy hiểm.

Chỉ nên dùng khi:

- có quyền kỹ sư
- có xác nhận rõ
- có điều kiện an toàn

### 14.2. Không assume robot đã định vị đúng

Trước khi điều hướng:

- luôn kiểm tra `1021`
- nếu confidence thấp hoặc reloc chưa xong thì không chạy

### 14.3. Kiểm tra quyền điều khiển

`1060` cho biết robot có đang bị hệ khác giữ quyền không.

Nếu web gửi lệnh khi control owner đang là hệ khác, rất dễ gây xung đột.

### 14.4. Không assume task mới sẽ "xếp hàng" giống nhau

- `3051` có thể hủy task đang chạy
- `3066` lại có cơ chế append task

Hai API này có hành vi khác nhau, phải phân biệt rõ trong backend.

### 14.5. Không phụ thuộc hoàn toàn vào polling

Nếu chỉ polling, UI có thể chậm hoặc giật.

Nên chuẩn bị thiết kế để nhận `19301`.

## 15. Kiến trúc đề xuất cho dự án NewAGV

### 15.1. Backend nên có các lớp chính

1. `SeerProtocol`
   - build header 16 byte
   - parse header
   - serialize / deserialize JSON

2. `SeerTcpClient`
   - mở TCP
   - gửi request
   - chờ response
   - xử lý reconnect

3. `SeerPushListener`
   - nhận gói `19301`
   - parse push data
   - cập nhật state

4. `RobotStateStore`
   - lưu trạng thái mới nhất của robot
   - làm nguồn dữ liệu cho web

5. `SeerCommandService`
   - relocation
   - goto station
   - pause / resume / cancel
   - switch map

### 15.2. Cách web nên dùng dữ liệu

Web không nên gọi trực tiếp từng API SEER.

Web chỉ nên gọi:

- API nội bộ của `NewAGV.Api`
- hoặc SignalR realtime từ backend

Ví dụ:

- `GET /api/fleet/robots`
- `GET /api/fleet/health`
- `POST /api/commands/dispatch`

Backend sẽ tự map xuống SEER TCP.

## 16. Gợi ý phạm vi phase 1

Phase 1 chỉ cần làm đủ để robot thật chạy được cơ bản và web nhìn thấy trạng thái.

### 16.1. Chức năng nên có

- kết nối TCP
- query trạng thái cơ bản
- lấy map hiện tại
- lấy station list
- relocation
- goto station bằng `3051`
- pause / resume / cancel
- xem nav status
- xem alarm / estop / battery

### 16.2. Chưa cần ngay

- tray rotation
- circular motion
- raw laser point cloud
- IMU raw
- RFID raw
- các script nâng cao
- cánh tay robot
- camera calibration

## 17. Những chỗ vẫn có thể cần xác nhận thêm

Tài liệu hiện tại đã đủ để bắt đầu phase 1, nhưng vẫn còn vài điểm đáng xác minh khi code thật:

1. Push `19301` đi trên cùng TCP session hay cơ chế session riêng
2. Robot thực tế của công ty đang dùng model nào
3. Robot có dùng tray / fork / jack / roller / hook hay không
4. Quyền điều khiển hiện tại có đang do hệ khác nắm không
5. Robot đang chạy đơn xe hay có scheduler / fleet manager

## 18. Kết luận

Nếu cần hiểu dự án ở mức "có thể bắt tay làm", người mới chỉ cần nhớ 5 điều:

1. SEER AGV giao tiếp chính bằng TCP nhị phân + JSON, không phải HTTP điều khiển trực tiếp.
2. Backend là nơi giữ kết nối với robot, web không điều khiển thẳng robot.
3. Trước khi chạy, phải kiểm tra estop, alarm, localization, và quyền điều khiển.
4. `3051` là API điều hướng quan trọng nhất cho phase 1.
5. `19301` là chìa khóa để làm realtime tốt.

---

Nếu cập nhật thêm tài liệu mới từ SEER, nên nối tiếp tài liệu này thay vì tạo tài liệu rời, để đội dự án chỉ cần đọc một nơi duy nhất.
