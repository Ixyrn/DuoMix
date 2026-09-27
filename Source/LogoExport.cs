using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace DuoMix;
static class LogoExport
{
    public static void Write(string folder)
    {
        Directory.CreateDirectory(folder); var logo = (DrawingImage)Application.Current.FindResource("DuoMixLogo"); byte[] Png(int size) { var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) dc.DrawImage(logo, new Rect(0, 0, size, size)); var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var ms = new MemoryStream(); encoder.Save(ms); return ms.ToArray(); }
        File.WriteAllBytes(Path.Combine(folder, "DuoMix.png"), Png(1024)); int[] sizes = [16, 24, 32, 48, 64, 128, 256]; var images = sizes.Select(Png).ToArray(); using var writer = new BinaryWriter(File.Create(Path.Combine(folder, "DuoMix.ico"))); writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length); int offset = 6 + 16 * sizes.Length; for (int i = 0; i < sizes.Length; i++) { writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length; }
        foreach (var image in images) writer.Write(image);
    }
}
