using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DeskCal
{
    // 창·작업 표시줄·트레이 아이콘에 오늘 요일·날짜 (AppIcon.cs). 1분마다 날이 바뀌었는지 보고 바뀌었으면 다시 그림
    public partial class MainWindow
    {
        private DateTime _iconDate = DateTime.MinValue;
        private DispatcherTimer? _iconTimer;

        private void InitAppIcon()
        {
            UpdateAppIcon();
            _iconTimer = new DispatcherTimer(TimeSpan.FromMinutes(1), DispatcherPriority.Background, (s, e) => UpdateAppIcon(), Dispatcher);
        }

        private void UpdateAppIcon()
        {
            var today = DateTime.Today;
            if (today == _iconDate) return;
            try
            {
                byte[] ico = AppIcon.BuildIco(today);
                Icon = BitmapFrame.Create(new MemoryStream(ico), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                if (_trayIcon != null)
                {
                    var old = _trayIcon.Icon;
                    _trayIcon.Icon = new System.Drawing.Icon(new MemoryStream(ico), System.Windows.Forms.SystemInformation.SmallIconSize);
                    old?.Dispose();
                }
                _iconDate = today;
            }
            catch (Exception ex) { Debug.WriteLine($"[AppIcon] {ex.Message}"); }
        }
    }
}
