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
    // 근무는 근무 API 를 공휴일처럼 받아 DB 에 두고 씀 (docs/shift-api.md). 사업장(묶음)마다 근무가 달라서
    // 설정에서 사업장을 고르고, 그 사업장의 근무만 받음. API 값이 없는 날은 그 사업장의 순서로 계산 (순서가 없으면 표시 안 함)
    // ──────────────────────────────────────────────
    public partial class MainWindow
    {
        private bool _shiftUiReady; // 설정값을 컨트롤에 채우는 동안 바뀜 이벤트를 무시

        // 고른 사업장의 근무 API 값: 날짜 → [A, B, C, D] 코드 (그날 빠진 조는 null)
        private readonly Dictionary<DateTime, string?[]> _shiftData = new();
        private string _shiftDataSite = ""; // _shiftData 가 어느 사업장 것인지
        private IReadOnlyList<ShiftSite> _shiftSites = ShiftSchedule.BuiltInSites; // API 가 준 목록, 없으면 기본 두 묶음
        private DateTime _shiftFetchedOn = DateTime.MinValue; // 마지막으로 받으려 한 날
        private bool _shiftFetchOk;  // 그때 한 해라도 받았는지 (못 받았으면 한 시간 뒤 다시)
        private bool _shiftFetching;
        private bool _shiftRefetch;  // 받는 도중에 사업장이 바뀌면 끝나고 한 번 더
        private DispatcherTimer? _shiftFetchTimer;

        private static Color ShiftMissingColor => Theme.L(Color.FromRgb(96, 104, 130));

        private bool ShowShifts => _settings.ShiftEnabled;

        private void InitShiftSettings()
        {
            ChkShiftEnabled.IsChecked = _settings.ShiftEnabled;
            _shiftSites = ParseCachedShiftSites(_settings.ShiftSites) ?? ShiftSchedule.BuiltInSites;
            RebuildShiftSiteButtons();
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

        // 저장값의 사업장. 목록에 없으면 첫 사업장
        private ShiftSite CurrentShiftSite => _shiftSites.FirstOrDefault(s => s.Id == _settings.ShiftSite) ?? _shiftSites[0];

        private string? ShiftCodeFor(DateTime date, int team) =>
            _shiftData.TryGetValue(date.Date, out var codes) ? codes[team] : CurrentShiftSite.Rule?.CodeFor(date, team);

        // API 값도 계산 순서도 없는 날은 배지를 그리지 않음
        private bool HasShiftDay(DateTime date) => _shiftData.ContainsKey(date.Date) || CurrentShiftSite.Rule != null;

        private static IReadOnlyList<ShiftSite>? ParseCachedShiftSites(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { var sites = ShiftSitesResponse.Parse(json); return sites.Count > 0 ? sites : null; }
            catch (Exception ex) { Debug.WriteLine($"[Shift] 사업장 목록 읽기 실패: {ex.Message}"); return null; }
        }

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

        // ── 사업장: 고르면 그 사업장의 받아 둔 근무로 바로 바꾸고, API 에서 새로 받음 ──
        private void RebuildShiftSiteButtons()
        {
            ShiftSiteButtons.Children.Clear();
            foreach (var site in _shiftSites)
            {
                var button = new ToggleButton { Content = site.Name, Tag = site.Id, Style = (Style)FindResource("SegmentToggle") };
                button.Click += BtnShiftSite_Click;
                ShiftSiteButtons.Children.Add(button);
            }
            ShiftSiteRow.Visibility = _shiftSites.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnShiftSite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string id } && id != CurrentShiftSite.Id)
            {
                _settings.ShiftSite = id;
                LoadShifts();
                _ = ImportShiftsAsync();
            }
            ShiftSettingsChanged(); // 이미 고른 사업장을 다시 눌러도 켜짐 표시는 그대로
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
            string site = CurrentShiftSite.Id;
            foreach (var button in ShiftSiteButtons.Children.OfType<ToggleButton>()) button.IsChecked = (string)button.Tag == site;
        }

        // ── 달력 칸 ──
        // 날짜 줄 오른쪽 끝에 근무 배지. 줄을 따로 쓰지 않으므로 일정 칸 높이는 그대로.
        // 칸이 좁으면 배지가 작아져서 날짜와 공휴일 이름을 가리지 않음
        private void AddCellHeader(Grid grid, StackPanel header, DateTime date)
        {
            UIElement headerRow = header;
            if (ShowShifts && HasShiftDay(date))
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
            if (!ShowShifts || !HasShiftDay(date)) return "";
            var selected = SelectedShiftTeams();
            if (selected.All(x => x))
                return "  ·  " + string.Join("  ", ShiftSchedule.Teams.Select((_, i) => ShiftCodeFor(date, i) ?? "-"));
            return "  ·  " + string.Join("  ", ShiftSchedule.Teams
                .Select((t, i) => (Team: t, Index: i))
                .Where(x => selected[x.Index])
                .Select(x => ShiftSchedule.Describe(x.Team, ShiftCodeFor(date, x.Index))));
        }

        // ── 근무 API (공휴일 API 와 같은 서버) ──
        // 6.2.2 까지의 shifts 표에는 사업장 칸이 없음. 그때 받은 값은 사업장 구분 없는 기본 근무라 1그룹("1")으로 옮김
        private static void MigrateShiftsTable(SqliteConnection conn)
        {
            using (var info = conn.CreateCommand())
            {
                info.CommandText = "SELECT COUNT(*) FROM pragma_table_info('shifts') WHERE name = 'site'";
                if (Convert.ToInt64(info.ExecuteScalar()) > 0) return;
            }
            using var tx = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                CREATE TABLE shifts_new (
                    site     TEXT NOT NULL DEFAULT '',
                    date_key TEXT NOT NULL,
                    team     TEXT NOT NULL,
                    code     TEXT NOT NULL,
                    PRIMARY KEY (site, date_key, team)
                );
                INSERT INTO shifts_new (site, date_key, team, code) SELECT @site, date_key, team, code FROM shifts;
                DROP TABLE shifts;
                ALTER TABLE shifts_new RENAME TO shifts;";
            cmd.Parameters.AddWithValue("@site", ShiftSchedule.BuiltInSites[0].Id);
            cmd.ExecuteNonQuery();
            tx.Commit();
        }

        private void LoadShifts()
        {
            _shiftData.Clear();
            _shiftDataSite = CurrentShiftSite.Id;
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT date_key, team, code FROM shifts WHERE site = @site";
                cmd.Parameters.AddWithValue("@site", _shiftDataSite);
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

        // 사업장 목록을 먼저 받고, 고른 사업장의 올해-1 ~ 올해+1 년을 한 해씩 받아 받은 해는 통째로 새 값으로 바꿈.
        // 연결 실패·200 이 아닌 응답이면 그 해는 받아 둔 값 그대로
        private async Task ImportShiftsAsync()
        {
            if (_shiftFetching) { _shiftRefetch = true; return; }
            _shiftFetching = true;
            try
            {
                do
                {
                    _shiftRefetch = false;
                    _shiftFetchedOn = DateTime.Today;
                    await ImportShiftSitesAsync();
                    string site = CurrentShiftSite.Id;
                    var received = await FetchShiftYearsAsync(site);
                    _shiftFetchOk = received.Count > 0;
                    if (received.Count > 0 && ApplyShiftYears(site, received)) RefreshCalendar();
                } while (_shiftRefetch && _settings.ShiftEnabled);
            }
            finally { _shiftFetching = false; }
        }

        // GET /shifts/sites. 200 이면 그 목록으로 (빈 목록이면 기본 두 묶음), 404 면 서버에 사업장 구분이 없으니 기본 두 묶음,
        // 연결 실패·그 밖의 응답이면 받아 둔 목록 그대로
        private async Task ImportShiftSitesAsync()
        {
            string json;
            try
            {
                using var response = await _httpClient.GetAsync($"{HOLIDAY_API_BASE}/shifts/sites");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound) json = "";
                else if (!response.IsSuccessStatusCode) { Debug.WriteLine($"[Shift API] 사업장: HTTP {(int)response.StatusCode}"); return; }
                else
                {
                    json = await response.Content.ReadAsStringAsync();
                    if (ShiftSitesResponse.Parse(json).Count == 0) json = "";
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Shift API] 사업장: {ex.Message}"); return; }

            if (json == _settings.ShiftSites) return;
            string before = CurrentShiftSite.Id;
            _settings.ShiftSites = json;
            _shiftSites = ParseCachedShiftSites(json) ?? ShiftSchedule.BuiltInSites;
            SaveSettings();
            RebuildShiftSiteButtons();
            UpdateShiftSettingsUi();
            if (CurrentShiftSite.Id != before) LoadShifts();
            RefreshCalendar(); // 이름·계산 순서가 바뀌었을 수 있음
        }

        private async Task<Dictionary<int, Dictionary<DateTime, string?[]>>> FetchShiftYearsAsync(string site)
        {
            int thisYear = DateTime.Today.Year;
            var received = new Dictionary<int, Dictionary<DateTime, string?[]>>();
            for (int year = thisYear - 1; year <= thisYear + 1; year++)
            {
                try
                {
                    using var response = await _httpClient.GetAsync($"{HOLIDAY_API_BASE}/shifts/{year}?site={Uri.EscapeDataString(site)}");
                    if (!response.IsSuccessStatusCode) { Debug.WriteLine($"[Shift API] {site} {year}년: HTTP {(int)response.StatusCode}"); continue; }
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
                catch (Exception ex) { Debug.WriteLine($"[Shift API] {site} {year}년: {ex.Message}"); } // 연결 실패·시간 초과·형식 오류
            }
            return received;
        }

        // 받은 해의 값을 DB 에, 지금 보고 있는 사업장이면 메모리에도 반영. 화면이 바뀌어야 하면 true
        private bool ApplyShiftYears(string site, Dictionary<int, Dictionary<DateTime, string?[]>> received)
        {
            if (site != _shiftDataSite) { SaveShiftYears(site, received); return false; } // 받는 도중 사업장이 바뀜

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
            SaveShiftYears(site, received);
            return true;
        }

        private void SaveShiftYears(string site, Dictionary<int, Dictionary<DateTime, string?[]>> received)
        {
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
                        del.CommandText = "DELETE FROM shifts WHERE site = @s AND date_key >= @from AND date_key <= @to";
                        del.Parameters.AddWithValue("@s", site);
                        del.Parameters.AddWithValue("@from", $"{year:D4}-01-01");
                        del.Parameters.AddWithValue("@to", $"{year:D4}-12-31");
                        del.ExecuteNonQuery();
                    }
                    using var ins = conn.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = "INSERT INTO shifts (site, date_key, team, code) VALUES (@s, @d, @t, @c)";
                    ins.Parameters.AddWithValue("@s", site);
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
        }
    }
}
