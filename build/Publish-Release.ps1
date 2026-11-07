# DeskCal 릴리스를 out\ 에 만듭니다:
#   DeskCal-Setup.exe     처음 설치용 (사용자별, 관리자 권한 없음; 시작 메뉴·바탕화면 바로 가기, Windows "앱" 목록 등록;
#                         Windows 에 들어 있는 .NET Framework 로 돌아가며, .NET 10 이 없는 PC 에는 .NET 10 실행 환경을 넣어 줌)
#   DeskCal-win-x64.zip   설치된 앱이 GitHub Release 에서 내려받아 스스로 업데이트하는 패키지
#   win-x64\              게시된 앱 폴더 (zip 의 내용)
# .NET 10 SDK 만 있으면 됩니다 (Microsoft.Data.Sqlite 는 NuGet 에서 복원).
# nuget.org 가 막힌 망이면 NuGet 미러 주소를 -NuGetSource 또는 환경 변수 NUGET_SOURCE 로 주세요
# (러너 PC 의 시스템 환경 변수나 러너 폴더의 .env 에 두면 워크플로에서도 쓰입니다).
# .github\workflows\release.yml 이 부르고, 손으로도 실행할 수 있습니다:
#   powershell -NoProfile -ExecutionPolicy Bypass -File build\Publish-Release.ps1 [-Version 1.1.3]
param(
    [string]$Version = '',
    [string]$OutputDirectory = 'out',
    [string]$NuGetSource = $env:NUGET_SOURCE
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$sdk = ''
try { $sdk = (& dotnet --version 2>$null | Select-Object -First 1) } catch { }
if (-not $sdk -or [int]($sdk.Split('.')[0]) -lt 10) { throw ".NET 10 SDK is required on the build machine (found: '$sdk')." }

if (-not $Version) {
    [xml]$project = Get-Content (Join-Path $root 'DeskCal.csproj') -Encoding UTF8
    $Version = @($project.Project.PropertyGroup.Version | Where-Object { $_ })[0]
}
if (-not (Test-Path (Join-Path $root '.env'))) {
    if ($env:GITHUB_ACTIONS -eq 'true') { Write-Warning '.env not found: updates check this repository without a token (UPDATE_TOKEN).' }
    else { Write-Warning '.env not found: this build will not check for updates.' }
}

$out = [IO.Path]::Combine($root, $OutputDirectory)
$publish = Join-Path $out 'win-x64'
$zip = Join-Path $out 'DeskCal-win-x64.zip'
$setup = Join-Path $out 'DeskCal-Setup.exe'
$setupStage = Join-Path $out 'setup-build'
foreach ($old in $publish, $setupStage) { if (Test-Path $old) { Remove-Item $old -Recurse -Force } }
foreach ($old in $zip, $setup) { if (Test-Path $old) { Remove-Item $old -Force } }

Write-Host "Publishing DeskCal $Version (SDK $sdk)"
$restoreSource = @()
if ($NuGetSource) { $restoreSource = @('--source', $NuGetSource); Write-Host "NuGet source: $NuGetSource" }
& dotnet publish (Join-Path $root 'DeskCal.csproj') -c Release -r win-x64 --self-contained false -nologo `
    -o $publish "-p:Version=$Version" "-p:ReleaseZip=$zip" @restoreSource
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }
foreach ($required in 'DeskCal.exe', 'DeskCal.dll', 'e_sqlite3.dll', 'update.json') {
    if (-not (Test-Path (Join-Path $publish $required))) { throw "$required is missing from the published folder." }
}
if (-not (Test-Path $zip)) { throw 'The release zip was not created.' }

# .NET 10 이 없는 PC 용 실행 환경: 이 빌드 PC 의 .NET 에서 복사 (SDK 가 함께 설치함; 다운로드 없음).
# Setup 은 PC 에 .NET 10 Desktop Runtime 이 없을 때만 이것을 앱 옆에 풉니다.
$runtimeStage = Join-Path $out 'runtime-build'
$uninstallStage = Join-Path $out 'uninstall-build'
foreach ($old in $runtimeStage, $uninstallStage) { if (Test-Path $old) { Remove-Item $old -Recurse -Force } }
$desktopRuntimes = @(& dotnet --list-runtimes | ForEach-Object {
    if ($_ -match '^Microsoft\.WindowsDesktop\.App (10\.\d+\.\d+) \[(.+)\]$') { [pscustomobject]@{ Version = [version]$Matches[1]; Text = $Matches[1]; Path = $Matches[2] } }
} | Sort-Object Version -Descending)
if ($desktopRuntimes.Count -eq 0) { throw 'The .NET 10 Desktop Runtime (installed with the .NET 10 SDK on Windows) was not found on the build machine.' }
$runtimeVersion = $desktopRuntimes[0].Text
$dotnetRoot = Split-Path -Parent (Split-Path -Parent $desktopRuntimes[0].Path)
$fxr = @(Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'host\fxr') -Directory | Where-Object { $_.Name -match '^(\d+)\.\d+\.\d+$' -and [int]$Matches[1] -ge 10 } |
    Sort-Object { [version]$_.Name } -Descending)
if ($fxr.Count -eq 0) { throw "No .NET 10 host resolver (host\fxr) on the build machine under $dotnetRoot." }
foreach ($part in "host\fxr\$($fxr[0].Name)", "shared\Microsoft.NETCore.App\$runtimeVersion", "shared\Microsoft.WindowsDesktop.App\$runtimeVersion") {
    $source = Join-Path $dotnetRoot $part
    if (-not (Test-Path -LiteralPath $source)) { throw "The .NET runtime part is missing on the build machine: $source" }
    $destination = Join-Path $runtimeStage $part
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Recurse
}
foreach ($notice in 'LICENSE.txt', 'ThirdPartyNotices.txt') {
    $path = Join-Path $dotnetRoot $notice
    if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $runtimeStage }
}
Write-Host "Bundled .NET runtime $runtimeVersion (host $($fxr[0].Name)) for PCs without .NET 10"

# 설치 프로그램 (.NET Framework 4.8, Windows 기본 포함): 작은 uninstall.exe 를 먼저, 그다음 릴리스 zip·실행 환경·
# 그 제거 프로그램을 품은 Setup.
$setupProject = Join-Path $root 'installer\DeskCal.Setup.csproj'
Write-Host 'Building uninstall.exe'
& dotnet publish $setupProject -c Release -nologo -o $uninstallStage "-p:Version=$Version" -p:BuildUninstaller=true
if ($LASTEXITCODE -ne 0) { throw "Uninstaller build failed (exit $LASTEXITCODE)." }
$installerObj = Join-Path $root 'installer\obj'
if (Test-Path $installerObj) { Remove-Item $installerObj -Recurse -Force }
Write-Host 'Building DeskCal-Setup.exe'
& dotnet publish $setupProject -c Release -nologo -o $setupStage "-p:Version=$Version" "-p:PayloadZip=$zip" `
    "-p:RuntimeDirectory=$runtimeStage" "-p:UninstallerExe=$(Join-Path $uninstallStage 'DeskCal-Setup.exe')"
if ($LASTEXITCODE -ne 0) { throw "Installer build failed (exit $LASTEXITCODE)." }
Move-Item -LiteralPath (Join-Path $setupStage 'DeskCal-Setup.exe') -Destination $setup -Force

# 마무리: 릴리스 파일(win-x64, zip, setup)만 남기고 중간 결과는 지움
foreach ($stage in $setupStage, $uninstallStage, $runtimeStage) { if (Test-Path $stage) { Remove-Item $stage -Recurse -Force } }
foreach ($intermediate in 'bin', 'obj') {
    $path = Join-Path (Join-Path $root 'installer') $intermediate
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}

Write-Host ("Release zip: {0} ({1:N1} MB, {2} files)" -f $zip, ((Get-Item $zip).Length / 1MB), @(Get-ChildItem $publish -Recurse -File).Count)
Write-Host ("Installer:   {0} ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB))
if ($env:GITHUB_OUTPUT) {
    Add-Content -Path $env:GITHUB_OUTPUT -Value "zip=$zip" -Encoding ASCII
    Add-Content -Path $env:GITHUB_OUTPUT -Value "setup=$setup" -Encoding ASCII
    Add-Content -Path $env:GITHUB_OUTPUT -Value "version=$Version" -Encoding ASCII
}
