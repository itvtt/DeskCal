using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskCal
{
    // ──────────────────────────────────────────────
    // 아이폰 캘린더처럼 오늘 요일·날짜를 그린 앱 아이콘 (창·작업 표시줄·트레이). 날이 바뀌면 다시 그림.
    // 흰 타일 위에 주황 요일 + 큰 날짜. 작은 크기(32 이하)는 요일 글씨 대신 짧은 주황 막대, 숫자는 굵게.
    // exe·설치 프로그램 아이콘(cal.ico)은 파일이라 바뀌지 않으므로 build\Make-Icon.ps1 이 같은 모양으로 고정 날짜를 그림
    // ──────────────────────────────────────────────
    internal static class AppIcon
    {
        private static readonly string[] Weekdays = { "일요일", "월요일", "화요일", "수요일", "목요일", "금요일", "토요일" };
        private static readonly Color Accent = Color.FromRgb(255, 149, 0);   // iOS 주황
        private static readonly Color Ink = Color.FromRgb(29, 29, 31);
        private static readonly FontFamily Pretendard = new(new Uri("pack://application:,,,/"), "./Fonts/#Pretendard");
        private static readonly FontFamily SegoeUi = new("Segoe UI");
        private static readonly int[] Sizes = { 256, 64, 48, 40, 32, 24, 20, 16 };

        public static BitmapSource Render(DateTime date, int s)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var accent = new SolidColorBrush(Accent);
                var ink = new SolidColorBrush(Ink);
                string day = date.Day.ToString();
                if (s <= 32)
                {
                    // 작은 크기: 타일 꽉 채움 + 위쪽 주황 막대 + 굵은 날짜
                    double inset = s >= 32 ? 1 : 0, r = Math.Round(s * 0.22, 1);
                    dc.DrawRoundedRectangle(new LinearGradientBrush(Colors.White, Color.FromRgb(240, 240, 242), 90),
                        new Pen(new SolidColorBrush(Color.FromRgb(196, 196, 201)), 1),
                        new Rect(inset + 0.5, inset + 0.5, s - 2 * inset - 1, s - 2 * inset - 1), r, r);
                    double barW = Math.Round(s * 0.40), barH = s >= 24 ? 3 : 2, barY = Math.Round(s * 0.12);
                    dc.DrawRoundedRectangle(accent, null, new Rect(Math.Round((s - barW) / 2), barY, barW, barH), barH / 2, barH / 2);
                    double top = barY + barH + 1, bottom = s - Math.Max(2, Math.Round(s * 0.1));
                    var weight = s >= 24 ? FontWeights.SemiBold : FontWeights.Bold;
                    DrawFitted(dc, day, SegoeUi, weight, Math.Round(s * (day.Length == 1 ? 0.50 : 0.44)), 0, top, s, bottom - top, ink, snap: true);
                }
                else
                {
                    double k = s / 256.0, inset = 10 * k, r = 54 * k;
                    if (s >= 64)
                        for (int g = 6; g >= 1; g--)
                        {
                            double o = g * 1.5 * k;
                            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(8, 0, 0, 0)), null,
                                new Rect(inset - o * 0.2, inset + o * 0.9, s - 2 * inset + o * 0.4, s - 2 * inset), r + o * 0.5, r + o * 0.5);
                        }
                    dc.DrawRoundedRectangle(new LinearGradientBrush(Colors.White, Color.FromRgb(241, 241, 243), 90),
                        new Pen(new SolidColorBrush(s >= 64 ? Color.FromArgb(26, 0, 0, 0) : Color.FromRgb(200, 200, 205)), Math.Max(1, 1.5 * k)),
                        new Rect(inset, inset, s - 2 * inset, s - 2 * inset), r, r);
                    // 요일 (주황) + 날짜. 큰 크기일수록 숫자를 얇게
                    DrawFitted(dc, Weekdays[(int)date.DayOfWeek], Pretendard, FontWeights.SemiBold, 27 * k, 0, 32 * k, s, 40 * k, accent, snap: false);
                    var numWeight = s >= 96 ? FontWeights.Light : s >= 48 ? FontWeights.Normal : FontWeights.SemiBold;
                    DrawFitted(dc, day, SegoeUi, numWeight, (day.Length == 1 ? 118 : 112) * k, 0, 82 * k, s, 150 * k, ink, snap: false);
                }
            }
            var rtb = new RenderTargetBitmap(s, s, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        // 글자 윤곽 높이가 정확히 height 가 되도록 크기를 맞춰 가운데에 그림 (작은 크기는 픽셀에 맞춤)
        private static void DrawFitted(DrawingContext dc, string text, FontFamily font, FontWeight weight, double height,
            double x, double y, double w, double h, Brush brush, bool snap)
        {
            var tf = new Typeface(font, FontStyles.Normal, weight, FontStretches.Normal);
            var probe = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 100, brush, 1.0);
            double probeH = probe.BuildGeometry(new Point(0, 0)).Bounds.Height;
            if (probeH <= 0) return;
            var ft = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 100 * height / probeH, brush, 1.0);
            var geo = ft.BuildGeometry(new Point(0, 0));
            var bb = geo.Bounds;
            double dx = x + (w - bb.Width) / 2 - bb.X, dy = y + (h - bb.Height) / 2 - bb.Y;
            if (snap) { dx = Math.Round(dx); dy = Math.Round(dy); }
            geo.Transform = new TranslateTransform(dx, dy);
            dc.DrawGeometry(brush, null, geo);
        }

        // 여러 크기를 담은 .ico (32비트 BMP 프레임: System.Drawing.Icon 과 WPF 둘 다 읽음)
        public static byte[] BuildIco(DateTime date)
        {
            var images = Sizes.Select(s => Dib(Render(date, s), s)).ToList();
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((short)0); w.Write((short)1); w.Write((short)Sizes.Length);
            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                byte dim = (byte)(Sizes[i] >= 256 ? 0 : Sizes[i]);
                w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32); w.Write(images[i].Length); w.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
            w.Flush();
            return ms.ToArray();
        }

        private static byte[] Dib(BitmapSource bmp, int s)
        {
            var straight = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            int stride = s * 4;
            var px = new byte[stride * s];
            straight.CopyPixels(px, stride, 0);
            int maskStride = (s + 31) / 32 * 4;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(40); w.Write(s); w.Write(s * 2); w.Write((short)1); w.Write((short)32);
            w.Write(0); w.Write(stride * s + maskStride * s); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = s - 1; y >= 0; y--) w.Write(px, y * stride, stride); // 아래에서 위로
            for (int y = s - 1; y >= 0; y--)
            {
                var row = new byte[maskStride];
                for (int x = 0; x < s; x++) if (px[y * stride + x * 4 + 3] == 0) row[x / 8] |= (byte)(0x80 >> (x % 8));
                w.Write(row);
            }
            w.Flush();
            return ms.ToArray();
        }
    }
}
