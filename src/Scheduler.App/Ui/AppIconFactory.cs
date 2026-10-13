using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace Scheduler.App.Ui;

internal static class AppIconFactory
{
    private static Icon? _notifyIcon;
    private static BitmapImage? _windowIcon;

    public static Icon NotifyIcon => _notifyIcon ??= CreateNotifyIcon();

    public static BitmapImage WindowIcon => _windowIcon ??= CreateWindowIcon();

    private static Icon CreateNotifyIcon()
    {
        using var bmp = CreateBitmap(32);
        using var handleIcon = Icon.FromHandle(bmp.GetHicon());
        return (Icon)handleIcon.Clone();
    }

    private static BitmapImage CreateWindowIcon()
    {
        using var bmp = CreateBitmap(32);
        using var stream = new MemoryStream();
        bmp.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static Bitmap CreateBitmap(int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(37, 99, 235));
        g.FillEllipse(brush, 1, 1, size - 2, size - 2);
        using var font = new Font("Segoe UI", size * 0.45f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("S", font, Brushes.White, new RectangleF(0, 1, size, size), sf);
        return bmp;
    }
}
