using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;

namespace DeskCal
{
    // ──────────────────────────────────────────────
    // 자동 업데이트: 러너가 올린 GitHub Release 를 실행할 때와 6시간마다 확인.
    // 실행할 때(처음 실행, 이미 떠 있는데 다시 실행) 새 버전을 찾으면 바로 설치할지 묻고,
    // 실행 중 주기 확인에서 찾으면 트레이 알림·메뉴로 알림. 설치는 내려받아 앱을 닫은 뒤 파일을 바꾸고 다시 시작
    // ──────────────────────────────────────────────
    public partial class MainWindow
    {
        private static readonly TimeSpan FirstUpdateCheckDelay = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(6);
        // 확인에 실패하면 (로그인 직후 네트워크가 아직 없을 때 등) 6시간을 기다리지 않고 1분부터 두 배씩, 최대 30분 간격으로 다시
        private static readonly TimeSpan FirstUpdateRetryDelay = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan MaxUpdateRetryDelay = TimeSpan.FromMinutes(30);
        private UpdateService? _updateService;
        private DispatcherTimer? _updateTimer;
        private TimeSpan _updateRetryDelay;
        private bool _askOnScheduledCheck = true; // 실행한 뒤 첫 확인이 성공할 때까지는 새 버전을 찾으면 바로 물어봄
        private AvailableUpdate? _availableUpdate;
        private Version? _notifiedUpdate; // 같은 버전은 트레이 알림을 한 번만
        private Version? _askedUpdate; // 같은 버전은 설치할지 한 번만 물어봄 (아니요를 고르면 트레이 메뉴로 설치)
        private bool _updateBusy;
        private System.Windows.Forms.ToolStripMenuItem? _trayUpdateItem;

        private void StartUpdateChecks()
        {
            UpdateService.SyncInstalledVersion();
            var settings = UpdateService.LoadSettings();
            if (settings is null)
            {
                VersionBadge.ToolTip = "자동 업데이트 꺼짐 · 이 빌드에 업데이트 저장소(UPDATE_REPOSITORY)가 없습니다.";
                return;
            }
            _updateService = new UpdateService(settings);
            VersionBadge.ToolTip = "업데이트 확인 전";
            _updateTimer = new DispatcherTimer(FirstUpdateCheckDelay, DispatcherPriority.Background,
                async (s, e) => await RunScheduledUpdateCheckAsync(), Dispatcher);
            if (_trayIcon != null) _trayIcon.BalloonTipClicked += (s, e) => InstallUpdate();
        }

        private async Task RunScheduledUpdateCheckAsync()
        {
            _updateTimer!.Stop(); // 설치 여부 창이 떠 있는 동안 다음 확인이 겹치지 않게
            bool checkedOk = await CheckForUpdatesAsync(_askOnScheduledCheck);
            if (checkedOk) _askOnScheduledCheck = false;
            _updateRetryDelay = checkedOk ? TimeSpan.Zero
                : _updateRetryDelay == TimeSpan.Zero ? FirstUpdateRetryDelay
                : TimeSpan.FromTicks(Math.Min(_updateRetryDelay.Ticks * 2, MaxUpdateRetryDelay.Ticks));
            _updateTimer.Interval = checkedOk ? UpdateCheckInterval : _updateRetryDelay;
            _updateTimer.Start();
        }

        // 이미 떠 있는데 다시 실행했을 때 (App.xaml.cs): 창을 꺼내고, 처음 실행처럼 확인해서 새 버전이면 물어봄
        internal void OnLaunchedAgain()
        {
            ShowFromTray();
            if (_updateService != null) _ = CheckForUpdatesAsync(ask: true);
        }

        // 확인에 성공하면 true. ask 면 새 버전을 찾았을 때 알림 대신 바로 설치할지 물어봄
        private async Task<bool> CheckForUpdatesAsync(bool ask)
        {
            if (_updateService is null || _updateBusy) return false;
            AvailableUpdate? update;
            _updateBusy = true;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                update = await _updateService.CheckAsync(UpdateService.CurrentVersion, timeout.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
            {
                // 오프라인이거나 서버에 닿지 않음: 창을 띄우지 않고 다음 확인 때 다시
                string reason = ex is UpdateAccessException ? ex.Message
                    : ex is HttpRequestException { StatusCode: { } code } ? $"서버 응답 {(int)code}" : ex.GetType().Name;
                VersionBadge.ToolTip = $"업데이트 확인 실패 · {DateTime.Now:HH:mm} · {reason}";
                return false;
            }
            finally { _updateBusy = false; }

            if (update is null)
            {
                VersionBadge.ToolTip = $"최신 버전 · {DateTime.Now:HH:mm} 확인";
                return true;
            }
            _availableUpdate = update;
            string next = "v" + update.Version.ToString(3);
            VersionBadge.ToolTip = $"새 버전 {next} 있음 · {DateTime.Now:HH:mm} 확인 · 트레이 메뉴에서 설치";
            ShowUpdateInTray(next);
            if (ask && _askedUpdate != update.Version)
            {
                _askedUpdate = _notifiedUpdate = update.Version;
                InstallUpdate(); // 설치할지 묻는 창
            }
            else if (_notifiedUpdate != update.Version)
            {
                _notifiedUpdate = update.Version;
                _trayIcon?.ShowBalloonTip(10000, "DeskCal 업데이트", $"새 버전 {next}이(가) 있습니다. 여기를 눌러 설치합니다.", System.Windows.Forms.ToolTipIcon.Info);
            }
            return true;
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
                VersionBadge.ToolTip = $"{next} 내려받는 중…";
                using var download = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                string package = await _updateService.DownloadAsync(update, updates, download.Token);
                UpdateService.LaunchUpdater(package, AppContext.BaseDirectory, Path.Combine(updates, "update.log"));
                // 업데이터는 이 프로세스가 끝나길 기다리므로, 창을 닫아 설정을 저장한 뒤 앱을 끝냄
                ExitApp();
                Application.Current.Shutdown();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                VersionBadge.ToolTip = $"업데이트 실패 · {DateTime.Now:HH:mm}";
                MessageBox.Show($"{next}을(를) 내려받거나 설치를 준비하지 못했습니다.\n\n{ex.Message}\n\n지금 버전({current})을 계속 사용합니다.",
                    "DeskCal 업데이트", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { _updateBusy = false; }
        }
    }
}
