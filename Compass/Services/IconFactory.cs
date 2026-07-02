using System.Drawing;
using System.Drawing.Drawing2D;

namespace Compass.Services;

/// <summary>Builds the tray/app icon at runtime so no binary asset file is needed.</summary>
public static class IconFactory
{
    public static Icon TrayIcon()
    {
        var bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var bg = new SolidBrush(ColorTranslator.FromHtml("#4F8CFF"));
            g.FillEllipse(bg, 1, 1, 30, 30);

            // A little compass needle.
            using var needle = new SolidBrush(Color.White);
            var north = new[]
            {
                new PointF(16, 6), new PointF(20, 16), new PointF(16, 14), new PointF(12, 16)
            };
            var south = new[]
            {
                new PointF(16, 26), new PointF(12, 16), new PointF(16, 18), new PointF(20, 16)
            };
            g.FillPolygon(needle, north);
            using var south2 = new SolidBrush(ColorTranslator.FromHtml("#BBD3FF"));
            g.FillPolygon(south2, south);
        }

        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
