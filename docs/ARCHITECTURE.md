# NewAGV Architecture

## Muc tieu

NewAGV la he thong giam sat va dieu khien AGV SEER theo huong an toan, de van hanh va mo rong dan. Muc tieu truoc mat khong phai clone toan bo RDS, ma la tao mot nen tang nho, ro rang va dung duoc voi AGV that.

## Kien truc tong quan

```text
Operator
-> NewAGV.Web
-> NewAGV.Api
-> NewAGV.Worker
-> SEER AGV TCP API
```

Vai tro tung thanh phan:

- `NewAGV.Web`
  - Hien thi AGV Monitor va cac man hinh van hanh.
  - Chi goi `NewAGV.Api`.
  - Khong ket noi truc tiep toi robot.
- `NewAGV.Api`
  - Cung cap endpoint cho Web.
  - Giu state, audit va command policy.
  - Thuc hien safety gate truoc khi day command xuong Worker.
  - Day cap nhat realtime qua SignalR khi can.
- `NewAGV.Worker`
  - Mo ket noi TCP toi robot SEER.
  - Poll trang thai va gui command.
  - Dong bo snapshot nguoc len API.
- `NewAGV.Contracts`
  - Chua model dung chung giua cac lop.

## Tich hop SEER

SEER dung TCP nhị phan ket hop JSON payload cho cac API cot loi. Browser khong nen giao tiep truc tiep toi AGV. Backend la noi giu ket noi va xu ly protocol.

Cac nhom API quan trong cho pham vi hien tai:

- Status: vi tri, pin, e-stop, localization, alarm, navigation status
- Map: current map, station list
- Command: pause, resume, cancel, relocation, goto station
- Push/polling: uu tien hybrid, poll de bootstrap va push de realtime khi co the

## Nguyen tac van hanh va safety

- Khong dua placeholder giong du lieu that vao UI.
- Khong coi raw station tu AGV la route target hop le neu chua duoc xac nhan.
- Khong gui command nguy hiem khi chua qua safety gate.
- Web chi noi chuyen voi API; API moi duoc noi voi Worker.
- Mọi command that can ghi audit va tra ket qua ro rang.

Safety gate toi thieu truoc khi gui command di chuyen:

- Robot online
- Co telemetry hop le
- Khong e-stop
- Khong bi control owner lock
- Khong co fatal/error alarm
- Localization san sang
- Target hop le neu la command goto

## Pham vi command hien tai

Nen uu tien an toan va kha nang van hanh:

- Uu tien cho phep: `Pause`, `Resume`, `Cancel`
- `GoToStation` chi nen bat khi da co target da validate va quy trinh test that
- Teleop/open-loop motion chi danh cho Engineer/Admin, hoac tam thoi tat hoan toan cho den khi policy va payload duoc xac minh day du

## AGV Monitor

AGV Monitor la man hinh van hanh trung tam cho 1 AGV that. Nguoi dung can nhin vao 1 trang de biet:

- Robot co online khong
- Robot dang o dau tren map
- Pin, e-stop, localization, alarm, control owner dang ra sao
- Co san sang nhan command khong
- Vi sao command dang bi chan neu he thong chua san sang

## Huong to chuc repo

- `README.md`: diem vao nhanh cho nguoi moi
- `docs/ARCHITECTURE.md`: kien truc, SEER integration, command safety
- `docs/ROADMAP.md`: phase phat trien va uu tien tiep theo
- `docs/CHANGELOG.md`: lich su thay doi

Moi tai lieu tam, note chuyen session, hoac plan ngan han nen duoc nhap vao cac file song phu hop thay vi tiep tuc tao file moi o `docs/`.
