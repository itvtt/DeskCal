using System.Windows.Media;

namespace DeskCal
{
    // ──────────────────────────────────────────────
    // 4조 3교대 (A·B·C·D조). 근무는 근무 API 가 주는 값이 우선이고 (docs/shift-api.md),
    // API 값이 없는 날만 정해진 순서로 계산: 하루씩 D → S → G → 휴.
    // 기준일에 A·B·C·D조가 D·S·G·휴이고 다음 날 모두 한 칸씩 넘어감
    // ──────────────────────────────────────────────
    public static class ShiftSchedule
    {
        public static readonly string[] Teams = { "A", "B", "C", "D" };
        public const string AllTeams = "All";

        // D = Day, S = Swing, G = Gy(Graveyard), 휴 = 휴무
        public static readonly string[] Rotation = { "D", "S", "G", "휴" };

        // A조가 D 인 날 (API 값이 없는 날의 계산 기준)
        public static readonly DateTime BaseDate = new(2026, 11, 6);

        private static int Mod(long a, int n) => (int)(((a % n) + n) % n);

        // date 에 team(0=A … 3=D)이 하는 근무: 조마다 한 칸씩 앞섬
        public static string CodeFor(DateTime date, int team) =>
            Rotation[Mod((long)(date.Date - BaseDate).Days + team, Rotation.Length)];

        // API 가 준 근무 코드 정리: 앞뒤 공백을 빼고, d·s·g 같은 영문 한 글자는 대문자로
        public static string NormalizeCode(string? code)
        {
            string c = (code ?? "").Trim();
            return c.Length == 1 && c[0] is >= 'a' and <= 'z' ? c.ToUpperInvariant() : c;
        }

        public static string NameOf(string code) => code switch
        {
            "D" => "Day",
            "S" => "Swing",
            "G" => "Gy",
            "휴" => "휴무",
            _ => code,
        };

        public static string Describe(string team, string? code) => code == null ? $"{team}조 -" : $"{team}조 {NameOf(code)}";

        // 유리 칩 위의 글자색. 칩 바탕에 색을 옅게 깔면 노랑·주황이 갈색으로 탁해지므로 글자에만 씀.
        // 라이트 테마는 흰 바탕에서 읽히는 진한 쪽 (금색 D 도 호박색으로)
        public static Color ColorOf(string code) => Theme.IsDark
            ? code switch
            {
                "D" => Color.FromRgb(255, 212, 71),   // 노란 금색
                "S" => Color.FromRgb(255, 138, 76),   // 주황 (작은 글자에서도 D 와 갈리게 노랑에서 떨어뜨림)
                "G" => Color.FromRgb(140, 155, 255),  // 남보라
                "휴" => Color.FromRgb(76, 215, 135),  // 초록
                _ => Color.FromRgb(176, 184, 208),
            }
            : code switch
            {
                "D" => Color.FromRgb(184, 122, 0),
                "S" => Color.FromRgb(214, 84, 24),
                "G" => Color.FromRgb(0, 119, 188),
                "휴" => Color.FromRgb(46, 125, 30),
                _ => Color.FromRgb(113, 113, 122),
            };

        public static int TeamIndex(string? team) => Array.IndexOf(Teams, team);
    }

    // GET {HOLIDAY_API_BASE}/shifts/{year} 응답
    // { "year": 2026, "shifts": [ { "date": "2026-11-06", "teams": { "A": "D", "B": "S", "C": "G", "D": "휴" } } ] }
    public sealed class ShiftApiResponse
    {
        public int Year { get; set; }
        public List<ShiftApiDay>? Shifts { get; set; }
    }

    public sealed class ShiftApiDay
    {
        public string? Date { get; set; }
        public Dictionary<string, string?>? Teams { get; set; }
    }
}
