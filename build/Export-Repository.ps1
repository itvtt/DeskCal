# 사내 저장소로 보낼 깨끗한 저장소 묶음을 만듭니다: out\DeskCal-repo.zip
#   안에 DeskCal\ 폴더 하나: main 브랜치에 커밋 하나가 든 git 저장소입니다.
#   bin·obj·out·.vs·.vshistory·.env·*.user 같은 빌드 결과·개인 설정은 빠집니다 (.gitignore 기준).
# 받은 쪽(사내 PC)에서:
#   1) 압축을 풀고 DeskCal 폴더로 이동
#   2) git remote add origin <사내 저장소 주소>      (README 이나 .gitignore 없이 "빈 저장소"로 만든 것)
#   3) git push -u origin main                        → 러너가 있으면 바로 첫 릴리스가 시작됩니다
# 실행:
#   powershell -NoProfile -ExecutionPolicy Bypass -File build\Export-Repository.ps1 [-Message "DeskCal 1.1"]
param(
    [string]$OutputDirectory = 'out',
    [string]$Message = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'git is required (https://git-scm.com).' }

# git 은 진행 상황을 stderr 로 쓰므로, 종료 코드로만 실패를 판단
function Invoke-Git {
    $previous = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { & git @args 2>&1 | ForEach-Object { "$_" } } finally { $ErrorActionPreference = $previous }
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed (exit $LASTEXITCODE)." }
}

[xml]$project = Get-Content (Join-Path $root 'DeskCal.csproj') -Encoding UTF8
$version = @($project.Project.PropertyGroup.Version | Where-Object { $_ })[0]
if (-not $Message) { $Message = "DeskCal $version" }

$stage = Join-Path ([IO.Path]::GetTempPath()) ('DeskCal-export-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$work = Join-Path $stage 'work'
$repo = Join-Path $stage 'DeskCal'
try {
    # 1) 작업 사본: 빌드 결과·VS 상태·개인 설정을 빼고 복사
    & robocopy $root $work /E /XD bin obj out artifacts .vs .vshistory .git /XF .env *.user /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying the source failed (robocopy exit $LASTEXITCODE)." }

    # 2) 커밋 하나짜리 저장소 (.gitignore 에 걸리는 파일은 들어가지 않음)
    $name = (& git config --get user.name) 2>$null; if (-not $name) { $name = 'DeskCal' }
    $email = (& git config --get user.email) 2>$null; if (-not $email) { $email = 'deskcal@localhost' }
    Invoke-Git -C $work init -q -b main | Out-Null
    Invoke-Git -C $work -c core.safecrlf=false add -A | Out-Null
    Invoke-Git -C $work -c user.name="$name" -c user.email="$email" commit -q -m $Message | Out-Null

    # 3) 깨끗하게 다시 받음: 작업 폴더가 .gitattributes(CRLF) 그대로이고 무시된 파일이 하나도 없음
    Invoke-Git clone -q --no-local $work $repo | Out-Null
    Invoke-Git -C $repo remote remove origin | Out-Null
    $files = @(Invoke-Git -C $repo ls-files)
    $commit = (Invoke-Git -C $repo rev-parse --short HEAD | Select-Object -First 1)

    $out = [IO.Path]::Combine($root, $OutputDirectory)
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    $zip = Join-Path $out 'DeskCal-repo.zip'
    if (Test-Path $zip) { Remove-Item $zip -Force }
    # Compress-Archive 는 숨김 폴더(.git)를 빠뜨리고, PowerShell 5.1 의 ZipFile.CreateFromDirectory 는 경로를 '\' 로 써서
    # Windows 밖의 도구가 잘못 풀므로, 항목을 '/' 경로로 하나씩 넣음 (빈 폴더도 항목으로)
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    Remove-Item $work -Recurse -Force
    $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($item in Get-ChildItem -LiteralPath $stage -Recurse -Force) {
            $relative = $item.FullName.Substring($stage.Length).TrimStart('\').Replace('\', '/')
            if ($item.PSIsContainer) {
                if (-not (Get-ChildItem -LiteralPath $item.FullName -Force | Select-Object -First 1)) { $archive.CreateEntry("$relative/") | Out-Null }
            }
            else {
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $item.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
        }
    }
    finally { $archive.Dispose() }

    Write-Host ("Repository zip: {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
    Write-Host ("  main @ {0} '{1}', {2} files" -f $commit, $Message, $files.Count)
    Write-Host ''
    Write-Host 'On the company PC:'
    Write-Host '  unzip, cd DeskCal'
    Write-Host '  git remote add origin <empty company repository URL>'
    Write-Host '  git push -u origin main'
}
finally {
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue }
}
