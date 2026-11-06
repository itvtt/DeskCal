using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;

namespace DeskCal
{
    // ──────────────────────────────────────────────
    // 자동 업데이트: 러너가 올린 GitHub Release 를 시작 직후와 6시간마다 확인.
    // 새 버전이 있으면 트레이 알림·메뉴로 알리고, 사용자가 고르면 내려받아 앱을 닫은 뒤 파일을 바꾸고 다시 시작
    // ──────────────────────────────────────────────
    public partial class MainWindow
    {
        private static readonly TimeSpan FirstUpdateCheckDelay = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(6);
        private UpdateService? _updateService;
        private DispatcherTimer? _updateTimer;
        private AvailableUpdate? _availableUpdate;
        private Version? _notifiedUpdate; // 같은 버전은 트레이 알림을 한 번만
        private bool _updateBusy;
        private System.Windows.Forms.ToolStripMenuItem? _trayUpdateItem;

        private void StartUpdateChecks()
        {
            UpdateService.SyncInstalledVersion();
            var settings = UpdateService.LoadSettings();
            if (settings is null)
            {
                TxtVersion.ToolTip = "자동 업데이트 꺼짐 · 이 빌드에 업데이트 저장소(UPDATE_REPOSITORY)가 없습니다.";
                return;
            }
            _updateService = new UpdateService(settings);
            TxtVersion.ToolTip = "업데이트 확인 전";
            _updateTimer = new DispatcherTimer(FirstUpdateCheckDelay, DispatcherPriority.Background, async (s, e) =>
            {
                _updateTimer!.Interval = UpdateCheckInterval;
                await CheckForUpdatesAsync();
            }, Dispatcher);
            if (_trayIcon != null) _trayIcon.BalloonTipClicked += (s, e) => InstallUpdate();
        }

        private async Task CheckForUpdatesAsync()
        {
            if (_updateService is null || _updateBusy) return;
            _updateBusy = true;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var update = await _updateService.CheckAsync(UpdateService.CurrentVersion, timeout.Token);
                if (update is null)
                {
                    TxtVersion.ToolTip = $"최신 버전 · {DateTime.Now:HH:mm} 확인";
                    return;
                }
                _availableUpdate = update;
                string next = "v" + update.Version.ToString(3);
                TxtVersion.ToolTip = $"새 버전 {next} 있음 · {DateTime.Now:HH:mm} 확인 · 트레이 메뉴에서 설치";
                ShowUpdateInTray(next);
                if (_notifiedUpdate != update.Version)
                {
                    _notifiedUpdate = update.Version;
                    _trayIcon?.ShowBalloonTip(10000, "DeskCal 업데이트", $"새 버전 {next}이(가) 있습니다. 여기를 눌러 설치합니다.", System.Windows.Forms.ToolTipIcon.Info);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
            {
                // 오프라인이거나 서버에 닿지 않음: 창을 띄우지 않고 다음 확인 때 다시
                string reason = ex is UpdateAccessException ? ex.Message
                    : ex is HttpRequestException { StatusCode: { } code } ? $"서버 응답 {(int)code}" : ex.GetType().Name;
                TxtVersion.ToolTip = $"업데이트 확인 실패 · {DateTime.Now:HH:mm} · {reason}";
            }
            finally { _updateBusy = false; }
        }

        // 트레이 메뉴의 '종료' 바로 위에 '업데이트 설치 (vX)' 항목
        private void ShowUpdateInTray(string next)
        {
            if (_trayIcon?.ContextMenuStrip is not { } menu) return;
            if (_trayUpdateItem == null)
            {
                _trayUpdateItem = new System.Windows.Forms.ToolStripMenuItem("", null, (s, e) => InstallUpdate());
                menu.Items.Insert(Math.Max(0, menu.Items.Count - 1), _trayUpdateItem);
            }
            _trayUpdateItem.Text = $"업데이트 설치 ({next})";
        }

        private async void InstallUpdate()
        {
            if (_updateService is null || _availableUpdate is not { } update || _updateBusy) return;
            string current = TxtVersion.Text, next = "v" + update.Version.ToString(3);
            string question = $"새 버전 {next}이(가) 있습니다. (현재 {current})\n\n지금 내려받아 업데이트한 뒤 다시 시작할까요?\n일정 데이터(C:\\Deskcal)와 설정은 그대로 유지됩니다.";
            var answer = IsVisible
                ? MessageBox.Show(this, question, "DeskCal 업데이트", MessageBoxButton.YesNo, MessageBoxImage.Question)
                : MessageBox.Show(question, "DeskCal 업데이트", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            _updateBusy = true;
            string updates = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskCal", "updates");
            try
            {
                TxtVersion.ToolTip = $"{next} 내려받는 중…";
                using var download = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                string package = await _updateService.DownloadAsync(update, updates, download.Token);
                UpdateService.LaunchUpdater(package, AppContext.BaseDirectory, Path.Combine(updates, "update.log"));
                // 업데이터는 이 프로세스가 끝나길 기다리므로, 창을 닫아 설정을 저장한 뒤 앱을 끝냄
                ExitApp();
                Application.Current.Shutdown();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                TxtVersion.ToolTip = $"업데이트 실패 · {DateTime.Now:HH:mm}";
                MessageBox.Show($"{next}을(를) 내려받거나 설치를 준비하지 못했습니다.\n\n{ex.Message}\n\n지금 버전({current})을 계속 사용합니다.",
                    "DeskCal 업데이트", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { _updateBusy = false; }
        }
    }
}
