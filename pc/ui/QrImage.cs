using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace RigBuddy.Ui;

/** A QR code as a WPF picture: black modules on white with the quiet zone the scanners need. */
static class QrImage
{
    const int Scale = 6, Quiet = 4;

    public static BitmapSource? Of(string text)
    {
        try
        {
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
            var m = data.ModuleMatrix;
            int n = m.Count + Quiet * 2, px = n * Scale;
            var pixels = new byte[px * px];
            Array.Fill(pixels, (byte)255);
            for (int y = 0; y < m.Count; y++)
                for (int x = 0; x < m.Count; x++)
                    if (m[y][x])
                        for (int dy = 0; dy < Scale; dy++)
                            for (int dx = 0; dx < Scale; dx++)
                                pixels[((y + Quiet) * Scale + dy) * px + (x + Quiet) * Scale + dx] = 0;
            var bmp = BitmapSource.Create(px, px, 96, 96, PixelFormats.Gray8, null, pixels, px);
            bmp.Freeze();
            return bmp;
        }
        catch (Exception) { return null; }
    }
}
