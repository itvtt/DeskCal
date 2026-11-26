using System.Globalization;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;

namespace DeskCal
{
    // ──────────────────────────────────────────────
    // 같은 달력 데이터(C:\Deskcal\deskcal.db)를 DeskCal 과 PARA 의 달력 탭이 함께 씀.
    // 쓸 때마다 settings 의 DataRevision 을 하나 올리고, 2초마다 그 값을 보아 다른 앱이 올렸으면 다시 읽음 (Changed).
    // 파일 감시는 열려 있는 DB 파일의 바뀜을 늦게 알려 줘서 쓰지 않음 (값 하나 읽기라 가벼움).
    // PARA 에도 같은 파일이 있음 (PARA\DeskCal\CalendarSync.cs): 고치면 둘 다
    // ──────────────────────────────────────────────
    internal sealed class CalendarSync
    {
        public const string RevisionKey = "DataRevision";

        private readonly string _connectionString;
        private readonly DispatcherTimer _timer;

        /// <summary>이 앱이 마지막으로 읽었거나 쓴 판</summary>
        public long Known { get; set; }

        /// <summary>다른 앱이 바꿈 (UI 스레드). 받은 쪽이 다시 읽고 Known 을 그 판으로</summary>
        public event Action<long>? Changed;

        /// <summary>true 인 동안은 다시 읽지 않고 다음 차례로 미룸 (일정을 끌어 옮기는 중)</summary>
        public Func<bool>? Busy { get; set; }

        public CalendarSync(string connectionString, Dispatcher dispatcher)
        {
            _connectionString = connectionString;
            _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += (s, e) => CheckNow();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        public void CheckNow()
        {
            if (Busy?.Invoke() == true) return;
            long stored;
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                stored = Read(conn, null);
            }
            catch { return; } // 다른 앱이 쓰는 중 · 파일 없음: 다음 차례에
            if (stored != Known) Changed?.Invoke(stored);
        }

        public long ReadNow()
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                return Read(conn, null);
            }
            catch { return Known; }
        }

        public static long Read(SqliteConnection conn, SqliteTransaction? tx)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT value FROM settings WHERE key = @k";
            cmd.Parameters.AddWithValue("@k", RevisionKey);
            return long.TryParse(cmd.ExecuteScalar() as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : 0;
        }

        /// <summary>쓰는 트랜잭션 안에서 판을 하나 올림. Stale = 이 앱이 읽은 뒤에 다른 앱이 바꿨음 (커밋한 뒤 다시 읽어야 함)</summary>
        public (long Revision, bool Stale) Bump(SqliteConnection conn, SqliteTransaction tx)
        {
            long stored = Read(conn, tx);
            long next = stored + 1;
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES (@k, @v)";
            cmd.Parameters.AddWithValue("@k", RevisionKey);
            cmd.Parameters.AddWithValue("@v", next.ToString(CultureInfo.InvariantCulture));
            cmd.ExecuteNonQuery();
            return (next, stored != Known);
        }
    }
}
