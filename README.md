# DeskCal

바탕화면 달력 (WPF, .NET 10). 일정 데이터는 `C:\Deskcal\deskcal.db` 에 있고, 설치·업데이트·제거 어느 때도 건드리지 않습니다.

## 폴더 구조

```
DeskCal.sln, DeskCal.csproj     앱 (Version = 릴리스 번호의 바닥값)
MainWindow.xaml(.cs)            화면·동작
AppIcon.cs                      창·작업 표시줄·트레이 아이콘에 오늘 요일·날짜를 그림 (아이폰 캘린더처럼, 자정에 바뀜)
Theme.cs                        라이트 / 다크 테마 (툴바의 해·달 버튼). 색은 다크 기준으로 적고, 라이트에서 같은 역할의 색으로 바꿈
MainWindow.Shift.cs             교대근무 표시 (설정의 교대근무 구역, 달력 칸의 조별 근무 배지)
ShiftSchedule.cs                4조 3교대(A·B·C·D) 계산: D → S → G → 휴, 날짜별 조 근무
docs\shift-api.md               교대근무 데이터 형식과 근무 API 형식 (공휴일 API 와 같은 방식)
MainWindow.Updates.cs           자동 업데이트 (확인 주기·트레이 알림·설치)
UpdateService.cs                GitHub Release 확인·내려받기·업데이터
installer\                      DeskCal-Setup.exe / uninstall.exe (.NET Framework 4.8, 사용자별 설치)
build\Publish-Release.ps1       릴리스 빌드 → out\DeskCal-Setup.exe, out\DeskCal-win-x64.zip
build\New-GitHubRelease.ps1     러너가 Release 를 만듦 (초안 → 파일 업로드 → 공개, 커밋 제목으로 노트)
build\Export-Repository.ps1     새 저장소로 보낼 묶음 → out\DeskCal-repo.zip
build\Make-Icon.ps1             cal.ico (exe·설치 프로그램·바로 가기 아이콘, AppIcon 과 같은 모양의 고정 날짜)를 그려서 만듦
.github\workflows\release.yml   셀프호스트 러너 릴리스
.env.sample                     업데이트 설정 예시 (.env 는 git 에 올리지 않음)
```

## 새 저장소에서 릴리스 (처음 한 번)

github.com 조직과 GitHub Enterprise 모두 같은 방법입니다. API 주소·저장소 이름은 러너가 알아서 넣습니다.

