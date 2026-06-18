# Ke hoach phat trien trang web quan ly AGV tuong tu RDS

Tai lieu nay mo ta cach phat trien NewAGV thanh mot trang web quan ly AGV theo huong tuong tu RDS web-based backend administration page. Muc tieu la di tung buoc, uu tien nhung tinh nang cot loi truoc, tranh co gang lam day du tat ca module RDS ngay tu dau.

## 1. RDS la gi trong ngu canh AGV?

RDS co the hieu la mot he thong dieu phoi va giam sat robot trung tam. No khong chi hien thi robot tren ban do, ma con quan ly task, order, station, worksite, alarm, event log, interface record va cac thiet bi ngoai vi nhu cua, thang may, den giao thong neu nha may co tich hop.

Voi NewAGV hien tai, muc tieu ban dau khong phai lam full RDS. Muc tieu dung hon la tao mot phien ban nho va thuc dung:

- Giam sat 1 AGV that.
- Doc ban do va station that tu AGV SEER.
- Hien thi vi tri, pin, e-stop, alarm, localization, task status.
- Cho phep gui lenh co kiem tra an toan: goto station, pause, resume, cancel.
- Ghi audit log cho moi lenh.

Sau khi nen tang nay chay on dinh, moi mo rong sang quan ly nhieu AGV, task/order, worksite va record.

## 2. Trang RDS Monitor dung de lam gi?

Trang Monitor la man hinh van hanh chinh. Nguoi dung nhin vao day de tra loi nhanh:

- AGV co online khong?
- AGV dang o dau tren map?
- AGV dang ranh hay dang chay task?
- AGV co loi, e-stop, canh bao hay mat dinh vi khong?
- AGV dang di toi station nao?
- Co the gui lenh di station, dung, tiep tuc hay huy task khong?

Day la man hinh nen lam dau tien vi no ket noi truc tiep voi du lieu that va luong van hanh that.

## 3. Kien truc muc tieu

Kien truc nen giu dung mo hinh hien tai:

```text
NewAGV.Web  ->  NewAGV.Api  ->  NewAGV.Worker  ->  SEER AGV TCP
     ^              ^                |
     |              |                |
 SignalR       State/Audit       Polling/Command
```

Vai tro tung thanh phan:

- `NewAGV.Web`
  - Hien thi UI cho nguoi van hanh.
  - Chi goi `NewAGV.Api`.
  - Khong giao tiep truc tiep TCP voi AGV.
- `NewAGV.Api`
  - La facade cho Web.
  - Giu state hien tai cua robot, map, station, audit.
  - Validate policy va safety truoc khi chuyen lenh sang Worker.
  - Day realtime event qua SignalR.
- `NewAGV.Worker`
  - Quan ly ket noi TCP toi AGV SEER.
  - Poll cac API trang thai.
  - Gui command toi AGV.
  - Dong bo snapshot nguoc ve Api.

## 4. Phase 0 - Nen tang ket noi AGV that

Trang thai hien tai cua du an da dat duoc phan lon phase nay.

Muc tieu:

- Ket noi duoc AGV `192.168.5.102`.
- Doc duoc robot info, map, station, battery, e-stop, alarm, localization, navigation.
- Dong bo du lieu len Web.
- Co safety gate co ban truoc khi gui lenh.

Tinh nang can co:

- Worker ket noi TCP port `19204`, `19205`, `19206`.
- Api co endpoint:
  - `GET /api/fleet/robots`
  - `GET /api/fleet/robots/{robotId}/detail`
  - `GET /api/map/entities`
  - `POST /api/commands/dispatch`
- Web hien thi robot that thay vi demo data.

Tieu chi hoan thanh:

- Mo web thay robot `AMB-01`.
- Thay map hien tai.
- Thay pin, e-stop, alarm, localization.
- Khi mat ket noi AGV, Web doi trang thai offline/degraded.

## 5. Phase 1 - Trang AGV Monitor giong RDS ban rut gon

Day la phase quan trong nhat tiep theo.

Muc tieu:

- Tao mot man hinh van hanh trung tam.
- Thay ro ban do, station, robot, trang thai va command tren cung mot trang.
- Giam so lan nguoi dung phai nhay qua nhieu page.

Bo cuc de xuat:

```text
------------------------------------------------------------
 Top toolbar: map, refresh, zoom, filters, role, connection
------------------------------------------------------------
 Left panel        | Main map canvas             | Right panel
 Robot list        | Station/path/robot view     | Robot detail
 Alarm summary     | Live position               | Command panel
------------------------------------------------------------
 Bottom status bar: API / Worker / AGV / map / warnings
------------------------------------------------------------
```

Chuc nang can co:

