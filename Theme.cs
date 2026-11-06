using System.Windows.Media;

namespace DeskCal
{
    // ──────────────────────────────────────────────
    // 라이트 / 다크 테마. 화면의 색은 모두 다크 테마 기준으로 적혀 있고, 라이트일 때 L() 이 같은 역할의 색으로 바꿈:
    //   · 흰 유리(반투명 흰색) → 같은 세기의 어두운 유리
    //   · 무채색(배경·글자) → 밝기를 뒤집음 (어두운 바탕 → 밝은 바탕, 밝은 글자 → 어두운 글자)
    //   · 진한 강조색(하늘·초록·보라·금색 …) → 흰 바탕에서 읽히도록 어둡게
    // 색 바탕 위의 흰 글자처럼 두 테마에서 같아야 하는 곳은 L() 을 거치지 않음
    // ──────────────────────────────────────────────
    public static class Theme
    {
        public static bool IsDark { get; set; } = true;

        public static Color L(Color c) => IsDark ? c : ToLight(c);
        // 자동 변환이 맞지 않는 곳(달력 칸 종이처럼 라이트에서 오히려 바탕보다 밝아야 하는 면)은 두 값을 직접
        public static SolidColorBrush Pick(Color dark, Color light) => new(IsDark ? dark : light);
        public static SolidColorBrush B(Color c) => new(L(c));
        public static SolidColorBrush B(byte r, byte g, byte b) => B(Color.FromRgb(r, g, b));
        public static SolidColorBrush B(byte a, byte r, byte g, byte b) => B(Color.FromArgb(a, r, g, b));

        public static Color ToLight(Color c)
        {
            byte min = Math.Min(c.R, Math.Min(c.G, c.B)), max = Math.Max(c.R, Math.Max(c.G, c.B));
            // 라이트 팔레트 = 흰색·무채색 회색(푸른 기 없음) + 먹색 + 호박색(#FDB522 / #FF9805, 처음 아이콘 색) 강조. 보라를 쓰지 않음
            if (c.A < 255)
            {
                if (min >= 200) return Color.FromArgb((byte)Math.Min(255, c.A * 0.55), 0, 0, 0); // 흰 유리 → 옅은 회색 유리
                if (max <= 40) return Color.FromArgb((byte)(c.A * 0.5), c.R, c.G, c.B);       // 그림자는 옅게
            }
            ToHsl(c, out double h, out double s, out double l);
            // 무채색 판단은 채도(HSL) 대신 색 폭(max - min)으로: 아주 밝은 연보라 흰색도 HSL 채도는 높게 나옴
            bool neutral = max - min < 64;
            if (neutral)
            {
                // 다크의 바탕·테두리(밝기 0.34 아래)는 흰색~옅은 회색으로, 다크에서 층이 나뉘던 만큼 밝기 차이를 넓게.
                // 그 위는 글자·아이콘 → 먹색·회색 글자 (#202020 ~ #4D4D4D ~ #767676)
                if (l < 0.34) return FromHsl(c.A, 0, 0, Math.Clamp(1.0 - (l - 0.04) * 0.45, 0.86, 1.0));
                return FromHsl(c.A, 0, 0, Math.Clamp(1 - l, 0.12, 0.50));
            }
            if (c.A < 255) // 강조색을 옅게 깐 바탕은 흰 바탕에서도 은은함. 하늘·파랑·보라 기운은 호박색으로
                return h >= 185 && h <= 300 ? Color.FromArgb(c.A, 255, 152, 5) : c;
            // 하늘·파랑·보라 강조색은 라이트의 대표색인 호박색·주황으로. 글자로 쓰여도 갈색이 되지 않게 대비 3.3 까지만 진하게
            if (h >= 185 && h <= 300) return Readable(c.A, 32, 1.0, Math.Min(l, 0.5), 3.3);
            return Readable(c.A, h, s, l, 4.5);
        }

        // 사용자가 고른 일정 색(다크 테마 기준의 밝은 색)을 라이트 바탕에서 읽히게: 같은 색 계열로 진하게
        public static Color TextOn(Color c)
        {
            if (IsDark) return c;
            ToHsl(c, out double h, out double s, out double l);
            if (s < 0.2) return FromHsl(255, h, s, Math.Min(l, 0.25)); // 회색·흰색 계열은 거의 검정
            return Readable(255, h, Math.Max(s, 0.6), l, 4.5);
        }

        // 흰 바탕 위 대비(WCAG)가 target 이 될 때까지 같은 색상·채도로 밝기만 낮춤. 노랑처럼 밝은 색일수록 더 내려감
        private static Color Readable(byte a, double h, double s, double l, double target)
        {
            l = Math.Min(l, 0.55);
            Color c = FromHsl(a, h, s, l);
            while (ContrastOnWhite(c) < target && l > 0.2) { l -= 0.01; c = FromHsl(a, h, s, l); }
            return c;
        }

        public static double ContrastOnWhite(Color c)
        {
            static double Ch(byte v) { double x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
            double lum = 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
            return 1.05 / (lum + 0.05);
        }

        // 연속 일정 막대: 다크는 일정 색을 어둡게 깐 막대 + 그 색 글자, 라이트는 일정 색을 옅게 깐 막대 + 진한 글자
        public static Color RunBar(Color c) => IsDark
            ? Color.FromRgb((byte)(c.R * 0.34), (byte)(c.G * 0.34), (byte)(c.B * 0.34))
            : Color.FromRgb(Mix(c.R, 255, 0.78), Mix(c.G, 255, 0.78), Mix(c.B, 255, 0.78));

        public static Color RunText(Color c)
        {
            if (!IsDark) return TextOn(c);
            double lum = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            if (lum >= 110) return c;
            return Color.FromRgb((byte)Math.Min(255, c.R + 95), (byte)Math.Min(255, c.G + 95), (byte)Math.Min(255, c.B + 95));
        }

        private static byte Mix(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);

        private static void ToHsl(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            l = (max + min) / 2;
            if (d < 1e-9) { h = 0; s = 0; return; }
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
        }

        private static Color FromHsl(byte a, double h, double s, double l)
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            double Hue(double t)
            {
                t = (t % 1 + 1) % 1;
                if (t < 1 / 6.0) return p + (q - p) * 6 * t;
                if (t < 0.5) return q;
                if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0 - t) * 6;
                return p;
            }
            double hk = h / 360;
            return Color.FromArgb(a, (byte)Math.Round(Hue(hk + 1 / 3.0) * 255), (byte)Math.Round(Hue(hk) * 255), (byte)Math.Round(Hue(hk - 1 / 3.0) * 255));
        }
    }
}
