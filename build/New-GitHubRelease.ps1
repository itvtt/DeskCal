# 빌드한 두 파일로 GitHub Release 를 만듭니다 (.github\workflows\release.yml 의 마지막 단계).
#   초안으로 만들어 파일을 모두 올린 뒤 공개: 파일이 빠진 릴리스가 "최신"으로 잡혀 설치된 앱이 헛걸음하지 않게.
#   올리다 실패하면 초안을 지우고 실패로 끝냄 (다음 실행이 같은 번호로 다시 시도).
#   릴리스 노트: 지난 릴리스 태그 이후의 커밋 제목 (병합 커밋 제외).
# 환경 변수: GH_TOKEN (contents: write), GITHUB_API_URL, GITHUB_REPOSITORY, GITHUB_SHA — Actions 가 넣어 줌.
#           RELEASE_VERSION, RELEASE_ZIP, RELEASE_SETUP, RELEASE_PREVIOUS(지난 태그, 없으면 비움) — 워크플로가 넣어 줌.
# (powershell -File 은 빈 인수를 버리므로 값은 환경 변수로 받음.) github.com 과 사내 GitHub Enterprise 모두 같은 API 입니다.
param(
    [string]$Version = $env:RELEASE_VERSION,
    [string]$Zip = $env:RELEASE_ZIP,
    [string]$Setup = $env:RELEASE_SETUP,
    [string]$Previous = $env:RELEASE_PREVIOUS
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
foreach ($name in 'GH_TOKEN', 'GITHUB_API_URL', 'GITHUB_REPOSITORY', 'GITHUB_SHA') {
    if (-not [Environment]::GetEnvironmentVariable($name)) { throw "$name is not set." }
}
if (-not $Version -or -not $Zip -or -not $Setup) { throw 'Version, Zip and Setup are required (RELEASE_VERSION, RELEASE_ZIP, RELEASE_SETUP).' }
foreach ($file in $Zip, $Setup) { if (-not (Test-Path -LiteralPath $file)) { throw "Release file not found: $file" } }

$api = "$($env:GITHUB_API_URL.TrimEnd('/'))/repos/$env:GITHUB_REPOSITORY"
$headers = @{ Authorization = "Bearer $env:GH_TOKEN"; Accept = 'application/vnd.github+json' }
function Send-Json([string]$Method, [string]$Uri, $Data) {
    Invoke-RestMethod -UseBasicParsing -Method $Method -Uri $Uri -Headers $headers -ContentType 'application/json; charset=utf-8' `
        -Body ([Text.Encoding]::UTF8.GetBytes(($Data | ConvertTo-Json)))
}

# 릴리스 노트. 러너에 git 이 없으면 checkout 이 .git 없이 받으므로 커밋 목록은 생략
$log = @()
if ((Get-Command git -ErrorAction SilentlyContinue) -and (Test-Path (Join-Path (Split-Path -Parent $PSScriptRoot) '.git'))) {
    $previousEncoding = [Console]::OutputEncoding
    [Console]::OutputEncoding = [Text.Encoding]::UTF8  # 한글 커밋 메시지
    try {
        $range = @('-n', '30')
        if ($Previous) {
            & git rev-parse -q --verify "refs/tags/$Previous" | Out-Null
            if ($LASTEXITCODE -eq 0) { $range = @("$Previous..HEAD") }
        }
        $log = @(& git log --no-merges --pretty=format:'- %s' @range)
    }
    finally { [Console]::OutputEncoding = $previousEncoding }
}
$notes = if ($log.Count) { "## 변경 사항`n`n" + ($log -join "`n") } else { '변경 사항 없음' }
$notes += "`n`n처음 설치: DeskCal-Setup.exe 를 실행하세요. 설치된 앱은 스스로 업데이트합니다."

$release = Send-Json Post "$api/releases" @{
    tag_name = "v$Version"; target_commitish = $env:GITHUB_SHA; name = "DeskCal v$Version"
    body = $notes; draft = $true; prerelease = $false
}
try {
    $upload = $release.upload_url -replace '\{.*\}', ''
    # 멈춘 업로드가 작업 시간(30분)을 다 잡아먹지 않게 파일마다 10분 제한. 넘으면 아래에서 초안을 지우고 실패
    Invoke-RestMethod -UseBasicParsing -TimeoutSec 600 -Method Post -Uri "$($upload)?name=DeskCal-win-x64.zip" -Headers $headers -ContentType 'application/zip' -InFile $Zip | Out-Null
    Invoke-RestMethod -UseBasicParsing -TimeoutSec 600 -Method Post -Uri "$($upload)?name=DeskCal-Setup.exe" -Headers $headers -ContentType 'application/octet-stream' -InFile $Setup | Out-Null
    $published = Send-Json Patch $release.url @{ draft = $false }
}
catch {
    $reason = $_.Exception.Message
    Write-Host "::error::Uploading or publishing the release failed; deleting the draft so the next run can retry: $reason"
    try { Invoke-RestMethod -UseBasicParsing -Method Delete -Uri $release.url -Headers $headers | Out-Null } catch { }
    throw
}
Write-Host "Released v$Version -> $($published.html_url)"