- Search station/robot.
- Zoom in, zoom out, fit map.
- Toggle hien thi:
  - station
  - robot
  - path
  - alarm
  - label
- Click station de xem thong tin.
- Click robot de xem:
  - battery
  - e-stop
  - localization
  - alarm
  - current task
  - current station / target station
- Gui command:
  - goto station
  - pause
  - resume
  - cancel
- Hien thi preflight truoc command:
  - robot offline
  - e-stop active
  - localization not ready
  - fatal/error alarm
  - control owner locked
  - station khong hop le

API/du lieu can dung:

- `1004` location
- `1007` battery
- `1012` e-stop
- `1020` navigation status
- `1021` localization
- `1050` alarm
- `1060` control owner
- `1300` map
- `1301` station
- `3051` goto station
- `3001`, `3002`, `3003` pause/resume/cancel

Tieu chi hoan thanh:

- Van hanh vien nhin vao 1 trang la biet AGV co san sang chay hay khong.
- Chon station that tu map va gui lenh goto station.
- Lenh nguy hiem bi chan ro ly do.
- Audit log ghi lai ket qua accepted/rejected.

## 6. Phase 2 - Map workspace nang cao

Sau khi Monitor dung on, moi nang cap map.

Muc tieu:

- Bien map thanh cong cu giam sat truc quan hon, gan voi van hanh thuc te.

Chuc nang:

- Ve path giua station neu co du lieu path.
- Phan biet station theo type:
  - LocationMark
  - ChargePoint
  - Workstation
  - Storage/bin station
- Hien thi robot heading theo `angle`.
- Hien thi confidence cua localization bang mau sac.
- Hien thi warning/error gan voi robot.
- Hien thi selected station va target station.
- Luu viewport cua nguoi dung.

Du lieu can bo sung:

- Path topology giua station neu lay duoc tu SEER map/project.
- Area/zone neu can hien thi vung cam, vung mutex, traffic control.
- Obstacle data neu can hien thi vat can.

Tieu chi hoan thanh:

- Map khong chi la danh sach diem, ma la man hinh dieu huong truc quan.
- Nguoi dung thay duoc robot dang huong ve dau va duong di lien quan.

## 7. Phase 3 - Quan ly task va command workflow

Phase nay bat dau giong RDS hon, vi RDS khong chi gui command truc tiep ma con quan ly task.

Muc tieu:

- Tach command tuc thoi va task co nghiep vu.
- Co lich su task, trang thai task va retry/cancel ro rang.

Chuc nang:

- Tao task di toi station.
- Gan task id.
- Xem task dang chay.
- Xem task da hoan thanh/thua/huy.
- Xem task status package tu SEER.
- Cho phep pause/resume/cancel task.
- Cho phep clear target list neu dung API `3066`.

API SEER lien quan:

- `3051` path navigation
- `3066` designated path navigation
- `3067` clear target list
- `3068` clear by task id
- `1020` navigation status
- `1110` task status package
- `3101` task chain status
- `3115` task chain list

Tieu chi hoan thanh:

- Moi lenh dieu huong co task id/audit id.
- Web biet task nao dang running, completed, failed, canceled.
- Nguoi dung co the truy lai lich su task.

## 8. Phase 4 - Worksite, station capacity va storage/bin

RDS co cac muc Worksite List, Robot, Transfer, storage/bin. Phase nay chi nen lam neu nha may co luong hang hoa/vi tri lam viec ro rang.

Muc tieu:

- Quan ly diem lay hang, tra hang, tram sac, khu vuc cho.
- Biet vi tri nao dang trong, day, bi khoa, hoac dang co task.

Chuc nang:

- Danh sach station/worksite.
- Trang thai tung station:
  - available
  - occupied
  - locked
  - disabled
  - unknown
- Gan station vao loai nghiep vu:
  - pickup
  - dropoff
  - charge
  - wait
- Hien thi bin/storage status neu robot ho tro.

API lien quan:

- `1301` station information
- `1803` storage bin information
- `1011` area status
- `1071` modbus data neu station gan voi PLC/thiet bi ngoai

Tieu chi hoan thanh:

- Nguoi dung biet station nao co the chon lam diem den.
- Command UI khong cho chon station dang disabled/locked.

## 9. Phase 5 - Transport order

Day la layer nghiep vu cao hon command robot.

Muc tieu:

- Thay vi noi "AGV di toi AP1", nguoi dung noi "van chuyen hang tu A sang B".

Chuc nang:

- Tao transport order:
  - source station
  - target station
  - priority
  - payload type
  - due time
- Gan order thanh mot hoac nhieu robot task.
- Theo doi order status:
  - created
  - assigned
  - running
  - completed
  - failed
  - canceled
- Audit order.

Du lieu can them:

