using System.Windows.Media;

namespace DeskCal
{
    // ──────────────────────────────────────────────
    // 4조 교대 (A·B·C·D조). 사업장(묶음)마다 근무가 달라서 사업장을 골라 씀.
    // 근무는 근무 API 가 주는 값이 우선이고 (docs/shift-api.md), API 값이 없는 날만 그 사업장의 순서(ShiftRule)로 계산
    // ──────────────────────────────────────────────
    public static class ShiftSchedule
    {
        public static readonly string[] Teams = { "A", "B", "C", "D" };
        public const string AllTeams = "All";

        // 기본 순서: 하루씩 D → S → G → 휴 (D = Day, S = Swing, G = Gy(Graveyard), 휴 = 휴무).
        // 2026-11-06 에 A·B·C·D조가 D·S·G·휴이고 다음 날 모두 한 칸씩 넘어감
        public static readonly ShiftRule DefaultRule = new(new DateTime(2026, 11, 6), new[] { "D", "S", "G", "휴" }, new[] { 0, 1, 2, 3 });

        // 근무 API 가 사업장 목록(/shifts/sites)을 주지 않을 때 쓰는 두 묶음. 2그룹은 계산 순서가 없어 API 값만 표시
        public static readonly IReadOnlyList<ShiftSite> BuiltInSites = new[]
        {
            new ShiftSite("1", "1그룹", DefaultRule),
            new ShiftSite("2", "2그룹", null),
        };

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

    // 사업장(묶음). Rule 이 없으면 API 값이 없는 날은 근무를 표시하지 않음
    public sealed record ShiftSite(string Id, string Name, ShiftRule? Rule);

    // API 값이 없는 날의 계산: n = (날짜 - BaseDate) 일수, 조 k 의 근무 = Rotation[(Start[k] + n) mod 길이]
    public sealed class ShiftRule
    {
        public DateTime BaseDate { get; }
        public string[] Rotation { get; }
        public int[] Start { get; } // 기준일에 A·B·C·D조가 서 있는 Rotation 칸

        public ShiftRule(DateTime baseDate, string[] rotation, int[] start)
        {
            BaseDate = baseDate.Date;
            Rotation = rotation;
            Start = start;
        }

        private static int Mod(long a, int n) => (int)(((a % n) + n) % n);

        public string CodeFor(DateTime date, int team) =>
            Rotation[Mod((long)(date.Date - BaseDate).Days + Start[team], Rotation.Length)];

        // API 의 rule. start 를 빼면 순서 길이의 1/4 칸씩 앞섬 (길이 4 면 A 0, B 1, C 2, D 3). 형식이 틀리면 null
        public static ShiftRule? FromApi(ShiftApiRule? rule)
        {
            if (rule == null) return null;
            if (!DateTime.TryParseExact(rule.BaseDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var baseDate)) return null;
            var rotation = (rule.Rotation ?? new List<string?>()).Select(ShiftSchedule.NormalizeCode).ToArray();
            if (rotation.Length == 0 || rotation.Any(c => c.Length == 0)) return null;

            var start = new int[ShiftSchedule.Teams.Length];
            if (rule.Start == null || rule.Start.Count == 0)
            {
                if (rotation.Length % ShiftSchedule.Teams.Length != 0) return null;
                for (int k = 0; k < start.Length; k++) start[k] = k * rotation.Length / ShiftSchedule.Teams.Length;
            }
            else
            {
                var given = new bool[start.Length];
                foreach (var (key, value) in rule.Start)
                {
                    int k = ShiftSchedule.TeamIndex(key.Trim().ToUpperInvariant());
                    if (k < 0) continue;
                    start[k] = Mod(value, rotation.Length);
                    given[k] = true;
                }
                if (given.Any(x => !x)) return null;
            }
            return new ShiftRule(baseDate, rotation, start);
        }
    }

    // GET {HOLIDAY_API_BASE}/shifts/sites 응답
    // { "sites": [ { "id": "1", "name": "1그룹", "rule": { "baseDate": "2026-11-06", "rotation": ["D","S","G","휴"] } } ] }
    public sealed class ShiftSitesResponse
    {
        public List<ShiftApiSite>? Sites { get; set; }

        // 형식이 맞는 사업장만, id 가 겹치면 앞의 것. 하나도 없으면 빈 목록
        public static List<ShiftSite> Parse(string json)
        {
            var body = System.Text.Json.JsonSerializer.Deserialize<ShiftSitesResponse>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var sites = new List<ShiftSite>();
            foreach (var site in body?.Sites ?? new List<ShiftApiSite>())
            {
                string id = (site.Id ?? "").Trim();
                if (id.Length == 0 || sites.Any(s => s.Id == id)) continue;
                string name = string.IsNullOrWhiteSpace(site.Name) ? id : site.Name.Trim();
                sites.Add(new ShiftSite(id, name, ShiftRule.FromApi(site.Rule)));
            }
            return sites;
        }
    }

    public sealed class ShiftApiSite
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public ShiftApiRule? Rule { get; set; }
    }

    public sealed class ShiftApiRule
    {
        public string? BaseDate { get; set; }
        public List<string?>? Rotation { get; set; }
        public Dictionary<string, int>? Start { get; set; }
    }

    // GET {HOLIDAY_API_BASE}/shifts/{year}?site={id} 응답
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
