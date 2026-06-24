# NewAGV Roadmap

## Dinh huong

NewAGV nen phat trien theo thu tu "nho truoc, dung duoc truoc, mo rong sau". Uu tien dau tien la van hanh an toan voi 1 AGV that, sau do moi mo rong task, order va multi-AGV.

## Trang thai hien tai

Nen tang da co:

- Cau truc 4 project: Web, Api, Worker, Contracts
- Ket noi AGV SEER qua Worker
- Doc cac trang thai cot loi nhu map, station, battery, e-stop, localization, alarm
- Man hinh AGV Monitor lam trung tam cho van hanh

Can tiep tuc cung co:

- Command safety gate
- Audit ro rang cho moi command
- Xac thuc target station hop le truoc khi bat goto
- Quy trinh test that voi AGV truoc khi mo rong command

## Uu tien gan nhat

1. Giu AGV Monitor la man hinh van hanh chinh cho 1 AGV.
2. Test on dinh `Pause`, `Resume`, `Cancel`.
3. Xac minh localization/relocation workflow truoc khi mo them command.
4. Xac dinh danh sach target station hop le thay vi dung raw station records.
5. Chi bat `GoToStation` sau khi da xac minh target, route va safety gate.

## Phase 0 - Nen tang AGV that

Muc tieu:

- Ket noi duoc AGV that
- Dong bo duoc state len Web
- Xu ly dung cac trang thai offline/degraded

Tieu chi:

- Web thay duoc robot, map va station that
- API/Worker bao trang thai ket noi ro rang

## Phase 1 - AGV Monitor

Muc tieu:

- 1 man hinh de giam sat va gui command van hanh cot loi

Pham vi:

- Hien thi robot, map, station, alarm, localization, readiness
- Cho phep `Pause`, `Resume`, `Cancel`
- `GoToStation` chi bat sau khi target hop le va da test

Khong lam qua som:

- Teleop cho Operator
- Clone toan bo menu RDS
- Multi-AGV dispatcher
- Chinh sua map/station truc tiep tu web

## Phase 2 - Map va route ro rang hon

Muc tieu:

- Nhin map truc quan va gan hon voi van hanh that

Co the bo sung:

- Path topology that neu lay duoc
- Phan biet station theo loai
- The hien heading, warning, selected target ro hon

## Phase 3 - Task va command workflow

Muc tieu:

- Tach command tuc thoi va task co audit/tracking ro rang

Co the bo sung:

- Task id
- Lich su task
- Retry/cancel ro rang
- Theo doi trang thai task chain

## Phase 4 tro di

Chi mo rong khi da co nhu cau thuc te:

- Worksite/station capacity
- Transport order
- Multi-AGV va dispatcher
- Event, alarm center, interface records
- Cau hinh va phan quyen day du

## Nguyen tac de repo khong tro lai tinh trang phan manh

- Khong tao file plan tam moi neu noi dung co the dua vao `ARCHITECTURE.md` hoac `ROADMAP.md`
- Moi thay doi da xong thi cap nhat `CHANGELOG.md`
- Moi muc tieu ngan han nen duoc dat trong phan "Uu tien gan nhat" thay vi tach thanh them nhieu file markdown
