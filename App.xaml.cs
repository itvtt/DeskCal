using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace desk_cal
{
    // 한 번에 하나만 실행. 이미 떠 있으면 새로 띄운 쪽은 그 창을 앞으로 불러내고 바로 끝남.
    // 일정 DB(C:\Deskcal)를 두 프로세스가 같이 쓰면 서로 덮어쓰므로, 설치본과 개발 빌드(bin\Debug)도 같이 묶음.
    // 업데이터는 이 프로세스가 끝난 뒤에 새 버전을 띄우므로 막히지 않음
    public partial class App : Application
    {
        private const string InstanceMutexName = @"Local\DeskCal.SingleInstance";
        private const string ShowWindowEventName = @"Local\DeskCal.ShowWindow";

        private Mutex? _instanceMutex;
        private EventWaitHandle? _showWindowEvent;
        private RegisteredWaitHandle? _showWindowWait;

        [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int processId);
        private const int ASFW_ANY = -1;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _instanceMutex = new Mutex(true, InstanceMutexName, out bool firstInstance);
            if (!firstInstance)
            {
                // 이미 실행 중: 그쪽이 창을 맨 앞으로 가져올 수 있게 전경 권한을 넘기고 신호만 보냄
                AllowSetForegroundWindow(ASFW_ANY);
                if (EventWaitHandle.TryOpenExisting(ShowWindowEventName, out var running))
                {
                    running.Set();
                    running.Dispose();
                }
                _instanceMutex.Dispose();
                _instanceMutex = null;
                Shutdown();
                return;
            }

            // 풍선 도움말·오른쪽 클릭 메뉴는 창 밖 별도 화면이라 글꼴을 물려받지 못하므로 앱 전체 스타일로
            foreach (var type in new[] { typeof(System.Windows.Controls.ToolTip), typeof(System.Windows.Controls.ContextMenu), typeof(System.Windows.Controls.MenuItem) })
                Resources[type] = new Style(type) { Setters = { new Setter(System.Windows.Controls.Control.FontFamilyProperty, DeskCal.MainWindow.AppFont) } };

            var window = new DeskCal.MainWindow();
            MainWindow = window;
            _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
            _showWindowWait = ThreadPool.RegisterWaitForSingleObject(_showWindowEvent,
                (_, _) => Dispatcher.InvokeAsync(window.OnLaunchedAgain), null, Timeout.Infinite, executeOnlyOnce: false);
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _showWindowWait?.Unregister(null);
            _showWindowEvent?.Dispose();
            if (_instanceMutex != null)
            {
                _instanceMutex.ReleaseMutex();
                _instanceMutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
