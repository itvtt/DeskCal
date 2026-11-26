using System.Windows.Threading;
using Microsoft.Data.Sqlite;

namespace DeskCal
{
    // ──────────────────────────────────────────────
    // PARA 의 달력 탭과 같은 데이터를 함께 씀 (CalendarSync.cs).
    // · 일정: 저장할 때 다른 앱이 그사이 바꿨으면 이 앱이 고친 날만 이 앱 것으로, 나머지 날은 DB 것으로 합쳐 씀 (서로 지우지 않음)
    // · 일정 · 공휴일 · 반복 일정 · 교대근무: 다른 앱이 바꾸면 2초 안에 다시 읽어 그림
    // · 설정은 교대근무(켜기 · 조 · 사업장)만 함께 씀. 창 · 보기 설정은 앱마다 따로
    // ──────────────────────────────────────────────
    public partial class MainWindow
    {
        internal static readonly string[] SharedSettingKeys = { "ShiftEnabled", "ShiftTeam", "ShiftSite", "ShiftSites" };

        private CalendarSync _sync = null!;
        private Dictionary<string, List<MemoItem>> _memoSnapshot = new(); // 마지막으로 DB 와 같았던 일정
        private Dictionary<string, string> _sharedLoaded = new();         // 함께 쓰는 설정의 마지막으로 DB 와 같았던 값

        private void CreateSync()
        {
            _sync = new CalendarSync(ConnectionString, Dispatcher) { Busy = () => _isDragging };
            _sync.Known = _sync.ReadNow();
            _sync.Changed += _ => ReloadSharedData();
        }

        // 다른 앱이 바꾼 것을 다시 읽어 그림
        private void ReloadSharedData()
        {
            long revision = _sync.ReadNow();
            LoadHolidays();
            LoadData();
            LoadMacros();
            ReloadSharedSettings();
            LoadShifts();
            _sync.Known = revision;
            RefreshCalendar();
        }

        // 쓰기를 마친 트랜잭션: 판을 올리고 커밋. 다른 앱이 그사이 바꿨으면 그것도 다시 읽음
        private void CommitShared(SqliteConnection conn, SqliteTransaction tx)
        {
            var (revision, stale) = _sync.Bump(conn, tx);
            tx.Commit();
            _sync.Known = revision;
            if (stale) Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ReloadSharedData));
        }

        private static Dictionary<string, List<MemoItem>> ReadMemos(SqliteConnection conn, SqliteTransaction? tx)
        {
            var data = new Dictionary<string, List<MemoItem>>();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
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
                if (!data.TryGetValue(dateKey, out var list)) data[dateKey] = list = new List<MemoItem>();
                list.Add(item);
            }
            return data;
        }

        private static Dictionary<string, List<MemoItem>> CloneMemos(Dictionary<string, List<MemoItem>> data) =>
            data.ToDictionary(kv => kv.Key, kv => kv.Value.Select(m => m.Clone()).ToList());

        private static string MemoSignature(List<MemoItem>? list) => list == null || list.Count == 0 ? ""
            : string.Join("\u001f", list.Select(m => $"{m.Text}\u001e{(m.IsDone ? 1 : 0)}\u001e{m.TextColor}\u001e{(m.IsMacro ? 1 : 0)}\u001e{m.MacroId}"));

        // 다른 앱이 그사이 바꾼 DB 일정(stored)에, 이 앱이 읽은 뒤 고친 날만 이 앱 것으로 덮음
        internal static Dictionary<string, List<MemoItem>> MergeMemos(
            Dictionary<string, List<MemoItem>> mine, Dictionary<string, List<MemoItem>> snapshot, Dictionary<string, List<MemoItem>> stored)
        {
            foreach (var key in mine.Keys.Union(snapshot.Keys).ToList())
            {
                mine.TryGetValue(key, out var now);
                snapshot.TryGetValue(key, out var before);
                if (MemoSignature(now) == MemoSignature(before)) continue; // 이 앱은 이 날을 고치지 않음
                if (now == null || now.Count == 0) stored.Remove(key);
                else stored[key] = now;
            }
            return stored;
        }

        // ── 함께 쓰는 설정 (교대근무) ──
        private string GetSharedSetting(string key) => key switch
        {
            "ShiftEnabled" => _settings.ShiftEnabled ? "1" : "0",
            "ShiftTeam" => _settings.ShiftTeam,
            "ShiftSite" => _settings.ShiftSite,
            "ShiftSites" => _settings.ShiftSites,
            _ => ""
        };

        private void SetSharedSetting(string key, string value)
        {
            switch (key)
            {
                case "ShiftEnabled": _settings.ShiftEnabled = value == "1"; break;
                case "ShiftTeam": if (ParseShiftTeams(value) != null) _settings.ShiftTeam = value; break;
                case "ShiftSite": _settings.ShiftSite = value; break;
                case "ShiftSites": _settings.ShiftSites = value; break;
            }
        }

        private Dictionary<string, string> CurrentSharedSettings() => SharedSettingKeys.ToDictionary(k => k, GetSharedSetting);

        private static Dictionary<string, string> ReadSharedSettings(SqliteConnection conn, SqliteTransaction? tx)
        {
            var values = new Dictionary<string, string>();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT key, value FROM settings WHERE key IN ('ShiftEnabled', 'ShiftTeam', 'ShiftSite', 'ShiftSites')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) values[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
            return values;
        }

        // 저장하기 전: 이 앱이 고치지 않은 함께 쓰는 설정은 DB 값(다른 앱이 바꾼 값)을 따름. 반환 = 함께 쓰는 설정을 이 앱이 바꿈
        private bool MergeSharedSettings(SqliteConnection conn, SqliteTransaction tx, out bool adopted)
        {
            adopted = false;
            bool changed = false;
            var stored = ReadSharedSettings(conn, tx);
            foreach (var key in SharedSettingKeys)
            {
                string mine = GetSharedSetting(key);
                _sharedLoaded.TryGetValue(key, out var loaded);
                if (!stored.TryGetValue(key, out var inDb) || inDb == mine) continue;
                if (mine == (loaded ?? mine)) { SetSharedSetting(key, inDb); adopted = true; } // 이 앱은 안 고침 → 다른 앱 값
                else changed = true;
            }
            return changed || SharedSettingKeys.Any(k => !stored.ContainsKey(k));
        }

        private void ReloadSharedSettings()
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                foreach (var (key, value) in ReadSharedSettings(conn, null)) SetSharedSetting(key, value);
            }
            catch { return; }
            _sharedLoaded = CurrentSharedSettings();
            ApplySharedSettingsUi();
        }

        // 교대근무 설정 칸을 지금 값으로 (바뀜 이벤트로 다시 저장하지 않게)
        private void ApplySharedSettingsUi()
        {
            _shiftUiReady = false;
            ChkShiftEnabled.IsChecked = _settings.ShiftEnabled;
            _shiftSites = ParseCachedShiftSites(_settings.ShiftSites) ?? ShiftSchedule.BuiltInSites;
            RebuildShiftSiteButtons();
            UpdateShiftSettingsUi();
            _shiftUiReady = true;
        }
    }
}