1. **보낼 묶음 만들기** (지금 소스가 있는 PC)
   ```
   powershell -NoProfile -ExecutionPolicy Bypass -File build\Export-Repository.ps1
   ```
   `out\DeskCal-repo.zip` 이 생깁니다. 안의 `DeskCal\` 는 `main` 에 커밋 하나가 든 git 저장소이고, 빌드 결과·VS 설정·`.env` 는 빠져 있습니다.
2. **GitHub 에 빈 저장소 만들기**: README·.gitignore·라이선스를 추가하지 않은 빈 저장소. 비공개(private)나 내부(internal) 둘 다 됩니다.
3. **올리기** (저장소를 올릴 PC): zip 을 풀고
   ```
   cd DeskCal
   git remote add origin <저장소 주소>
   git push -u origin main
   ```
4. **러너**: 저장소(또는 조직) Settings → Actions → Runners → New self-hosted runner → Windows x64 안내대로 설치·등록하고 서비스로 실행합니다. 레이블 `self-hosted`, `windows` 는 기본으로 붙으므로, 조직에 이미 Windows 셀프호스트 러너가 있으면 그것을 써도 됩니다.
   - 러너 PC 에 필요한 것: **.NET 10 SDK**, **Git** (릴리스 노트용. 없어도 빌드는 됨)
   - 빌드 때 Microsoft.Data.Sqlite 를 nuget.org 에서 받습니다. 막혀 있으면 러너 PC 시스템 환경 변수 `NUGET_SOURCE` 에 NuGet 미러 주소를 넣고 러너 서비스를 다시 시작하세요.
5. **업데이트 토큰** (비공개·내부 저장소일 때): 설치된 앱이 릴리스를 확인할 때 쓸, 이 저장소를 **읽기만** 하는 토큰을 만들어 저장소 Settings → Secrets and variables → Actions 에 `DESKCAL_UPDATE_TOKEN` 으로 넣습니다.
   - fine-grained token 이면 이 저장소만, Contents: Read-only. classic token 이면 `repo` 범위 (전용 계정 권장)
   - 토큰은 앱 안에 들어갑니다. 쓰기 권한은 주지 마세요.
   - **만료 주의**: 이미 설치된 앱은 빌드 때의 토큰을 들고 있습니다. 토큰이 만료되면 그 앱들은 업데이트를 확인하지 못하고, 새 토큰도 업데이트로만 받을 수 있습니다. 만료 전에 새 토큰으로 바꾸고 릴리스를 한 번 내세요.
   - 공개 저장소면 이 단계는 필요 없습니다.
6. **첫 릴리스**: 러너가 연결된 뒤 푸시하거나 Actions → Release → Run workflow → `v1.1.0` 이 만들어집니다.

## 평소 릴리스

1. `main` 에 푸시 (또는 Actions → Release → Run workflow)
2. 러너가 버전을 고름: 마지막 릴리스의 다음 번호 (`v1.1.3` → `1.1.4`). `DeskCal.csproj` 의 `<Version>` 이 바닥값이라, 거기를 `1.2.0` 으로 올리면 다음 릴리스는 `1.2.0` 부터
3. `build\Publish-Release.ps1` 로 빌드
4. GitHub Release `vX.Y.Z` 를 초안으로 만들어 `DeskCal-Setup.exe`, `DeskCal-win-x64.zip` 을 올린 뒤 공개합니다. 올리다 실패하면 초안을 지우므로 파일이 빠진 릴리스가 "최신"이 되지 않습니다.
5. 릴리스 노트는 지난 릴리스 이후 커밋 제목으로 채워집니다. 커밋 메시지를 사용자에게 보여 줄 문장으로 쓰면 그대로 변경 내역이 됩니다.

`.md`, `.env.sample`, `.gitignore`, `.gitattributes`, `build\Export-Repository.ps1` 만 바꾼 푸시는 릴리스하지 않습니다.

## 사용자 PC

- **설치**: Release 의 `DeskCal-Setup.exe` 를 실행하면 `%LOCALAPPDATA%\Programs\DeskCal` 에 설치하고 시작 메뉴·바탕화면 바로 가기를 만든 뒤 바로 실행합니다. 관리자 권한이 필요 없고, .NET 10 이 없는 PC 에는 실행 환경을 함께 넣습니다. 비공개 저장소면 Release 페이지를 열 수 있는 사람만 받을 수 있으니, 필요하면 Setup 파일을 따로 공유하세요.
- **업데이트**: 앱을 실행할 때(이미 트레이에 떠 있는데 다시 실행한 경우 포함)와 이후 6시간마다 최신 릴리스를 확인합니다. 실행할 때 새 버전을 찾으면 바로 업데이트할지 묻습니다. 실행 중에 찾으면 트레이 알림이 뜹니다. 어느 쪽이든 트레이 메뉴에 `업데이트 설치 (vX.Y.Z)` 가 생깁니다. 확인에 실패하면 1분부터 최대 30분 간격으로 다시 확인합니다. 설치를 고르면 내려받고, 앱을 닫았다가 새 버전으로 다시 시작합니다. 좌상단 버전 글자에 마우스를 올리면 마지막 확인 결과가 보입니다.
- **한 번에 하나만 실행**: 이미 켜져 있을 때 다시 실행하면 새로 뜨지 않고, 켜져 있던 창이 트레이에서 나와 맨 앞으로 옵니다. 설치본과 개발 빌드도 함께 하나로 묶입니다 (같은 `C:\Deskcal` 데이터를 씀).
- **닫기**: ✕ (Alt+F4, 작업 표시줄의 "창 닫기"도 같음) 는 끝내지 않고 트레이로 숨깁니다. 트레이 아이콘을 누르면 다시 나오고, 끝내려면 트레이 아이콘 우클릭 → 종료.
- **제거**: 설정 → 앱 → DeskCal. 일정 데이터(`C:\Deskcal`)는 남습니다.
- **예전 ClickOnce 판을 쓰던 PC**: DeskCal 을 종료(트레이 아이콘 우클릭 → 종료)한 뒤 Setup 을 실행하면, 설치가 끝나고 예전 사본을 제거할지 묻습니다. '윈도우 시작 시 실행' 은 새 앱이 처음 실행될 때 새 위치로 바뀝니다. 일정 데이터는 같은 `C:\Deskcal` 을 그대로 씁니다.

## 손으로 빌드

```
powershell -NoProfile -ExecutionPolicy Bypass -File build\Publish-Release.ps1 -Version 1.1.0 [-NuGetSource <NuGet 미러>]
```

`out\` 에 `DeskCal-Setup.exe`, `DeskCal-win-x64.zip` 이 생깁니다. `.env` 가 없으면 업데이트를 확인하지 않는 빌드가 됩니다.
Visual Studio 에서 F5 로 돌리는 개발 빌드도 `.env` 가 없으면 업데이트를 확인하지 않으므로 `bin\Debug` 를 덮어쓰지 않습니다.

## 문제 해결

| 증상 | 확인할 것 |
|---|---|
| Build 단계: `.NET 10 SDK is required` | 러너 PC 에 .NET 10 SDK 설치 후 러너 서비스 다시 시작 |
| Build 단계: NuGet 복원 실패 | 러너 PC 의 인터넷/프록시, 또는 `NUGET_SOURCE` |
| Create release 단계: 403 | 저장소 Settings → Actions → General → Workflow permissions 가 조직 정책으로 막혀 있지 않은지 (워크플로는 `contents: write` 를 요청함) |
| 작업이 대기(Queued)에서 안 넘어감 | 러너가 온라인인지, 레이블에 `self-hosted`, `windows` 가 있는지 |
| 앱 버전 글자 툴팁: "업데이트 저장소에 접근할 수 없습니다" | `DESKCAL_UPDATE_TOKEN` 이 있는지, 만료되지 않았는지, 이 저장소 읽기 권한이 있는지 |
