using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;

namespace DeskCal
{
    // ──────────────────────────────────────────────
    // 교대근무 표시 (설정 → 교대근무). 달력 칸 날짜 줄 오른쪽 끝에 근무 배지:
    // 여러 조면 A·B·C·D 네 자리를 늘 같은 자리에 두고 고른 조만 근무를 씀 ("D S G 휴", B·D조면 "_ S _ 휴"),
    // 한 조만 고르면 "Day" · "Swing" · "Gy" · "휴무".
    // 근무는 근무 API 를 공휴일처럼 받아 DB 에 두고 씀 (docs/shift-api.md). API 값이 없는 날은 정해진 순서로 계산
    // ──────────────────────────────────────────────
    public partial class MainWindow
    {
        private bool _shiftUiReady; // 설정값을 컨트롤에 채우는 동안 바뀜 이벤트를 무시

        // 근무 API 로 받은 근무: 날짜 → [A, B, C, D] 코드 (그날 빠진 조는 null)
        private readonly Dictionary<DateTime, string?[]> _shiftData = new();
        private DateTime _shiftFetchedOn = DateTime.MinValue; // 마지막으로 받으려 한 날
        private bool _shiftFetchOk;  // 그때 한 해라도 받았는지 (못 받았으면 한 시간 뒤 다시)
        private bool _shiftFetching;
        private DispatcherTimer? _shiftFetchTimer;

        private static Color ShiftMissingColor => Theme.L(Color.FromRgb(96, 104, 130));

        private bool ShowShifts => _settings.ShiftEnabled;

        private void InitShiftSettings()
        {
            ChkShiftEnabled.IsChecked = _settings.ShiftEnabled;
            UpdateShiftSettingsUi();
            _shiftUiReady = true;

            LoadShifts();
            // 날이 바뀌면 다시 받고, 서버가 꺼져 있었으면 한 시간마다 다시 시도
            _shiftFetchTimer = new DispatcherTimer(TimeSpan.FromHours(1), DispatcherPriority.Background, (s, e) =>
            {
                if (_settings.ShiftEnabled && (_shiftFetchedOn != DateTime.Today || !_shiftFetchOk)) _ = ImportShiftsAsync();
            }, Dispatcher);
            if (_settings.ShiftEnabled) _ = ImportShiftsAsync();
        }

        private string? ShiftCodeFor(DateTime date, int team) =>
            _shiftData.TryGetValue(date.Date, out var codes) ? codes[team] : ShiftSchedule.CodeFor(date, team);

        // ── 표시할 조: "All" 또는 "B,D" 처럼 쉼표로. 저장값이 이상하면 전체 ──
        private bool[] SelectedShiftTeams() => ParseShiftTeams(_settings.ShiftTeam) ?? new[] { true, true, true, true };

        internal static bool[]? ParseShiftTeams(string? value)
        {
            if (value == ShiftSchedule.AllTeams) return new[] { true, true, true, true };
            var selected = new bool[ShiftSchedule.Teams.Length];
            foreach (var part in (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int i = ShiftSchedule.TeamIndex(part);
                if (i < 0) return null;
                selected[i] = true;
            }
            return selected.Any(x => x) ? selected : null;
        }

        private static string FormatShiftTeams(bool[] selected) => selected.All(x => x)
            ? ShiftSchedule.AllTeams
            : string.Join(",", ShiftSchedule.Teams.Where((_, i) => selected[i]));

        // ── 설정 이벤트 ──
        private void ChkShiftEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (!_shiftUiReady) return;
            _settings.ShiftEnabled = ChkShiftEnabled.IsChecked == true;
            ShiftSettingsChanged();
            if (_settings.ShiftEnabled && (_shiftFetchedOn != DateTime.Today || !_shiftFetchOk)) _ = ImportShiftsAsync();
        }

