# NewAGV

NewAGV la bo ung dung giam sat va dieu khien AGV SEER theo mo hinh tach lop:

- `NewAGV.Web`: giao dien van hanh
- `NewAGV.Api`: facade va command safety gate
- `NewAGV.Worker`: ket noi TCP toi SEER AGV
- `NewAGV.Contracts`: model dung chung

## Cau truc repo

- `NewAGV.sln`: solution chinh dung cho Visual Studio va .NET CLI
- `src/`: ma nguon cac project
- `docs/ARCHITECTURE.md`: tong quan kien truc, tich hop SEER va nguyen tac van hanh
- `docs/ROADMAP.md`: huong phat trien, pham vi hien tai va uu tien tiep theo
- `docs/CHANGELOG.md`: lich su thay doi
- `Start-NewAGV.ps1`: script chay local
- `docker-compose.yml`: ho tro local infrastructure khi can

## Cach chay local

```powershell
./Start-NewAGV.ps1
```

Mac dinh script se build va mo:

- Web: `http://localhost:5209`
- API: `http://localhost:5222`
- Worker: `http://localhost:5230`

## Nguyen tac repo

- Chi giu mot solution chinh: `NewAGV.sln`
- Tai lieu song duoc gop trong `docs/ARCHITECTURE.md` va `docs/ROADMAP.md`
- `docs/CHANGELOG.md` giu rieng de theo doi thay doi
- Log runtime khong dat trong root neu khong that su can thiet
