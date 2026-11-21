// ============================================================
// 필수 NuGet 패키지:  Microsoft.Data.Sqlite
// ============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace DeskCal
{
    // ──────────────────────────────────────────────
    // 데이터 모델
    // ──────────────────────────────────────────────
    public class MemoItem
    {
        public string Text { get; set; } = "";
        public bool IsDone { get; set; } = false;
        public string TextColor { get; set; } = null;
        public bool IsMacro { get; set; } = false;
        public int MacroId { get; set; } = 0;


        public MemoItem Clone() => new MemoItem
        {
            Text = this.Text,
            IsDone = this.IsDone,
            TextColor = this.TextColor,
            IsMacro = this.IsMacro,
            MacroId = this.MacroId
        };
    }

    public class MemoDragData
    {
        public MemoItem Item { get; set; }
        public string SourceDate { get; set; }
        public bool ConfirmCopy { get; set; } // 고정 + 드래그 잠금 중 Ctrl 드래그: 이동은 막고, 놓으면 확인한 뒤 복사
    }

    public class HolidayItem
    {
        public string DateKey { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsRecurring { get; set; } = false;
    }

    public class AppSettings
    {
        public bool IsDarkMode { get; set; } = true;
        public double Opacity { get; set; } = 0.9;
        public double FontSize { get; set; } = 12;
        public bool HideWeekends { get; set; } = false;
        public bool LockDragWhenPinned { get; set; } = false;
        public bool Pinned { get; set; } = false;
        public PinMode PinMode { get; set; } = PinMode.Back;
        public double LastWindowLeft { get; set; } = double.NaN;
        public double LastWindowTop { get; set; } = double.NaN;
        public double LastWindowWidth { get; set; } = 850;
        public double LastWindowHeight { get; set; } = 650;
        public bool IsMaximized { get; set; } = false;
        public int BulletStyle { get; set; } = 0;
        public bool ShowAdjacentMonths { get; set; } = false;
        public bool ConnectConsecutive { get; set; } = false;
        public bool MiniMode { get; set; } = false;
        public bool ShowInTaskbar { get; set; } = false; // 끄면 트레이 아이콘으로만 열기 (기존 동작)

        // 교대근무 (ShiftSchedule.cs)
        public bool ShiftEnabled { get; set; } = false;
        public string ShiftTeam { get; set; } = ShiftSchedule.AllTeams; // "All" 또는 고른 조를 쉼표로 ("B,D")
        public string ShiftSite { get; set; } = "";  // 고른 사업장 id (비면 목록의 첫 사업장)
        public string ShiftSites { get; set; } = ""; // 근무 API 가 준 사업장 목록 JSON (비면 기본 두 묶음)
    }

    // 고정 위치: 맨 뒤(바탕화면 바로 위) / 맨 앞(항상 위)
    public enum PinMode { Back, Front }

    public class MacroSchedule
    {
        public int MacroId { get; set; }
        public string Name { get; set; } = "";
        public string Text { get; set; } = "";
        public string TextColor { get; set; } = null;
        public string RepeatType { get; set; } = "daily";
        public string RepeatValue { get; set; } = "";
        public bool MonthEndFallback { get; set; } = false;
        public DateTime? StartDate { get; set; } = null;
        public DateTime? EndDate { get; set; } = null;
    }

    public class HolidayApiResponse
    {
        public int Year { get; set; }
        public List<HolidayApiItem> Holidays { get; set; } = new();
    }

    public class HolidayApiItem
    {
        public string Date { get; set; }
        public string Name { get; set; }
    }

    // 대화 상자 색. 다크 기준 값이고 라이트 테마에서는 Theme.L 이 같은 역할의 색으로 바꿈 (열 때마다 읽으므로 테마 전환 뒤 새로 연 창부터 적용)
    public static class ModalPalette
    {

        public static Color BgSurface => Theme.L(Color.FromRgb(13, 16, 28));
        public static Color GlowAccent => Theme.L(Color.FromArgb(45, 90, 200, 250));
        // 배경
        public static Color BgDeep => Theme.L(Color.FromRgb(10, 12, 22));        // 최심부 배경
        public static Color BgCard => Theme.L(Color.FromRgb(16, 19, 33));        // 카드 배경
        public static Color BgInput => Theme.L(Color.FromRgb(12, 14, 26));        // 입력창
        public static Color BgRow => Theme.L(Color.FromRgb(20, 23, 38));        // 목록 행
        public static Color BgRowAlt => Theme.L(Color.FromRgb(17, 20, 34));        // 목록 행(교차)
        public static Color BgHover => Theme.L(Color.FromArgb(22, 90, 200, 250)); // hover 배경

        // 테두리
        public static Color BorderGlass => Theme.L(Color.FromArgb(28, 255, 255, 255));
        public static Color BorderAccent => Theme.L(Color.FromArgb(80, 90, 200, 250));
        public static Color BorderInput => Theme.IsDark ? Color.FromArgb(35, 90, 200, 250) : Color.FromRgb(208, 214, 226);

        // 텍스트
        public static Color TxtPrimary => Theme.L(Color.FromRgb(220, 226, 248));
        public static Color TxtSecondary => Theme.L(Color.FromRgb(150, 160, 196));
        public static Color TxtMuted => Theme.L(Color.FromRgb(120, 128, 162));

        // 엑센트
        public static Color Accent => Theme.L(Color.FromRgb(90, 200, 250));
        public static Color AccentDim => Theme.L(Color.FromArgb(30, 90, 200, 250));
        public static Color AccentGlow => Theme.L(Color.FromArgb(60, 90, 200, 250));

        // 시맨틱
        public static Color Danger => Theme.L(Color.FromRgb(230, 70, 80));
        public static Color Success => Theme.L(Color.FromRgb(46, 200, 118));
        public static Color Warning => Theme.L(Color.FromRgb(245, 176, 55));
        public static Color Purple => Theme.L(Color.FromRgb(162, 112, 250));

        // 브러시 헬퍼
        public static SolidColorBrush Brush(Color c) => new SolidColorBrush(c);
        public static LinearGradientBrush AccentGradient(double angle = 45) =>
            new LinearGradientBrush(
                Theme.L(Color.FromRgb(55, 155, 235)),
                Theme.L(Color.FromRgb(90, 200, 250)), angle);
    }


    // ──────────────────────────────────────────────
    // MainWindow
    // ──────────────────────────────────────────────
    public partial class MainWindow : Window
    {
        private DateTime _currentDate = DateTime.Now;
        private Dictionary<string, List<MemoItem>> _memoData = new();
        private List<HolidayItem> _holidayList = new();
        private List<MacroSchedule> _macroList = new();
        private Dictionary<string, int> _runPatternIndex = new();
        private AppSettings _settings = new();

        private Rect _restoreBounds;
        private bool _isFakeMaximized = false;
        private bool _isPrivacyMode = false;

        private bool _isMiniMode = false;
        private double _miniModeRestoreHeight = 650;

        private const string DATA_FOLDER = @"C:\Deskcal";
        private const string DB_FILE = "deskcal.db";
        private const string AppRegistryName = "WpfDesktopCalendar";

        private const string HOLIDAY_API_BASE = "http://localhost:8765";

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private string DbPath => System.IO.Path.Combine(DATA_FOLDER, DB_FILE);
        private string ConnectionString => $"Data Source={DbPath}";

        private List<MonitorInfo> _monitors = new();
        private Point _dragStartPosition;
        private bool _isDragging;


        // ──────────────────────────────────────────
        // 생성자
        // ──────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();

            InitDatabase();

            LoadHolidays();
            LoadData();
            LoadSettings();
            LoadMacros();

            _ = ImportKoreanHolidaysAsync();

            LoadAutoStartStatus();

            SliderOpacity.ValueChanged -= SliderOpacity_ValueChanged;
            SliderFontSize.ValueChanged -= SliderFontSize_ValueChanged;
            TxtFontSizeInput.TextChanged -= TxtFontSizeInput_TextChanged;
            BtnHideWeekends.Checked -= BtnHideWeekends_CheckedChanged;
            BtnHideWeekends.Unchecked -= BtnHideWeekends_CheckedChanged;

            if (ChkLockDrag != null) ChkLockDrag.IsChecked = _settings.LockDragWhenPinned;
            ChkLockDrag.Checked += ChkLockDrag_Changed;
            ChkLockDrag.Unchecked += ChkLockDrag_Changed;

            ChkLockMinimize.IsChecked = _settings.Pinned;
            ChkLockMinimize.Checked += ChkLockMinimize_Changed;
            ChkLockMinimize.Unchecked += ChkLockMinimize_Changed;
            // 고정 상태로 시작(부팅 자동 실행 등)하면 사용 중인 창의 포커스를 뺏지 않음
            ShowActivated = !_settings.Pinned;
            Topmost = IsPinned && _settings.PinMode == PinMode.Front;

            ChkShowInTaskbar.IsChecked = _settings.ShowInTaskbar;
            ShowInTaskbar = _settings.ShowInTaskbar; // 창 핸들이 생기기 전이라 스타일은 SourceInitialized 에서 적용
            ChkShowInTaskbar.Checked += ChkShowInTaskbar_Changed;
            ChkShowInTaskbar.Unchecked += ChkShowInTaskbar_Changed;

            if (ChkConnectConsecutive != null) ChkConnectConsecutive.IsChecked = _settings.ConnectConsecutive;
            ChkConnectConsecutive.Checked += ChkConnectConsecutive_Changed;
            ChkConnectConsecutive.Unchecked += ChkConnectConsecutive_Changed;

            UpdateBulletButtonsVisual();
            SliderOpacity.Value = _settings.Opacity;
            if (TxtOpacityValue != null)
                TxtOpacityValue.Text = $"{(int)(_settings.Opacity * 100)}%";
            SliderFontSize.Value = _settings.FontSize;
            TxtFontSizeInput.Text = ((int)_settings.FontSize).ToString();
            BtnHideWeekends.IsChecked = _settings.HideWeekends;
            if (CalChkShowAdjacentMonths != null)
                CalChkShowAdjacentMonths.IsChecked = _settings.ShowAdjacentMonths;

            SliderOpacity.ValueChanged += SliderOpacity_ValueChanged;
            SliderFontSize.ValueChanged += SliderFontSize_ValueChanged;
            TxtFontSizeInput.TextChanged += TxtFontSizeInput_TextChanged;
            BtnHideWeekends.Checked += BtnHideWeekends_CheckedChanged;
            BtnHideWeekends.Unchecked += BtnHideWeekends_CheckedChanged;

            InitShiftSettings();

            _isMiniMode = _settings.MiniMode;
            UpdateViewModeButton();
            TxtVersion.Text = GetAppVersion();

            ApplyTheme();
            RefreshCalendar();
            MainBorder.SizeChanged += (s, e) => UpdateMainClip();

            // StateChanged 는 XAML 에서 연결됨
            this.SourceInitialized += MainWindow_SourceInitialized;
            this.Closing += MainWindow_Closing;
            this.Closed += MainWindow_Closed;
            this.Loaded += MainWindow_Loaded;

            InitTrayIcon();
            InitAppIcon(); // 아이콘에 오늘 요일·날짜 (아이폰 캘린더처럼)
            StartUpdateChecks();
            UpdatePinIndicators();

            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                { ShowSearchWindow(); e.Handled = true; }
            };
        }

        // ══════════════════════════════════════════════════════════════
        // FastAPI 기반 한국 공휴일 자동 등록
        // ══════════════════════════════════════════════════════════════
        private async Task ImportKoreanHolidaysAsync(bool showResult = false)
        {
            int currentYear = DateTime.Now.Year;
            int fromYear = currentYear - 1;
            int toYear = currentYear + 3;
            int addedCount = 0;
            var errors = new List<string>();

            var recurringHolidays = new List<(string Key, string Name)>
            {
                ("01-01", "신정"), ("03-01", "삼일절"), ("05-05", "어린이날"),
                ("06-06", "현충일"), ("08-15", "광복절"), ("10-03", "개천절"),
                ("10-09", "한글날"), ("12-25", "크리스마스"),
            };

            foreach (var (key, name) in recurringHolidays)
            {
                if (_holidayList.Any(x => x.IsRecurring && x.DateKey == key)) continue;
                _holidayList.Add(new HolidayItem { DateKey = key, Name = name, IsRecurring = true });
                addedCount++;
            }

            for (int year = fromYear; year <= toYear; year++)
            {
                try
                {
                    string url = $"{HOLIDAY_API_BASE}/holidays/KR/{year}";
                    string json = await _httpClient.GetStringAsync(url);
                    var resp = JsonSerializer.Deserialize<HolidayApiResponse>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    foreach (var h in resp?.Holidays ?? new List<HolidayApiItem>())
                    {
                        bool dupExact = _holidayList.Any(x => !x.IsRecurring && x.DateKey == h.Date);
                        if (dupExact) continue;
                        if (DateTime.TryParse(h.Date, out DateTime hDate))
                        {
                            string mmdd = hDate.ToString("MM-dd");
                            if (_holidayList.Any(x => x.IsRecurring && x.DateKey == mmdd)) continue;
                        }
                        _holidayList.Add(new HolidayItem { DateKey = h.Date, Name = h.Name, IsRecurring = false });
                        addedCount++;
                    }
                }
                catch (HttpRequestException ex) { errors.Add($"{year}년 API 연결 실패: {ex.Message}"); }
                catch (TaskCanceledException) { errors.Add($"{year}년 API 타임아웃"); }
                catch (Exception ex) { errors.Add($"{year}년 오류: {ex.Message}"); }
            }

            if (addedCount > 0)
            {
                _holidayList = _holidayList.OrderByDescending(h => h.IsRecurring).ThenBy(h => h.DateKey).ToList();
                await Dispatcher.InvokeAsync(() =>
                {
                    try { SaveHolidays(); } catch (Exception ex) { errors.Add($"DB 저장 실패: {ex.Message}"); }
                    try { RefreshCalendar(); } catch (Exception ex) { errors.Add($"캘린더 갱신 실패: {ex.Message}"); }
                });
            }

            if (showResult || errors.Count > 0)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    string errInfo = errors.Count > 0 ? $"\n\n⚠️ 오류:\n{string.Join("\n", errors)}" : "";
                    if (showResult)
                    {
                        string msg = addedCount > 0
                            ? $"한국 공휴일 {addedCount}건을 새로 등록했습니다.\n({fromYear}년 ~ {toYear}년){errInfo}"
                            : $"새로 추가할 공휴일이 없습니다.\n({fromYear}~{toYear}년은 이미 등록되어 있습니다.){errInfo}";
                        MessageBox.Show(msg, "한국 공휴일 자동 등록", MessageBoxButton.OK,
                            errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                    }
                    else if (errors.Count > 0)
                        Debug.WriteLine($"[Holiday API] 오류: {string.Join(", ", errors)}");
                });
            }
        }

        private void BtnViewMode_Click(object sender, RoutedEventArgs e)
        {
            bool weekly = sender == BtnViewWeek;
            if (weekly == _isMiniMode) { UpdateViewModeButton(); return; } // 이미 선택된 탭: 눌러서 풀린 선택 표시만 되돌림
            _isMiniMode = weekly;
            if (_isMiniMode)
            {
                if (_isFakeMaximized) PerformRestore();
                _miniModeRestoreHeight = this.Height;
                double cellH = Math.Max(120, _settings.FontSize * 8 + 40);
                this.Height = 72 + 42 + cellH + 24;
            }
            else
            {
                this.Height = _miniModeRestoreHeight;
            }
            UpdateViewModeButton();
            _settings.MiniMode = _isMiniMode;
            SaveSettings();
            RefreshCalendar();
        }

        // 좌상단 버전 표시 (러너가 릴리스 번호로 빌드, 직접 빌드하면 csproj 의 Version)
        private static string GetAppVersion() => "v" + UpdateService.CurrentVersion.ToString(3);

        // 현재 보기(월간/주간) 탭 선택 표시
        private void UpdateViewModeButton()
        {
            BtnViewMonth.IsChecked = !_isMiniMode;
            BtnViewWeek.IsChecked = _isMiniMode;
        }

        // ══════════════════════════════════════════
        // SQLite 초기화
        // ══════════════════════════════════════════
        private void InitDatabase()
        {
            try
            {
                if (!Directory.Exists(DATA_FOLDER))
                    Directory.CreateDirectory(DATA_FOLDER);

                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS memos (
                date_key   TEXT    NOT NULL,
                item_index INTEGER NOT NULL,
                text       TEXT    DEFAULT '',
                is_done    INTEGER DEFAULT 0,
                text_color TEXT,
                PRIMARY KEY (date_key, item_index)
            );

            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT
            );

            -- 근무 API 로 받은 교대근무 (docs/shift-api.md). 사업장마다 하루 한 조에 한 줄
            CREATE TABLE IF NOT EXISTS shifts (
                site     TEXT NOT NULL DEFAULT '',
                date_key TEXT NOT NULL,
                team     TEXT NOT NULL,
                code     TEXT NOT NULL,
                PRIMARY KEY (site, date_key, team)
            );

            CREATE TABLE IF NOT EXISTS holidays (
                date_key     TEXT    NOT NULL,
                is_recurring INTEGER NOT NULL DEFAULT 0,
                name         TEXT    DEFAULT '',
                PRIMARY KEY (date_key, is_recurring)
            );

            CREATE TABLE IF NOT EXISTS macro_schedules (
                macro_id     INTEGER PRIMARY KEY AUTOINCREMENT,
                name         TEXT    NOT NULL DEFAULT '',
                text         TEXT    NOT NULL DEFAULT '',
                text_color   TEXT,
                repeat_type  TEXT    NOT NULL DEFAULT 'daily',
                repeat_value TEXT    DEFAULT '',
                month_end_fallback INTEGER DEFAULT 0
            );
        ";
                cmd.ExecuteNonQuery();

                MigrateShiftsTable(conn);

                // is_macro 컬럼 마이그레이션
                try
                {
                    using var alter = conn.CreateCommand();
                    alter.CommandText = "ALTER TABLE memos ADD COLUMN is_macro INTEGER DEFAULT 0";
                    alter.ExecuteNonQuery();
                }
                catch { }

                // macro_id 컬럼 마이그레이션
                try
                {
                    using var alter2 = conn.CreateCommand();
                    alter2.CommandText = "ALTER TABLE memos ADD COLUMN macro_id INTEGER DEFAULT 0";
                    alter2.ExecuteNonQuery();
                }
                catch { }

                try
                {
                    using var alter3 = conn.CreateCommand();
                    alter3.CommandText = "ALTER TABLE macro_schedules ADD COLUMN start_date TEXT";
                    alter3.ExecuteNonQuery();
                }
                catch { }

                try
                {
                    using var alter4 = conn.CreateCommand();
                    alter4.CommandText = "ALTER TABLE macro_schedules ADD COLUMN end_date TEXT";
                    alter4.ExecuteNonQuery();
                }
                catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DB 초기화 실패:\n{ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════
        // 메모 데이터 – Load / Save
        // ══════════════════════════════════════════
        private void LoadData()
        {
            _memoData = new Dictionary<string, List<MemoItem>>();
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT date_key, text, is_done, text_color, is_macro, macro_id FROM memos ORDER BY date_key, item_index";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string dateKey = reader.GetString(0);
                    var item = new MemoItem
                    {
                        Text = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        IsDone = reader.GetInt32(2) == 1,
                        TextColor = reader.IsDBNull(3) ? null : reader.GetString(3),
                        IsMacro = !reader.IsDBNull(4) && reader.GetInt32(4) == 1,
                        MacroId = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
                    };
                    if (!_memoData.ContainsKey(dateKey))
                        _memoData[dateKey] = new List<MemoItem>();
                    _memoData[dateKey].Add(item);
                }
            }
            catch { }
            RebuildRunPatterns();
        }

        private void SaveData()
        {
            ILuncher.AppKit.AppUsage.Count("event");
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();
                using (var del = conn.CreateCommand())
                {
                    del.Transaction = tx;
                    del.CommandText = "DELETE FROM memos";
                    del.ExecuteNonQuery();
                }

                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO memos (date_key, item_index, text, is_done, text_color, is_macro, macro_id) VALUES (@dk, @idx, @txt, @done, @color, @macro, @macroid)";
                var pDk = ins.Parameters.Add("@dk", SqliteType.Text);
                var pIdx = ins.Parameters.Add("@idx", SqliteType.Integer);
                var pTxt = ins.Parameters.Add("@txt", SqliteType.Text);
                var pDone = ins.Parameters.Add("@done", SqliteType.Integer);
                var pColor = ins.Parameters.Add("@color", SqliteType.Text);
                var pMacro = ins.Parameters.Add("@macro", SqliteType.Integer);
                var pMacroId = ins.Parameters.Add("@macroid", SqliteType.Integer);

                foreach (var kv in _memoData)
                {
                    for (int i = 0; i < kv.Value.Count; i++)
                    {
                        var m = kv.Value[i];
                        pDk.Value = kv.Key;
                        pIdx.Value = i;
                        pTxt.Value = (object?)m.Text ?? DBNull.Value;
                        pDone.Value = m.IsDone ? 1 : 0;
                        pColor.Value = (object?)m.TextColor ?? DBNull.Value;
                        pMacro.Value = m.IsMacro ? 1 : 0;
                        pMacroId.Value = m.MacroId;
                        ins.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
            catch { }
            RebuildRunPatterns();
        }

        // ══════════════════════════════════════════
        // 설정 – Load / Save
        // ══════════════════════════════════════════
        private void LoadSettings()
        {
            _settings = new AppSettings();
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT key, value FROM settings";
                using var reader = cmd.ExecuteReader();
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (reader.Read()) dict[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);

                if (dict.TryGetValue("Opacity", out var v)) _settings.Opacity = TryDouble(v, 0.9);
                if (dict.TryGetValue("FontSize", out v)) _settings.FontSize = TryDouble(v, 12);
                if (dict.TryGetValue("HideWeekends", out v)) _settings.HideWeekends = v == "1";
                if (dict.TryGetValue("LastWindowLeft", out v)) _settings.LastWindowLeft = TryDouble(v, double.NaN);
                if (dict.TryGetValue("LastWindowTop", out v)) _settings.LastWindowTop = TryDouble(v, double.NaN);
                if (dict.TryGetValue("LastWindowWidth", out v)) _settings.LastWindowWidth = TryDouble(v, 850);
                if (dict.TryGetValue("LastWindowHeight", out v)) _settings.LastWindowHeight = TryDouble(v, 650);
                if (dict.TryGetValue("IsMaximized", out v)) _settings.IsMaximized = v == "1";
                if (dict.TryGetValue("BulletStyle", out v)) _settings.BulletStyle = TryInt(v, 0);
                if (dict.TryGetValue("ShowAdjacentMonths", out v)) _settings.ShowAdjacentMonths = v == "1";
                if (dict.TryGetValue("LockDragWhenPinned", out v)) _settings.LockDragWhenPinned = v == "1";
                if (dict.TryGetValue("Pinned", out v)) _settings.Pinned = v == "1";
                if (dict.TryGetValue("PinMode", out v) && Enum.TryParse(v, out PinMode pinMode)) _settings.PinMode = pinMode;
                if (dict.TryGetValue("ConnectConsecutive", out v)) _settings.ConnectConsecutive = v == "1";
                if (dict.TryGetValue("MiniMode", out v)) _settings.MiniMode = v == "1";
                if (dict.TryGetValue("IsDarkMode", out v)) _settings.IsDarkMode = v != "0";
                if (dict.TryGetValue("ShowInTaskbar", out v)) _settings.ShowInTaskbar = v == "1";
                if (dict.TryGetValue("ShiftEnabled", out v)) _settings.ShiftEnabled = v == "1";
                if (dict.TryGetValue("ShiftTeam", out v) && ParseShiftTeams(v) != null) _settings.ShiftTeam = v;
                if (dict.TryGetValue("ShiftSite", out v)) _settings.ShiftSite = v;
                if (dict.TryGetValue("ShiftSites", out v)) _settings.ShiftSites = v;
            }
            catch { }
        }


        private void SaveSettings()
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES (@k, @v)";
                var pk = cmd.Parameters.Add("@k", SqliteType.Text);
                var pv = cmd.Parameters.Add("@v", SqliteType.Text);
                void Upsert(string key, string value) { pk.Value = key; pv.Value = value; cmd.ExecuteNonQuery(); }

                Upsert("Opacity", _settings.Opacity.ToString(CultureInfo.InvariantCulture));
                Upsert("FontSize", _settings.FontSize.ToString(CultureInfo.InvariantCulture));
                Upsert("HideWeekends", _settings.HideWeekends ? "1" : "0");
                Upsert("LastWindowLeft", _settings.LastWindowLeft.ToString(CultureInfo.InvariantCulture));
                Upsert("LastWindowTop", _settings.LastWindowTop.ToString(CultureInfo.InvariantCulture));
                Upsert("LastWindowWidth", _settings.LastWindowWidth.ToString(CultureInfo.InvariantCulture));
                Upsert("LastWindowHeight", _settings.LastWindowHeight.ToString(CultureInfo.InvariantCulture));
                Upsert("IsMaximized", _settings.IsMaximized ? "1" : "0");
                Upsert("BulletStyle", _settings.BulletStyle.ToString());
                Upsert("ShowAdjacentMonths", _settings.ShowAdjacentMonths ? "1" : "0");
                Upsert("LockDragWhenPinned", _settings.LockDragWhenPinned ? "1" : "0");
                Upsert("Pinned", _settings.Pinned ? "1" : "0");
                Upsert("PinMode", _settings.PinMode.ToString());
                Upsert("ConnectConsecutive", _settings.ConnectConsecutive ? "1" : "0");
                Upsert("MiniMode", _settings.MiniMode ? "1" : "0");
                Upsert("IsDarkMode", _settings.IsDarkMode ? "1" : "0");
                Upsert("ShowInTaskbar", _settings.ShowInTaskbar ? "1" : "0");
                Upsert("ShiftEnabled", _settings.ShiftEnabled ? "1" : "0");
                Upsert("ShiftTeam", _settings.ShiftTeam);
                Upsert("ShiftSite", _settings.ShiftSite);
                Upsert("ShiftSites", _settings.ShiftSites);
                tx.Commit();
            }
            catch { }
        }

        private void ChkConnectConsecutive_Changed(object sender, RoutedEventArgs e)
        {
            _settings.ConnectConsecutive = (ChkConnectConsecutive.IsChecked == true);
            RefreshCalendar();
            SaveSettings();
        }

        // 연속일정 판정 (텍스트만 일치, 공백 제거 후 비교)
        private bool HasMatchingMemo(DateTime day, MemoItem memo)
        {
            if (memo == null) return false;
            string t = (memo.Text ?? "").Trim();
            if (t.Length == 0) return false;
            string key = day.ToString("yyyy-MM-dd");
            if (!_memoData.TryGetValue(key, out var list) || list == null) return false;
            return list.Any(m => string.Equals((m.Text ?? "").Trim(), t, StringComparison.Ordinal));
        }

        // 평일(월~금) 반복 매크로 메모인지
        private bool IsWeekdayMacro(MemoItem memo)
        {
            if (memo == null || !memo.IsMacro || memo.MacroId <= 0) return false;
            var macro = _macroList.FirstOrDefault(x => x.MacroId == memo.MacroId);
            return macro != null && macro.RepeatType == "weekdays";
        }

        // 두 날짜가 같은 시각적 줄(일~토 한 주)에 있는지
        private static bool SameVisualRow(DateTime a, DateTime b)
            => a.AddDays(-(int)a.DayOfWeek) == b.AddDays(-(int)b.DayOfWeek);

        // 주말 칸: 금↔월을 가로지르는 평일 매크로(다리)들의 (텍스트, 색) 목록
        private List<(string text, string color)> WeekendBridges(DateTime weekendDate)
        {
            var result = new List<(string text, string color)>();
            if (!_settings.ConnectConsecutive || _settings.HideWeekends || _isPrivacyMode) return result;
            if (weekendDate.DayOfWeek != DayOfWeek.Saturday && weekendDate.DayOfWeek != DayOfWeek.Sunday) return result;

            DateTime fri = weekendDate; while (fri.DayOfWeek != DayOfWeek.Friday) fri = fri.AddDays(-1);
            DateTime mon = weekendDate; while (mon.DayOfWeek != DayOfWeek.Monday) mon = mon.AddDays(1);

            if (!_memoData.TryGetValue(fri.ToString("yyyy-MM-dd"), out var friList)) return result;
            if (!_memoData.TryGetValue(mon.ToString("yyyy-MM-dd"), out var monList)) return result;

            foreach (var fm in friList)
            {
                if (!IsWeekdayMacro(fm)) continue;
                string t = (fm.Text ?? "").Trim();
                if (t.Length == 0 || result.Any(r => r.text == t)) continue;
                if (monList.Any(mm => IsWeekdayMacro(mm) && (mm.Text ?? "").Trim() == t))
                    result.Add((t, fm.TextColor));
            }
            return result;
        }




        // 양방향 인접일로 이어지는 같은 텍스트 non-macro 메모에 색 전파 (인접 매칭 없으면 전파 안 함 = 연속만)
        private void PropagateRunColor(string dateKey, string text, string newColor)
        {
            if (!DateTime.TryParse(dateKey, out var d0)) return;
            text = (text ?? "").Trim();
            if (text.Length == 0) return;

            // 왼쪽
            for (DateTime d = d0.AddDays(-1); ; d = d.AddDays(-1))
            {
                string k = d.ToString("yyyy-MM-dd");
                if (!_memoData.TryGetValue(k, out var list)) break;
                var matches = list.Where(m => !m.IsMacro && (m.Text ?? "").Trim() == text).ToList();
                if (matches.Count == 0) break;
                foreach (var m in matches) m.TextColor = newColor;
            }
            // 오른쪽
            for (DateTime d = d0.AddDays(1); ; d = d.AddDays(1))
            {
                string k = d.ToString("yyyy-MM-dd");
                if (!_memoData.TryGetValue(k, out var list)) break;
                var matches = list.Where(m => !m.IsMacro && (m.Text ?? "").Trim() == text).ToList();
                if (matches.Count == 0) break;
                foreach (var m in matches) m.TextColor = newColor;
            }
        }


        // 연속일정 연결 ON: 같은 텍스트 non-macro 연속 run의 색을 하나로 통일 (가장 이른 날의 지정색 우선)
        // 연속 run 색 통일. 매크로가 끼면 매크로 색을 기준으로, 적용은 일반(non-macro) 메모에만
        private void UnifyRunColor(string dateKey, string text)
        {
            if (!DateTime.TryParse(dateKey, out var d0)) return;
            text = (text ?? "").Trim();
            if (text.Length == 0) return;

            // 같은 텍스트 메모(매크로 포함) — run 범위 판정용
            List<MemoItem> MatchesOn(DateTime d)
            {
                string k = d.ToString("yyyy-MM-dd");
                if (!_memoData.TryGetValue(k, out var list)) return null;
                var ms = list.Where(m => (m.Text ?? "").Trim() == text).ToList();
                return ms.Count > 0 ? ms : null;
            }

            var runDays = new List<DateTime> { d0 };
            for (DateTime d = d0.AddDays(-1); MatchesOn(d) != null; d = d.AddDays(-1)) runDays.Add(d);
            for (DateTime d = d0.AddDays(1); MatchesOn(d) != null; d = d.AddDays(1)) runDays.Add(d);

            if (runDays.Count <= 1) return; // 연속 아님
            runDays.Sort();

            bool hasMacro = runDays.Any(d => MatchesOn(d)?.Any(m => m.IsMacro) == true);

            string unified = null;
            if (hasMacro)
            {
                // 매크로 색을 기준색으로 (가장 이른 날의 매크로)
                foreach (var d in runDays)
                {
                    var macroM = MatchesOn(d)?.FirstOrDefault(m => m.IsMacro);
                    if (macroM != null) { unified = macroM.TextColor; break; }
                }
            }
            else
            {
                // 매크로 없으면 가장 이른 날의 첫 지정색
                foreach (var d in runDays)
                {
                    var colored = MatchesOn(d)?.FirstOrDefault(m => !string.IsNullOrEmpty(m.TextColor));
                    if (colored != null) { unified = colored.TextColor; break; }
                }
            }

            // 적용: 일반 메모에만 (매크로 인스턴스는 정의색 유지)
            foreach (var d in runDays)
            {
                var ms = MatchesOn(d);
                if (ms == null) continue;
                foreach (var m in ms) if (!m.IsMacro) m.TextColor = unified;
            }
        }

        // 색 직접 변경: 연속 run(같은 텍스트, 매크로 포함) + 관련 매크로 전체 인스턴스 + 매크로 정의색 갱신
        private void ApplyColorToMergedRun(string dateKey, string text, string newColor)
        {
            if (!DateTime.TryParse(dateKey, out var d0)) return;
            text = (text ?? "").Trim();
            if (text.Length == 0) return;

            List<MemoItem> MatchesOn(DateTime d)
            {
                string k = d.ToString("yyyy-MM-dd");
                if (!_memoData.TryGetValue(k, out var list)) return null;
                var ms = list.Where(m => (m.Text ?? "").Trim() == text).ToList();
                return ms.Count > 0 ? ms : null;
            }

            // 1) 연속 run 범위(매크로 포함)에 색 적용 + 매크로 id 수집
            var runDays = new List<DateTime> { d0 };
            for (DateTime d = d0.AddDays(-1); MatchesOn(d) != null; d = d.AddDays(-1)) runDays.Add(d);
            for (DateTime d = d0.AddDays(1); MatchesOn(d) != null; d = d.AddDays(1)) runDays.Add(d);

            var macroIds = new HashSet<int>();
            foreach (var d in runDays)
            {
                var ms = MatchesOn(d);
                if (ms == null) continue;
                foreach (var m in ms)
                {
                    m.TextColor = newColor;
                    if (m.IsMacro && m.MacroId > 0) macroIds.Add(m.MacroId);
                }
            }

            // 2) run에 매크로가 끼면 그 매크로 전체 인스턴스 + 정의색도 갱신
            foreach (var id in macroIds)
            {
                foreach (var kv in _memoData)
                    foreach (var m in kv.Value)
                        if (m.IsMacro && m.MacroId == id)
                            m.TextColor = newColor;
                var macro = _macroList.FirstOrDefault(x => x.MacroId == id);
                if (macro != null) { macro.TextColor = newColor; SaveMacro(macro); }
            }
        }

        // 매크로 색 변경: 같은 MacroId의 모든 메모 + 매크로 정의 색을 함께 갱신
        private void PropagateMacroColor(int macroId, string newColor)
        {
            if (macroId <= 0) return;
            foreach (var kv in _memoData)
                foreach (var m in kv.Value)
                    if (m.IsMacro && m.MacroId == macroId)
                        m.TextColor = newColor;

            var macro = _macroList.FirstOrDefault(x => x.MacroId == macroId);
            if (macro != null)
            {
                macro.TextColor = newColor;
                SaveMacro(macro);   // 재적용 시에도 색 유지
            }
        }


        // 같은 날 겹치는 서로 다른 연속일정에 다른 패턴 인덱스를 배정 (전역 1회)
        private void RebuildRunPatterns()
        {
            _runPatternIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            if (_memoData == null || _memoData.Count == 0) return;

            // 날짜별 텍스트 집합
            var dayTexts = new Dictionary<DateTime, HashSet<string>>();
            foreach (var kv in _memoData)
            {
                if (!DateTime.TryParse(kv.Key, out var d)) continue;
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var m in kv.Value)
                {
                    string t = (m.Text ?? "").Trim();
                    if (t.Length > 0) set.Add(t);
                }
                if (set.Count > 0) dayTexts[d.Date] = set;
            }

            // 같은 날 run-active(인접일에도 존재)인 서로 다른 텍스트 = 충돌
            var conflicts = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            void EnsureNode(string t) { if (!conflicts.ContainsKey(t)) conflicts[t] = new HashSet<string>(StringComparer.Ordinal); }

            foreach (var pair in dayTexts)
            {
                DateTime d = pair.Key;
                var active = new List<string>();
                foreach (var t in pair.Value)
                {
                    bool prev = dayTexts.TryGetValue(d.AddDays(-1), out var ps) && ps.Contains(t);
                    bool next = dayTexts.TryGetValue(d.AddDays(1), out var ns) && ns.Contains(t);
                    if (prev || next) active.Add(t);
                }
                for (int i = 0; i < active.Count; i++)
                {
                    EnsureNode(active[i]);
                    for (int j = i + 1; j < active.Count; j++)
                    {
                        EnsureNode(active[j]);
                        conflicts[active[i]].Add(active[j]);
                        conflicts[active[j]].Add(active[i]);
                    }
                }
            }

            // 그리디 컬러링 (패턴 4종)
            const int PATTERN_COUNT = 4;
            foreach (var t in conflicts.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                var used = new HashSet<int>();
                foreach (var nb in conflicts[t])
                    if (_runPatternIndex.TryGetValue(nb, out int pi)) used.Add(pi);
                int idx = 0;
                while (idx < PATTERN_COUNT && used.Contains(idx)) idx++;
                if (idx >= PATTERN_COUNT) idx %= PATTERN_COUNT; // 4색 초과(드묾) 폴백
                _runPatternIndex[t] = idx;
            }
        }

        private static readonly Color[] RunPalette =
        {
            Color.FromRgb(84, 160, 255),   // 파랑
            Color.FromRgb(255, 107, 107),  // 빨강
            Color.FromRgb(46, 204, 113),   // 초록
            Color.FromRgb(254, 202, 87),   // 노랑
            Color.FromRgb(162, 112, 250),  // 보라
            Color.FromRgb(255, 159, 67),   // 주황
            Color.FromRgb(72, 219, 251),   // 시안
            Color.FromRgb(163, 203, 56),   // 연두
        };

        // 막대 배경·글자색 = 일정 색 계열. 다크는 어둡게 깐 막대 + 밝은 글자, 라이트는 옅게 깐 막대 + 진한 글자 (Theme.cs)
        private static Color RunBarBg(Color c) => Theme.RunBar(c);
        private static Color RunTextColor(Color c) => Theme.RunText(c);


        private void ChkLockDrag_Changed(object sender, RoutedEventArgs e)
        {
            _settings.LockDragWhenPinned = (ChkLockDrag.IsChecked == true);
            SaveSettings();
        }

        // ══════════════════════════════════════════
        // 공휴일 – Load / Save
        // ══════════════════════════════════════════
        private void LoadHolidays()
        {
            _holidayList = new List<HolidayItem>();
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT date_key, is_recurring, name FROM holidays ORDER BY is_recurring DESC, date_key";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    _holidayList.Add(new HolidayItem
                    {
                        DateKey = reader.GetString(0),
                        IsRecurring = reader.GetInt32(1) == 1,
                        Name = reader.IsDBNull(2) ? "" : reader.GetString(2)
                    });
            }
            catch { }
        }

        private void SaveHolidays()
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();
                using (var del = conn.CreateCommand()) { del.Transaction = tx; del.CommandText = "DELETE FROM holidays"; del.ExecuteNonQuery(); }
                using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO holidays (date_key, is_recurring, name) VALUES (@dk, @recur, @name)";
                var pDk = ins.Parameters.Add("@dk", SqliteType.Text);
                var pRecur = ins.Parameters.Add("@recur", SqliteType.Integer);
                var pName = ins.Parameters.Add("@name", SqliteType.Text);
                foreach (var h in _holidayList) { pDk.Value = h.DateKey; pRecur.Value = h.IsRecurring ? 1 : 0; pName.Value = h.Name; ins.ExecuteNonQuery(); }
                tx.Commit();
            }
            catch { }
        }

        private string GetHolidayName(DateTime d)
        {
            string fullKey = d.ToString("yyyy-MM-dd");
            string recurKey = d.ToString("MM-dd");
            var exact = _holidayList.FirstOrDefault(h => !h.IsRecurring && h.DateKey == fullKey);
            if (exact != null) return exact.Name;
            var recur = _holidayList.FirstOrDefault(h => h.IsRecurring && h.DateKey == recurKey);
            return recur?.Name;
        }

        // ══════════════════════════════════════════
        // 매크로 일정 – Load / Save / Delete
        // ══════════════════════════════════════════
        private void LoadMacros()
        {
            _macroList = new List<MacroSchedule>();
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT macro_id, name, text, text_color, repeat_type, repeat_value, month_end_fallback, start_date, end_date FROM macro_schedules ORDER BY macro_id";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    _macroList.Add(new MacroSchedule
                    {
                        MacroId = reader.GetInt32(0),
                        Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Text = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        TextColor = reader.IsDBNull(3) ? null : reader.GetString(3),
                        RepeatType = reader.IsDBNull(4) ? "daily" : reader.GetString(4),
                        RepeatValue = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        MonthEndFallback = !reader.IsDBNull(6) && reader.GetInt32(6) == 1,
                        StartDate = reader.IsDBNull(7) ? null : DateTime.TryParse(reader.GetString(7), out var sd) ? sd : null,
                        EndDate = reader.IsDBNull(8) ? null : DateTime.TryParse(reader.GetString(8), out var ed) ? ed : null
                    });
            }
            catch { }
        }

        private void SaveMacro(MacroSchedule macro)
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                if (macro.MacroId == 0)
                {
                    cmd.CommandText = @"INSERT INTO macro_schedules (name, text, text_color, repeat_type, repeat_value, month_end_fallback, start_date, end_date)
                                VALUES (@name, @text, @color, @rtype, @rval, @mef, @sdate, @edate)";
                }
                else
                {
                    cmd.CommandText = @"INSERT OR REPLACE INTO macro_schedules (macro_id, name, text, text_color, repeat_type, repeat_value, month_end_fallback, start_date, end_date)
                                VALUES (@id, @name, @text, @color, @rtype, @rval, @mef, @sdate, @edate)";
                    cmd.Parameters.AddWithValue("@id", macro.MacroId);
                }
                cmd.Parameters.AddWithValue("@name", macro.Name);
                cmd.Parameters.AddWithValue("@text", macro.Text);
                cmd.Parameters.AddWithValue("@color", (object)macro.TextColor ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rtype", macro.RepeatType);
                cmd.Parameters.AddWithValue("@rval", macro.RepeatValue ?? "");
                cmd.Parameters.AddWithValue("@mef", macro.MonthEndFallback ? 1 : 0);
                cmd.Parameters.AddWithValue("@sdate", macro.StartDate.HasValue ? (object)macro.StartDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                cmd.Parameters.AddWithValue("@edate", macro.EndDate.HasValue ? (object)macro.EndDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                cmd.ExecuteNonQuery();

                if (macro.MacroId == 0)
                {
                    using var cmd2 = conn.CreateCommand();
                    cmd2.CommandText = "SELECT last_insert_rowid()";
                    macro.MacroId = Convert.ToInt32(cmd2.ExecuteScalar());
                }
            }
            catch { }
        }

        private void DeleteMacro(int macroId)
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM macro_schedules WHERE macro_id = @id";
                cmd.Parameters.AddWithValue("@id", macroId);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        private bool IsMacroDateMatch(MacroSchedule macro, DateTime date)
        {
            switch (macro.RepeatType)
            {
                case "daily":
                    return true;
                case "weekdays":
                    return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
                case "weekly":
                    if (string.IsNullOrEmpty(macro.RepeatValue)) return false;
                    var days = macro.RepeatValue.Split(',').Select(s => int.TryParse(s.Trim(), out int v) ? v : -1).Where(v => v >= 0 && v <= 6).ToHashSet();
                    return days.Contains((int)date.DayOfWeek);
                case "monthly":
                    if (string.IsNullOrEmpty(macro.RepeatValue)) return false;
                    var monthDays = macro.RepeatValue.Split(',').Select(s => int.TryParse(s.Trim(), out int v) ? v : -1).Where(v => v >= 1 && v <= 31).ToHashSet();
                    if (monthDays.Contains(date.Day)) return true;
                    if (macro.MonthEndFallback)
                    {
                        int lastDay = DateTime.DaysInMonth(date.Year, date.Month);
                        if (date.Day == lastDay && monthDays.Any(d => d > lastDay)) return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private int ApplyMacroToRange(MacroSchedule macro, DateTime from, DateTime to)
        {
            int count = 0;
            for (DateTime d = from.Date; d <= to.Date; d = d.AddDays(1))
            {
                if (!IsMacroDateMatch(macro, d)) continue;
                string dateKey = d.ToString("yyyy-MM-dd");
                if (!_memoData.ContainsKey(dateKey))
                    _memoData[dateKey] = new List<MemoItem>();
                if (_memoData[dateKey].Any(m => m.IsMacro && m.MacroId == macro.MacroId)) continue;
                _memoData[dateKey].Add(new MemoItem
                {
                    Text = macro.Text,
                    IsDone = false,
                    TextColor = macro.TextColor,
                    IsMacro = true,
                    MacroId = macro.MacroId
                });
                count++;
            }
            if (count > 0) { SaveData(); RefreshCalendar(); }
            return count;
        }

        private int RemoveMacroFromRange(MacroSchedule macro, DateTime from, DateTime to)
        {
            int count = 0;
            for (DateTime d = from.Date; d <= to.Date; d = d.AddDays(1))
            {
                string dateKey = d.ToString("yyyy-MM-dd");
                if (!_memoData.ContainsKey(dateKey)) continue;
                int removed = _memoData[dateKey].RemoveAll(m => m.IsMacro && m.MacroId == macro.MacroId);
                if (removed > 0) count += removed;
                if (_memoData[dateKey].Count == 0) _memoData.Remove(dateKey);
            }
            if (count > 0) { SaveData(); RefreshCalendar(); }
            return count;
        }

        // ══════════════════════════════════════════
        // 파싱 헬퍼
        // ══════════════════════════════════════════
        private static double TryDouble(string s, double fallback) =>
            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        private static int TryInt(string s, int fallback) =>
            int.TryParse(s, out var v) ? v : fallback;

        // ──────────────────────────────────────────
        // 설정 이벤트 핸들러
        // ──────────────────────────────────────────
        private void CalChkShowAdjacentMonths_Changed(object sender, RoutedEventArgs e)
        {
            _settings.ShowAdjacentMonths = (CalChkShowAdjacentMonths.IsChecked == true);
            RefreshCalendar(); SaveSettings();
        }

        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            InitMonitors();
            if (_settings.LastWindowWidth > 0 && _settings.LastWindowHeight > 0) { this.Width = _settings.LastWindowWidth; this.Height = _settings.LastWindowHeight; }
            bool positionRestored = false;
            if (!double.IsNaN(_settings.LastWindowLeft) && !double.IsNaN(_settings.LastWindowTop)) { this.Left = _settings.LastWindowLeft; this.Top = _settings.LastWindowTop; positionRestored = true; }
            if (!positionRestored) MoveToMonitor(0, center: true);
            else EnsureOnScreen();
            if (_settings.IsMaximized) PerformMaximize(force: true);
            UpdateBulletButtonsVisual();
            if (ShouldStayOnDesktop()) SendToDesktopLayer();
        }

        // 작업 표시줄 버튼이 없어 화면 밖으로 나간 창은 되찾을 방법이 없으므로, 저장 위치가 화면을 벗어나면 가운데로
        private void EnsureOnScreen()
        {
            var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                                  SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            var titleBar = Rect.Intersect(screen, new Rect(this.Left, this.Top, this.Width, 72));
            if (!titleBar.IsEmpty && titleBar.Width >= 100 && titleBar.Height >= 30) return;
            Rect workArea = SystemParameters.WorkArea;
            this.Left = workArea.Left + Math.Max(0, (workArea.Width - this.Width) / 2);
            this.Top = workArea.Top + Math.Max(0, (workArea.Height - this.Height) / 2);
        }

        // ✕·Alt+F4·작업 표시줄 "창 닫기" 는 종료하지 않고 트레이로 숨김. 진짜 종료는 트레이 메뉴 "종료"·업데이트 설치
        // (ExitApp), Windows 종료 (WPF 가 앱을 끝낼 때는 Cancel 을 무시하고 닫음) 뿐
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowState();
            if (_exitRequested) return;
            e.Cancel = true;
            Dispatcher.InvokeAsync(HideToTray); // 닫는 중에는 창 표시 상태를 바꿀 수 없으므로 한 박자 뒤에
        }

        private bool _exitRequested;

        private void ExitApp()
        {
            _exitRequested = true;
            Close();
        }

        private void HideToTray()
        {
            _isPeeking = false;
            Hide();
        }

        private void SaveWindowState()
        {
            _settings.IsMaximized = _isFakeMaximized;
            if (_isFakeMaximized)
            {
                if (!_restoreBounds.IsEmpty)
                {
                    _settings.LastWindowLeft = _restoreBounds.Left; _settings.LastWindowTop = _restoreBounds.Top;
                    _settings.LastWindowWidth = _restoreBounds.Width; _settings.LastWindowHeight = _restoreBounds.Height;
                }
            }
            else if (this.WindowState == WindowState.Minimized)
            {
                // 최소화된 채 트레이로 숨은 경우 Left/Top 대신 복원 위치를 저장
                Rect rb = this.RestoreBounds;
                if (!rb.IsEmpty)
                {
                    _settings.LastWindowLeft = rb.Left; _settings.LastWindowTop = rb.Top;
                    _settings.LastWindowWidth = rb.Width; _settings.LastWindowHeight = rb.Height;
                }
            }
            else
            {
                _settings.LastWindowLeft = this.Left; _settings.LastWindowTop = this.Top;
                _settings.LastWindowWidth = this.Width; _settings.LastWindowHeight = this.Height;
            }
            SaveSettings();
        }

        // ──────────────────────────────────────────
        // 모니터 / 창 관리
        // ──────────────────────────────────────────
        private void InitMonitors() => _monitors = MonitorHelper.GetMonitors();

        private int GetCurrentMonitorIndex()
        {
            if (_monitors == null || _monitors.Count == 0) _monitors = MonitorHelper.GetMonitors();
            double centerX = this.Left + (this.Width / 2); double centerY = this.Top + (this.Height / 2);
            for (int i = 0; i < _monitors.Count; i++)
                if (_monitors[i].Bounds.Contains(centerX, centerY)) return i;
            return 0;
        }

        private void MoveToMonitor(int monitorIndex, bool center = false)
        {
            if (_monitors == null || _monitors.Count == 0) _monitors = MonitorHelper.GetMonitors();
            if (monitorIndex < 0 || monitorIndex >= _monitors.Count) monitorIndex = 0;
            Rect workArea = _monitors[monitorIndex].WorkArea;
            if (center) { this.Left = workArea.Left + (workArea.Width - this.Width) / 2; this.Top = workArea.Top + (workArea.Height - this.Height) / 2; }
            else { this.Left = workArea.Left + 50; this.Top = workArea.Top + 50; }
            if (this.Left < workArea.Left) this.Left = workArea.Left;
            if (this.Top < workArea.Top) this.Top = workArea.Top;
        }

        private void PerformMaximize(bool force = false)
        {
            if (_isFakeMaximized && !force) return;
            if (_monitors == null || _monitors.Count == 0) _monitors = MonitorHelper.GetMonitors();
            int targetIndex = GetCurrentMonitorIndex();
            Rect workArea = _monitors[targetIndex].WorkArea;
            if (this.WindowState == WindowState.Maximized) this.WindowState = WindowState.Normal;
            if (!_isFakeMaximized && WindowState == WindowState.Normal)
                _restoreBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
            MyWindowChrome.ResizeBorderThickness = new Thickness(0);
            RootGrid.Margin = new Thickness(0); MainBorder.CornerRadius = new CornerRadius(0); UpdateMainClip();
            this.Left = workArea.Left; this.Top = workArea.Top; this.Width = workArea.Width; this.Height = workArea.Height;
            BtnMaximize.Content = "\uE923"; _isFakeMaximized = true;
        }

        private void PerformRestore()
        {
            if (!_isFakeMaximized) return;
            this.Left = _restoreBounds.Left; this.Top = _restoreBounds.Top;
            this.Width = _restoreBounds.Width; this.Height = _restoreBounds.Height;
            RootGrid.Margin = new Thickness(10); MainBorder.CornerRadius = new CornerRadius(10); UpdateMainClip();
            MyWindowChrome.ResizeBorderThickness = new Thickness(12);
            BtnMaximize.Content = "\uE922"; _isFakeMaximized = false;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e) { if (_isFakeMaximized) PerformRestore(); else PerformMaximize(); }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { if (_isFakeMaximized) PerformRestore(); else PerformMaximize(); }
            else if (e.ButtonState == MouseButtonState.Pressed)
            {
                bool dragLocked = IsPinned && _settings.LockDragWhenPinned;
                if (!dragLocked) this.DragMove();
            }
        }

        private void TitleBar_SizeChanged(object sender, SizeChangedEventArgs e) => ArrangeTitle();

        // 가운데 제목(◀ 연월 ▶ 오늘)을 창 가운데에 두되, 왼쪽 버전·오른쪽 버튼들과 겹치면 그 사이 남는 공간의 가운데로 옮김.
        // 그래도 모자라면 '오늘' 버튼을 숨기고 (연월 글자를 눌러도 오늘로 이동), 그다음 버전 표시를 숨김.
        // 창 MinWidth(770)는 둘 다 숨겼을 때 가장 긴 제목(주간 W53)이 툴바에 닿지 않는 폭
        private void ArrangeTitle()
        {
            const double gap = 12;
            double toolbarLeft = TitleBarGrid.ActualWidth - TitleToolbar.ActualWidth - TitleToolbar.Margin.Right;
            double versionEdge = VersionBadge.Margin.Left + VersionBadge.ActualWidth + gap;
            double todaySpace = BtnToday.Width + BtnToday.Margin.Left;
            double fullWidth = TitleCenter.ActualWidth + (BtnToday.Visibility == Visibility.Visible ? 0 : todaySpace);
            bool showToday = fullWidth <= toolbarLeft - gap - versionEdge;
            BtnToday.Visibility = showToday ? Visibility.Visible : Visibility.Collapsed;
            double titleWidth = showToday ? fullWidth : fullWidth - todaySpace;
            bool showVersion = titleWidth <= toolbarLeft - gap - versionEdge;
            VersionBadge.Visibility = showVersion ? Visibility.Visible : Visibility.Hidden; // Hidden 은 숨겨도 폭이 유지되어 다시 계산 가능
            double leftEdge = showVersion ? versionEdge : VersionBadge.Margin.Left;
            bool fitsCentered = (TitleBarGrid.ActualWidth - titleWidth) / 2 >= leftEdge
                && (TitleBarGrid.ActualWidth + titleWidth) / 2 <= toolbarLeft - gap;
            TitleCenter.HorizontalAlignment = fitsCentered ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            TitleCenter.Margin = fitsCentered ? new Thickness(0) : new Thickness(Math.Max(leftEdge, (leftEdge + toolbarLeft - gap - titleWidth) / 2), 0, 0, 0);
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (SettingsPopup.Visibility == Visibility.Visible)
            {
                // 설정 안 콤보 목록 같은 팝업은 따로 뜬 창이라 설정 칸 밖에 있을 수 있음: 그 안을 누른 것은 바깥 클릭이 아님
                if (e.OriginalSource is Visual source && PresentationSource.FromVisual(source) is PresentationSource src
                    && src != PresentationSource.FromVisual(this)) return;
                Point mousePos = e.GetPosition(this);
                Point popupLoc = SettingsPopup.TranslatePoint(new Point(0, 0), this);
                Rect popupRect = new Rect(popupLoc, new Size(SettingsPopup.ActualWidth, SettingsPopup.ActualHeight));
                Point btnLoc = BtnSettings.TranslatePoint(new Point(0, 0), this);
                Rect btnRect = new Rect(btnLoc, new Size(BtnSettings.ActualWidth, BtnSettings.ActualHeight));
                if (!popupRect.Contains(mousePos) && !btnRect.Contains(mousePos))
                    SettingsPopup.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnHideWeekends_CheckedChanged(object sender, RoutedEventArgs e) { _settings.HideWeekends = (BtnHideWeekends.IsChecked == true); RefreshCalendar(); SaveSettings(); }

        private void BtnStyle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag != null && int.TryParse(btn.Tag.ToString(), out int styleIndex))
                _settings.BulletStyle = styleIndex;
            UpdateBulletButtonsVisual(); RefreshCalendar(); SaveSettings();
        }

        // 일정 기호 칸: 고른 것만 켜짐 (다시 눌러 풀린 칸도 되돌림)
        private void UpdateBulletButtonsVisual()
        {
            ToggleButton[] buttons = { BtnStyle0, BtnStyle1, BtnStyle2, BtnStyle3 };
            if (buttons[0] == null) return;
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].IsChecked = i == _settings.BulletStyle;
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _settings.Opacity = e.NewValue;
            if (TxtOpacityValue != null) TxtOpacityValue.Text = $"{(int)(_settings.Opacity * 100)}%";
            ApplyWindowOpacity(); SaveSettings();
        }

        private void SliderFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _settings.FontSize = e.NewValue;
            TxtFontSizeInput.TextChanged -= TxtFontSizeInput_TextChanged;
            TxtFontSizeInput.Text = ((int)_settings.FontSize).ToString();
            TxtFontSizeInput.TextChanged += TxtFontSizeInput_TextChanged;
            RefreshCalendar(); SaveSettings();
        }

        private void TxtFontSizeInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(TxtFontSizeInput.Text, out int size))
            {
                size = Math.Clamp(size, 10, 30); _settings.FontSize = size;
                SliderFontSize.ValueChanged -= SliderFontSize_ValueChanged;
                SliderFontSize.Value = size;
                SliderFontSize.ValueChanged += SliderFontSize_ValueChanged;
                RefreshCalendar(); SaveSettings();
            }
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized) { this.WindowState = WindowState.Normal; PerformMaximize(); }
            if (this.WindowState == WindowState.Minimized)
            {
                // Win+D·Win+M 등 바깥에서 최소화된 경우: 최소화 처리가 끝난 뒤 되돌린다
                Dispatcher.InvokeAsync(() =>
                {
                    if (this.WindowState != WindowState.Minimized) return;
                    // 고정: WindowState=Normal 은 창을 활성화하며 맨 앞으로 올리므로, 비활성 상태로 제자리 복원
                    if (IsPinned) DesktopWindowNative.ShowWindow(_hwnd, DesktopWindowNative.SW_SHOWNOACTIVATE);
                    else if (!_settings.ShowInTaskbar) Hide(); // 작업 표시줄 버튼이 없으므로 트레이로 숨김
                });
            }
        }

        // ── 라이트 / 다크 (툴바의 해·달 버튼) ──
        // XAML 의 테마 브러시(Th_…, Th.…)와 그라데이션은 다크 값으로 적혀 있음. 처음 한 번 그 값을 기억해 두고,
        // 라이트면 Theme.ToLight 로 (또는 아래 지정 값으로) 바꿔 끼움. 코드로 그리는 달력 칸·대화 상자는 그릴 때마다 Theme.L 을 거침
        private Dictionary<string, Brush>? _darkThemeBrushes;
        private Brush? _darkRootBackground;

        // 라이트 팔레트: 흰 바탕 + 파랑(#2563EB) 강조 + 무채색(zinc) 글자. 회청색 면·보라 없이, 누르는 것들은 회색 대신 옅은 파랑
        // 화면 글꼴: 함께 넣은 Pretendard (Fonts\*.otf), 없으면 Segoe UI · 맑은 고딕
        internal static readonly FontFamily AppFont = new(new Uri("pack://application:,,,/"), "./Fonts/#Pretendard, Segoe UI, Malgun Gothic");

        // 라이트 팔레트: 흰색과 푸른 기 없는 회색 면 + 먹색 툴바 + 호박색·주황 강조(처음 아이콘 색 #FDB522 / #FF9805), 글자는 먹색·회색
        private static readonly Color LightAccent = Color.FromRgb(255, 152, 5);
        private static readonly Color LightAmber = Color.FromRgb(253, 181, 34);
        private static readonly Dictionary<string, Color> LightThemeOverrides = new()
        {
            ["AccentBrush"] = Color.FromRgb(255, 152, 5),       // 켜진 스위치·슬라이더·체크 (주황)
            ["AccentBorderBrush"] = Color.FromRgb(255, 152, 5),
            ["AccentSoftBrush"] = Color.FromRgb(255, 241, 214),
            ["Th.SegSel"] = Color.FromRgb(253, 181, 34),        // 고른 칸: 호박색 알약 + 먹색 글자
            ["Th.SegSelText"] = Color.FromRgb(32, 32, 32),
            ["Th_FF4EB8E8"] = Color.FromRgb(47, 125, 209),      // 토요일은 달력 관례대로 파랑
            ["Th.Privacy"] = Color.FromRgb(255, 243, 219),      // 가리기 버튼 (툴바는 먹색이라 실제로는 다크 값이 쓰임)
            ["Th.PrivacyOn"] = Color.FromRgb(254, 226, 170),
            ["Th.PrivacyHover"] = Color.FromRgb(254, 234, 194),
            ["Th.PrivacyIcon"] = Color.FromRgb(146, 92, 0),
            ["Th.KnobStroke"] = Color.FromRgb(204, 204, 204),   // 흰 카드 위 흰 손잡이 테두리
            ["Th_FF2E3547"] = Color.FromRgb(204, 204, 204),     // 꺼진 스위치·슬라이더 남은 쪽·입력 칸 테두리
            ["Th_FF3A4058"] = Color.FromRgb(204, 204, 204),
            ["Th_FF4A5270"] = Color.FromRgb(253, 181, 34),      // 입력 칸에 마우스를 올렸을 때 테두리
            ["Th_205AC8FA"] = Color.FromRgb(255, 244, 222),     // '오늘' 버튼 (툴바 안에서는 아래 툴바 값)
            ["Th_405AC8FA"] = Color.FromRgb(251, 211, 141),
            ["Th_FFC0D8F8"] = Color.FromRgb(168, 95, 0),
            ["Th.CloseHover"] = Color.FromRgb(229, 72, 77),     // ✕ 위 흰 글자
            ["Th.ClosePressed"] = Color.FromRgb(201, 58, 63),
            ["Th.CardBg"] = Color.FromRgb(255, 255, 255),       // 설정 카드
            ["Th.PopupBg"] = Color.FromRgb(245, 245, 245),      // 설정 패널: 옅은 회색 바탕 위 흰 카드
            ["ScrollThumbBrush"] = Color.FromRgb(204, 204, 204),
            ["ScrollThumbHoverBrush"] = Color.FromRgb(153, 153, 153),
        };

        // 라이트의 큰 면: 먹색 툴바, 옅은 회색 바탕(#EBEBEB) 위의 흰 달력 판. 요일은 그 바탕 위 글자
        private static readonly Color LightCanvas = Color.FromRgb(235, 235, 235);
        private static readonly Color LightToolbarBg = Color.FromRgb(32, 32, 32);
        private static Brush? LightSurface(string key) => key switch
        {
            "TopBarGradient" => new LinearGradientBrush(Color.FromRgb(36, 36, 36), Color.FromRgb(26, 26, 26), 90),
            "HeaderGradient" => Brushes.Transparent,
            "CalendarBackgroundGradient" => new SolidColorBrush(Colors.White),
            "Root" => new SolidColorBrush(LightCanvas),
            _ => null,
        };

        private static readonly Color LightToolbarIcon = Color.FromRgb(208, 208, 208); // 먹색 툴바 위 아이콘
        private static readonly Color LightHairline = Color.FromRgb(224, 224, 224);    // 라이트 달력 칸 선
        private static readonly Color LightTodayDot = Color.FromRgb(253, 181, 34);     // 라이트 오늘 날짜 동그라미 (호박색 + 먹색 숫자)

        // 창 안쪽을 둥근 모서리로 잘라 툴바의 각진 모서리가 바깥으로 보이지 않게 (라이트의 먹색 툴바)
        private void UpdateMainClip()
        {
            if (MainBorder.Child is not FrameworkElement inner || inner.ActualWidth <= 0) return;
            double r = Math.Max(0, MainBorder.CornerRadius.TopLeft - MainBorder.BorderThickness.Left);
            inner.Clip = new RectangleGeometry(new Rect(0, 0, inner.ActualWidth, inner.ActualHeight), r, r);
        }

        private void ApplyTheme()
        {
            Theme.IsDark = _settings.IsDarkMode;
            if (_darkThemeBrushes == null)
            {
                _darkThemeBrushes = new();
                foreach (var key in Resources.Keys.OfType<string>().ToList())
                    if ((key.StartsWith("Th") || key is "TopBarGradient" or "HeaderGradient" or "CalendarBackgroundGradient"
                            or "ScrollThumbBrush" or "ScrollThumbHoverBrush" or "CardBackgroundDark" or "CardBackgroundDarker" or "CardBorderBrush"
                            or "AccentBrush" or "AccentBorderBrush" or "AccentSoftBrush")
                        && Resources[key] is Brush brush)
                        _darkThemeBrushes[key] = brush;
                _darkRootBackground = RootGrid.Background;
            }
            foreach (var (key, dark) in _darkThemeBrushes) Resources[key] = ThemedBrush(key, dark);
            TitleBarGrid.Resources.Clear();
            if (!Theme.IsDark)
            {
                foreach (var (key, dark) in _darkThemeBrushes) TitleBarGrid.Resources[key] = dark;
                TitleBarGrid.Resources["TopBarGradient"] = LightSurface("TopBarGradient")!;
                // 먹색 툴바 위 강조도 호박색 (다크의 하늘색 대신)
                TitleBarGrid.Resources["Th.SegSel"] = new SolidColorBrush(LightAmber);
                TitleBarGrid.Resources["Th.SegSelText"] = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                TitleBarGrid.Resources["Th_205AC8FA"] = new SolidColorBrush(Color.FromArgb(56, 253, 181, 34));
                TitleBarGrid.Resources["Th_405AC8FA"] = new SolidColorBrush(Color.FromArgb(128, 253, 181, 34));
                TitleBarGrid.Resources["Th_FFC0D8F8"] = new SolidColorBrush(Color.FromRgb(255, 214, 128));
            }
            if (_darkRootBackground != null) RootGrid.Background = ThemedBrush("Root", _darkRootBackground);
            MainBorder.Background = Theme.Pick(Color.FromRgb(22, 25, 35), LightCanvas);

            // 라이트: 요일 띠 테두리 없이, 달력 판 안쪽 여백 줄이기, 창 그림자 옅게, 툴바 아이콘은 한 가지 슬레이트색
            WeekdayHeader.BorderThickness = new Thickness(Theme.IsDark ? 1 : 0);
            // 설정 패널 테두리: 라이트에서는 흰 패널이 흰 달력에 묻히지 않게 회색 실선
            if (Theme.IsDark) SettingsPopup.SetResourceReference(Border.BorderBrushProperty, "Th_20FFFFFF");
            else SettingsPopup.BorderBrush = new SolidColorBrush(Color.FromRgb(214, 214, 214));
            CalendarGrid.Margin = new Thickness(Theme.IsDark ? 6 : 4);
            if (MainBorder.Effect is DropShadowEffect windowShadow) windowShadow.Opacity = Theme.IsDark ? 0.5 : 0.14;
            UpdateMainClip();
            foreach (var (button, darkKey) in new (Button, string)[] { (BtnSearch, "Th_FF5AC8FA"), (BtnMacro, "Th_FF2EC876"), (BtnSettings, "Th_FFA270FA") })
            {
                if (Theme.IsDark) button.SetResourceReference(Control.ForegroundProperty, darkKey);
                else button.Foreground = new SolidColorBrush(LightToolbarIcon);
            }

            // 큰 제목: 다크는 검은 그림자로 띄우고, 라이트는 그림자 없이
            if (TxtYearMonth.Effect is DropShadowEffect titleShadow)
            {
                titleShadow.Color = Colors.Black;
                titleShadow.Opacity = Theme.IsDark ? 0.6 : 0.0;
            }
            UpdateThemeButton();
            ApplyWindowOpacity();
        }

        private static Brush ThemedBrush(string key, Brush dark)
        {
            if (Theme.IsDark) return dark;
            if (LightThemeOverrides.TryGetValue(key, out var fixedColor)) return new SolidColorBrush(fixedColor);
            if (LightSurface(key) is Brush surface) return surface;
            switch (dark)
            {
                case SolidColorBrush solid:
                    return new SolidColorBrush(Theme.ToLight(solid.Color));
                case GradientBrush gradient:
                    var light = gradient.Clone();
                    foreach (var stop in light.GradientStops) stop.Color = Theme.ToLight(stop.Color);
                    return light;
                default:
                    return dark;
            }
        }

        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            _settings.IsDarkMode = !_settings.IsDarkMode;
            ApplyTheme();
            SaveSettings();
            RefreshCalendar();
        }

        // 지금이 다크면 해(누르면 라이트), 라이트면 달(누르면 다크)
        private void UpdateThemeButton()
        {
            BtnTheme.Content = Theme.IsDark ? "" : "";
            BtnTheme.Foreground = new SolidColorBrush(Theme.IsDark ? Color.FromRgb(255, 201, 77) : LightToolbarIcon);
            BtnTheme.ToolTip = Theme.IsDark ? "라이트 모드로 바꾸기" : "다크 모드로 바꾸기";
        }

        private void ApplyWindowOpacity() => this.Opacity = _settings.Opacity;

        // ──────────────────────────────────────────
        // 달력 렌더링
        // ──────────────────────────────────────────
        private int GetWeekNum(DateTime date)
        {
            DateTime rowSunday = date.AddDays(-(int)date.DayOfWeek);
            DateTime thursday = rowSunday.AddDays(4);
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                thursday, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        private Border CreateWeekNumCell(int weekNum)
        {
            Border cell = new Border
            {
                Background = new SolidColorBrush(Theme.L(Color.FromArgb(8, 255, 255, 255))),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 1, 2, 1)
            };
            if (!Theme.IsDark)
            {
                cell.Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)); // 주 번호 줄은 옅은 회색
                cell.Margin = new Thickness(0);
                cell.CornerRadius = new CornerRadius(0);
                cell.BorderThickness = new Thickness(0, 0, 1, 1);
                cell.BorderBrush = new SolidColorBrush(LightHairline);
            }
            cell.Child = new TextBlock
            {
                Text = $"W{weekNum}",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(120, 128, 162))),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            return cell;
        }

        private void SetupGridColumns(Grid grid, int dayColumnsCount)
        {
            grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30, GridUnitType.Pixel) });
            for (int i = 0; i < dayColumnsCount; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        private DateTime AddWeekdays(DateTime date, int days)
        {
            int step = days > 0 ? 1 : -1; int remaining = Math.Abs(days); DateTime result = date;
            while (remaining > 0) { result = result.AddDays(step); if (result.DayOfWeek != DayOfWeek.Saturday && result.DayOfWeek != DayOfWeek.Sunday) remaining--; }
            return result;
        }

        private void RefreshCalendar()
        {
            CalendarGrid.Children.Clear(); HeaderGrid.Children.Clear();
            TxtYearMonth.Text = _currentDate.ToString("yyyy. MM");

            // ── 미니 모드: 현재 주 한 줄만 ──
            if (_isMiniMode)
            {
                DateTime miniWeekStart, miniWeekEnd;
                if (_settings.HideWeekends)
                {
                    int dow = (int)_currentDate.DayOfWeek;
                    if (dow == 0) dow = 7;
                    miniWeekStart = _currentDate.AddDays(1 - dow);
                    miniWeekEnd = miniWeekStart.AddDays(4);
                }
                else
                {
                    miniWeekStart = _currentDate.AddDays(-(int)_currentDate.DayOfWeek);
                    miniWeekEnd = miniWeekStart.AddDays(6);
                }

                int miniWeekNum = GetWeekNum(miniWeekStart);
                TxtYearMonth.Text = $"{miniWeekStart:yyyy}  W{miniWeekNum}";

                int miniDayColsCount = _settings.HideWeekends ? 5 : 7;
                SetupGridColumns(HeaderGrid, miniDayColsCount);
                SetupGridColumns(CalendarGrid, miniDayColsCount);

                var miniWLabel = new TextBlock { Text = "W", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(120, 128, 162))), FontSize = 10, FontWeight = FontWeights.SemiBold };
                HeaderGrid.Children.Add(miniWLabel); Grid.SetColumn(miniWLabel, 0);

                List<UIElement> miniHeaders;
                if (_settings.HideWeekends)
                {
                    TxtHeaderSun.Visibility = Visibility.Collapsed; TxtHeaderSat.Visibility = Visibility.Collapsed;
                    miniHeaders = new List<UIElement> { TxtMon, TxtTue, TxtWed, TxtThu, TxtFri };
                }
                else
                {
                    TxtHeaderSun.Visibility = Visibility.Visible; TxtHeaderSat.Visibility = Visibility.Visible;
                    miniHeaders = new List<UIElement> { TxtHeaderSun, TxtMon, TxtTue, TxtWed, TxtThu, TxtFri, TxtHeaderSat };
                }
                for (int i = 0; i < miniHeaders.Count; i++) { HeaderGrid.Children.Add(miniHeaders[i]); Grid.SetColumn(miniHeaders[i], i + 1); }

                Brush miniCellBorder = new SolidColorBrush(Theme.L(Color.FromArgb(20, 255, 255, 255)));
                Brush miniCellBg = Theme.Pick(Color.FromArgb(14, 255, 255, 255), Color.FromArgb(235, 255, 255, 255)); // 라이트: 흰 종이 칸
                Brush miniAdjBg = Theme.Pick(Color.FromArgb(6, 255, 255, 255), Color.FromArgb(110, 255, 255, 255));
                Brush miniTextBrush = new SolidColorBrush(Theme.L(Color.FromRgb(200, 206, 224)));

                CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                var miniWc = CreateWeekNumCell(GetWeekNum(miniWeekStart));
                CalendarGrid.Children.Add(miniWc); Grid.SetRow(miniWc, 0); Grid.SetColumn(miniWc, 0);

                int miniCurrentMonth = _currentDate.Month;
                for (DateTime d = miniWeekStart; d <= miniWeekEnd; d = d.AddDays(1))
                {
                    if (_settings.HideWeekends && (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)) continue;
                    string dk = d.ToString("yyyy-MM-dd");
                    _memoData.TryGetValue(dk, out var its); its ??= new List<MemoItem>();
                    bool isAdj = d.Month != miniCurrentMonth;
                    var cell = CreateDayCell(d, d.Day, its, GetHolidayName(d), dk,
                        isAdj ? miniAdjBg : miniCellBg, miniCellBorder, miniTextBrush, isAdj);
                    CalendarGrid.Children.Add(cell); Grid.SetRow(cell, 0); Grid.SetColumn(cell, GetColIndex(d));
                }
                return;
            }

            DateTime firstDay = new DateTime(_currentDate.Year, _currentDate.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(_currentDate.Year, _currentDate.Month);
            DateTime lastDay = new DateTime(_currentDate.Year, _currentDate.Month, daysInMonth);
            int dayColsCount = _settings.HideWeekends ? 5 : 7;

            SetupGridColumns(HeaderGrid, dayColsCount);
            SetupGridColumns(CalendarGrid, dayColsCount);

            var wLabel = new TextBlock { Text = "W", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(120, 128, 162))), FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) };
            HeaderGrid.Children.Add(wLabel); Grid.SetColumn(wLabel, 0);

            List<UIElement> headers = new();
            if (_settings.HideWeekends)
            {
                TxtHeaderSun.Visibility = Visibility.Collapsed; TxtHeaderSat.Visibility = Visibility.Collapsed;
                headers.AddRange(new[] { TxtMon, TxtTue, TxtWed, TxtThu, TxtFri });
            }
            else
            {
                TxtHeaderSun.Visibility = Visibility.Visible; TxtHeaderSat.Visibility = Visibility.Visible;
                headers.AddRange(new UIElement[] { TxtHeaderSun, TxtMon, TxtTue, TxtWed, TxtThu, TxtFri, TxtHeaderSat });
            }
            for (int i = 0; i < headers.Count; i++) { HeaderGrid.Children.Add(headers[i]); Grid.SetColumn(headers[i], i + 1); }

            // ── 디자인 개선: 셀 테두리/배경 ──
            Brush cellBorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(20, 255, 255, 255)));
            Brush cellBg = Theme.Pick(Color.FromArgb(14, 255, 255, 255), Color.FromArgb(235, 255, 255, 255)); // 라이트: 흰 종이 칸
            Brush adjBg = Theme.Pick(Color.FromArgb(6, 255, 255, 255), Color.FromArgb(110, 255, 255, 255));
            Brush defaultTextBrush = new SolidColorBrush(Theme.L(Color.FromRgb(200, 206, 224)));

            var cellList = new List<(DateTime Date, int Row, int Col, bool IsAdj)>();

            DateTime firstRowStart = GetRowStart(firstDay);
            DateTime lastRowEnd = GetRowEnd(lastDay);
            DateTime prevWeekStart = _settings.HideWeekends ? AddWeekdays(firstRowStart, -5) : firstRowStart.AddDays(-7);
            DateTime prevWeekEnd = _settings.HideWeekends ? AddWeekdays(firstRowStart, -1) : firstRowStart.AddDays(-1);
            DateTime nextWeekStart = _settings.HideWeekends ? AddWeekdays(lastRowEnd, 1) : lastRowEnd.AddDays(1);
            DateTime nextWeekEnd = _settings.HideWeekends ? AddWeekdays(lastRowEnd, 5) : lastRowEnd.AddDays(7);

            int currentRow = 0;

            if (_settings.ShowAdjacentMonths)
            {
                CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                var wc = CreateWeekNumCell(GetWeekNum(prevWeekStart));
                CalendarGrid.Children.Add(wc); Grid.SetRow(wc, currentRow); Grid.SetColumn(wc, 0);
                for (DateTime d = prevWeekStart; d <= prevWeekEnd; d = d.AddDays(1))
                {
                    if (_settings.HideWeekends && (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)) continue;
                    cellList.Add((d, currentRow, GetColIndex(d), true));
                }
                currentRow++;
            }

            bool firstRowAdded = false;
            for (int day = 1; day <= daysInMonth; day++)
            {
                DateTime date = new DateTime(_currentDate.Year, _currentDate.Month, day);
                if (_settings.HideWeekends && (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)) continue;
                bool isRowStart = _settings.HideWeekends ? date.DayOfWeek == DayOfWeek.Monday : date.DayOfWeek == DayOfWeek.Sunday;

                if (isRowStart || !firstRowAdded)
                {
                    if (firstRowAdded) currentRow++;
                    CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    var wc = CreateWeekNumCell(GetWeekNum(date));
                    CalendarGrid.Children.Add(wc); Grid.SetRow(wc, currentRow); Grid.SetColumn(wc, 0);
                    firstRowAdded = true;
                }

                int col = GetColIndex(date);

                if (day == 1)
                {
                    for (int c = 1; c < col; c++)
                    {
                        DateTime adjDate = _settings.HideWeekends ? AddWeekdays(firstDay, c - col) : firstDay.AddDays(c - col);
                        if (_settings.HideWeekends && (adjDate.DayOfWeek == DayOfWeek.Saturday || adjDate.DayOfWeek == DayOfWeek.Sunday)) continue;
                        string adjKey = adjDate.ToString("yyyy-MM-dd");
                        _memoData.TryGetValue(adjKey, out List<MemoItem> adjItemsFill); adjItemsFill ??= new();
                        var adjCell = CreateDayCell(adjDate, adjDate.Day, adjItemsFill, GetHolidayName(adjDate), adjKey, adjBg, cellBorderBrush, defaultTextBrush, true);
                        CalendarGrid.Children.Add(adjCell); Grid.SetRow(adjCell, currentRow); Grid.SetColumn(adjCell, c);
                    }
                }

                string dateKey = date.ToString("yyyy-MM-dd");
                _memoData.TryGetValue(dateKey, out List<MemoItem> items); items ??= new();
                var dayCell = CreateDayCell(date, date.Day, items, GetHolidayName(date), dateKey, cellBg, cellBorderBrush, defaultTextBrush, false);
                CalendarGrid.Children.Add(dayCell); Grid.SetRow(dayCell, currentRow); Grid.SetColumn(dayCell, col);
            }

            DateTime effectiveLastDay = lastDay;
            if (_settings.HideWeekends)
            {
                if (effectiveLastDay.DayOfWeek == DayOfWeek.Saturday) effectiveLastDay = effectiveLastDay.AddDays(-1);
                else if (effectiveLastDay.DayOfWeek == DayOfWeek.Sunday) effectiveLastDay = effectiveLastDay.AddDays(-2);
            }
            int lastColUsed = GetColIndex(effectiveLastDay);
            int totalDayCols = _settings.HideWeekends ? 5 : 7;
            int nextMonthOffset = 1;

            for (int c = lastColUsed + 1; c <= totalDayCols; c++)
            {
                DateTime adjDate = _settings.HideWeekends ? AddWeekdays(effectiveLastDay, nextMonthOffset++) : effectiveLastDay.AddDays(c - lastColUsed);
                if (_settings.HideWeekends && (adjDate.DayOfWeek == DayOfWeek.Saturday || adjDate.DayOfWeek == DayOfWeek.Sunday)) continue;
                string adjKey = adjDate.ToString("yyyy-MM-dd");
                _memoData.TryGetValue(adjKey, out List<MemoItem> adjItemsFill); adjItemsFill ??= new();
                var adjCell = CreateDayCell(adjDate, adjDate.Day, adjItemsFill, GetHolidayName(adjDate), adjKey, adjBg, cellBorderBrush, defaultTextBrush, true);
                CalendarGrid.Children.Add(adjCell); Grid.SetRow(adjCell, currentRow); Grid.SetColumn(adjCell, c);
            }

            if (_settings.ShowAdjacentMonths)
            {
                currentRow++;
                CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                var wc = CreateWeekNumCell(GetWeekNum(nextWeekStart));
                CalendarGrid.Children.Add(wc); Grid.SetRow(wc, currentRow); Grid.SetColumn(wc, 0);
                for (DateTime d = nextWeekStart; d <= nextWeekEnd; d = d.AddDays(1))
                {
                    if (_settings.HideWeekends && (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)) continue;
                    cellList.Add((d, currentRow, GetColIndex(d), true));
                }
            }

            foreach (var (date, row, col, isAdj) in cellList)
            {
                string adjDateKey = date.ToString("yyyy-MM-dd");
                _memoData.TryGetValue(adjDateKey, out List<MemoItem> adjItems); adjItems ??= new();
                var adjCell = CreateDayCell(date, date.Day, adjItems, GetHolidayName(date), adjDateKey, adjBg, cellBorderBrush, defaultTextBrush, isAdjacentMonth: true);
                CalendarGrid.Children.Add(adjCell); Grid.SetRow(adjCell, row); Grid.SetColumn(adjCell, col);
            }
        }

        private DateTime GetRowStart(DateTime date)
        {
            if (_settings.HideWeekends) { int dow = (int)date.DayOfWeek; if (dow == 0) return date.AddDays(1); if (dow == 6) return date.AddDays(2); return date.AddDays(1 - dow); }
            return date.AddDays(-(int)date.DayOfWeek);
        }

        private DateTime GetRowEnd(DateTime date)
        {
            if (_settings.HideWeekends) { int dow = (int)date.DayOfWeek; if (dow == 0) return date.AddDays(-3); if (dow == 6) return date.AddDays(-1); return date.AddDays(5 - dow); }
            return date.AddDays(6 - (int)date.DayOfWeek);
        }

        private int GetColIndex(DateTime date) =>
            _settings.HideWeekends ? (int)date.DayOfWeek : (int)date.DayOfWeek + 1;

        private Border CreateDayCell(DateTime date, int day, List<MemoItem> items, string holiday, string dateKey, Brush bg, Brush border, Brush textBrush, bool isAdjacentMonth = false)
        {
            bool isToday = date.Date == DateTime.Today;

            Border cell = new Border
            {
                Background = bg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(2),
                Tag = dateKey,
                AllowDrop = true,
                Opacity = isAdjacentMonth ? 0.50 : 1.0
            };

            if (isAdjacentMonth)
                cell.BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(12, 255, 255, 255)));

            if (!Theme.IsDark)
            {
                cell.Margin = new Thickness(0);
                cell.CornerRadius = new CornerRadius(0);
                cell.BorderThickness = new Thickness(0, 0, 1, 1);
                cell.BorderBrush = new SolidColorBrush(LightHairline);
                cell.Background = new SolidColorBrush(isAdjacentMonth ? Color.FromRgb(248, 248, 248)
                    : isToday ? Color.FromRgb(255, 246, 229)
                    : date.DayOfWeek == DayOfWeek.Saturday ? Color.FromRgb(246, 251, 254)
                    : date.DayOfWeek == DayOfWeek.Sunday ? Color.FromRgb(254, 248, 248)
                    : Colors.White);
                if (isAdjacentMonth) cell.Opacity = 0.85;
            }

            if (isToday && Theme.IsDark)
            {
                cell.Background = Theme.Pick(Color.FromArgb(30, 90, 200, 250), Color.FromRgb(226, 241, 254));
                cell.BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(120, 90, 200, 250)));
                cell.BorderThickness = new Thickness(1);
                cell.Opacity = 1.0;
                cell.Effect = new DropShadowEffect
                {
                    Color = Theme.L(Color.FromRgb(90, 200, 250)),
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.25
                };
            }

            Brush normalBorder = cell.BorderBrush;
            Brush normalBg = cell.Background;

            if (!isAdjacentMonth)
            {
                cell.MouseEnter += (s, e) =>
                {
                    if (cell.BorderBrush == normalBorder)
                    {
                        cell.Background = Theme.Pick(Color.FromArgb(28, 255, 255, 255), Color.FromRgb(246, 246, 246));
                    }
                };
                cell.MouseLeave += (s, e) =>
                {
                    if (cell.BorderBrush == normalBorder)
                        cell.Background = isToday && Theme.IsDark ? Theme.Pick(Color.FromArgb(30, 90, 200, 250), Color.FromRgb(226, 241, 254)) : normalBg;
                };
            }

            cell.Drop += (s, e) =>
            {
                if (e.Data.GetData(typeof(MemoDragData)) is MemoDragData data)
                {
                    if (data.SourceDate == dateKey) return;
                    bool isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) != 0;

                    if (data.ConfirmCopy)
                    {
                        // 드래그가 끝난 뒤에 확인창 (놓는 도중에 창을 띄우면 드래그가 안 끝남)
                        if (isCopy) Dispatcher.InvokeAsync(() => ConfirmLockedCopy(data.Item, dateKey));
                        e.Handled = true;
                        return;
                    }
                    if (isCopy) AddMemoCopy(data.Item, dateKey);
                    else
                    {
                        if (_memoData.TryGetValue(data.SourceDate, out var sourceList)) { sourceList.Remove(data.Item); if (sourceList.Count == 0) _memoData.Remove(data.SourceDate); }
                        if (!_memoData.TryGetValue(dateKey, out var targetList)) { targetList = new List<MemoItem>(); _memoData[dateKey] = targetList; }
                        targetList.Add(data.Item);
                    }
                    SaveData(); RefreshCalendar();
                }
                e.Handled = true;
            };

            cell.DragOver += (s, e) =>
            {
                if (e.Data.GetData(typeof(MemoDragData)) is MemoDragData data)
                {
                    e.Effects = (e.KeyStates & DragDropKeyStates.ControlKey) != 0 ? DragDropEffects.Copy
                        : data.ConfirmCopy ? DragDropEffects.None // 잠금 중엔 이동 불가 (Ctrl 을 떼면 놓을 수 없음)
                        : DragDropEffects.Move;
                    e.Handled = true;
                }
            };

            cell.DragEnter += (s, e) => { if (e.Data.GetDataPresent(typeof(MemoDragData))) { cell.BorderBrush = new SolidColorBrush(Theme.L(Color.FromRgb(90, 200, 250))); cell.BorderThickness = new Thickness(2); } };
            cell.DragLeave += (s, e) => { cell.BorderBrush = normalBorder; cell.BorderThickness = new Thickness(1); };

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            StackPanel header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 5, 0, 3) };
            if (isAdjacentMonth)
                header.Children.Add(new TextBlock { Text = $"{date.Month}/", FontSize = 10, Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(120, 128, 162))), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 1, 1) });

            TextBlock txtDay = new TextBlock { Text = day.ToString(), FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };

            if (!string.IsNullOrEmpty(holiday) || date.DayOfWeek == DayOfWeek.Sunday)
                txtDay.Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(224, 80, 80)));
            else if (date.DayOfWeek == DayOfWeek.Saturday)
                txtDay.Foreground = Theme.Pick(Color.FromRgb(78, 184, 232), Color.FromRgb(47, 125, 209)); // 토요일은 파랑
            else
                txtDay.Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(210, 216, 236)));

            if (isToday)
            {
                txtDay.FontWeight = FontWeights.Bold;
                txtDay.Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(90, 200, 250)));
            }

            if (Theme.IsDark) txtDay.Effect = new DropShadowEffect { BlurRadius = 2, ShadowDepth = 0, Opacity = 0.4, Color = Colors.Black };
            if (isToday && !Theme.IsDark)
            {
                txtDay.Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                txtDay.HorizontalAlignment = HorizontalAlignment.Center;
                header.Children.Add(new Border
                {
                    Background = new SolidColorBrush(LightTodayDot),
                    CornerRadius = new CornerRadius(11),
                    MinWidth = 22, Height = 22, Padding = new Thickness(6, 0, 6, 1),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(-4, -2, 0, -2),
                    Child = txtDay,
                });
            }
            else header.Children.Add(txtDay);

            if (!string.IsNullOrEmpty(holiday))
                header.Children.Add(new TextBlock { Text = $"  {holiday}", FontSize = 10, Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(220, 110, 70))), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0) });

            StackPanel memoPanel = new StackPanel { Margin = new Thickness(4, 1, 4, 2), Orientation = Orientation.Vertical };

            if (items != null && items.Count > 0)
            {
                bool isWeekStart = _settings.HideWeekends ? date.DayOfWeek == DayOfWeek.Monday : date.DayOfWeek == DayOfWeek.Sunday;

                // ── 연속일정 막대 + 주말 다리 ──
                var rendered = new List<(MemoItem memo, bool isRun, bool contPrev, bool contNext, int pat, bool labelDay, bool isBridge)>();

                if (items != null)
                {
                    foreach (var memo in items)
                    {
                        bool cp = false, cn = false, labelDay = true;
                        if (_settings.ConnectConsecutive && !_isPrivacyMode)
                        {
                            bool wd = IsWeekdayMacro(memo);  // 평일 매크로는 항상 주말 건너뜀
                            DateTime pd = wd ? AddWeekdays(date, -1) : date.AddDays(-1);
                            DateTime nd = wd ? AddWeekdays(date, 1) : date.AddDays(1);
                            cp = HasMatchingMemo(pd, memo);
                            cn = HasMatchingMemo(nd, memo);
                            labelDay = !cp || !SameVisualRow(pd, date);  // 줄(주)마다 한 번 라벨
                        }
                        bool run = cp || cn;
                        int pat = 0;
                        if (run) _runPatternIndex.TryGetValue((memo.Text ?? "").Trim(), out pat);
                        rendered.Add((memo, run, cp, cn, pat, labelDay, false));
                    }
                }

                //// 주말 칸: 평일 매크로가 금↔월을 가로지르면 빈 색 막대(다리) 추가
                //foreach (var (bt, bc) in WeekendBridges(date))
                //{
                //    _runPatternIndex.TryGetValue(bt, out int bpat);
                //    rendered.Add((new MemoItem { Text = bt, TextColor = bc, IsMacro = true }, true, true, true, bpat, false, true));
                //}

                if (rendered.Count > 0)
                {
                    var ordered = rendered
                        .OrderBy(r => r.isRun ? 0 : 1)
                        .ThenBy(r => r.isRun ? r.pat : int.MaxValue)
                        .ThenBy(r => r.memo.IsDone ? 1 : 0)
                        .ToList();

                    int numberCounter = 1;
                    foreach (var (memo, isRun, contPrev, contNext, pat, labelDay, isBridge) in ordered)
                    {
                        double itemHeight = Math.Max(26, _settings.FontSize + 10);
                        var txtMemo = new TextBlock
                        {
                            FontSize = _settings.FontSize,
                            TextWrapping = TextWrapping.NoWrap,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            Background = Brushes.Transparent,
                            VerticalAlignment = VerticalAlignment.Center,
                            Padding = new Thickness(6, 0, 6, 0),
                            Tag = isBridge ? null : memo
                        };
                        // 한 줄 = 둥근 칩 (일정 색을 옅게 깐 바탕, 글자는 가운데). 연속 일정은 이어지는 막대라 이어지는 쪽 모서리만 각지게
                        var item = new Border
                        {
                            Height = itemHeight - 2,
                            Margin = new Thickness(0, 1, 0, 1),
                            CornerRadius = new CornerRadius(5),
                            Background = Brushes.Transparent,
                            Cursor = isBridge ? Cursors.Arrow : Cursors.Hand,
                            Child = txtMemo,
                        };

                        Brush restBg = Brushes.Transparent;
                        Color runColor = Colors.Transparent;
                        bool runHasColor = false;
                        if (isRun)
                        {
                            if (!string.IsNullOrEmpty(memo.TextColor))
                            {
                                try { runColor = (Color)ColorConverter.ConvertFromString(memo.TextColor); runHasColor = true; }
                                catch { runHasColor = false; }
                            }
                            restBg = runHasColor
                                ? new SolidColorBrush(RunBarBg(runColor))
                                : new SolidColorBrush(Theme.L(Color.FromRgb(58, 62, 76)));  // 무색 = 기본 중립 막대
                            item.Margin = new Thickness(contPrev ? -4 : 0, 1, contNext ? -4 : 0, 1);
                            item.CornerRadius = new CornerRadius(contPrev ? 0 : 5, contNext ? 0 : 5, contNext ? 0 : 5, contPrev ? 0 : 5);
                        }
                        if (isBridge)
                        {
                            // 주말 다리: 글자/핸들러 없이 색 막대만
                            txtMemo.Text = "";
                            item.Background = restBg;
                        }
                        else if (_isPrivacyMode) { txtMemo.Text = "•••"; txtMemo.Foreground = Brushes.Gray; txtMemo.Opacity = 0.5; item.Background = MemoChipBg(null, false); }
                        else if (isRun)
                        {
                            txtMemo.Text = labelDay ? memo.Text : "";
                            item.Background = restBg;
                            if (memo.IsDone)
                            {
                                txtMemo.TextDecorations = TextDecorations.Strikethrough;
                                txtMemo.Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(150, 156, 178)));
                                txtMemo.Opacity = 0.6;
                            }
                            else txtMemo.Foreground = runHasColor
                            ? new SolidColorBrush(RunTextColor(runColor))
                            : (new SolidColorBrush(Theme.L(Color.FromRgb(220, 226, 244))));  // 무색 = 기본 글자색
                            txtMemo.PreviewMouseLeftButtonDown += TxtMemo_PreviewMouseLeftButtonDown;
                            txtMemo.MouseMove += TxtMemo_MouseMove;
                            item.MouseEnter += (s, e) => { item.Opacity = 0.85; };
                            item.MouseLeave += (s, e) => { item.Opacity = 1.0; };
                        }
                        else
                        {
                            string bullet = _settings.BulletStyle switch { 1 => "■ ", 2 => "- ", 3 => $"{numberCounter++}. ", _ => "● " };
                            txtMemo.Text = $"{bullet}{memo.Text}";
                            Color? chipColor = null;
                            if (!string.IsNullOrEmpty(memo.TextColor) && new BrushConverter().ConvertFromString(memo.TextColor) is SolidColorBrush userColor) chipColor = userColor.Color;
                            if (memo.IsDone) { txtMemo.TextDecorations = TextDecorations.Strikethrough; txtMemo.Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(100, 108, 140))); txtMemo.Opacity = Theme.IsDark ? 0.55 : 0.9; chipColor = null; }
                            else txtMemo.Foreground = chipColor is Color cc ? new SolidColorBrush(Theme.TextOn(cc)) : new SolidColorBrush(Theme.L(Color.FromRgb(220, 226, 244)));
                            item.Background = MemoChipBg(chipColor, false);
                            txtMemo.PreviewMouseLeftButtonDown += TxtMemo_PreviewMouseLeftButtonDown;
                            txtMemo.MouseMove += TxtMemo_MouseMove;
                            item.MouseEnter += (s, e) => { item.Background = MemoChipBg(chipColor, true); };
                            item.MouseLeave += (s, e) => { item.Background = MemoChipBg(chipColor, false); };
                        }
                        memoPanel.Children.Add(item);
                    }
                }
               
            }

            if (memoPanel.Children.Count == 0) memoPanel.Children.Add(new TextBlock { Text = "", Height = 24 });

            ScrollViewer scroll = new ScrollViewer { Content = memoPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(2, 0, 2, 2), Padding = new Thickness(0), BorderThickness = new Thickness(0) };

            AddCellHeader(grid, header, date); // 교대근무를 켜면 날짜 줄 오른쪽에 근무 배지가 붙음
            grid.Children.Add(scroll); Grid.SetRow(scroll, 1);
            cell.Child = grid;

            cell.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { if (isAdjacentMonth) { _currentDate = new DateTime(date.Year, date.Month, 1); RefreshCalendar(); } OpenInputBox(dateKey); e.Handled = true; }
            };
            cell.MouseRightButtonDown += (s, e) => { ShowDayScheduleWindow(dateKey); e.Handled = true; };
            return cell;
        }

        private void TxtMemo_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock) { _dragStartPosition = e.GetPosition(null); _isDragging = false; e.Handled = true; }
        }

        private void TxtMemo_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is TextBlock tb && e.LeftButton == MouseButtonState.Pressed)
            {
                var diff = _dragStartPosition - e.GetPosition(null);
                if (!_isDragging && (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
                {
                    // 고정 + 드래그 잠금: 이동은 막고, Ctrl 을 누른 채 끌면 놓을 때 확인한 뒤 복사만
                    bool locked = IsPinned && _settings.LockDragWhenPinned;
                    if (locked && (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

                    _isDragging = true;
                    if (tb.Tag is MemoItem mi && FindCellDateKey(tb) is string sourceDateKey)
                        DragDrop.DoDragDrop(tb, new MemoDragData { Item = mi, SourceDate = sourceDateKey, ConfirmCopy = locked },
                            locked ? DragDropEffects.Copy : DragDropEffects.Move | DragDropEffects.Copy);
                    _isDragging = false;
                }
            }
        }

        // 일정 글자에서 위로 올라가 날짜 칸(Tag = "yyyy-MM-dd" 인 Border)을 찾음
        private static string? FindCellDateKey(DependencyObject element)
        {
            for (var node = VisualTreeHelper.GetParent(element); node != null; node = VisualTreeHelper.GetParent(node))
                if (node is Border { Tag: string dateKey }) return dateKey;
            return null;
        }

        private void AddMemoCopy(MemoItem item, string dateKey)
        {
            if (!_memoData.TryGetValue(dateKey, out var targetList)) { targetList = new List<MemoItem>(); _memoData[dateKey] = targetList; }
            var cloned = item.Clone();
            cloned.IsMacro = false;
            targetList.Add(cloned);
        }

        private void ConfirmLockedCopy(MemoItem item, string dateKey)
        {
            var target = DateTime.ParseExact(dateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (MessageBox.Show(this, $"{target:M월 d일}에 일정을 복사할까요?\n\n\"{item.Text}\"", "일정 복사",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            AddMemoCopy(item, dateKey);
            SaveData(); RefreshCalendar();
        }

        // ══════════════════════════════════════════════════════════════
        // 검색 기능
        // ══════════════════════════════════════════════════════════════
        private void BtnSearch_Click(object sender, RoutedEventArgs e) => ShowSearchWindow();

        private void ShowSearchWindow()
        {
            var P = typeof(ModalPalette); // 팔레트 alias

            Window searchWin = new Window
            { FontFamily = AppFont,
                Title = "일정 검색",
                Width = 760,
                Height = 920,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize
            };
            searchWin.Resources.MergedDictionaries.Add(this.Resources);

            // ── 최외각 컨테이너 ──
            Border outerBorder = new Border
            {
                CornerRadius = new CornerRadius(20),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0),
                BorderBrush = new LinearGradientBrush(
        new GradientStopCollection {
            new GradientStop(Theme.L(Color.FromArgb(60, 90, 200, 250)), 0),
            new GradientStop(Theme.L(Color.FromArgb(15, 255, 255, 255)), 0.5),
            new GradientStop(Theme.L(Color.FromArgb(40, 90, 200, 250)), 1)
        }, 45)
            };
            outerBorder.Background = new LinearGradientBrush(
                new GradientStopCollection {
        new GradientStop(Theme.L(Color.FromRgb(11, 14, 26)), 0),
        new GradientStop(Theme.L(Color.FromRgb(8, 10, 20)), 1)
                }, 90);
            outerBorder.Effect = new DropShadowEffect
            {
                BlurRadius = 80,
                Opacity = 0.85,
                ShadowDepth = 0,
                Color = Color.FromRgb(2, 4, 12)
            };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });      // 헤더
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });      // 검색바
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });      // 필터
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });      // 카운트
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 결과

            // ── 헤더 ──
            Border headerBorder = new Border
            {
                CornerRadius = new CornerRadius(20, 20, 0, 0),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(28, 0, 20, 0),
                BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(20, 90, 200, 250)))
            };
            headerBorder.Background = new LinearGradientBrush(
                new GradientStopCollection {
        new GradientStop(Theme.L(Color.FromRgb(14, 18, 34)), 0),
        new GradientStop(Theme.L(Color.FromRgb(9, 11, 22)), 1)
                }, 90);
            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.MouseLeftButtonDown += (s, e) => searchWin.DragMove();

            // 아이콘 + 제목 스택
            StackPanel titleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 🔍 아이콘 박스
            Border iconBox = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 14, 0),
                Background = new LinearGradientBrush(
        new GradientStopCollection {
            new GradientStop(Theme.L(Color.FromArgb(50, 90, 200, 250)), 0),
            new GradientStop(Theme.L(Color.FromArgb(15, 90, 200, 250)), 1)
        }, 135),
                BorderBrush = new LinearGradientBrush(
        new GradientStopCollection {
            new GradientStop(Theme.L(Color.FromArgb(120, 90, 200, 250)), 0),
            new GradientStop(Theme.L(Color.FromArgb(30, 90, 200, 250)), 1)
        }, 135)
            };
            iconBox.Child = new TextBlock
            {
                Text = "⌕",
                FontSize = 20,
                Foreground = ModalPalette.Brush(ModalPalette.Accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            StackPanel headTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            headTexts.Children.Add(new TextBlock
            {
                Text = "일정 검색",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = ModalPalette.Brush(ModalPalette.TxtPrimary),
            });
            headTexts.Children.Add(new TextBlock
            {
                Text = "메모 내용으로 전체 일정을 검색합니다",
                FontSize = 11,
                Foreground = ModalPalette.Brush(ModalPalette.TxtSecondary)
            });

            titleStack.Children.Add(iconBox);
            titleStack.Children.Add(headTexts);
            Grid.SetColumn(titleStack, 0);
            headerGrid.Children.Add(titleStack);

            Button btnClose = MakeCloseButton();
            btnClose.Click += (s, e) => searchWin.Close();
            Grid.SetColumn(btnClose, 1);
            headerGrid.Children.Add(btnClose);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            layout.Children.Add(headerBorder);

            // ── 검색바 ──
            Border searchBarOuter = new Border
            {
                Padding = new Thickness(24, 8, 24, 0)
            };
            Grid searchBar = new Grid();
            searchBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });

            // 검색 입력창 – 좌측 아이콘 내장
            Grid inputGrid = new Grid();
            Border inputBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Theme.L(Color.FromRgb(9, 11, 22))),
                BorderBrush = new LinearGradientBrush(
        new GradientStopCollection {
            new GradientStop(Theme.L(Color.FromArgb(50, 90, 200, 250)), 0),
            new GradientStop(Theme.L(Color.FromArgb(15, 255, 255, 255)), 1)
        }, 90)
            };

            Grid inputInner = new Grid();
            inputInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            inputInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock searchIcon = new TextBlock
            {
                Text = "⌕",
                FontSize = 17,
                Foreground = ModalPalette.Brush(ModalPalette.TxtMuted),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(searchIcon, 0);

            TextBox txtSearch = new TextBox
            {
                FontSize = 14,
                Background = Brushes.Transparent,
                Foreground = ModalPalette.Brush(ModalPalette.TxtPrimary),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 10, 12, 10),
                CaretBrush = ModalPalette.Brush(ModalPalette.Accent)
            };
            txtSearch.GotFocus += (s, e) =>
            {
                inputBorder.BorderBrush = new LinearGradientBrush(
    new GradientStopCollection {
        new GradientStop(Theme.L(Color.FromArgb(200, 90, 200, 250)), 0),
        new GradientStop(Theme.L(Color.FromArgb(80, 90, 200, 250)), 1)
    }, 90);

            };
            txtSearch.LostFocus += (s, e) =>
            {
                inputBorder.BorderBrush = new LinearGradientBrush(
    new GradientStopCollection {
        new GradientStop(Theme.L(Color.FromArgb(50, 90, 200, 250)), 0),
        new GradientStop(Theme.L(Color.FromArgb(15, 255, 255, 255)), 1)
    }, 90);
                inputBorder.Effect = null;
            };
            Grid.SetColumn(txtSearch, 1);

            inputInner.Children.Add(searchIcon);
            inputInner.Children.Add(txtSearch);
            inputBorder.Child = inputInner;
            inputGrid.Children.Add(inputBorder);
            Grid.SetColumn(inputGrid, 0);
            searchBar.Children.Add(inputGrid);

            // 검색 버튼
            Button btnDoSearch = new Button
            {
                Content = "검색",
                Height = 42,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            btnDoSearch.Background = new LinearGradientBrush(
    new GradientStopCollection {
        new GradientStop(Theme.L(Color.FromRgb(40, 130, 220)), 0),
        new GradientStop(Theme.L(Color.FromRgb(80, 195, 250)), 1)
    }, 90);
            btnDoSearch.Template = MakeRoundedButtonTemplate(12);
            Grid.SetColumn(btnDoSearch, 1);
            searchBar.Children.Add(btnDoSearch);

            searchBarOuter.Child = searchBar;
            Grid.SetRow(searchBarOuter, 1);
            layout.Children.Add(searchBarOuter);

            // ── 필터 ──
            Border filterOuter = new Border
            {
                Padding = new Thickness(24, 0, 24, 0)
            };
            StackPanel filterPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 날짜 범위
            TextBlock lblFrom = new TextBlock
            {
                Text = "기간",
                Foreground = ModalPalette.Brush(ModalPalette.TxtSecondary),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            DatePicker dpFrom = MakeDatePicker();
            Border dpFromSep = MakeDateSep();
            DatePicker dpTo = MakeDatePicker();

            // 완료 포함 pill 토글
            CheckBox chkIncludeDone = new CheckBox
            {
                Content = "완료 포함",
                IsChecked = true,
                Foreground = ModalPalette.Brush(ModalPalette.TxtPrimary),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0),
                Cursor = Cursors.Hand,
                Style = (Style)FindResource("ModernCheckBox")
            };

            filterPanel.Children.Add(lblFrom);
            filterPanel.Children.Add(WrapDatePicker(dpFrom, "시작날짜(옵션)"));
            filterPanel.Children.Add(dpFromSep);
            filterPanel.Children.Add(WrapDatePicker(dpTo, "종료날짜(옵션)"));
            filterPanel.Children.Add(chkIncludeDone);

            filterOuter.Child = filterPanel;
            Grid.SetRow(filterOuter, 2);
            layout.Children.Add(filterOuter);

            // ── 결과 카운트 ──
            Border countOuter = new Border { Padding = new Thickness(26, 0, 24, 0) };
            TextBlock txtResultCount = new TextBlock
            {
                Foreground = ModalPalette.Brush(ModalPalette.TxtMuted),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            countOuter.Child = txtResultCount;
            Grid.SetRow(countOuter, 3);
            layout.Children.Add(countOuter);

            // ── 결과 목록 ──
            Border resultOuter = new Border
            {
                Padding = new Thickness(16, 0, 16, 16)
            };
            ScrollViewer resultScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            StackPanel resultPanel = new StackPanel {};
            resultScroll.Content = resultPanel;
            resultOuter.Child = resultScroll;
            Grid.SetRow(resultOuter, 4);
            layout.Children.Add(resultOuter);

            // ── 검색 로직 ──
            Action doSearch = () =>
            {
                resultPanel.Children.Clear();
                string keyword = txtSearch.Text.Trim();
                if (string.IsNullOrEmpty(keyword)) { txtResultCount.Text = ""; return; }

                bool includeDone = (chkIncludeDone.IsChecked == true);
                DateTime? fromDate = dpFrom.SelectedDate;
                DateTime? toDate = dpTo.SelectedDate;

                var results = new List<(string DateKey, MemoItem Item)>();
                foreach (var kv in _memoData.OrderBy(k => k.Key))
                {
                    if (!DateTime.TryParse(kv.Key, out DateTime d)) continue;
                    if (fromDate.HasValue && d.Date < fromDate.Value.Date) continue;
                    if (toDate.HasValue && d.Date > toDate.Value.Date) continue;
                    foreach (var item in kv.Value)
                    {
                        if (!includeDone && item.IsDone) continue;
                        if (item.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            results.Add((kv.Key, item));
                    }
                }

                txtResultCount.Text = results.Count == 0
                    ? "검색 결과가 없습니다"
                    : $"{results.Count}건 발견";

                if (results.Count == 0)
                {
                    Border empty = new Border { Padding = new Thickness(0, 40, 0, 0) };
                    StackPanel ep = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    ep.Children.Add(new TextBlock
                    {
                        Text = "결과 없음",
                        FontSize = 32,
                        Opacity = 0.15,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = ModalPalette.Brush(ModalPalette.TxtPrimary)
                    });
                    ep.Children.Add(new TextBlock
                    {
                        Text = $"'{keyword}' 에 대한 일정이 없습니다",
                        FontSize = 13,
                        Foreground = ModalPalette.Brush(ModalPalette.TxtMuted),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 10, 0, 0)
                    });
                    empty.Child = ep;
                    resultPanel.Children.Add(empty);
                    return;
                }

                foreach (var (dk, item) in results)
                {
                    // ── 결과 행 ──
                    Border rowBorder = new Border
                    {
                        Background = ModalPalette.Brush(ModalPalette.BgRow),
                        CornerRadius = new CornerRadius(10),
                        BorderBrush = ModalPalette.Brush(ModalPalette.BorderGlass),
                        BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand,
                        Padding = new Thickness(0)
                    };
                    rowBorder.Effect = new DropShadowEffect
                    {
                        BlurRadius = 8,
                        ShadowDepth = 2,
                        Opacity = 0.15,
                        Color = Color.FromRgb(5, 8, 18)
                    };

                    Grid row = new Grid { Height = 52 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });

                    // 상태 바
                    Color barColor = item.IsDone
                        ? ModalPalette.TxtMuted
                        : ModalPalette.Accent;
                    Border accentBar = new Border
                    {
                        Width = 4,
                        CornerRadius = new CornerRadius(4, 0, 0, 4),
                        Background = new SolidColorBrush(barColor),
                        VerticalAlignment = VerticalAlignment.Stretch
                    };
                    if (!item.IsDone)
                    Grid.SetColumn(accentBar, 0);
                    row.Children.Add(accentBar);

                    // 날짜 칼럼
                    StackPanel dateStack = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(14, 0, 0, 0)
                    };
                    if (DateTime.TryParse(dk, out DateTime dd))
                    {
                        dateStack.Children.Add(new TextBlock
                        {
                            Text = dd.ToString("MM.dd"),
                            FontSize = 15,
                            FontWeight = FontWeights.Bold,
                            Foreground = ModalPalette.Brush(ModalPalette.Accent)
                        });
                        dateStack.Children.Add(new TextBlock
                        {
                            Text = dd.ToString("ddd  yyyy"),
                            FontSize = 10,
                            Foreground = ModalPalette.Brush(ModalPalette.TxtMuted)
                        });
                    }
                    Grid.SetColumn(dateStack, 1);
                    row.Children.Add(dateStack);

                    // 텍스트
                    StackPanel textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    if (item.IsMacro)
                        textStack.Children.Add(new Border
                        {
                            Background = ModalPalette.Brush(ModalPalette.AccentDim),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(5, 1, 5, 1),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(0, 0, 0, 2),
                            Child = new TextBlock
                            {
                                Text = "매크로",
                                FontSize = 9,
                                FontWeight = FontWeights.Bold,
                                Foreground = ModalPalette.Brush(ModalPalette.Accent)
                            }
                        });
                    TextBlock tbText = new TextBlock
                    {
                        Text = item.Text,
                        FontSize = 13,
                        Foreground = item.IsDone
                            ? ModalPalette.Brush(ModalPalette.TxtMuted)
                            : ModalPalette.Brush(ModalPalette.TxtPrimary),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    if (item.IsDone)
                        tbText.TextDecorations = TextDecorations.Strikethrough;
                    textStack.Children.Add(tbText);
                    Grid.SetColumn(textStack, 2);
                    row.Children.Add(textStack);

                    // 화살표
                    TextBlock arrow = new TextBlock
                    {
                        Text = "›",
                        FontSize = 20,
                        Foreground = ModalPalette.Brush(ModalPalette.TxtMuted),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Opacity = Theme.IsDark ? 0.5 : 0.85
                    };
                    Grid.SetColumn(arrow, 3);
                    row.Children.Add(arrow);

                    rowBorder.Child = row;

                    // hover 애니메이션 느낌
                    rowBorder.MouseEnter += (s2, e2) =>
                    {
                        rowBorder.Background = new SolidColorBrush(Theme.L(Color.FromArgb(18, 90, 200, 250)));
                        rowBorder.BorderBrush = new LinearGradientBrush(
                            new GradientStopCollection {
        new GradientStop(Theme.L(Color.FromArgb(100, 90, 200, 250)), 0),
        new GradientStop(Theme.L(Color.FromArgb(30, 90, 200, 250)), 1)
                            }, 90);
                        arrow.Opacity = 1.0;
                    };
                    rowBorder.MouseLeave += (s2, e2) =>
                    {
                        rowBorder.Background = ModalPalette.Brush(ModalPalette.BgRow);
                        rowBorder.BorderBrush = ModalPalette.Brush(ModalPalette.BorderGlass);
                        arrow.Opacity = 0.5;
                    };
                    rowBorder.MouseLeftButtonDown += (s2, e2) =>
                    {
                        if (DateTime.TryParse(dk, out DateTime target))
                            _currentDate = new DateTime(target.Year, target.Month, 1);
                        RefreshCalendar();
                        searchWin.Close();
                        OpenInputBox(dk);
                    };

                    resultPanel.Children.Add(rowBorder);
                }
            };

            btnDoSearch.Click += (s, e) => doSearch();
            txtSearch.KeyDown += (s, e) => { if (e.Key == Key.Enter) doSearch(); };
            searchWin.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Escape) return;
                // 날짜 선택 달력이 열려 있으면 그것만 닫음 (창 전체가 닫혀 입력이 사라지지 않게)
                if (dpFrom.IsDropDownOpen || dpTo.IsDropDownOpen) { dpFrom.IsDropDownOpen = false; dpTo.IsDropDownOpen = false; }
                else searchWin.Close();
                e.Handled = true;
            };

            outerBorder.Child = layout;
            ApplyModalEdge(outerBorder); searchWin.Content = outerBorder;
            searchWin.Loaded += (s, e) => txtSearch.Focus();
            searchWin.ShowDialog();
        }


        // ══════════════════════════════════════════════════════════════
        // 매크로 일정 관리 창
        // ══════════════════════════════════════════════════════════════
        private void BtnMacro_Click(object sender, RoutedEventArgs e) => ShowMacroWindow();

        private void ShowMacroWindow()
        {
            LoadMacros();

            Window macroWin = new Window
            { FontFamily = AppFont,
                Title = "매크로 일정 관리",
                Width = 840,
                Height = 920,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize
            };
            macroWin.Resources.MergedDictionaries.Add(this.Resources);

            // 팔레트 별칭
            SolidColorBrush bgDeep = ModalPalette.Brush(ModalPalette.BgDeep);
            SolidColorBrush bgCard = ModalPalette.Brush(ModalPalette.BgCard);
            SolidColorBrush bgInput = ModalPalette.Brush(ModalPalette.BgInput);
            SolidColorBrush txtPrimary = ModalPalette.Brush(ModalPalette.TxtPrimary);
            SolidColorBrush txtSecondary = ModalPalette.Brush(ModalPalette.TxtSecondary);
            SolidColorBrush txtMuted = ModalPalette.Brush(ModalPalette.TxtMuted);
            SolidColorBrush accentBr = ModalPalette.Brush(ModalPalette.Accent);
            SolidColorBrush accentDimBr = ModalPalette.Brush(ModalPalette.AccentDim);
            SolidColorBrush dangerBr = ModalPalette.Brush(ModalPalette.Danger);
            SolidColorBrush successBr = ModalPalette.Brush(ModalPalette.Success);
            SolidColorBrush warningBr = ModalPalette.Brush(ModalPalette.Warning);
            SolidColorBrush purpleBr = ModalPalette.Brush(ModalPalette.Purple);
            SolidColorBrush borderGlass = ModalPalette.Brush(ModalPalette.BorderGlass);

            Brush toggleOffBg = ModalPalette.Brush(Theme.L(Color.FromRgb(24, 27, 44)));
            Brush toggleOffFg = txtSecondary;
            Brush selectedBg = ModalPalette.Brush(Theme.L(Color.FromArgb(30, 90, 200, 250)));
            Brush hoverBg = ModalPalette.Brush(ModalPalette.BgHover);
            Brush inputBorderBr = ModalPalette.Brush(ModalPalette.BorderInput);

            // ── 최외각 ──
            Border outerBorder = new Border
            {
                CornerRadius = new CornerRadius(22),
                BorderThickness = new Thickness(1),
                Background = new LinearGradientBrush(
        new GradientStopCollection {
            new GradientStop(Theme.L(Color.FromRgb(11, 14, 26)), 0),
            new GradientStop(Theme.L(Color.FromRgb(7, 9, 18)), 1)
        }, 90),
                BorderBrush = new LinearGradientBrush(
        new GradientStopCollection {
            new GradientStop(Theme.L(Color.FromArgb(70, 90, 200, 250)), 0),
            new GradientStop(Theme.L(Color.FromArgb(12, 255, 255, 255)), 0.5),
            new GradientStop(Theme.L(Color.FromArgb(50, 90, 200, 250)), 1)
        }, 45)
            };
            outerBorder.Effect = new DropShadowEffect { BlurRadius = 90, Opacity = 0.90, ShadowDepth = 0, Color = Color.FromRgb(1, 2, 8) };

            Grid rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78) });      // 헤더
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 목록
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });       // 구분선
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // 폼

            // ══ 헤더 ══
            Border headerBd = new Border
            {
                CornerRadius = new CornerRadius(22, 22, 0, 0),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(28, 0, 20, 0),
                Background = new LinearGradientBrush(
        new GradientStopCollection {
            new GradientStop(Theme.L(Color.FromRgb(14, 18, 34)), 0),
            new GradientStop(Theme.L(Color.FromRgb(8, 10, 20)), 1)
        }, 90),
                BorderBrush = new LinearGradientBrush(
        new GradientStopCollection {
            new GradientStop(Theme.L(Color.FromArgb(40, 90, 200, 250)), 0),
            new GradientStop(Theme.L(Color.FromArgb(8, 255, 255, 255)), 1)
        }, 0)
            };
            Grid hdrGrid = new Grid();
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdrGrid.MouseLeftButtonDown += (s, e) => macroWin.DragMove();

            // 아이콘박스
            Border hdrIcon = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(13),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 14, 0),
                Background = new LinearGradientBrush(
        new GradientStopCollection {
            new GradientStop(Theme.L(Color.FromArgb(55, 90, 200, 250)), 0),
            new GradientStop(Theme.L(Color.FromArgb(15, 90, 200, 250)), 1)
        }, 135),
                BorderBrush = new LinearGradientBrush(
        new GradientStopCollection {
            new GradientStop(Theme.L(Color.FromArgb(130, 90, 200, 250)), 0),
            new GradientStop(Theme.L(Color.FromArgb(25, 90, 200, 250)), 1)
        }, 135)
            };
            hdrIcon.Child = new TextBlock
            {
                Text = "⟳",
                FontSize = 20,
                Foreground = accentBr,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(hdrIcon, 0);
            hdrGrid.Children.Add(hdrIcon);

            StackPanel hdrTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            hdrTexts.Children.Add(new TextBlock
            {
                Text = "매크로 일정",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = txtPrimary,
                Margin = new Thickness(0, 0, 0, 2)
            });
            hdrTexts.Children.Add(new TextBlock
            {
                Text = "반복 패턴으로 여러 날짜에 한번에 일정을 등록합니다",
                FontSize = 11,
                Foreground = txtSecondary
            });
            Grid.SetColumn(hdrTexts, 1);
            hdrGrid.Children.Add(hdrTexts);

            Button btnClose = MakeCloseButton();
            btnClose.Click += (s, e) => macroWin.Close();
            Grid.SetColumn(btnClose, 2);
            hdrGrid.Children.Add(btnClose);

            headerBd.Child = hdrGrid;
            Grid.SetRow(headerBd, 0);
            rootGrid.Children.Add(headerBd);

            // ══ 매크로 목록 ══
            ScrollViewer listScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(22, 16, 22, 12)
            };
            StackPanel listPanel = new StackPanel {};
            listScroll.Content = listPanel;
            Grid.SetRow(listScroll, 1);
            rootGrid.Children.Add(listScroll);

            // ══ 구분선 ══
            Border divider = new Border
            {
                Height = 1,
                Background = borderGlass
            };
            Grid.SetRow(divider, 2);
            rootGrid.Children.Add(divider);

            // ══ 폼 패널 ══
            Border formCard = new Border
            {
                CornerRadius = new CornerRadius(0, 0, 22, 22),
                Padding = new Thickness(28, 18, 28, 22),
                Background = new LinearGradientBrush(
        new GradientStopCollection {
            new GradientStop(Theme.L(Color.FromRgb(13, 16, 30)), 0),
            new GradientStop(Theme.L(Color.FromRgb(9, 11, 22)), 1)
        }, 90),
                BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(18, 90, 200, 250))),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            StackPanel formStack = new StackPanel {};

            // 메모 내용
            formStack.Children.Add(MakeFormLabel("메모 내용"));
            TextBox txtText = MakeFormInput(bgInput, txtPrimary, inputBorderBr, accentBr);
            formStack.Children.Add(txtText);

            // 색상 선택
            formStack.Children.Add(MakeFormLabel("메모 색상", topMargin: 6));

            string[] macroColors = { null, "#FF5C5C", "#FF9F43", "#FECA57", "#2ECC71", "#54A0FF", "#5F27CD", "#A3CB38" };
            string[] macroColorNames = { "기본", "빨강", "주황", "노랑", "초록", "파랑", "보라", "연두" };
            string selectedMacroColor = null;

            WrapPanel colorPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 0) };
            Button[] colorBtns = new Button[macroColors.Length];

            Action<int> selectColorBtn = null;
            selectColorBtn = (selIdx) =>
            {
                selectedMacroColor = macroColors[selIdx];
                for (int ci = 0; ci < colorBtns.Length; ci++)
                {
                    bool isSel = ci == selIdx;
                    colorBtns[ci].BorderThickness = new Thickness(isSel ? 2 : 1);
                    colorBtns[ci].BorderBrush = isSel
                        ? ModalPalette.Brush(ModalPalette.Accent)
                        : ModalPalette.Brush(ModalPalette.BorderGlass);
                    colorBtns[ci].Opacity = isSel ? 1.0 : 0.85;
                }

                // 제목 입력창 글씨색 즉시 반영
                txtText.Foreground = selectedMacroColor == null
                    ? txtPrimary
                    : UserTextBrush(selectedMacroColor, txtPrimary);
            };

            for (int ci = 0; ci < macroColors.Length; ci++)
            {
                int idx = ci;
                string hex = macroColors[ci];
                Color dotColor = hex == null
                    ? ModalPalette.TxtPrimary
                    : (Color)ColorConverter.ConvertFromString(hex);

                Border dot = new Border
                {
                    Width = 14,
                    Height = 14,
                    CornerRadius = new CornerRadius(7),
                    Background = new SolidColorBrush(dotColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0)
                };

                Button cb = new Button
                {
                    Height = 30,
                    Margin = new Thickness(0, 0, 6, 4),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(1),
                    BorderBrush = ModalPalette.Brush(ModalPalette.BorderGlass),
                    Opacity = 0.85,
                    Padding = new Thickness(8, 0, 10, 0)
                };
                cb.Background = ModalPalette.Brush(ModalPalette.BgRow);
                cb.Template = MakeRoundedButtonTemplate(8);

                StackPanel cbInner = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                cbInner.Children.Add(dot);
                cbInner.Children.Add(new TextBlock
                {
                    Text = macroColorNames[ci],
                    FontSize = 11,
                    Foreground = ModalPalette.Brush(ModalPalette.TxtPrimary), // 어두운 색 글자는 안 보이므로 색은 동그라미로만
                    VerticalAlignment = VerticalAlignment.Center
                });
                cb.Content = cbInner;
                cb.Click += (s, e) => selectColorBtn(idx);
                colorBtns[ci] = cb;
                colorPanel.Children.Add(cb);
            }
            selectColorBtn(0); // 기본 선택
            formStack.Children.Add(colorPanel);

            // 반복 설정
            formStack.Children.Add(MakeFormLabel("반복 설정", topMargin: 10));

            ComboBox cmbRepeat = new ComboBox
            {
                FontSize = 13,
                Width = 180,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = bgInput,
                Foreground = txtPrimary,
                BorderBrush = ModalPalette.Brush(ModalPalette.BorderInput),
                BorderThickness = new Thickness(1)
            };
            // 기존 cmbRepeat 생성 코드 아래에 Template 추가
            cmbRepeat.Template = CreateComboBoxTemplate();
            cmbRepeat.ItemContainerStyle = CreateComboItemStyle();
            cmbRepeat.MouseEnter += (s, e) =>
            {
                if (cmbRepeat.Template.FindName("PART_CmbBorder", cmbRepeat) is Border b)
                {
                    b.BorderBrush = ModalPalette.Brush(ModalPalette.Accent);
                }
            };
            cmbRepeat.MouseLeave += (s, e) =>
            {
                if (cmbRepeat.Template.FindName("PART_CmbBorder", cmbRepeat) is Border b)
                {
                    b.BorderBrush = ModalPalette.Brush(ModalPalette.BorderInput);
                    b.Effect = null;
                }
            };


            cmbRepeat.Items.Add(new ComboBoxItem { Content = "매일 반복", Tag = "daily" });
            cmbRepeat.Items.Add(new ComboBoxItem { Content = "평일만 (월~금)", Tag = "weekdays" });
            cmbRepeat.Items.Add(new ComboBoxItem { Content = "매주 (요일 지정)", Tag = "weekly" });
            cmbRepeat.Items.Add(new ComboBoxItem { Content = "매월 (날짜 지정)", Tag = "monthly" });
            cmbRepeat.SelectedIndex = 0;
            formStack.Children.Add(cmbRepeat);

            // 요일 선택
            string[] dayNames = { "일", "월", "화", "수", "목", "금", "토" };
            WrapPanel weekdayPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
            var weekdayToggles = new ToggleButton[7];
            for (int i = 0; i < 7; i++)
            {
                int dayIdx = i;
                Color onColor = dayIdx == 0 ? ModalPalette.Danger
                              : dayIdx == 6 ? ModalPalette.Accent
                              : ModalPalette.Accent;
                var t = MakeDayToggle(dayNames[i], toggleOffBg, toggleOffFg, new SolidColorBrush(onColor));
                weekdayToggles[i] = t;
                weekdayPanel.Children.Add(t);
            }
            formStack.Children.Add(weekdayPanel);

            // 날짜 선택
            WrapPanel monthdayPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
            var monthdayToggles = new ToggleButton[31];
            for (int d = 1; d <= 31; d++)
            {
                var t = MakeDayToggle(d.ToString(), toggleOffBg, toggleOffFg,
                            accentBr, w: 34, h: 28, fontSize: 11);
                monthdayToggles[d - 1] = t;
                monthdayPanel.Children.Add(t);
            }
            formStack.Children.Add(monthdayPanel);

            cmbRepeat.SelectionChanged += (s, e) =>
            {
                string tag = (cmbRepeat.SelectedItem as ComboBoxItem)?.Tag as string ?? "daily";
                weekdayPanel.Visibility = tag == "weekly" ? Visibility.Visible : Visibility.Collapsed;
                monthdayPanel.Visibility = tag == "monthly" ? Visibility.Visible : Visibility.Collapsed;
            };

            Func<string> getRepeatValue = () =>
            {
                string tag = (cmbRepeat.SelectedItem as ComboBoxItem)?.Tag as string ?? "daily";
                if (tag == "weekly")
                    return string.Join(",", weekdayToggles
                        .Select((t, idx) => t.IsChecked == true ? idx.ToString() : null)
                        .Where(x => x != null));
                if (tag == "monthly")
                    return string.Join(",", monthdayToggles
                        .Select((t, idx) => t.IsChecked == true ? (idx + 1).ToString() : null)
                        .Where(x => x != null));
                return "";
            };

            Action<string, string> loadRepeatToggles = (repeatType, repeatValue) =>
            {
                foreach (var t in weekdayToggles) t.IsChecked = false;
                foreach (var t in monthdayToggles) t.IsChecked = false;
                if (string.IsNullOrEmpty(repeatValue)) return;
                var vals = repeatValue.Split(',')
                    .Select(s2 => int.TryParse(s2.Trim(), out int v) ? v : -1)
                    .Where(v => v >= 0).ToHashSet();
                if (repeatType == "weekly")
                    foreach (int v in vals) { if (v < 7) weekdayToggles[v].IsChecked = true; }
                else if (repeatType == "monthly")
                    foreach (int v in vals) { if (v >= 1 && v <= 31) monthdayToggles[v - 1].IsChecked = true; }
            };

            Action<string> loadMacroColor = (colorHex) =>
            {
                int matchIdx = 0;
                for (int ci = 0; ci < macroColors.Length; ci++)
                    if (macroColors[ci] == colorHex) { matchIdx = ci; break; }
                selectColorBtn(matchIdx);
            };

            // 적용 기간 + 버튼
            formStack.Children.Add(MakeFormLabel("적용 기간", topMargin: 10));

            Grid bottomRow = new Grid();
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel rangePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DatePicker dpFrom2 = MakeDatePicker(); dpFrom2.SelectedDate = DateTime.Today;
            DatePicker dpTo2 = MakeDatePicker();

            TextBlock rangeSep = new TextBlock
            {
                Text = " – ",
                Foreground = txtSecondary,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0)
            };
            rangePanel.Children.Add(WrapDatePicker(dpFrom2, ""));
            rangePanel.Children.Add(rangeSep);
            rangePanel.Children.Add(WrapDatePicker(dpTo2, "종료일"));
            Grid.SetColumn(rangePanel, 0);
            bottomRow.Children.Add(rangePanel);

            StackPanel btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            Button btnNew = new Button
            {
                Content = "초기화",
                Width = 76,
                Height = 36,
                Background = ModalPalette.Brush(Theme.L(Color.FromRgb(22, 25, 40))),
                Foreground = txtSecondary,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
                BorderThickness = new Thickness(1),
                BorderBrush = borderGlass
            };
            var tNew = new ControlTemplate(typeof(Button));
            var bNew = new FrameworkElementFactory(typeof(Border));
            bNew.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bNew.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            bNew.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            bNew.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            var cpNew = new FrameworkElementFactory(typeof(ContentPresenter));
            cpNew.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cpNew.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bNew.AppendChild(cpNew);
            tNew.VisualTree = bNew;
            var trNew = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            trNew.Setters.Add(new Setter(Border.BackgroundProperty,
                new SolidColorBrush(Theme.L(Color.FromArgb(25, 90, 200, 250)))));
            trNew.Setters.Add(new Setter(Border.BorderBrushProperty,
                new SolidColorBrush(Theme.L(Color.FromArgb(60, 90, 200, 250)))));
            tNew.Triggers.Add(trNew);
            btnNew.Template = tNew;

            Button btnSaveMacro = new Button
            {
                Content = "저장 및 적용",
                Width = 128,
                Height = 38,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            btnSaveMacro.Background = new LinearGradientBrush(
    new GradientStopCollection {
        new GradientStop(Theme.L(Color.FromRgb(35, 120, 215)), 0),
        new GradientStop(Theme.L(Color.FromRgb(75, 190, 248)), 1)
    }, 90);
            btnSaveMacro.Template = MakeRoundedButtonTemplate(12);

            btnPanel.Children.Add(btnNew);
            btnPanel.Children.Add(btnSaveMacro);
            Grid.SetColumn(btnPanel, 1);
            bottomRow.Children.Add(btnPanel);

            formStack.Children.Add(bottomRow);
            formCard.Child = formStack;
            Grid.SetRow(formCard, 3);
            rootGrid.Children.Add(formCard);

            // ══ 상태 ══
            int editingMacroId = 0;
            string editingOriginalText = "";
            Border _selectedRowBorder = null;

            Action clearForm = () =>
            {
                editingMacroId = 0;
                txtText.Text = "";
                cmbRepeat.SelectedIndex = 0;
                foreach (var t in weekdayToggles) t.IsChecked = false;
                foreach (var t in monthdayToggles) t.IsChecked = false;
                dpFrom2.SelectedDate = DateTime.Today;
                dpTo2.SelectedDate = null;
                if (_selectedRowBorder != null)
                {
                    _selectedRowBorder.BorderBrush = borderGlass;
                    _selectedRowBorder.Background = bgCard;
                    _selectedRowBorder = null;
                }
            };

            Action refreshList = null;
            refreshList = () =>
            {
                listPanel.Children.Clear();
                _selectedRowBorder = null;

                if (_macroList.Count == 0)
                {
                    Border emptyBox = new Border { Padding = new Thickness(0, 30, 0, 0) };
                    StackPanel ep = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    ep.Children.Add(new TextBlock
                    {
                        Text = "등록된 매크로 없음",
                        FontSize = 28,
                        Opacity = Theme.IsDark ? 0.1 : 0.22, // 흰 바탕에서는 0.1 이면 거의 안 보임
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = txtPrimary
                    });
                    ep.Children.Add(new TextBlock
                    {
                        Text = "아래 폼에서 반복 일정을 추가하세요",
                        FontSize = 12,
                        Foreground = txtMuted,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 8, 0, 0)
                    });
                    emptyBox.Child = ep;
                    listPanel.Children.Add(emptyBox);
                    return;
                }

                for (int idx = 0; idx < _macroList.Count; idx++)
                {
                    var macro = _macroList[idx];

                    // ── 적용 날짜 집계 ──
                    var appliedKeys = _memoData
                        .Where(kv => kv.Value.Any(m => m.IsMacro && m.MacroId == macro.MacroId))
                        .Select(kv => kv.Key)
                        .OrderBy(k => k)
                        .ToList();
                    int appliedCount = appliedKeys.Count;
                    string appliedRange = appliedCount == 0 ? "적용 없음"
                        : appliedKeys.First() == appliedKeys.Last() ? appliedKeys.First()
                        : $"{appliedKeys.First()} ~ {appliedKeys.Last()}";

                    Border rowBd = new Border
                    {
                        Background = bgCard,
                        CornerRadius = new CornerRadius(12),
                        BorderBrush = borderGlass,
                        BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand,
                        Padding = new Thickness(0),
                        Margin = new Thickness(0, 0, 0, 6)
                    };
                    rowBd.Effect = new DropShadowEffect
                    {
                        BlurRadius = 8,
                        ShadowDepth = 2,
                        Opacity = 0.18,
                        Color = Color.FromRgb(3, 5, 14)
                    };

                    Grid row = new Grid { Height = 56 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });

                    Color barClr = ModalPalette.Accent; // 기본값
                    if (!string.IsNullOrEmpty(macro.TextColor))
                    {
                        try { barClr = (Color)ColorConverter.ConvertFromString(macro.TextColor); }
                        catch { }
                    }


                    Border bar = new Border
                    {
                        Background = new SolidColorBrush(barClr),
                        CornerRadius = new CornerRadius(4, 0, 0, 4),
                        VerticalAlignment = VerticalAlignment.Stretch
                    };
                    Grid.SetColumn(bar, 0);
                    row.Children.Add(bar);

                    // ── 텍스트 + 서브라인(적용 일정) ──
                    StackPanel textStack = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(14, 0, 8, 0)
                    };
                    // 수정 — TextColor가 지정된 경우만 그 색, 기본이면 txtPrimary
                    Brush macroTextFg = string.IsNullOrEmpty(macro.TextColor)
                        ? (Brush)txtPrimary
                        : new SolidColorBrush(Theme.TextOn(barClr));

                    textStack.Children.Add(new TextBlock
                    {
                        Text = macro.Text,
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = macroTextFg,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });

                    // 서브라인: 날짜 범위 + 건수
                    StackPanel subLine = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    subLine.Children.Add(new TextBlock
                    {
                        Text = appliedRange,
                        FontSize = 10,
                        Foreground = appliedCount > 0
                            ? new SolidColorBrush(Theme.IsDark ? Color.FromArgb(180, barClr.R, barClr.G, barClr.B) : Theme.TextOn(barClr))
                            : txtMuted
                    });
                    if (appliedCount > 0)
                        subLine.Children.Add(new TextBlock
                        {
                            Text = $"  ({appliedCount}건)",
                            FontSize = 10,
                            Foreground = txtMuted
                        });
                    textStack.Children.Add(subLine);

                    Grid.SetColumn(textStack, 1);
                    row.Children.Add(textStack);

                    // 반복 타입 pill
                    string repeatLabel = macro.RepeatType switch
                    {
                        "daily" => "매일",
                        "weekdays" => "평일",
                        "weekly" => "매주",
                        "monthly" => "매월",
                        _ => macro.RepeatType
                    };
                    string repeatSub = "";
                    if (macro.RepeatType == "weekly" && !string.IsNullOrEmpty(macro.RepeatValue))
                        repeatSub = " " + string.Join(" ", macro.RepeatValue
                            .Split(',')
                            .Select(s2 => int.TryParse(s2.Trim(), out int v) && v >= 0 && v < 7 ? dayNames[v] : ""));
                    else if (macro.RepeatType == "monthly" && !string.IsNullOrEmpty(macro.RepeatValue))
                        repeatSub = " " + macro.RepeatValue + "일";

                    Border pill = new Border
                    {
                        Background = new SolidColorBrush(Theme.L(Color.FromArgb(22, barClr.R, barClr.G, barClr.B))), // ← 배경 반투명
                        CornerRadius = new CornerRadius(20),
                        Padding = new Thickness(11, 5, 11, 5),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    pill.Child = new TextBlock
                    {
                        Text = repeatLabel + repeatSub,
                        Foreground = new SolidColorBrush(Theme.TextOn(barClr)),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold
                    };
                    Grid.SetColumn(pill, 2);
                    row.Children.Add(pill);

                    // 삭제 버튼
                    Button btnDel = new Button
                    {
                        Content = "✕",
                        Width = 24,
                        Height = 24,
                        Background = Brushes.Transparent,
                        Foreground = txtMuted,
                        BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand,
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    btnDel.Template = CreateCloseBtnTemplate();

                    int macroIdCapture = macro.MacroId;
                    string macroTextCap = macro.Text;
                    btnDel.Click += (s2, e2) =>
                    {
                        var result = MessageBox.Show(
                            $"매크로 삭제와 함께\n관련 일정도 모두 삭제할까요?\n\n\"{macroTextCap}\"",
                            "매크로 삭제", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result != MessageBoxResult.Yes) { e2.Handled = true; return; }
                        foreach (var key in _memoData.Keys.ToList())
                        {
                            _memoData[key].RemoveAll(m => m.IsMacro && m.MacroId == macroIdCapture);
                            if (_memoData[key].Count == 0) _memoData.Remove(key);
                        }
                        SaveData(); RefreshCalendar();
                        DeleteMacro(macroIdCapture);
                        _macroList.RemoveAll(m => m.MacroId == macroIdCapture);
                        if (editingMacroId == macroIdCapture) clearForm();
                        refreshList();
                        e2.Handled = true;
                    };
                    Grid.SetColumn(btnDel, 3);
                    row.Children.Add(btnDel);

                    rowBd.Child = row;

                    rowBd.MouseEnter += (s2, e2) =>
                    {
                        if (rowBd != _selectedRowBorder)
                            rowBd.Background = new SolidColorBrush(Theme.L(Color.FromArgb(16, 90, 200, 250)));
                        rowBd.BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(35, 90, 200, 250)));
                    };
                    rowBd.MouseLeave += (s2, e2) =>
                    {
                        if (rowBd != _selectedRowBorder)
                        {
                            rowBd.Background = bgCard;
                            rowBd.BorderBrush = borderGlass;
                        }
                    };

                    var capturedMacro = macro;
                    var capturedBorder = rowBd;
                   

                    rowBd.MouseLeftButtonDown += (s2, e2) =>
                    {
                        if (_selectedRowBorder != null)
                        {
                            _selectedRowBorder.BorderBrush = borderGlass;
                            _selectedRowBorder.Background = bgCard;
                        }
                        _selectedRowBorder = capturedBorder;
                        capturedBorder.BorderBrush = accentBr;
                        capturedBorder.Background = selectedBg;

                        editingMacroId = capturedMacro.MacroId;
                        editingOriginalText = capturedMacro.Text;

                        txtText.Text = capturedMacro.Text;
                        for (int ci = 0; ci < cmbRepeat.Items.Count; ci++)
                            if (cmbRepeat.Items[ci] is ComboBoxItem cbi &&
                                (string)cbi.Tag == capturedMacro.RepeatType)
                            { cmbRepeat.SelectedIndex = ci; break; }
                        loadRepeatToggles(capturedMacro.RepeatType, capturedMacro.RepeatValue);
                        loadMacroColor(capturedMacro.TextColor);

                        // ── 적용 기간 복원 ──
                        if (capturedMacro.StartDate.HasValue)
                        {
                            dpFrom2.SelectedDate = capturedMacro.StartDate.Value;
                            dpFrom2.DisplayDate = capturedMacro.StartDate.Value;
                        }
                        else
                        {
                            dpFrom2.SelectedDate = DateTime.Today;
                            dpFrom2.DisplayDate = DateTime.Today;
                        }

                        if (capturedMacro.EndDate.HasValue)
                        {
                            dpTo2.SelectedDate = capturedMacro.EndDate.Value;
                            dpTo2.DisplayDate = capturedMacro.EndDate.Value;
                        }
                        else
                        {
                            dpTo2.SelectedDate = null;
                        }
                    };
                 

                    listPanel.Children.Add(rowBd);
                }
            };
            refreshList();

            btnNew.Click += (s, e) => clearForm();

            btnSaveMacro.Click += (s, e) =>
            {
                string text = txtText.Text.Trim();
                if (string.IsNullOrEmpty(text))
                { MessageBox.Show("메모 내용을 입력해 주세요.", "입력 오류"); return; }

                string repeatType = (cmbRepeat.SelectedItem as ComboBoxItem)?.Tag as string ?? "daily";
                string repeatValue = getRepeatValue();
                if (repeatType == "weekly" && string.IsNullOrEmpty(repeatValue))
                { MessageBox.Show("요일을 하나 이상 선택해 주세요.", "입력 오류"); return; }
                if (repeatType == "monthly" && string.IsNullOrEmpty(repeatValue))
                { MessageBox.Show("날짜를 하나 이상 선택해 주세요.", "입력 오류"); return; }
                if (!dpFrom2.SelectedDate.HasValue || !dpTo2.SelectedDate.HasValue)
                { MessageBox.Show("적용 기간을 설정해 주세요.", "입력 오류"); return; }
                if (dpFrom2.SelectedDate.Value > dpTo2.SelectedDate.Value)
                { MessageBox.Show("시작일이 종료일보다 늦습니다.", "입력 오류"); return; }

                bool monthEndFallback = false;
                if (repeatType == "monthly")
                {
                    var selDays = repeatValue.Split(',')
                        .Select(sv => int.TryParse(sv.Trim(), out int v) ? v : -1)
                        .Where(v => v >= 1).ToList();
                    if (selDays.Any(d => d == 31))
                    {
                        var res = MessageBox.Show(
                            "31일이 없는 달에도 해당 월의 마지막 날에 적용할까요?",
                            "월말 처리", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                        if (res == MessageBoxResult.Cancel) return;
                        monthEndFallback = (res == MessageBoxResult.Yes);
                    }
                }

                DateTime savedFrom = dpFrom2.SelectedDate.Value;
                DateTime savedTo = dpTo2.SelectedDate.Value;
                string autoName = text.Length > 30 ? text[..30] + "…" : text;

                // ── 편집 모드이고 텍스트가 바뀐 경우에만 1차 다이얼로그 표시 ──
                if (editingMacroId > 0)
                {
                    var currentOriginal = _macroList.FirstOrDefault(m => m.MacroId == editingMacroId)?.Text ?? editingOriginalText;

                    if (text != currentOriginal)
                    {
                        MessageBoxResult? choice = null;

                        Window dlgWin = new Window
                        { FontFamily = AppFont,
                            Width = 500,
                            Height = 300,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = macroWin,
                            ShowInTaskbar = false,
                            WindowStyle = WindowStyle.None,
                            AllowsTransparency = true,
                            Background = Brushes.Transparent,
                            ResizeMode = ResizeMode.NoResize
                        };

                        Border dlgOuter = new Border
                        {
                            CornerRadius = new CornerRadius(18),
                            BorderThickness = new Thickness(1),
                            Background = new LinearGradientBrush(
                                new GradientStopCollection {
                        new GradientStop(Theme.L(Color.FromRgb(13, 16, 30)), 0),
                        new GradientStop(Theme.L(Color.FromRgb(9, 11, 22)), 1)
                                }, 90),
                            BorderBrush = new LinearGradientBrush(
                                new GradientStopCollection {
                        new GradientStop(Theme.L(Color.FromArgb(80, 90, 200, 250)), 0),
                        new GradientStop(Theme.L(Color.FromArgb(15, 255, 255, 255)), 0.5),
                        new GradientStop(Theme.L(Color.FromArgb(60, 90, 200, 250)), 1)
                                }, 45)
                        };
                        dlgOuter.Effect = new DropShadowEffect { BlurRadius = 60, Opacity = 0.9, ShadowDepth = 0, Color = Color.FromRgb(1, 2, 8) };

                        Grid dlgRoot = new Grid();
                        dlgRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });
                        dlgRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                        dlgRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });

                        Border dlgHeader = new Border
                        {
                            CornerRadius = new CornerRadius(18, 18, 0, 0),
                            BorderThickness = new Thickness(0, 0, 0, 1),
                            Padding = new Thickness(22, 0, 16, 0),
                            Background = new SolidColorBrush(Theme.L(Color.FromRgb(14, 17, 32))),
                            BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(25, 90, 200, 250)))
                        };
                        dlgHeader.MouseLeftButtonDown += (_, __) => dlgWin.DragMove();

                        Grid dlgHdrGrid = new Grid();
                        dlgHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        dlgHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        dlgHdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        Border dlgIconBox = new Border
                        {
                            Width = 36,
                            Height = 36,
                            CornerRadius = new CornerRadius(10),
                            Margin = new Thickness(0, 0, 12, 0),
                            Background = new SolidColorBrush(Theme.L(Color.FromArgb(40, 90, 200, 250))),
                            BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(90, 90, 200, 250))),
                            BorderThickness = new Thickness(1),
                            Child = new TextBlock
                            {
                                Text = "⎇",
                                FontSize = 17,
                                Foreground = ModalPalette.Brush(ModalPalette.Accent),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        };
                        Grid.SetColumn(dlgIconBox, 0);
                        dlgHdrGrid.Children.Add(dlgIconBox);

                        StackPanel dlgTitleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                        dlgTitleStack.Children.Add(new TextBlock { Text = "저장 방식 선택", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = ModalPalette.Brush(ModalPalette.TxtPrimary) });
                        dlgTitleStack.Children.Add(new TextBlock { Text = "메모 내용이 변경되었습니다", FontSize = 11, Foreground = ModalPalette.Brush(ModalPalette.TxtSecondary) });
                        Grid.SetColumn(dlgTitleStack, 1);
                        dlgHdrGrid.Children.Add(dlgTitleStack);

                        Button dlgBtnX = MakeCloseButton();
                        dlgBtnX.Click += (_, __) => { choice = MessageBoxResult.Cancel; dlgWin.Close(); };
                        Grid.SetColumn(dlgBtnX, 2);
                        dlgHdrGrid.Children.Add(dlgBtnX);

                        dlgHeader.Child = dlgHdrGrid;
                        Grid.SetRow(dlgHeader, 0);
                        dlgRoot.Children.Add(dlgHeader);

                        Grid dlgContent = new Grid { Margin = new Thickness(22, 16, 22, 12) };
                        dlgContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        dlgContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
                        dlgContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        Border dlgOldBox = new Border
                        {
                            Background = new SolidColorBrush(Theme.L(Color.FromRgb(20, 23, 38))),
                            CornerRadius = new CornerRadius(10),
                            BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(35, 255, 255, 255))),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(12, 10, 12, 10)
                        };
                        StackPanel dlgOldStack = new StackPanel();
                        dlgOldStack.Children.Add(new TextBlock { Text = "기존", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = ModalPalette.Brush(ModalPalette.TxtMuted), Margin = new Thickness(0, 0, 0, 4) });
                        dlgOldStack.Children.Add(new TextBlock { Text = currentOriginal, FontSize = 13, Foreground = ModalPalette.Brush(ModalPalette.TxtSecondary), TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 48 });
                        dlgOldBox.Child = dlgOldStack;
                        Grid.SetColumn(dlgOldBox, 0);
                        dlgContent.Children.Add(dlgOldBox);

                        TextBlock dlgArrow = new TextBlock { Text = "→", FontSize = 18, Foreground = ModalPalette.Brush(ModalPalette.Accent), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                        Grid.SetColumn(dlgArrow, 1);
                        dlgContent.Children.Add(dlgArrow);

                        Border dlgNewBox = new Border
                        {
                            Background = new SolidColorBrush(Theme.L(Color.FromArgb(25, 90, 200, 250))),
                            CornerRadius = new CornerRadius(10),
                            BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(80, 90, 200, 250))),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(12, 10, 12, 10)
                        };
                        StackPanel dlgNewStack = new StackPanel();
                        dlgNewStack.Children.Add(new TextBlock { Text = "변경", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = ModalPalette.Brush(ModalPalette.Accent), Margin = new Thickness(0, 0, 0, 4) });
                        dlgNewStack.Children.Add(new TextBlock { Text = text, FontSize = 13, Foreground = ModalPalette.Brush(ModalPalette.TxtPrimary), TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 48 });
                        dlgNewBox.Child = dlgNewStack;
                        Grid.SetColumn(dlgNewBox, 2);
                        dlgContent.Children.Add(dlgNewBox);

                        Grid.SetRow(dlgContent, 1);
                        dlgRoot.Children.Add(dlgContent);

                        Border dlgFooter = new Border
                        {
                            CornerRadius = new CornerRadius(0, 0, 18, 18),
                            BorderThickness = new Thickness(0, 1, 0, 0),
                            Padding = new Thickness(22, 0, 22, 0),
                            Background = new SolidColorBrush(Theme.L(Color.FromRgb(11, 13, 24))),
                            BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(20, 90, 200, 250)))
                        };
                        StackPanel dlgBtnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

                        Button dlgBtnCancel = new Button { Content = "취소", Width = 68, Height = 36, Margin = new Thickness(0, 0, 8, 0), Foreground = ModalPalette.Brush(ModalPalette.TxtMuted), FontSize = 12, Cursor = Cursors.Hand, BorderThickness = new Thickness(1), BorderBrush = ModalPalette.Brush(ModalPalette.BorderGlass), Background = Brushes.Transparent };
                        dlgBtnCancel.Template = MakeRoundedButtonTemplate(9);
                        dlgBtnCancel.Click += (_, __) => { choice = MessageBoxResult.Cancel; dlgWin.Close(); };

                        Button dlgBtnEdit = new Button { Content = "수정", Width = 80, Height = 36, Margin = new Thickness(0, 0, 8, 0), Foreground = ModalPalette.Brush(ModalPalette.Accent), FontSize = 12, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, BorderThickness = new Thickness(1), BorderBrush = ModalPalette.Brush(ModalPalette.BorderAccent), Background = new SolidColorBrush(Theme.L(Color.FromArgb(20, 90, 200, 250))) };
                        dlgBtnEdit.Template = MakeRoundedButtonTemplate(9);
                        dlgBtnEdit.Click += (_, __) => { choice = MessageBoxResult.No; dlgWin.Close(); };

                        Button dlgBtnNew = new Button { Content = "신규 등록", Width = 100, Height = 36, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand, BorderThickness = new Thickness(0) };
                        dlgBtnNew.Background = new LinearGradientBrush(new GradientStopCollection { new GradientStop(Theme.L(Color.FromRgb(35, 120, 215)), 0), new GradientStop(Theme.L(Color.FromRgb(75, 190, 248)), 1) }, 90);
                        dlgBtnNew.Template = MakeRoundedButtonTemplate(9);
                        dlgBtnNew.Click += (_, __) => { choice = MessageBoxResult.Yes; dlgWin.Close(); };

                        dlgBtnRow.Children.Add(dlgBtnCancel);
                        dlgBtnRow.Children.Add(dlgBtnEdit);
                        dlgBtnRow.Children.Add(dlgBtnNew);
                        dlgFooter.Child = dlgBtnRow;
                        Grid.SetRow(dlgFooter, 2);
                        dlgRoot.Children.Add(dlgFooter);

                        dlgOuter.Child = dlgRoot;
                        ApplyModalEdge(dlgOuter); dlgWin.Content = dlgOuter;
                        dlgWin.PreviewKeyDown += (_, e2) =>
                        {
                            if (e2.Key == Key.Escape) { choice = MessageBoxResult.Cancel; dlgWin.Close(); e2.Handled = true; }
                            if (e2.Key == Key.Enter) { choice = MessageBoxResult.Yes; dlgWin.Close(); e2.Handled = true; }
                        };
                        dlgWin.ShowDialog();

                        if (choice == null || choice == MessageBoxResult.Cancel) return;
                        if (choice == MessageBoxResult.Yes) editingMacroId = 0; // 신규 등록
                                                                                // choice == No(수정)이면 editingMacroId 그대로 유지
                    }
                    // 텍스트 동일 → 다이얼로그 없이 수정으로 바로 진행
                }

                // ── 모든 확인 완료 후 단 한 번만 저장 ──
                int finalEditingId = editingMacroId;

                MacroSchedule macro = new MacroSchedule();
                if (finalEditingId > 0)
                {
                    var existing = _macroList.FirstOrDefault(m => m.MacroId == finalEditingId);
                    macro.MacroId = existing?.MacroId ?? 0;
                }

                macro.Name = autoName;
                macro.Text = text;
                macro.RepeatType = repeatType;
                macro.RepeatValue = repeatValue;
                macro.MonthEndFallback = monthEndFallback;
                macro.TextColor = selectedMacroColor;
                macro.StartDate = savedFrom;
                macro.EndDate = savedTo;

                // ── 편집 시: 기존 적용 메모 제거 전 2차 확인 ──
                if (finalEditingId > 0)
                {
                    int existingCount = _memoData
                        .Sum(kv => kv.Value.Count(m => m.IsMacro && m.MacroId == macro.MacroId));

                    if (existingCount > 0)
                    {
                        var confirm = MessageBox.Show(
                            $"기존 적용된 일정 {existingCount}건을 모두 제거하고\n새 기간으로 재적용할까요?\n\n" +
                            $"새 기간: {savedFrom:yyyy-MM-dd} ~ {savedTo:yyyy-MM-dd}",
                            "기존 일정 제거 확인",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (confirm != MessageBoxResult.Yes) return;
                    }
                }

                // ── DB 저장 (단 한 번) ──
                SaveMacro(macro);

                // ── 메모리 목록 갱신 (단 한 번) ──
                if (finalEditingId == 0)
                    _macroList.Add(macro);
                else
                {
                    var idx = _macroList.FindIndex(m => m.MacroId == macro.MacroId);
                    if (idx >= 0) _macroList[idx] = macro;
                }

                // ── 기존 메모 제거 후 재적용 ──
                foreach (var key in _memoData.Keys.ToList())
                {
                    _memoData[key].RemoveAll(m => m.IsMacro && m.MacroId == macro.MacroId);
                    if (_memoData[key].Count == 0) _memoData.Remove(key);
                }
                SaveData();

                int count = ApplyMacroToRange(macro, savedFrom, savedTo);

                clearForm();
                refreshList();
                MessageBox.Show(
                    $"저장 완료!  적용 {count}건\n{savedFrom:yyyy-MM-dd} ~ {savedTo:yyyy-MM-dd}",
                    "저장 및 적용", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            macroWin.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Escape) return;
                // 날짜 선택 달력·반복 설정 목록이 열려 있으면 그것만 닫음 (작성 중인 내용이 사라지지 않게)
                if (dpFrom2.IsDropDownOpen || dpTo2.IsDropDownOpen || cmbRepeat.IsDropDownOpen) { dpFrom2.IsDropDownOpen = false; dpTo2.IsDropDownOpen = false; cmbRepeat.IsDropDownOpen = false; }
                else macroWin.Close();
                e.Handled = true;
            };
            outerBorder.Child = rootGrid;
            ApplyModalEdge(outerBorder); macroWin.Content = outerBorder;
            macroWin.ShowDialog();
        }

        private static ControlTemplate CreateComboBoxTemplate()
        {
            string xaml = @"
    <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                     xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                     TargetType='ComboBox'>
        <Grid>
            <Border x:Name='PART_CmbBorder'
                    Background='#0C0E1A'
                    BorderBrush='#225AC8FA'
                    BorderThickness='1'
                    CornerRadius='10'>
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='*'/>
                        <ColumnDefinition Width='36'/>
                    </Grid.ColumnDefinitions>
                    <ContentPresenter x:Name='ContentSite'
                        IsHitTestVisible='False'
                        Content='{TemplateBinding SelectionBoxItem}'
                        ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'
                        ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}'
                        Margin='14,0,0,0'
                        VerticalAlignment='Center'
                        HorizontalAlignment='Left'/>
                    <Border Grid.Column='1'
                            Background='#115AC8FA'
                            CornerRadius='0,9,9,0'
                            BorderBrush='#225AC8FA'
                            BorderThickness='1,0,0,0'>
                        <TextBlock Text='⌄'
                                   FontSize='14'
                                   Foreground='#5AC8FA'
                                   HorizontalAlignment='Center'
                                   VerticalAlignment='Center'
                                   Margin='0,2,0,0'/>
                    </Border>
                    <ToggleButton Grid.ColumnSpan='2'
                                  Focusable='False'
                                  IsChecked='{Binding IsDropDownOpen, Mode=TwoWay,
                                      RelativeSource={RelativeSource TemplatedParent}}'
                                  Background='Transparent'
                                  BorderThickness='0'
                                  Opacity='0'/>
                </Grid>
            </Border>
            <Popup x:Name='Popup'
                   Placement='Bottom'
                   IsOpen='{TemplateBinding IsDropDownOpen}'
                   AllowsTransparency='True'
                   Focusable='False'
                   PopupAnimation='Slide'>
                <Border x:Name='DropDownBorder'
                        MinWidth='{TemplateBinding ActualWidth}'
                        MaxHeight='{TemplateBinding MaxDropDownHeight}'
                        Background='#0A0C18'
                        BorderBrush='#335AC8FA'
                        BorderThickness='1'
                        CornerRadius='10'
                        Margin='0,4,0,0'>
                    <Border.Effect>
                        <DropShadowEffect BlurRadius='24' ShadowDepth='0' Opacity='0.6' Color='#000000'/>
                    </Border.Effect>
                    <ScrollViewer SnapsToDevicePixels='True' Padding='4'>
                        <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Contained'/>
                    </ScrollViewer>
                </Border>
            </Popup>
        </Grid>
        <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='PART_CmbBorder' Property='BorderBrush' Value='#5AC8FA'/>
                <Setter TargetName='PART_CmbBorder' Property='Effect'>
                    <Setter.Value>
                        <DropShadowEffect Color='#5AC8FA' BlurRadius='14' ShadowDepth='0' Opacity='0.22'/>
                    </Setter.Value>
                </Setter>
            </Trigger>
            <Trigger Property='IsDropDownOpen' Value='True'>
                <Setter TargetName='PART_CmbBorder' Property='BorderBrush' Value='#5AC8FA'/>
                <Setter TargetName='PART_CmbBorder' Property='CornerRadius' Value='10,10,0,0'/>
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(ThemeXaml(xaml));
        }

        // CreateComboBoxTemplate 의 목록 항목 (어두운 바탕, 올리면 강조색)
        // 문자열로 만든 템플릿의 색('#RRGGBB')도 테마에 맞게: 다크 기준 값을 Theme.L 로 바꿔 넣음
        // 대화 상자 바깥 테두리: 라이트에서는 흰 창이 흰 화면에 묻히므로 또렷한 회색 실선 + 옅은 검은 그림자 (다크는 그대로)
        private static void ApplyModalEdge(Border outer)
        {
            if (Theme.IsDark) return;
            outer.BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));
            outer.BorderThickness = new Thickness(1);
            if (outer.Effect is DropShadowEffect shadow)
            {
                shadow.Color = Colors.Black;
                shadow.Opacity = 0.22;
                shadow.BlurRadius = Math.Min(shadow.BlurRadius, 36);
                shadow.ShadowDepth = 6;
                shadow.Direction = 270;
            }
        }

        // 일정 칩 바탕: 색 있는 일정은 그 색을 옅게, 없는 일정은 은은한 유리(다크) / 옅은 파랑(라이트). 마우스를 올리면 조금 진하게
        private static Brush MemoChipBg(Color? color, bool hover)
        {
            if (color is Color c) return new SolidColorBrush(Color.FromArgb((byte)(hover ? 60 : 34), c.R, c.G, c.B));
            return Theme.IsDark
                ? new SolidColorBrush(Color.FromArgb((byte)(hover ? 32 : 16), 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb((byte)(hover ? 24 : 12), 0, 0, 0));
        }

        // 사용자가 고른 일정 색을 글자로 쓸 때: 라이트 테마에서는 같은 계열로 진하게 (색 견본 점은 원래 색 그대로)
        private static Brush UserTextBrush(string? hex, Brush fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            try { return new SolidColorBrush(Theme.TextOn((Color)ColorConverter.ConvertFromString(hex))); }
            catch { return fallback; }
        }

        private static string ThemeXaml(string xaml) => System.Text.RegularExpressions.Regex.Replace(xaml, "'#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})'", m =>
        {
            var c = (Color)ColorConverter.ConvertFromString("#" + m.Groups[1].Value);
            var t = Theme.L(c);
            return $"'#{t.A:X2}{t.R:X2}{t.G:X2}{t.B:X2}'";
        });

        private static Style CreateComboItemStyle()
        {
            Style style = new Style(typeof(ComboBoxItem));
            style.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, ModalPalette.Brush(ModalPalette.BgInput)));
            style.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, ModalPalette.Brush(ModalPalette.TxtPrimary)));
            style.Setters.Add(new Setter(ComboBoxItem.PaddingProperty, new Thickness(10, 6, 10, 6)));
            var hover = new Trigger { Property = ComboBoxItem.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, ModalPalette.Brush(ModalPalette.AccentDim)));
            hover.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, ModalPalette.Brush(ModalPalette.Accent)));
            style.Triggers.Add(hover);
            return style;
        }

        private static Button MakeCloseButton()
        {
            var btn = new Button
            {
                Content = "✕",
                Width = 36,
                Height = 36,
                Background = Brushes.Transparent,
                Foreground = ModalPalette.Brush(ModalPalette.TxtSecondary),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 14
            };
            var closeTemplate = new ControlTemplate(typeof(Button));
            var closeBorder = new FrameworkElementFactory(typeof(Border));
            closeBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            closeBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            var closeCp = new FrameworkElementFactory(typeof(ContentPresenter));
            closeCp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            closeCp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            closeBorder.AppendChild(closeCp);
            closeTemplate.VisualTree = closeBorder;
            var tr = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            tr.Setters.Add(new Setter(Button.BackgroundProperty, ModalPalette.Brush(ModalPalette.Danger)));
            tr.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            closeTemplate.Triggers.Add(tr);
            btn.Template = closeTemplate;
            return btn;
        }

        private static ControlTemplate MakeRoundedButtonTemplate(double radius = 8)
        {
            var t = new ControlTemplate(typeof(Button));
            var b = new FrameworkElementFactory(typeof(Border));
            b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            b.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            b.AppendChild(cp); t.VisualTree = b;

            var tr = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            tr.Setters.Add(new Setter(Border.BackgroundProperty,
    new LinearGradientBrush(
        new GradientStopCollection {
            new GradientStop(Theme.L(Color.FromRgb(55, 165, 245)), 0),
            new GradientStop(Theme.L(Color.FromRgb(95, 210, 255)), 1)
        }, 90)));
            t.Triggers.Add(tr);
            return t;
        }

        private static TextBlock MakeFormLabel(string text, double topMargin = 0)
            => new TextBlock
            {
                Text = text,
                Foreground = ModalPalette.Brush(ModalPalette.TxtSecondary),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(2, topMargin, 0, 4)
            };

        private static TextBox MakeFormInput(Brush bg, Brush fg, Brush borderBr, Brush focusBorder)
        {
            var tb = new TextBox
            {
                FontSize = 14,
                Background = bg,
                Foreground = fg,
                BorderBrush = borderBr,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 9, 12, 9)
            };
            tb.GotFocus += (s, e) =>
            {
                tb.BorderBrush = focusBorder;
            };
            tb.LostFocus += (s, e) => { tb.BorderBrush = borderBr; tb.Effect = null; };
            return tb;
        }

       

        // ── VisualTree 탐색 헬퍼 (클래스 내 아무 위치에 추가) ──
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result) return result;
                var sub = FindVisualChild<T>(child);
                if (sub != null) return sub;
            }
            return null;
        }

        private static void ApplyForegroundToTextBlocks(DependencyObject parent, Color color)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TextBlock tb && tb.Text.Length <= 2)
                    tb.Foreground = ModalPalette.Brush(color);
                ApplyForegroundToTextBlocks(child, color);
            }
        }

        private static Border MakeDateSep()
            => new Border
            {
                Width = 16,
                Height = 1,
                Background = ModalPalette.Brush(ModalPalette.TxtMuted),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0)
            };



        private static DatePicker MakeDatePicker()
        {
            var dp = new DatePicker
            {
                Width = 130,
                FontSize = 12,
                Background = ModalPalette.Brush(ModalPalette.BgInput),
                Foreground = ModalPalette.Brush(ModalPalette.TxtPrimary),
                BorderBrush = ModalPalette.Brush(ModalPalette.BorderInput),
                BorderThickness = new Thickness(1)
            };

            // ── 밝은 톤 캘린더 스타일 (두 테마 모두 흰 팝업이라 Theme.L 을 거치지 않음) ──
            var lightBg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            var lightCard = new SolidColorBrush(Color.FromRgb(245, 247, 252));
            var lightText = new SolidColorBrush(Color.FromRgb(30, 35, 55));
            var lightMuted = new SolidColorBrush(Color.FromRgb(140, 148, 175));
            var accentBrush = ModalPalette.Brush(ModalPalette.Accent);
            var accentDimLight = new SolidColorBrush(Color.FromArgb(40, 90, 200, 250));

            Style calStyle = new Style(typeof(System.Windows.Controls.Calendar));
            calStyle.Setters.Add(new Setter(System.Windows.Controls.Calendar.BackgroundProperty, lightBg));
            calStyle.Setters.Add(new Setter(System.Windows.Controls.Calendar.ForegroundProperty, lightText));
            calStyle.Setters.Add(new Setter(System.Windows.Controls.Calendar.BorderBrushProperty,
                new SolidColorBrush(Color.FromRgb(210, 218, 235))));
            calStyle.Setters.Add(new Setter(System.Windows.Controls.Calendar.BorderThicknessProperty,
                new Thickness(1)));

            // 날짜 버튼
            Style dayBtnStyle = new Style(typeof(CalendarDayButton));
            dayBtnStyle.Setters.Add(new Setter(CalendarDayButton.ForegroundProperty, lightText));
            dayBtnStyle.Setters.Add(new Setter(CalendarDayButton.BackgroundProperty, Brushes.Transparent));
            dayBtnStyle.Setters.Add(new Setter(CalendarDayButton.FontSizeProperty, 12.0));

            var dayHover = new Trigger { Property = CalendarDayButton.IsMouseOverProperty, Value = true };
            dayHover.Setters.Add(new Setter(CalendarDayButton.BackgroundProperty, accentDimLight));
            dayBtnStyle.Triggers.Add(dayHover);

            var daySelected = new Trigger { Property = CalendarDayButton.IsSelectedProperty, Value = true };
            daySelected.Setters.Add(new Setter(CalendarDayButton.BackgroundProperty, accentBrush));
            daySelected.Setters.Add(new Setter(CalendarDayButton.ForegroundProperty, Brushes.White));
            dayBtnStyle.Triggers.Add(daySelected);

            var dayToday = new Trigger { Property = CalendarDayButton.IsHighlightedProperty, Value = true };
            dayToday.Setters.Add(new Setter(CalendarDayButton.BackgroundProperty,
                new SolidColorBrush(Color.FromArgb(25, 90, 200, 250))));
            dayBtnStyle.Triggers.Add(dayToday);

            var dayInactive = new Trigger { Property = CalendarDayButton.IsInactiveProperty, Value = true };
            dayInactive.Setters.Add(new Setter(CalendarDayButton.ForegroundProperty, lightMuted));
            dayBtnStyle.Triggers.Add(dayInactive);

            // 월/년도 버튼
            Style calBtnStyle = new Style(typeof(CalendarButton));
            calBtnStyle.Setters.Add(new Setter(CalendarButton.ForegroundProperty, lightText));
            calBtnStyle.Setters.Add(new Setter(CalendarButton.FontSizeProperty, 12.0));
            var calBtnHover = new Trigger { Property = CalendarButton.IsMouseOverProperty, Value = true };
            calBtnHover.Setters.Add(new Setter(CalendarButton.BackgroundProperty, accentDimLight));
            calBtnStyle.Triggers.Add(calBtnHover);
            


            calStyle.Resources.Add(typeof(CalendarDayButton), dayBtnStyle);
            calStyle.Resources.Add(typeof(CalendarButton), calBtnStyle);
            dp.CalendarStyle = calStyle;

            dp.Loaded += (s, e) =>
            {
                if (dp.Template.FindName("PART_TextBox", dp) is DatePickerTextBox tb)
                {
                    tb.Background = ModalPalette.Brush(ModalPalette.BgInput);
                    tb.Foreground = ModalPalette.Brush(ModalPalette.TxtPrimary);
                    tb.CaretBrush = ModalPalette.Brush(ModalPalette.Accent);
                    tb.BorderThickness = new Thickness(0);
                    tb.ApplyTemplate();
                    if (tb.Template.FindName("PART_Watermark", tb) is ContentControl wm)
                        wm.Visibility = Visibility.Collapsed;
                }
            };

            dp.CalendarOpened += (s2, e2) =>
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                {
                    if (dp.Template.FindName("PART_Popup", dp) is Popup popup && popup.Child != null)
                    {
                        // ── 팝업 배경 밝게 ──
                        ApplyLightToVisualTree(popup.Child);

                        var calItem = FindVisualChild<System.Windows.Controls.Primitives.CalendarItem>(popup.Child);
                        if (calItem != null)
                        {
                            calItem.Background = lightBg;

                            // 내비게이션 버튼 색상
                            if (calItem.Template.FindName("PART_PreviousButton", calItem) is Button pb)
                            {
                                pb.Foreground = lightText;
                                pb.Background = Brushes.Transparent;
                            }
                            if (calItem.Template.FindName("PART_NextButton", calItem) is Button nb)
                            {
                                nb.Foreground = lightText;
                                nb.Background = Brushes.Transparent;
                            }
                            if (calItem.Template.FindName("PART_HeaderButton", calItem) is Button hb)
                            {
                                hb.Foreground = lightText;
                                hb.FontWeight = FontWeights.Bold;
                            }

                            // 요일 헤더 텍스트
                            ApplyForegroundToTextBlocks(calItem, Theme.L(Color.FromRgb(100, 110, 150)));
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };

            dp.GotFocus += (s, e) =>
            {
                dp.BorderBrush = ModalPalette.Brush(ModalPalette.Accent);
            };
            dp.LostFocus += (s, e) =>
            {
                dp.BorderBrush = ModalPalette.Brush(ModalPalette.BorderInput);
                dp.Effect = null;
            };
            return dp;
        }

        private static void ApplyLightToVisualTree(DependencyObject obj)
        {
            var lightBg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            var lightBorder = new SolidColorBrush(Color.FromRgb(210, 218, 235));

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);

                if (child is Border bd)
                {
                    // 어두운 배경을 흰색으로 교체
                    if (bd.Background is SolidColorBrush bg &&
                        (bg.Color.R < 50 && bg.Color.G < 50 && bg.Color.B < 60))
                        bd.Background = lightBg;

                    if (bd.BorderBrush is SolidColorBrush bb &&
                        (bb.Color.R < 80 && bb.Color.G < 80 && bb.Color.B < 100))
                        bd.BorderBrush = lightBorder;
                }

                ApplyLightToVisualTree(child);
            }
        }

        private static Grid WrapDatePicker(DatePicker dp, string placeholder)
        {
            // 외부 Grid (DatePicker + 플레이스홀더 오버레이)
            Grid wrapper = new Grid { Width = 155, Height = 36 };

            dp.Width = 155;
            dp.Height = 36;
            dp.BorderThickness = new Thickness(1);
            dp.BorderBrush = ModalPalette.Brush(ModalPalette.BorderInput);
            dp.Background = ModalPalette.Brush(ModalPalette.BgInput);
            dp.Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(220, 226, 248)));
            dp.Cursor = Cursors.Hand;
            dp.Padding = new Thickness(0);
            dp.FontSize = 12;
            dp.VerticalAlignment = VerticalAlignment.Center;
            dp.VerticalContentAlignment = VerticalAlignment.Center;

            // ── 플레이스홀더 TextBlock (DatePicker 위에 겹치기) ──
            TextBlock placeholder_tb = new TextBlock
            {
                Text = placeholder,
                Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(160, 168, 200))),
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(10, 0, 32, 0),  // 오른쪽은 달력 버튼 공간
                IsHitTestVisible = false                          // 클릭 통과
            };

            // 날짜 선택 시 플레이스홀더 숨김
            dp.SelectedDateChanged += (s, e) =>
            {
                placeholder_tb.Visibility = dp.SelectedDate.HasValue
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            dp.Loaded += (s, e) =>
            {
                // ── 기존 코드 유지 ──
                if (dp.Template.FindName("PART_TextBox", dp) is DatePickerTextBox tb)
                {
                    tb.Background = Brushes.Transparent;
                    tb.Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(220, 226, 248)));
                    tb.CaretBrush = ModalPalette.Brush(ModalPalette.Accent);
                    tb.BorderThickness = new Thickness(0);
                    tb.FontSize = 12;
                    tb.VerticalAlignment = VerticalAlignment.Center;
                    tb.VerticalContentAlignment = VerticalAlignment.Center;
                    tb.Padding = new Thickness(8, 0, 0, 0);
                    tb.Height = double.NaN; // Auto — 고정 높이 해제
                    tb.Margin = new Thickness(0, 0, 30, 0); // 달력 버튼 공간 확보

                    tb.ApplyTemplate();
                    if (tb.Template.FindName("PART_Watermark", tb) is ContentControl wm)
                        wm.Visibility = Visibility.Collapsed;

                    // 내부 PART_ContentHost도 수직 중앙 정렬
                    if (tb.Template.FindName("PART_ContentHost", tb) is ScrollViewer sv)
                    {
                        sv.VerticalAlignment = VerticalAlignment.Center;
                        sv.VerticalContentAlignment = VerticalAlignment.Center;
                    }
                }

                if (dp.Template.FindName("PART_Button", dp) is Button btn)
                {
                    btn.Width = 26;
                    btn.Height = 26;
                    btn.Background = Brushes.Transparent;
                    btn.Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(160, 168, 200)));
                    btn.BorderThickness = new Thickness(0);
                }

                // ── 추가: 내부 흰색 테두리 제거 ──
                RemoveDatePickerInnerBorder(dp);
            };

            dp.MouseEnter += (s, e) =>
            {
                dp.BorderBrush = ModalPalette.Brush(ModalPalette.Accent);
            };
            dp.MouseLeave += (s, e) =>
            {
                dp.BorderBrush = ModalPalette.Brush(ModalPalette.BorderInput);
                dp.Effect = null;
            };

            wrapper.Children.Add(dp);
            wrapper.Children.Add(placeholder_tb);  // DatePicker 위에 오버레이
            return wrapper;
        }

        private static void RemoveDatePickerInnerBorder(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is Border bd)
                {
                    // 흰색 or 밝은 계열 테두리 → 제거
                    if (bd.BorderBrush is SolidColorBrush scb)
                    {
                        var c = scb.Color;
                        if (c.R > 200 && c.G > 200 && c.B > 200)
                        {
                            bd.BorderBrush = Brushes.Transparent;
                            bd.BorderThickness = new Thickness(0);
                        }
                    }

                    // 흰색 배경도 투명으로
                    if (bd.Background is SolidColorBrush bgb)
                    {
                        var c = bgb.Color;
                        if (c.R > 240 && c.G > 240 && c.B > 240 && c.A > 200)
                            bd.Background = Brushes.Transparent;
                    }
                }

                RemoveDatePickerInnerBorder(child);
            }
        }

        // ── 헬퍼 (클래스 내 아무 위치) ──
        private static void ApplyDarkToVisualTree(DependencyObject obj)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is Border bd && bd.Background is SolidColorBrush scb && scb.Color == Colors.White)
                    bd.Background = ModalPalette.Brush(ModalPalette.BgCard);
                ApplyDarkToVisualTree(child);
            }
        }

  
        private static ToggleButton MakePillToggle(string text, bool defaultChecked = false)
        {
            var tb = new ToggleButton
            {
                Content = text,
                IsChecked = defaultChecked,
                Height = 30,
                Margin = new Thickness(12, 0, 0, 0),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(14, 0, 14, 0),
                BorderThickness = new Thickness(1),
                BorderBrush = ModalPalette.Brush(ModalPalette.BorderAccent)
            };
            tb.Background = defaultChecked
                ? ModalPalette.Brush(ModalPalette.AccentDim)
                : ModalPalette.Brush(ModalPalette.BgRow);
            tb.Foreground = defaultChecked
                ? ModalPalette.Brush(ModalPalette.Accent)
                : ModalPalette.Brush(ModalPalette.TxtSecondary);
            tb.Checked += (s, e) =>
            {
                tb.Background = ModalPalette.Brush(ModalPalette.AccentDim);
                tb.Foreground = ModalPalette.Brush(ModalPalette.Accent);
            };
            tb.Unchecked += (s, e) =>
            {
                tb.Background = ModalPalette.Brush(ModalPalette.BgRow);
                tb.Foreground = ModalPalette.Brush(ModalPalette.TxtSecondary);
            };
            return tb;
        }

        private static ToggleButton MakeDayToggle(
            string label, Brush offBg, Brush offFg, Brush onBg,
            double w = 44, double h = 32, double fontSize = 13)
        {
            var t = new ToggleButton
            {
                Content = label,
                Width = w,
                Height = h,
                Margin = new Thickness(0, 0, 5, 4),
                Cursor = Cursors.Hand,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Background = offBg,
                Foreground = offFg,
                BorderThickness = new Thickness(0)
            };
            // 기본 템플릿은 켜짐 상태 배경을 시스템 하늘색으로 덮어써 지정한 색·글자 대비가 무시되므로 직접 그림
            var tpl = new ControlTemplate(typeof(ToggleButton));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            tpl.VisualTree = bd;
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.85));
            tpl.Triggers.Add(hover);
            t.Template = tpl;
            // 켜진 배경이 밝으면(하늘색) 어두운 글자, 어두우면 흰 글자
            Brush onFg = onBg is SolidColorBrush sb && (0.299 * sb.Color.R + 0.587 * sb.Color.G + 0.114 * sb.Color.B) > 150
                ? new SolidColorBrush(Theme.L(Color.FromRgb(10, 12, 22))) : Brushes.White;
            t.Checked += (s, e) => { t.Background = onBg; t.Foreground = onFg; };
            t.Unchecked += (s, e) => { t.Background = offBg; t.Foreground = offFg; };
            return t;
        }


        // ──────────────────────────────────────────
        // 메모 입력창
        // ──────────────────────────────────────────
        private ControlTemplate CreateHueWheelButtonTemplate()
        {
            string xaml = @"
            <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>
                <Viewbox>
                    <Grid Width='100' Height='100'>
                        <Ellipse Fill='#FF2D2D30' Width='100' Height='100'/>
                        <Canvas Width='100' Height='100'>
                            <Ellipse Fill='#FFFF0000' Width='18' Height='18' Canvas.Left='41' Canvas.Top='2'/>
                            <Ellipse Fill='#FFFF8000' Width='18' Height='18' Canvas.Left='69' Canvas.Top='10'/>
                            <Ellipse Fill='#FFFFBF00' Width='18' Height='18' Canvas.Left='88' Canvas.Top='31'/>
                            <Ellipse Fill='#FFFFFF00' Width='18' Height='18' Canvas.Left='88' Canvas.Top='59'/>
                            <Ellipse Fill='#FF80FF00' Width='18' Height='18' Canvas.Left='69' Canvas.Top='80'/>
                            <Ellipse Fill='#FF00FF00' Width='18' Height='18' Canvas.Left='41' Canvas.Top='88'/>
                            <Ellipse Fill='#FF00FF80' Width='18' Height='18' Canvas.Left='13' Canvas.Top='80'/>
                            <Ellipse Fill='#FF00FFFF' Width='18' Height='18' Canvas.Left='-6' Canvas.Top='59'/>
                            <Ellipse Fill='#FF0080FF' Width='18' Height='18' Canvas.Left='-6' Canvas.Top='31'/>
                            <Ellipse Fill='#FF0000FF' Width='18' Height='18' Canvas.Left='13' Canvas.Top='10'/>
                            <Ellipse Fill='#FF8000FF' Width='18' Height='18' Canvas.Left='22' Canvas.Top='-6'/>
                            <Ellipse Fill='#FFFF00FF' Width='18' Height='18' Canvas.Left='60' Canvas.Top='-6'/>
                        </Canvas>
                        <TextBlock Text='Hue' Foreground='White' FontSize='20' FontWeight='Bold' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                    </Grid>
                </Viewbox>
                <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.8'/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>";
            // 색상환 칸은 실제 색이라 그대로, 뒤의 원판만 테마에 맞게 (라이트: 흰 원판)
            return (ControlTemplate)XamlReader.Parse(Theme.IsDark ? xaml : xaml.Replace("Fill='#FF2D2D30'", "Fill='#FFFFFFFF' Stroke='#FFD0D6E0' StrokeThickness='1'"));
        }

        private void OpenInputBox(string dateKey)
        {
            _memoData.TryGetValue(dateKey, out List<MemoItem> items);
            if (items == null) items = new List<MemoItem>();

            Window inputWin = new Window { FontFamily = AppFont, Title = $"{dateKey} 메모", Width = 900, Height = 700, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ShowInTaskbar = false, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize };
            inputWin.Resources["ScrollThumbBrush"] = this.Resources["ScrollThumbBrush"];
            inputWin.Resources["ScrollThumbHoverBrush"] = this.Resources["ScrollThumbHoverBrush"];
            inputWin.Resources.MergedDictionaries.Add(this.Resources);

            // ── 디자인 개선: 입력창 배경/텍스트 색상 ──
            Brush bgBrush = new SolidColorBrush(Theme.L(Color.FromRgb(22, 25, 38)));
            Brush textBrush = new SolidColorBrush(Theme.L(Color.FromRgb(224, 228, 244)));
            Brush inputBg = new SolidColorBrush(Theme.L(Color.FromRgb(18, 20, 30)));

            Border border = new Border { Background = bgBrush, CornerRadius = new CornerRadius(14), BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(25, 255, 255, 255))), BorderThickness = new Thickness(1), Padding = new Thickness(20) };
            border.Effect = new DropShadowEffect { BlurRadius = 30, Opacity = 0.55, ShadowDepth = 6, Color = Colors.Black };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });

            Brush titleColor = textBrush;
            string dayString = "";
            if (DateTime.TryParse(dateKey, out DateTime selectedDate))
            {
                dayString = selectedDate.ToString(" (ddd)");
                string hol = GetHolidayName(selectedDate);
                if (string.IsNullOrEmpty(hol) && selectedDate.DayOfWeek == DayOfWeek.Saturday)
                    titleColor = Theme.Pick(Color.FromRgb(78, 184, 232), Color.FromRgb(47, 125, 209));
            }

            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock title = new TextBlock { Text = $"{dateKey}{dayString}", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = titleColor, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
            Button btnCloseHeader = new Button { Content = "✕", Width = 40, Height = 40, Background = Brushes.Transparent, Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(120, 128, 160))), BorderThickness = new Thickness(0), FontSize = 18, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand, ToolTip = "닫기 (ESC)" };
            btnCloseHeader.Template = CreateCloseBtnTemplate();
            btnCloseHeader.Click += (s, e) => inputWin.Close();

            headerGrid.Children.Add(title); headerGrid.Children.Add(btnCloseHeader);
            Grid.SetColumn(btnCloseHeader, 1);
            layout.Children.Add(headerGrid); Grid.SetRow(headerGrid, 0);

            // ── 디자인 개선: 헤더 하단 separator ──
            Border headerSep = new Border { Height = 1, Background = new SolidColorBrush(Theme.L(Color.FromArgb(15, 255, 255, 255))), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 0) };
            layout.Children.Add(headerSep); Grid.SetRow(headerSep, 0);

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 10, 0, 10) };
            StackPanel memoPanel = new StackPanel { AllowDrop = true };
            scroll.Content = memoPanel;

            ControlTemplate hueWheelTemplate = CreateHueWheelButtonTemplate();
            bool isInitializing = true;

            Border _draggedRowBorder = null;
            Point _dragHandleStart = default;
            bool _isDraggingRow = false;
            Brush insertLineBrush = new SolidColorBrush(Theme.L(Color.FromRgb(90, 200, 250)));

            void ClearInsertLines()
            { foreach (var child in memoPanel.Children.OfType<Border>()) { child.BorderBrush = Brushes.Transparent; child.BorderThickness = new Thickness(0); } }

            void ShowInsertLine(Border targetBorder, bool above)
            {
                ClearInsertLines(); if (targetBorder == null) return;
                targetBorder.BorderThickness = above ? new Thickness(0, 2, 0, 0) : new Thickness(0, 0, 0, 2);
                targetBorder.BorderBrush = insertLineBrush;
            }

            (Border rowBorder, bool above) GetDropTarget(DragEventArgs e)
            {
                foreach (var child in memoPanel.Children.OfType<Border>())
                { var pos = e.GetPosition(child); if (pos.Y >= 0 && pos.Y <= child.ActualHeight) return (child, pos.Y < child.ActualHeight / 2); }
                return (null, false);
            }

            Action<MemoItem> addRow = null;
            addRow = (src) =>
            {
                int rowIndex = memoPanel.Children.Count;
                Border rowBorder = new Border { BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0), AllowDrop = true, Margin = new Thickness(0, 2, 0, 2) };
                rowBorder.Tag = src;

                Grid row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                // ── 디자인 개선: 행 배경색 더 부드러운 교차 ──
                row.Background = rowIndex % 2 == 0 ? new SolidColorBrush(Theme.L(Color.FromRgb(28, 31, 44))) : new SolidColorBrush(Theme.L(Color.FromRgb(24, 27, 38)));
                rowBorder.Child = row;

                Grid leftPanel = new Grid { VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Center, Width = 50 };
                leftPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                leftPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                leftPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // ── 디자인 개선: 핸들 색상 ──
                TextBlock handle = new TextBlock { Text = "≡", FontSize = 16, Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(120, 128, 162))), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.SizeAll, Margin = new Thickness(0, 4, 0, 2), ToolTip = "드래그하여 순서 변경" };
                Grid.SetRow(handle, 0); leftPanel.Children.Add(handle);

                TextBox tb = new TextBox { Text = src?.Text ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = _settings.FontSize, Background = inputBg, Foreground = textBrush, BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(20, 90, 200, 250))), Padding = new Thickness(8, 5, 8, 5), MinHeight = _settings.FontSize * 5 + 10, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                tb.GotFocus += (s, e) => tb.BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(60, 90, 200, 250)));
                tb.LostFocus += (s, e) => tb.BorderBrush = new SolidColorBrush(Theme.L(Color.FromArgb(20, 90, 200, 250)));

                string currentColor = src?.TextColor;
                if (!string.IsNullOrEmpty(currentColor)) tb.Foreground = UserTextBrush(currentColor, textBrush);

                Button btnColor = new Button { Width = 18, Height = 18, Margin = new Thickness(0, 2, 0, 0), Cursor = Cursors.Hand, ToolTip = "글자 색상 변경", BorderThickness = new Thickness(0), Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, Template = hueWheelTemplate, Tag = currentColor };
                Grid.SetRow(btnColor, 1); leftPanel.Children.Add(btnColor);

                ContextMenu colorMenu = new ContextMenu();
                string[] colors = { "Default", "#FF5C5C", "#FF9F43", "#FECA57", "#2ECC71", "#54A0FF", "#5F27CD", "#A3CB38" };
                string[] colorNames = { "기본 색상", "빨강", "주황", "노랑", "초록", "파랑", "보라", "연두" };
                for (int i = 0; i < colors.Length; i++)
                {
                    string hex = colors[i]; string name = colorNames[i];
                    MenuItem mi2 = new MenuItem { Header = name };
                    Rectangle icon = new Rectangle { Width = 16, Height = 16, RadiusX = 2, RadiusY = 2, Stroke = new SolidColorBrush(Theme.L(Color.FromRgb(120, 128, 162))), StrokeThickness = 1 }; // 밝은 메뉴에서 기본 색 견본도 보이게
                    icon.Fill = hex == "Default" ? textBrush : new BrushConverter().ConvertFromString(hex) as Brush;
                    mi2.Icon = icon;
                    mi2.Click += (s, e) => { if (hex == "Default") { tb.Foreground = textBrush; btnColor.Tag = null; } else { tb.Foreground = UserTextBrush(hex, textBrush); btnColor.Tag = hex; } };
                    colorMenu.Items.Add(mi2);
                }
                btnColor.Click += (s, e) => colorMenu.IsOpen = true;

                CheckBox chkDone = new CheckBox { IsChecked = src?.IsDone ?? false, Style = (Style)FindResource("ModernCheckBox"), Foreground = textBrush, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(5, 4, 0, 0), Content = "" };
                Grid.SetRow(chkDone, 2); leftPanel.Children.Add(chkDone);
                tb.Tag = chkDone;

                row.Children.Add(leftPanel); Grid.SetColumn(leftPanel, 0);
                row.Children.Add(tb); Grid.SetColumn(tb, 1);

                tb.TextChanged += (s, e) =>
                {
                    if (isInitializing) return;
                    if (memoPanel.Children.Count > 0 && ReferenceEquals(memoPanel.Children[memoPanel.Children.Count - 1], rowBorder) && !string.IsNullOrWhiteSpace(tb.Text))
                        addRow(null);
                };

                Button btnDelete = new Button { Content = "✕", Width = 28, Height = 26, Margin = new Thickness(8, 0, 0, 0), Background = Brushes.Transparent, Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(120, 128, 160))), BorderThickness = new Thickness(0), Cursor = Cursors.Hand, ToolTip = "이 일정 줄 삭제" };
                btnDelete.Template = CreateCloseBtnTemplate();
                btnDelete.Click += (s, e) => memoPanel.Children.Remove(rowBorder);
                row.Children.Add(btnDelete); Grid.SetColumn(btnDelete, 2);

                handle.PreviewMouseLeftButtonDown += (s, e) => { _dragHandleStart = e.GetPosition(null); _draggedRowBorder = rowBorder; _isDraggingRow = false; e.Handled = true; };
                handle.MouseMove += (s, e) =>
                {
                    if (_draggedRowBorder == null || e.LeftButton != MouseButtonState.Pressed) return;
                    var diff = _dragHandleStart - e.GetPosition(null);
                    if (!_isDraggingRow && (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
                    {
                        // 고정 + 드래그 잠금 시 차단
                        if (IsPinned && _settings.LockDragWhenPinned)
                        {
                            _draggedRowBorder = null;
                            return;
                        }

                        _isDraggingRow = true; _draggedRowBorder.Opacity = 0.4;
                        DragDrop.DoDragDrop(handle, _draggedRowBorder, DragDropEffects.Move);
                        if (_draggedRowBorder != null) _draggedRowBorder.Opacity = 1.0;
                        _draggedRowBorder = null; _isDraggingRow = false; ClearInsertLines();
                    }
                };
                handle.MouseLeave += (s, e) => { if (!_isDraggingRow) _draggedRowBorder = null; };

                rowBorder.DragOver += (s, e) =>
                {
                    if (!e.Data.GetDataPresent(typeof(Border))) return;
                    var (targetBorder, above) = GetDropTarget(e);
                    ShowInsertLine(targetBorder, above); e.Effects = DragDropEffects.Move; e.Handled = true;
                };
                rowBorder.DragLeave += (s, e) =>
                {
                    var pos = e.GetPosition(memoPanel);
                    if (pos.X < 0 || pos.Y < 0 || pos.X > memoPanel.ActualWidth || pos.Y > memoPanel.ActualHeight) ClearInsertLines();
                };
                rowBorder.Drop += (s, e) =>
                {
                    if (!e.Data.GetDataPresent(typeof(Border))) return;
                    Border dragged = e.Data.GetData(typeof(Border)) as Border;
                    if (dragged == null || ReferenceEquals(dragged, rowBorder)) { ClearInsertLines(); return; }
                    var (targetBorder, above) = GetDropTarget(e);
                    if (targetBorder == null) { ClearInsertLines(); return; }
                    int fromIdx = memoPanel.Children.IndexOf(dragged);
                    int toIdx = memoPanel.Children.IndexOf(targetBorder);
                    if (fromIdx < 0 || toIdx < 0) { ClearInsertLines(); return; }
                    int insertIdx = above ? toIdx : toIdx + 1;
                    if (fromIdx < insertIdx) insertIdx--;
                    memoPanel.Children.Remove(dragged);
                    if (insertIdx >= memoPanel.Children.Count) memoPanel.Children.Add(dragged);
                    else memoPanel.Children.Insert(insertIdx, dragged);
                    ClearInsertLines(); e.Handled = true;
                };

                memoPanel.Children.Add(rowBorder);
            };

            if (items.Count > 0) foreach (var it in items) addRow(it);
            addRow(null);
            isInitializing = false;

            layout.Children.Add(scroll); Grid.SetRow(scroll, 1);

            Grid bottomGrid = new Grid();
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock infoText = new TextBlock { Text = "Ctrl+Q : 현재 줄 완료 토글 / Ctrl+S : 저장  |  ≡ 드래그로 순서 변경", Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(110, 118, 150))), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0), FontSize = 11 };
            // ── 디자인 개선: 저장 버튼 gradient ──
            Button btnSave = new Button { Content = "저장", Width = 110, Height = 42, HorizontalAlignment = HorizontalAlignment.Right, Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
            btnSave.Background = new LinearGradientBrush(Theme.L(Color.FromRgb(50, 160, 240)), Theme.L(Color.FromRgb(90, 200, 250)), 0);
            btnSave.Template = CreateRoundedBtnTemplate();

            bottomGrid.Children.Add(infoText); Grid.SetColumn(infoText, 0);
            bottomGrid.Children.Add(btnSave); Grid.SetColumn(btnSave, 1);
            layout.Children.Add(bottomGrid); Grid.SetRow(bottomGrid, 2);

            Action saveAction = () =>
            {
                var newList = new List<MemoItem>();
                var colorChanges = new List<(string text, string color)>(); // 색 직접 변경 전파용
                foreach (var rowBorder in memoPanel.Children.OfType<Border>())
                {
                    if (rowBorder.Child is not Grid innerRow) continue;
                    var tb2 = innerRow.Children.OfType<TextBox>().FirstOrDefault();
                    var leftGrid = innerRow.Children.OfType<Grid>().FirstOrDefault();
                    if (tb2 == null || leftGrid == null) continue;
                    var chk2 = leftGrid.Children.OfType<CheckBox>().FirstOrDefault();
                    var btnColor2 = leftGrid.Children.OfType<Button>().FirstOrDefault();
                    if (chk2 == null) continue;
                    string text2 = tb2.Text.Trim();
                    if (string.IsNullOrEmpty(text2)) continue;

                    var origItem = rowBorder.Tag as MemoItem;
                    string newColor = btnColor2?.Tag as string;

                    // 색을 직접 지정/변경한 경우 (기존 메모 색 변경 OR 새 메모에 색 지정)
                    bool colorChanged =
                        (origItem != null && !string.Equals(origItem.TextColor, newColor, StringComparison.OrdinalIgnoreCase))
                        || (origItem == null && !string.IsNullOrEmpty(newColor));
                    if (colorChanged)
                        colorChanges.Add((text2, newColor));

                    newList.Add(new MemoItem
                    {
                        Text = text2,
                        IsDone = (chk2.IsChecked == true),
                        TextColor = newColor,
                        IsMacro = origItem?.IsMacro ?? false,
                        MacroId = origItem?.MacroId ?? 0
                    });
                }
                if (newList.Count > 0) _memoData[dateKey] = newList;
                else if (_memoData.ContainsKey(dateKey)) _memoData.Remove(dateKey);

                // 색 직접 변경 → 연속 run(매크로 포함) + 관련 매크로 전체에 전파
                foreach (var (text, color) in colorChanges)
                    ApplyColorToMergedRun(dateKey, text, color);

                // 연속일정 연결 ON이면 run 색 자동 통일 (새로 합류한 메모는 매크로 색 따라감)
                if (_settings.ConnectConsecutive && _memoData.TryGetValue(dateKey, out var todayList))
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var m in todayList)
                    {
                        string t = (m.Text ?? "").Trim();
                        if (t.Length == 0 || !seen.Add(t)) continue;
                        UnifyRunColor(dateKey, t);
                    }
                }

                SaveData(); RefreshCalendar();
            };

            btnSave.Click += (s, e) => { saveAction(); inputWin.Close(); };
            inputWin.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) { btnSave.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
                else if (e.Key == Key.Q && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) { if (Keyboard.FocusedElement is TextBox tbf && tbf.Tag is CheckBox cb) { cb.IsChecked = !(cb.IsChecked == true); e.Handled = true; } }
                else if (e.Key == Key.Escape) inputWin.Close();
            };

            border.Child = layout; ApplyModalEdge(border); inputWin.Content = border;
            inputWin.Loaded += (s, e) =>
            {
                var firstTb = memoPanel.Children.OfType<Border>().Select(b => (b.Child as Grid)?.Children.OfType<TextBox>().FirstOrDefault()).FirstOrDefault(t => t != null);
                if (firstTb != null) { firstTb.Focus(); firstTb.CaretIndex = firstTb.Text.Length; }
            };
            inputWin.ShowDialog();
        }

        private void ShowDayScheduleWindow(string dateKey)
        {
            if (!_memoData.TryGetValue(dateKey, out var items) || items == null || items.Count == 0)
            { MessageBox.Show("이 날짜에는 등록된 일정이 없습니다.", "일정 없음", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++) { if (i > 0) { sb.AppendLine(); sb.AppendLine("────────────────────────"); sb.AppendLine(); } sb.AppendLine(items[i].Text ?? string.Empty); }
            string allText = sb.ToString().TrimEnd();

            Window viewWin = new Window { FontFamily = AppFont, Title = $"{dateKey} 일정", Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ShowInTaskbar = false, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize };
            viewWin.Resources["ScrollThumbBrush"] = this.Resources["ScrollThumbBrush"];
            viewWin.Resources["ScrollThumbHoverBrush"] = this.Resources["ScrollThumbHoverBrush"];
            viewWin.Resources.MergedDictionaries.Add(this.Resources);

            Brush bg = new SolidColorBrush(Theme.L(Color.FromRgb(22, 25, 38)));
            Brush borderBrush2 = new SolidColorBrush(Theme.L(Color.FromArgb(25, 255, 255, 255)));
            Brush titleBrush2 = new SolidColorBrush(Theme.L(Color.FromRgb(224, 228, 244)));
            Brush inputBg2 = new SolidColorBrush(Theme.L(Color.FromRgb(14, 16, 24)));
            Brush inputFg2 = new SolidColorBrush(Theme.L(Color.FromRgb(210, 216, 236)));

            Border viewBorder = new Border { Background = bg, CornerRadius = new CornerRadius(14), BorderBrush = borderBrush2, BorderThickness = new Thickness(1), Padding = new Thickness(20) };
            viewBorder.Effect = new DropShadowEffect { BlurRadius = 30, Opacity = 0.55, ShadowDepth = 6, Color = Colors.Black };

            Grid vLayout = new Grid();
            vLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            vLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid vHeader = new Grid();
            vHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            vHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            vHeader.MouseLeftButtonDown += (s, e) => viewWin.DragMove();

            string headerText = dateKey;
            if (DateTime.TryParse(dateKey, out DateTime sd2)) { string hol = GetHolidayName(sd2); headerText = string.IsNullOrEmpty(hol) ? $"{dateKey}{sd2.ToString(" (ddd)")}" : $"{dateKey}{sd2.ToString(" (ddd)")} - {hol}"; headerText += ShiftSummary(sd2); }

            TextBlock vTitle = new TextBlock { Text = headerText, FontSize = 18, FontWeight = FontWeights.Bold, Foreground = titleBrush2, VerticalAlignment = VerticalAlignment.Center };
            Button vClose = new Button { Content = "✕", Width = 32, Height = 32, Background = Brushes.Transparent, Foreground = new SolidColorBrush(Theme.L(Color.FromRgb(120, 128, 160))), BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(8, 0, 0, 0), ToolTip = "닫기 (ESC)" };
            vClose.Template = CreateCloseBtnTemplate();
            vClose.Click += (s, e) => viewWin.Close();
            vHeader.Children.Add(vTitle); vHeader.Children.Add(vClose);
            Grid.SetColumn(vClose, 1); Grid.SetRow(vHeader, 0);
            vLayout.Children.Add(vHeader);

            // ── 디자인 개선: 헤더 하단 separator ──
            Border viewHeaderSep = new Border { Height = 1, Background = new SolidColorBrush(Theme.L(Color.FromArgb(15, 255, 255, 255))), VerticalAlignment = VerticalAlignment.Bottom };
            Grid.SetRow(viewHeaderSep, 0);
            vLayout.Children.Add(viewHeaderSep);

            viewWin.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { viewWin.Close(); e.Handled = true; } };

            TextBox txtBox = new TextBox { Text = allText, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = _settings.FontSize, Background = inputBg2, Foreground = inputFg2, BorderThickness = new Thickness(0), Padding = new Thickness(12), IsReadOnly = true, Margin = new Thickness(0, 8, 0, 0) };
            Grid.SetRow(txtBox, 1); vLayout.Children.Add(txtBox);

            viewBorder.Child = vLayout; ApplyModalEdge(viewBorder); viewWin.Content = viewBorder;
            viewWin.Loaded += (s, e) => { txtBox.Focus(); txtBox.CaretIndex = 0; };
            viewWin.ShowDialog();
        }

        // ──────────────────────────────────────────
        // 버튼 핸들러
        // ──────────────────────────────────────────
        private void BtnPrivacy_Click(object sender, RoutedEventArgs e) { _isPrivacyMode = !_isPrivacyMode; RefreshCalendar(); }
        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            if (IsPinned) return;
            if (_settings.ShowInTaskbar) WindowState = WindowState.Minimized; // 작업 표시줄로
            else Hide(); // 트레이로
        }
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsPopup.Visibility = SettingsPopup.Visibility != Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
            if (SettingsPopup.Visibility != Visibility.Visible) return;
            InitMonitors();
        }
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close(); // 트레이로 숨음 (MainWindow_Closing)
        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_isMiniMode)
                _currentDate = _settings.HideWeekends
                    ? AddWeekdays(_currentDate, -5)
                    : _currentDate.AddDays(-7);
            else
                _currentDate = _currentDate.AddMonths(-1);
            RefreshCalendar();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_isMiniMode)
                _currentDate = _settings.HideWeekends
                    ? AddWeekdays(_currentDate, 5)
                    : _currentDate.AddDays(7);
            else
                _currentDate = _currentDate.AddMonths(1);
            RefreshCalendar();
        }
        private void TxtYearMonth_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { _currentDate = DateTime.Now; RefreshCalendar(); }
        private void BtnToday_Click(object sender, RoutedEventArgs e) { _currentDate = DateTime.Now; RefreshCalendar(); }

        // ──────────────────────────────────────────
        // 버튼 템플릿
        // ──────────────────────────────────────────
        private ControlTemplate CreateCloseBtnTemplate()
        {
            var t = new ControlTemplate(typeof(Button));
            var b = new FrameworkElementFactory(typeof(Border));
            b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            b.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            var c = new FrameworkElementFactory(typeof(ContentPresenter));
            c.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            c.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            b.AppendChild(c); t.VisualTree = b;
            var tr = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            tr.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Theme.L(Color.FromRgb(220, 60, 60)))));
            tr.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            t.Triggers.Add(tr); return t;
        }

        private ControlTemplate CreateRoundedBtnTemplate()
        {
            var t = new ControlTemplate(typeof(Button));
            var b = new FrameworkElementFactory(typeof(Border));
            b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            b.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            var c = new FrameworkElementFactory(typeof(ContentPresenter));
            c.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            c.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            b.AppendChild(c); t.VisualTree = b;
            var tr = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            tr.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Theme.L(Color.FromRgb(60, 180, 240)))));
            t.Triggers.Add(tr); return t;
        }

        // ──────────────────────────────────────────
        // 자동 실행 (레지스트리)
        // ──────────────────────────────────────────
        private void LoadAutoStartStatus()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                object? registered = key.GetValue(AppRegistryName);
                ChkAutoStart.IsChecked = registered != null;
                // 설치본이면, 예전 위치(ClickOnce 캐시 등)를 가리키는 자동 실행을 지금 실행 파일로 바로잡음.
                // 개발 빌드(bin\Debug)는 사용자의 자동 실행을 건드리지 않음
                if (registered is string path && UpdateService.IsInstalledCopy &&
                    !string.Equals(path.Trim('"'), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                    key.SetValue(AppRegistryName, AutoStartCommand);
            }
            catch (Exception ex) { Debug.WriteLine($"레지스트리 읽기 실패: {ex.Message}"); }
        }

        private static string AutoStartCommand => $"\"{Environment.ProcessPath}\"";

        private void ChkAutoStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (ChkAutoStart.IsChecked == true) key.SetValue(AppRegistryName, AutoStartCommand);
                else key.DeleteValue(AppRegistryName, false);
            }
            catch (Exception ex) { MessageBox.Show($"설정 저장 중 오류가 발생했습니다.\n{ex.Message}"); }
        }

        // ──────────────────────────────────────────
        // 트레이 아이콘 / 바탕화면 고정
        // ──────────────────────────────────────────
        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private System.Windows.Forms.ToolStripMenuItem? _trayPinItem, _trayFrontItem, _trayBackItem;
        private IntPtr _hwnd;
        private bool _isPeeking; // 고정 상태에서 트레이로 불러내 잠시 앞에 보여주는 중
        private IntPtr _peekForeground; // 꺼내 보일 때의 전경 창. 여기서 다른 창으로 바뀌어야 꺼내보기 종료
        private bool _restoringActivation; // 작업 표시줄 설정을 바꾼 뒤 활성 상태를 되돌리는 중 (꺼내 보기 아님)
        private DesktopWindowNative.WinEventProc? _foregroundProc; // 네이티브 콜백이 GC 되지 않게 보관
        private IntPtr _foregroundHook;
        private DispatcherTimer? _desktopRecheck;

        private bool IsPinned => ChkLockMinimize.IsChecked == true;
        private bool IsPinnedToBack => IsPinned && _settings.PinMode == PinMode.Back;

        private void InitTrayIcon()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();
            _trayPinItem = new System.Windows.Forms.ToolStripMenuItem("고정", null, (s, e) => ChkLockMinimize.IsChecked = !IsPinned);
            _trayFrontItem = new System.Windows.Forms.ToolStripMenuItem("맨 앞으로", null, (s, e) => SetPinMode(PinMode.Front));
            _trayBackItem = new System.Windows.Forms.ToolStripMenuItem("맨 뒤로 (바탕화면)", null, (s, e) => SetPinMode(PinMode.Back));
            menu.Items.Add("열기", null, (s, e) => ShowFromTray());
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(_trayPinItem);
            menu.Items.Add(_trayFrontItem);
            menu.Items.Add(_trayBackItem);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            // 메뉴 클릭 처리 중에 아이콘이 정리되지 않도록 한 박자 뒤에 닫기
            menu.Items.Add("종료", null, (s, e) => Dispatcher.InvokeAsync(ExitApp));

            _trayIcon = new System.Windows.Forms.NotifyIcon { Icon = LoadTrayIcon(), Text = "DeskCal", ContextMenuStrip = menu, Visible = true };
            // 한 번 클릭·더블 클릭 모두 잠깐 꺼내 보기
            System.Windows.Forms.MouseEventHandler peek = (s, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) ShowFromTray(); };
            _trayIcon.MouseClick += peek;
            _trayIcon.MouseDoubleClick += peek;
        }

        private static System.Drawing.Icon LoadTrayIcon()
        {
            try
            {
                var res = Application.GetResourceStream(new Uri("pack://application:,,,/cal.ico"));
                if (res != null)
                    using (res.Stream)
                        return new System.Drawing.Icon(res.Stream, System.Windows.Forms.SystemInformation.SmallIconSize);
            }
            catch (Exception ex) { Debug.WriteLine($"트레이 아이콘 로드 실패: {ex.Message}"); }
            return System.Drawing.SystemIcons.Application;
        }

        internal void ShowFromTray() // 트레이 아이콘, 그리고 두 번째로 실행했을 때 (App.xaml.cs)
        {
            _isPeeking = IsPinnedToBack; // 맨 뒤 고정 중이면 잠시 앞으로 꺼내 보여주고, 포커스를 잃으면 다시 바탕화면으로
            if (!IsVisible) Show();
            if (this.WindowState == WindowState.Minimized) this.WindowState = WindowState.Normal;
            if (!Topmost) { Topmost = true; Topmost = false; } // 포그라운드 권한이 없어도 맨 앞으로 (맨 앞 고정이면 이미 위)
            Activate();
            if (DesktopWindowNative.GetForegroundWindow() != _hwnd) ForceActivate();
            _peekForeground = DesktopWindowNative.GetForegroundWindow(); // 그래도 거부됐다면 다른 창
        }

        // 사용자가 트레이를 눌러 연 경우인데도 활성화가 거부되면, 현재 전경 스레드에 입력을 잠시 붙여 활성화.
        // 활성화되지 않은 채 앞에만 떠 있으면 원래 창을 다시 눌러도 전경이 바뀌지 않아 뒤로 돌아가지 않음
        private void ForceActivate()
        {
            uint fgThread = DesktopWindowNative.GetWindowThreadProcessId(DesktopWindowNative.GetForegroundWindow(), out _);
            uint myThread = DesktopWindowNative.GetCurrentThreadId();
            if (fgThread == 0 || fgThread == myThread) return;
            if (!DesktopWindowNative.AttachThreadInput(myThread, fgThread, true)) return;
            try { DesktopWindowNative.SetForegroundWindow(_hwnd); }
            finally { DesktopWindowNative.AttachThreadInput(myThread, fgThread, false); }
        }

        private void ChkLockMinimize_Changed(object sender, RoutedEventArgs e)
        {
            _settings.Pinned = IsPinned;
            SaveSettings();
            ApplyPinState();
        }

        private void ChkShowInTaskbar_Changed(object sender, RoutedEventArgs e)
        {
            _settings.ShowInTaskbar = ChkShowInTaskbar.IsChecked == true;
            SaveSettings();
            ApplyTaskbarVisibility();
        }

        // 작업 표시줄 버튼 켜기/끄기. 끄면 작업 표시줄·Alt+Tab 에 나오지 않는 도구 창 (트레이 아이콘으로만 접근)
        private void ApplyTaskbarVisibility()
        {
            // 숨기는 동안 다른 창으로 넘어간 활성 상태를 되찾기 위해 먼저 기억 (WPF 도 ShowInTaskbar 를 바꿀 때 잠깐 숨김)
            bool wasActive = _hwnd != IntPtr.Zero && DesktopWindowNative.GetForegroundWindow() == _hwnd;
            ShowInTaskbar = _settings.ShowInTaskbar; // WPF 의 숨은 소유자 창 연결/해제
            if (_hwnd == IntPtr.Zero) return;
            // 작업 표시줄은 창이 나타날 때 버튼 여부를 정하므로, 보이는 중이면 스타일을 바꾸는 동안 잠깐 숨김
            bool visible = DesktopWindowNative.IsWindowVisible(_hwnd);
            if (visible) DesktopWindowNative.ShowWindow(_hwnd, DesktopWindowNative.SW_HIDE);
            int exStyle = DesktopWindowNative.GetWindowLong(_hwnd, DesktopWindowNative.GWL_EXSTYLE);
            DesktopWindowNative.SetWindowLong(_hwnd, DesktopWindowNative.GWL_EXSTYLE, ToTaskbarExStyle(exStyle));
            UpdateMinimizeBox();
            if (visible) DesktopWindowNative.ShowWindow(_hwnd, DesktopWindowNative.SW_SHOWNA);
            if (wasActive)
            {
                // 사용자가 방금 이 창을 눌렀으므로 전경 권한이 있음. 맨 뒤 고정이면 꺼내 보기로 바뀌지 않게 표시
                _restoringActivation = true;
                try { Activate(); }
                finally { _restoringActivation = false; }
            }
            if (ShouldStayOnDesktop()) SendToDesktopLayer();
        }

        private void BtnPinMode_Click(object sender, RoutedEventArgs e) =>
            SetPinMode(_settings.PinMode == PinMode.Back ? PinMode.Front : PinMode.Back);

        // 고정 위치 선택. 이미 고정 중이면 바로 적용
        private void SetPinMode(PinMode mode)
        {
            _settings.PinMode = mode;
            SaveSettings();
            ApplyPinState();
        }

        private void ApplyPinState()
        {
            UpdatePinIndicators();
            UpdateMinimizeBox();
            Topmost = IsPinned && _settings.PinMode == PinMode.Front;
            if (!IsPinned) return;

            _isPeeking = false;
            if (!IsVisible) { ShowActivated = false; Show(); } // 트레이에 숨어 있었다면 다시 표시
            if (this.WindowState == WindowState.Minimized) DesktopWindowNative.ShowWindow(_hwnd, DesktopWindowNative.SW_SHOWNOACTIVATE);
            if (ShouldStayOnDesktop()) SendToDesktopLayer();
        }

        // 고정 위치 버튼·트레이 메뉴 표시
        private void UpdatePinIndicators()
        {
            bool front = _settings.PinMode == PinMode.Front;
            BtnPinMode.IsChecked = front;
            BtnPinMode.ToolTip = front
                ? "고정하면 항상 다른 창 위에 표시 (클릭: 맨 뒤로 바꾸기)"
                : "고정하면 바탕화면에 붙어 다른 창 뒤에 머무름 (클릭: 맨 앞으로 바꾸기)";
            if (_trayPinItem != null) _trayPinItem.Checked = IsPinned;
            if (_trayFrontItem != null) _trayFrontItem.Checked = front;
            if (_trayBackItem != null) _trayBackItem.Checked = !front;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
            ApplyTaskbarAppId(); // 작업 표시줄 버튼도 오늘 날짜 아이콘으로 (MainWindow.AppIcon.cs)
            // 설정에 따라 작업 표시줄 창 또는 작업 표시줄·Alt+Tab 에 나오지 않는 도구 창 (트레이 아이콘으로만 접근)
            int exStyle = DesktopWindowNative.GetWindowLong(_hwnd, DesktopWindowNative.GWL_EXSTYLE);
            DesktopWindowNative.SetWindowLong(_hwnd, DesktopWindowNative.GWL_EXSTYLE, ToTaskbarExStyle(exStyle));
            UpdateMinimizeBox();
            // 다른 프로그램 창이 앞으로 오면 (활성화된 적이 없어도) 다시 바탕화면 층으로
            _foregroundProc = OnForegroundChanged;
            _foregroundHook = DesktopWindowNative.SetWinEventHook(DesktopWindowNative.EVENT_SYSTEM_FOREGROUND, DesktopWindowNative.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _foregroundProc, 0, 0, DesktopWindowNative.WINEVENT_OUTOFCONTEXT);
            if (ShouldStayOnDesktop()) SendToDesktopLayer();
        }

        // 작업 표시줄에 표시 + 고정이면 최소화 상자를 뺌. 작업 표시줄 창은 Win+D·(활성 상태에서) 작업 표시줄 버튼이
        // 최소화하는데, 고정 창은 StateChanged 에서 곧바로 되살리므로 깜박임. 최소화 상자가 없으면 둘 다 최소화하지 않음
        // (작업 표시줄에 없는 도구 창은 원래 Win+D 가 건드리지 않음)
        private bool BlockMinimize => _settings.ShowInTaskbar && IsPinned;

        private void UpdateMinimizeBox()
        {
            if (_hwnd == IntPtr.Zero) return;
            int style = DesktopWindowNative.GetWindowLong(_hwnd, DesktopWindowNative.GWL_STYLE);
            int wanted = BlockMinimize ? style & ~DesktopWindowNative.WS_MINIMIZEBOX : style | DesktopWindowNative.WS_MINIMIZEBOX;
            if (wanted != style) DesktopWindowNative.SetWindowLong(_hwnd, DesktopWindowNative.GWL_STYLE, wanted);
        }

        private int ToTaskbarExStyle(int exStyle) => _settings.ShowInTaskbar
            ? (exStyle & ~DesktopWindowNative.WS_EX_TOOLWINDOW) | DesktopWindowNative.WS_EX_APPWINDOW
            : (exStyle | DesktopWindowNative.WS_EX_TOOLWINDOW) & ~DesktopWindowNative.WS_EX_APPWINDOW;

        private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            // 이벤트가 늦게 도착할 수 있어 현재 전경 창으로 판단. 내 대화 상자·트레이 메뉴로 옮긴 경우는 그대로
            IntPtr foreground = DesktopWindowNative.GetForegroundWindow();
            DesktopWindowNative.GetWindowThreadProcessId(foreground, out uint pid);
            if (pid == (uint)Environment.ProcessId) return;
            if (_isPeeking && foreground == _peekForeground) return; // 꺼낸 뒤 아직 다른 창으로 옮기지 않음
            if (_isPeeking && IsTaskbarWindow(foreground)) return;  // 트레이 아이콘을 다시 누르는 중(더블 클릭 등)
            _isPeeking = false;
            if (!ShouldStayOnDesktop()) return;
            SendToDesktopLayer();
            // Win+D 는 전경을 바꾼 뒤에 바탕화면 창을 위로 올리므로, 조금 뒤 한 번 더 바탕화면 바로 위로 맞춤
            _desktopRecheck ??= new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background, (s, e) =>
            {
                _desktopRecheck!.Stop();
                if (ShouldStayOnDesktop()) SendToDesktopLayer();
            }, Dispatcher);
            _desktopRecheck.Stop();
            _desktopRecheck.Start();
        }

        // 작업 표시줄·알림 영역(넘침 창 포함). 여기로 전경이 옮겨 가도 다른 앱으로 이동한 것으로 보지 않음
        private static bool IsTaskbarWindow(IntPtr hwnd)
        {
            var cls = new StringBuilder(64);
            DesktopWindowNative.GetClassName(hwnd, cls, cls.Capacity);
            return cls.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland";
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            if (_foregroundHook != IntPtr.Zero) { DesktopWindowNative.UnhookWinEvent(_foregroundHook); _foregroundHook = IntPtr.Zero; }
            _desktopRecheck?.Stop();
            if (_trayIcon == null) return;
            _trayIcon.Visible = false; // 종료 후 트레이에 유령 아이콘이 남지 않게
            _trayIcon.ContextMenuStrip?.Dispose();
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        // 모달 대화 상자가 열려 있으면(소유 창은 소유자와 함께 Z-순서가 움직임) 대화 상자까지 뒤로 밀리지 않게 제외
        private bool ShouldStayOnDesktop() =>
            IsPinnedToBack && !_isPeeking && _hwnd != IntPtr.Zero
            && DesktopWindowNative.IsWindowEnabled(_hwnd)
            && !OwnedWindows.Cast<Window>().Any(w => w.IsVisible);

        private void SendToDesktopLayer()
        {
            if (TryGetDesktopSlot(out IntPtr insertAfter))
                DesktopWindowNative.SetWindowPos(_hwnd, insertAfter, 0, 0, 0, 0,
                    DesktopWindowNative.SWP_NOMOVE | DesktopWindowNative.SWP_NOSIZE | DesktopWindowNative.SWP_NOACTIVATE);
        }

        // 바탕화면(셸 창) 바로 위 자리. HWND_BOTTOM 은 Win+D 로 셸 창이 위로 올라와 있으면 바탕화면 뒤로 숨어 버림.
        // 이미 그 자리면 false. 셸 창 바로 위에는 WPF 가 만든 숨은 소유자 창이 함께 붙어 있어 건너뜀
        private bool TryGetDesktopSlot(out IntPtr insertAfter)
        {
            insertAfter = DesktopWindowNative.HWND_BOTTOM;
            IntPtr shell = DesktopWindowNative.GetShellWindow();
            if (shell == IntPtr.Zero) return true;
            IntPtr owner = DesktopWindowNative.GetWindow(_hwnd, DesktopWindowNative.GW_OWNER);
            IntPtr above = DesktopWindowNative.GetWindow(shell, DesktopWindowNative.GW_HWNDPREV);
            while (above != IntPtr.Zero && above == owner) above = DesktopWindowNative.GetWindow(above, DesktopWindowNative.GW_HWNDPREV);
            if (above == _hwnd) return false;
            insertAfter = above == IntPtr.Zero ? DesktopWindowNative.HWND_TOP : above;
            return true;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case DesktopWindowNative.WM_WINDOWPOSCHANGING:
                    // 고정 중에는 활성화·복원 등 어떤 이유로든 Z-순서가 바뀌려 하면 바탕화면 바로 위로 바꿈
                    if (ShouldStayOnDesktop())
                    {
                        var pos = Marshal.PtrToStructure<DesktopWindowNative.WINDOWPOS>(lParam);
                        if ((pos.flags & DesktopWindowNative.SWP_NOZORDER) == 0)
                        {
                            if (TryGetDesktopSlot(out IntPtr insertAfter)) pos.hwndInsertAfter = insertAfter;
                            else pos.flags |= DesktopWindowNative.SWP_NOZORDER; // 이미 제자리
                            Marshal.StructureToPtr(pos, lParam, false);
                        }
                    }
                    break;
                case DesktopWindowNative.WM_ACTIVATE:
                    // 작업 표시줄 버튼·Alt+Tab 으로 불러낸 경우(창을 직접 눌러 활성화한 WA_CLICKACTIVE 는 제외),
                    // 맨 뒤 고정 중이어도 트레이로 연 것처럼 잠시 앞으로 꺼내 보여 줌
                    if ((wParam.ToInt64() & 0xFFFF) == DesktopWindowNative.WA_ACTIVE
                        && _settings.ShowInTaskbar && IsPinnedToBack && !_isPeeking && !_restoringActivation)
                        Dispatcher.InvokeAsync(ShowFromTray);
                    break;
                case DesktopWindowNative.WM_SYSCOMMAND:
                    // 최소화 상자를 뺀 상태에서도 시스템 명령으로 오는 최소화는 막음 (고정 = 최소화 안 함)
                    if ((wParam.ToInt64() & 0xFFF0) == DesktopWindowNative.SC_MINIMIZE && BlockMinimize)
                        handled = true;
                    break;
                case DesktopWindowNative.WM_STYLECHANGING:
                    // WPF 가 스타일을 다시 쓰더라도 설정한 작업 표시줄 표시 여부·최소화 상자 제거 유지
                    int styleIndex = unchecked((int)wParam.ToInt64());
                    if (styleIndex == DesktopWindowNative.GWL_EXSTYLE || (styleIndex == DesktopWindowNative.GWL_STYLE && BlockMinimize))
                    {
                        var style = Marshal.PtrToStructure<DesktopWindowNative.STYLESTRUCT>(lParam);
                        int fixedStyle = styleIndex == DesktopWindowNative.GWL_EXSTYLE
                            ? ToTaskbarExStyle(style.styleNew)
                            : style.styleNew & ~DesktopWindowNative.WS_MINIMIZEBOX;
                        if (fixedStyle != style.styleNew)
                        {
                            style.styleNew = fixedStyle;
                            Marshal.StructureToPtr(style, lParam, false);
                        }
                    }
                    break;
            }
            return IntPtr.Zero;
        }
    }

    // ──────────────────────────────────────────────
    // 창 Z-순서 / 스타일 (바탕화면 고정용)
    // ──────────────────────────────────────────────
    internal static class DesktopWindowNative
    {
        public const int WM_WINDOWPOSCHANGING = 0x0046;
        public const int WM_STYLECHANGING = 0x007C;
        public const int WM_ACTIVATE = 0x0006, WA_ACTIVE = 1;
        public const int WM_SYSCOMMAND = 0x0112, SC_MINIMIZE = 0xF020;
        public const int GWL_EXSTYLE = -20, GWL_STYLE = -16;
        public const int WS_MINIMIZEBOX = 0x00020000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_APPWINDOW = 0x00040000;
        public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
        public const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4, SW_SHOWNA = 8;
        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003, WINEVENT_OUTOFCONTEXT = 0x0000;
        public const uint GW_OWNER = 4, GW_HWNDPREV = 3;
        public static readonly IntPtr HWND_TOP = IntPtr.Zero, HWND_BOTTOM = new IntPtr(1);

        public delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPOS { public IntPtr hwnd, hwndInsertAfter; public int x, y, cx, cy; public uint flags; }

        [StructLayout(LayoutKind.Sequential)]
        public struct STYLESTRUCT { public int styleOld, styleNew; }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool IsWindowEnabled(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc callback, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    }

    // ──────────────────────────────────────────────
    // MonitorInfo / MonitorHelper
    // ──────────────────────────────────────────────
    public class MonitorInfo
    {
        public Rect Bounds { get; set; }
        public Rect WorkArea { get; set; }
    }

    public static class MonitorHelper
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);
        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public int dwFlags; }

        public static List<MonitorInfo> GetMonitors()
        {
            var monitors = new List<MonitorInfo>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                {
                    MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                    if (GetMonitorInfo(hMonitor, ref mi))
                        monitors.Add(new MonitorInfo
                        {
                            Bounds = new Rect(mi.rcMonitor.left, mi.rcMonitor.top, Math.Abs(mi.rcMonitor.right - mi.rcMonitor.left), Math.Abs(mi.rcMonitor.bottom - mi.rcMonitor.top)),
                            WorkArea = new Rect(mi.rcWork.left, mi.rcWork.top, Math.Abs(mi.rcWork.right - mi.rcWork.left), Math.Abs(mi.rcWork.bottom - mi.rcWork.top))
                        });
                    return true;
                }, IntPtr.Zero);
            return monitors;
        }
    }
}