using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace DeskCal
{
    // 새 버전을 찾을 곳: GitHub(또는 GitHub Enterprise) 저장소의 Release
    public sealed record UpdateSettings(string Api, string Repository, string? Token, string Asset)
    {
        public const string DefaultAsset = "DeskCal-win-x64.zip";
    }

    public sealed record AvailableUpdate(Version Version, string Tag, string AssetUrl, long Size, string? Notes);

    // 릴리스 저장소를 앱에 들어간 토큰으로 읽을 수 없음 (토큰 없음·틀림·읽기 권한 없음)
    public sealed class UpdateAccessException(string repository, int status) : InvalidOperationException(
        $"업데이트 저장소({repository})에 접근할 수 없습니다 ({status}). .env의 UPDATE_TOKEN(읽기 권한)을 확인하세요.");

    // 사내 Actions 러너가 올린 최신 GitHub Release 를 확인하고 패키지를 내려받은 뒤,
    // 이 프로세스가 끝나면 파일을 바꾸는 작은 업데이터에 넘김
    public sealed class UpdateService(UpdateSettings settings, HttpMessageHandler? handler = null)
    {
        private const string ExecutableName = "DeskCal.exe";
        internal const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DeskCal";

        // update.json 은 빌드할 때 .env(UPDATE_*)에서 만들어져 앱 옆에 놓임
        public static UpdateSettings? LoadSettings()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "update.json");
            try
            {
                if (!File.Exists(path)) return null;
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                string Read(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()!.Trim() : "";
                string repository = Read("Repository");
                if (repository.Length == 0 || !repository.Contains('/')) return null;
                string api = Read("Api");
                string asset = Read("Asset");
                string token = Read("Token");
                return new UpdateSettings(api.Length == 0 ? "https://api.github.com" : api.TrimEnd('/'), repository,
                    token.Length == 0 ? null : token, asset.Length == 0 ? UpdateSettings.DefaultAsset : asset);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        // DeskCal-Setup.exe 로 설치한 폴더에서 실행 중인지 (Windows "앱" 목록의 설치 위치가 이 폴더)
        public static bool IsInstalledCopy
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
                    return key?.GetValue("InstallLocation") is string location &&
                        string.Equals(Path.GetFullPath(location).TrimEnd('\\'), AppContext.BaseDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
                {
                    return false;
                }
            }
        }

        // 앱이 스스로 파일을 바꿔 업데이트하므로, Windows "앱" 목록의 버전을 실제 실행 중인 버전과 맞춤
        public static void SyncInstalledVersion()
        {
            if (!IsInstalledCopy) return;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true);
                string version = CurrentVersion.ToString(3);
                if (key != null && !string.Equals(key.GetValue("DisplayVersion") as string, version, StringComparison.Ordinal)) key.SetValue("DisplayVersion", version);
            }
            catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        }

        public static Version CurrentVersion => Normalize(typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0));

        private HttpClient CreateClient(string accept)
        {
            var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DeskCal", CurrentVersion.ToString()));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
            if (settings.Token is { } token) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        // 최신 릴리스가 current 보다 새로우면 돌려줌
        public async Task<AvailableUpdate?> CheckAsync(Version current, CancellationToken token)
        {
            using var client = CreateClient("application/vnd.github+json");
            using var response = await client.GetAsync($"{settings.Api}/repos/{settings.Repository}/releases/latest", token);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // GitHub 는 "아직 릴리스 없음"과 "토큰으로 못 읽는 비공개 저장소" 모두 404 로 답함
                using var repository = await client.GetAsync($"{settings.Api}/repos/{settings.Repository}", token);
                if (repository.IsSuccessStatusCode) return null;
                throw new UpdateAccessException(settings.Repository, (int)repository.StatusCode);
            }
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                throw new UpdateAccessException(settings.Repository, (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var root = document.RootElement;
            string tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var parsed)) return null;
            var latest = Normalize(parsed);
            if (latest <= Normalize(current)) return null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                if (!string.Equals(asset.GetProperty("name").GetString(), settings.Asset, StringComparison.OrdinalIgnoreCase)) continue;
                // API 의 asset 주소(Accept: octet-stream)는 비공개·Enterprise 저장소에서도 받아짐
                string url = asset.GetProperty("url").GetString() ?? "";
                long size = asset.TryGetProperty("size", out var length) ? length.GetInt64() : 0;
                string? notes = root.TryGetProperty("body", out var body) ? body.GetString() : null;
                return url.Length == 0 ? null : new AvailableUpdate(latest, tag, url, size, notes);
            }
            return null;
        }

        // 패키지를 내려받아 확인한 뒤 zip 경로를 돌려줌
        public async Task<string> DownloadAsync(AvailableUpdate update, string directory, CancellationToken token)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"DeskCal-{update.Version}.zip");
            string partial = path + ".part";
            using (var client = CreateClient("application/octet-stream"))
            using (var response = await client.GetAsync(update.AssetUrl, HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(token);
                await using var target = File.Create(partial);
                await source.CopyToAsync(target, token);
            }
            using (var archive = ZipFile.OpenRead(partial))
            {
                if (!archive.Entries.Any(entry => string.Equals(Path.GetFileName(entry.FullName), ExecutableName, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("업데이트 패키지에 DeskCal.exe가 없습니다.");
            }
            File.Move(partial, path, overwrite: true);
            return path;
        }

        // 업데이터: 이 프로세스가 끝나길 기다렸다가 패키지를 설치 폴더에 풀고 새 버전을 시작.
        // 일정 데이터(C:\Deskcal)와 <앱>\dotnet 실행 환경은 건드리지 않음
        public static string BuildUpdaterScript(string zipPath, string installDirectory, int processId, string logPath, bool restart = true)
        {
            static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
            return string.Join("\n",
                "$ErrorActionPreference = 'Stop'",
                $"$zip = {Quote(zipPath)}; $target = {Quote(installDirectory.TrimEnd('\\'))}; $log = {Quote(logPath)}",
                "function Log($m) { Add-Content -LiteralPath $log -Value ((Get-Date -Format s) + ' ' + $m) -Encoding UTF8 }",
                "try {",
                $"  Log 'waiting for process {processId}'",
                $"  Wait-Process -Id {processId} -ErrorAction SilentlyContinue",
                "  $staging = Join-Path $env:TEMP ('DeskCalUpdate_' + [guid]::NewGuid().ToString('N'))",
                "  Expand-Archive -LiteralPath $zip -DestinationPath $staging -Force",
                "  $root = $staging",
                "  if (-not (Test-Path -LiteralPath (Join-Path $root 'DeskCal.exe'))) {",
                "    $sub = Get-ChildItem -LiteralPath $staging -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'DeskCal.exe') } | Select-Object -First 1",
                "    if ($sub) { $root = $sub.FullName } else { throw 'package has no DeskCal.exe' }",
                "  }",
                "  for ($i = 0; $i -lt 20; $i++) {",
                "    try {",
                "      Get-ChildItem -LiteralPath $root | Copy-Item -Destination $target -Recurse -Force",
                "      break",
                "    } catch { if ($i -eq 19) { throw }; Start-Sleep -Seconds 1 }",
                "  }",
                "  Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue",
                "  Log 'updated'",
                restart ? "  Start-Process -FilePath (Join-Path $target 'DeskCal.exe')" : "",
                "} catch { Log ('failed: ' + $_.Exception.Message)" + (restart ? "; Start-Process -FilePath (Join-Path $target 'DeskCal.exe')" : "") + " }");
        }

        public static void LaunchUpdater(string zipPath, string installDirectory, string logPath)
        {
            string script = BuildUpdaterScript(zipPath, installDirectory, Environment.ProcessId, logPath);
            // 스크립트 파일이 아닌 인코딩된 명령이라 실행 정책에 막히지 않음
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var start = new System.Diagnostics.ProcessStartInfo("powershell.exe",
                $"-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {encoded}")
            { UseShellExecute = false, CreateNoWindow = true };
            System.Diagnostics.Process.Start(start)?.Dispose();
        }

        private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));
    }
}