- Database that cho order/task.
- Scheduler/dispatcher neu co nhieu AGV.
- Rule chon robot phu hop.

Tieu chi hoan thanh:

- He thong co the quan ly yeu cau van chuyen o muc nghiep vu, khong chi command truc tiep.

## 10. Phase 6 - Multi-AGV va dieu phoi

Chi lam phase nay khi co tu 2 AGV tro len.

Muc tieu:

- Giam sat va dieu phoi nhieu AGV.
- Giam xung dot duong di.

Chuc nang:

- Robot list nhieu xe.
- Filter theo status, battery, alarm, current task.
- Assign task cho robot.
- Theo doi robot nao dang giu station/path.
- Hien thi mutex/traffic area neu co.
- Rule sac pin, return home, idle parking.

Can can nhac:

- Neu SEER RDS/core da dieu phoi traffic, NewAGV nen tich hop voi RDS/core thay vi tu viet dispatcher.
- Neu NewAGV tu dieu phoi, can thiet ke scheduler rat can than de tranh xung dot an toan.

Tieu chi hoan thanh:

- Nhieu AGV cung hien thi va cap nhat realtime.
- Task duoc gan dung robot.
- Khong gui lenh gay tranh chap dieu khien.

## 11. Phase 7 - Event, alarm, interface record

Phase nay phuc vu van hanh va bao tri.

Muc tieu:

- Truy vet su co.
- Giai thich vi sao robot dung, loi, bi reject command.

Chuc nang:

- Alarm center.
- Event timeline.
- Command audit.
- Interface/API record.
- Loc theo robot, severity, time range, task id.
- Export CSV/Excel.

Du lieu can luu:

- Command request/response.
- SEER ret_code/err_msg.
- Alarm code, desc, method, reason.
- Connectivity change.
- Operator role/user.

Tieu chi hoan thanh:

- Khi co loi, nguoi dung co the tra loi: loi luc nao, do ai gui lenh, AGV tra ve gi, cach xu ly la gi.

## 12. Phase 8 - Cau hinh va phan quyen

Muc tieu:

- Bien he thong thanh cong cu co the dung trong nha may, khong can sua code moi khi doi cau hinh.

Chuc nang:

- Cau hinh AGV host/port.
- Cau hinh home station.
- Cau hinh station type.
- Cau hinh command policy.
- Phan quyen:
  - Operator
  - Engineer
  - Admin
- Lock cac lenh nguy hiem:
  - teleop/open-loop
  - relocation
  - map switch

Tieu chi hoan thanh:

- Admin co the doi cau hinh van hanh ma khong sua code.
- Operator chi thay va dung nhung lenh an toan.

## 13. Thu tu uu tien de lam ngay

Thu tu de xuat cho NewAGV:

1. Hoan thien Monitor page cho 1 AGV.
2. Test command an toan: pause, resume, cancel.
3. Xac nhan localization/relocation workflow.
4. Test goto station voi mot station that hop le.
5. Nang cap map UI: robot position, station labels, selected target.
6. Them task/audit detail.
7. Them task records.
8. Them transport order neu co yeu cau nghiep vu.
9. Sau cung moi lam multi-AGV/dispatcher.

## 14. Nhung viec khong nen lam qua som

Khong nen lam ngay:

- Clone toan bo RDS menu.
- Viet dispatcher nhieu AGV khi moi co 1 AGV.
- Bat teleop/open-loop cho operator.
- Cho sua map/station truc tiep tu web.
- Tu sinh task/order phuc tap khi chua test xong command AGV that.

Ly do: cac phan nay co rui ro cao, phu thuoc nhieu vao quy trinh nha may va an toan van hanh.

## 15. Dinh nghia "xong" cho ban RDS Monitor dau tien

Ban dau tien duoc coi la xong khi:

- Web hien thi dung robot that.
- Web hien thi dung map va station that.
- Robot duoc ve tren map theo pose that.
- Pin, e-stop, alarm, localization cap nhat realtime hoac polling deu.
- Co nut refresh/fit/zoom co ban.
- Co command panel.
- Command nguy hiem bi chan neu robot chua san sang.
- Pause/resume/cancel da test thanh cong voi AGV.
- Goto station da test voi station hop le.
- Audit ghi du moi command accepted/rejected.

## 16. Ket luan

RDS la he thong lon. NewAGV nen phat trien theo cach "tu nho den lon":

1. Nhin thay AGV that.
2. Hieu trang thai AGV that.
3. Dieu khien AGV that mot cach an toan.
4. Quan ly task.
5. Quan ly order.
6. Quan ly nhieu AGV va quy trinh nha may.

Neu lam dung thu tu nay, moi phase deu co gia tri su dung that va giam rui ro khi dua AGV vao van hanh.