        // 전체 = 네 조 모두. 전체인 상태에서 한 조를 누르면 그 조만, 그다음부터는 누를 때마다 넣고 빼기.
        // 마지막 하나까지 빼면 다시 전체
        private void BtnShiftTeam_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string tag })
            {
                var selected = SelectedShiftTeams();
                int team = ShiftSchedule.TeamIndex(tag);
                if (team < 0) selected = new[] { true, true, true, true };
                else if (selected.All(x => x)) { selected = new bool[ShiftSchedule.Teams.Length]; selected[team] = true; }
                else selected[team] = !selected[team];
                _settings.ShiftTeam = selected.Any(x => x) ? FormatShiftTeams(selected) : ShiftSchedule.AllTeams;
            }
            ShiftSettingsChanged(); // 눌러서 바뀐 토글 표시도 여기서 저장값대로 되돌림
        }

        private void ShiftSettingsChanged()
        {
            UpdateShiftSettingsUi();
            SaveSettings();
            RefreshCalendar();
        }

        private void UpdateShiftSettingsUi()
        {
            ShiftDetails.Visibility = _settings.ShiftEnabled ? Visibility.Visible : Visibility.Collapsed;
            var selected = SelectedShiftTeams();
            BtnShiftTeamAll.IsChecked = selected.All(x => x);
            var teamButtons = new ToggleButton[] { BtnShiftTeamA, BtnShiftTeamB, BtnShiftTeamC, BtnShiftTeamD };
            for (int i = 0; i < teamButtons.Length; i++) teamButtons[i].IsChecked = selected[i] && !selected.All(x => x);
        }

        // ── 달력 칸 ──
        // 날짜 줄 오른쪽 끝에 근무 배지. 줄을 따로 쓰지 않으므로 일정 칸 높이는 그대로.
        // 칸이 좁으면 배지가 작아져서 날짜와 공휴일 이름을 가리지 않음
        private void AddCellHeader(Grid grid, StackPanel header, DateTime date)
        {
            UIElement headerRow = header;
            if (ShowShifts)
            {
                var selected = SelectedShiftTeams();
                int only = selected.Count(x => x) == 1 ? Array.IndexOf(selected, true) : -1;
                Border? badge = only >= 0 ? MakeSingleTeamBadge(date, only) : MakeTeamsBadge(date, selected);
                if (badge != null)
                {
                    var box = new Viewbox
                    {
                        Child = badge,
                        Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 4, 5, 2),
                    };
                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.Children.Add(header);
                    Grid.SetColumn(box, 1);
                    row.Children.Add(box);
                    headerRow = row;
                }
            }
            grid.Children.Add(headerRow); Grid.SetRow(headerRow, 0);
        }

        // A·B·C·D 네 자리 (폭 고정이라 날마다 같은 자리). 고르지 않은 조는 빈칸, API 에서 빠진 조는 "-"
        private Border MakeTeamsBadge(DateTime date, bool[] selected)
        {
            var slots = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < ShiftSchedule.Teams.Length; i++)
            {
                string? code = selected[i] ? ShiftCodeFor(date, i) : null;
                slots.Children.Add(new TextBlock
                {
                    Text = selected[i] ? code ?? "-" : "",
                    Width = 14, TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(i > 0 ? 2 : 0, 0, 0, 0),
                    FontSize = 11.5, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(code != null ? ShiftSchedule.ColorOf(code) : ShiftMissingColor),
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }
            var badge = MakeShiftGlass(slots);
            badge.ToolTip = string.Join("\n", ShiftSchedule.Teams
                .Select((t, i) => (Team: t, Index: i))
                .Where(x => selected[x.Index])
                .Select(x => ShiftSchedule.Describe(x.Team, ShiftCodeFor(date, x.Index))));
            return badge;
        }

        // 한 조 보기: "Day" · "Swing" · "Gy" · "휴무". 그날 API 에 그 조가 없으면 배지 없음
        private Border? MakeSingleTeamBadge(DateTime date, int team)
        {
            string? code = ShiftCodeFor(date, team);
            if (code == null) return null;
            var badge = MakeShiftGlass(new TextBlock
            {
                Text = ShiftSchedule.NameOf(code), FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(ShiftSchedule.ColorOf(code)), VerticalAlignment = VerticalAlignment.Center,
            });
            badge.ToolTip = ShiftSchedule.Describe(ShiftSchedule.Teams[team], code);
            return badge;
        }

        private static Border MakeShiftGlass(UIElement content) => new()
        {
            Background = Theme.IsDark ? Theme.B(18, 255, 255, 255) : Brushes.Transparent, // 라이트는 칩 없이 색 글자만
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1, 6, 2),
            Child = content,
        };

        // 일정 보기 창 제목 뒤에 붙일 근무: 전체면 " · D S G 휴", 아니면 " · B조 Swing  D조 휴무"
        private string ShiftSummary(DateTime date)
        {
            if (!ShowShifts) return "";
            var selected = SelectedShiftTeams();
            if (selected.All(x => x))
                return "  ·  " + string.Join("  ", ShiftSchedule.Teams.Select((_, i) => ShiftCodeFor(date, i) ?? "-"));
            return "  ·  " + string.Join("  ", ShiftSchedule.Teams
                .Select((t, i) => (Team: t, Index: i))
                .Where(x => selected[x.Index])
                .Select(x => ShiftSchedule.Describe(x.Team, ShiftCodeFor(date, x.Index))));
        }

        // ── 근무 API (공휴일 API 와 같은 서버) ──
        private void LoadShifts()
        {
            _shiftData.Clear();
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT date_key, team, code FROM shifts";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    if (TryParseShiftDate(reader.GetString(0), out var date) && ShiftSchedule.TeamIndex(reader.GetString(1)) is int team and >= 0)
                        ShiftSlotsFor(_shiftData, date)[team] = reader.GetString(2);
            }
            catch (Exception ex) { Debug.WriteLine($"[Shift] DB 읽기 실패: {ex.Message}"); }
        }

        private static string?[] ShiftSlotsFor(Dictionary<DateTime, string?[]> data, DateTime date)
        {
            if (!data.TryGetValue(date, out var slots)) data[date] = slots = new string?[ShiftSchedule.Teams.Length];
            return slots;
        }

        private static bool TryParseShiftDate(string? text, out DateTime date) =>
            DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

        // 올해-1 ~ 올해+1 년을 한 해씩 받아, 받은 해는 통째로 새 값으로 바꿈. 연결 실패·200 이 아닌 응답이면 그 해는 받아 둔 값 그대로
        private async Task ImportShiftsAsync()
        {
            if (_shiftFetching) return;
            _shiftFetching = true;
            _shiftFetchedOn = DateTime.Today;
            try
            {
                int thisYear = DateTime.Today.Year;
                var received = new Dictionary<int, Dictionary<DateTime, string?[]>>();
                for (int year = thisYear - 1; year <= thisYear + 1; year++)
                {
                    try
                    {
                        using var response = await _httpClient.GetAsync($"{HOLIDAY_API_BASE}/shifts/{year}");
                        if (!response.IsSuccessStatusCode) { Debug.WriteLine($"[Shift API] {year}년: HTTP {(int)response.StatusCode}"); continue; }
                        var body = JsonSerializer.Deserialize<ShiftApiResponse>(await response.Content.ReadAsStringAsync(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        var days = new Dictionary<DateTime, string?[]>();
                        foreach (var day in body?.Shifts ?? new List<ShiftApiDay>())
                        {
                            if (!TryParseShiftDate(day.Date, out var date) || date.Year != year || day.Teams == null) continue;
                            foreach (var (key, value) in day.Teams)
                            {
                                int team = ShiftSchedule.TeamIndex(key.Trim().ToUpperInvariant());
                                string code = ShiftSchedule.NormalizeCode(value);
                                if (team >= 0 && code.Length > 0) ShiftSlotsFor(days, date)[team] = code;
                            }
                        }
                        received[year] = days;
                    }
                    catch (Exception ex) { Debug.WriteLine($"[Shift API] {year}년: {ex.Message}"); } // 연결 실패·시간 초과·형식 오류
                }
                _shiftFetchOk = received.Count > 0;
                if (received.Count > 0 && ApplyShiftYears(received)) RefreshCalendar();
            }
            finally { _shiftFetching = false; }
        }

        // 받은 해의 값을 메모리·DB 에 반영. 바뀐 게 있으면 true
        private bool ApplyShiftYears(Dictionary<int, Dictionary<DateTime, string?[]>> received)
        {
            bool changed = false;
            foreach (var (year, days) in received)
            {
                var old = _shiftData.Where(x => x.Key.Year == year).ToDictionary(x => x.Key, x => x.Value);
                bool same = old.Count == days.Count && days.All(d => old.TryGetValue(d.Key, out var o) && o.SequenceEqual(d.Value));
                if (same) continue;
                changed = true;
                foreach (var date in old.Keys) _shiftData.Remove(date);
                foreach (var (date, slots) in days) _shiftData[date] = slots;
            }
            if (!changed) return false;

            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();
                foreach (var (year, days) in received)
                {
                    using (var del = conn.CreateCommand())
                    {
                        del.Transaction = tx;
                        del.CommandText = "DELETE FROM shifts WHERE date_key >= @from AND date_key <= @to";
                        del.Parameters.AddWithValue("@from", $"{year:D4}-01-01");
                        del.Parameters.AddWithValue("@to", $"{year:D4}-12-31");
                        del.ExecuteNonQuery();
                    }
                    using var ins = conn.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = "INSERT INTO shifts (date_key, team, code) VALUES (@d, @t, @c)";
                    var pd = ins.Parameters.Add("@d", SqliteType.Text);
                    var pt = ins.Parameters.Add("@t", SqliteType.Text);
                    var pc = ins.Parameters.Add("@c", SqliteType.Text);
                    foreach (var (date, slots) in days)
                        for (int i = 0; i < slots.Length; i++)
                        {
                            if (slots[i] == null) continue;
                            pd.Value = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            pt.Value = ShiftSchedule.Teams[i];
                            pc.Value = slots[i];
                            ins.ExecuteNonQuery();
                        }
                }
                tx.Commit();
            }
            catch (Exception ex) { Debug.WriteLine($"[Shift] DB 저장 실패: {ex.Message}"); }
            return true;
        }
    }
}
