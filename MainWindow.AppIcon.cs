using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DeskCal
{
    // 창·작업 표시줄·트레이 아이콘에 오늘 요일·날짜 (AppIcon.cs). 1분마다 날이 바뀌었는지 보고 바뀌었으면 다시 그림
    public partial class MainWindow
    {
        private DateTime _iconDate = DateTime.MinValue;
        private DispatcherTimer? _iconTimer;

        // Windows 11 작업 표시줄은 앱 ID 가 exe·시작 메뉴 바로 가기로 이어지면 창 아이콘 대신 exe 아이콘(cal.ico)을 그림.
        // 이 창에만 따로 앱 ID 를 주면 창 아이콘(오늘 날짜)을 그대로 쓰고, 날이 바뀌어 아이콘을 바꾸면 같이 바뀜.
        // 프로세스 전체(SetCurrentProcessExplicitAppUserModelID)에 주면 트레이 풍선 알림(업데이트 안내)이 안 떠서 창에만 줌.
        // 작업 표시줄에 고정하면 고정 바로 가기의 아이콘이 쓰여 날짜가 바뀌지 않음 (Windows 동작)
        private const string TaskbarAppId = "DeskCal.Calendar";

        private void ApplyTaskbarAppId()
        {
            try
            {
                var iid = typeof(IPropertyStore).GUID;
                if (SHGetPropertyStoreForWindow(_hwnd, ref iid, out var store) != 0) return;
                var key = new PropertyKey { FormatId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), PropertyId = 5 }; // System.AppUserModel.ID
                var value = new PropVariant { VarType = 31 /* VT_LPWSTR */, Pointer = Marshal.StringToCoTaskMemUni(TaskbarAppId) };
                try
                {
                    if (store.SetValue(ref key, ref value) == 0) store.Commit();
                }
                finally
                {
                    Marshal.FreeCoTaskMem(value.Pointer);
                    Marshal.ReleaseComObject(store);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[AppIcon] taskbar id: {ex.Message}"); }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey { public Guid FormatId; public uint PropertyId; }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropVariant { public ushort VarType; public ushort Reserved1, Reserved2, Reserved3; public IntPtr Pointer; public IntPtr Pointer2; }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint index, out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
            [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
            [PreserveSig] int Commit();
        }

        [DllImport("shell32.dll")]
        private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

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
                    // Windows 트레이 아이콘 설정 목록의 그림도 오늘 날짜로 (Windows 는 처음 한 번만 찍어 두어 '31' 이나 예전 날짜가 남았음)
                    ILuncher.AppKit.AppTrayIcon.Show(_trayIcon.Icon.Handle);
                }
                _iconDate = today;
            }
            catch (Exception ex) { Debug.WriteLine($"[AppIcon] {ex.Message}"); }
        }
    }
}
