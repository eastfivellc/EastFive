using System.Text;

using SkiaSharp;

using EastFive.Images;

namespace EastFive.Api.Tests.Images;

/// <summary>
/// Inputs for the image tests. Everything that names the image library lives here, so the
/// assertions in the test classes survive a change of library unmodified.
/// </summary>
internal static class ImageFixtures
{
    /// <summary>
    /// A 2x1 baseline JPEG (left pixel white, right pixel black) carrying only its JFIF header.
    /// Synthetic: written by macOS <c>sips</c> from a two-pixel PPM, other APPn segments removed.
    /// </summary>
    public const string TwoByOneJpegBase64 =
        "/9j/4AAQSkZJRgABAQAASABIAAD/wAARCAABAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9sAQwACAgICAgIDAgIDBQMDAwUGBQUFBQYIBgYGBgYICggICAgICAoKCgoKCgoKDAwMDAwMDg4ODg4PDw8PDw8PDw8P/9sAQwECAgIEBAQHBAQHEAsJCxAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQ/90ABAAB/9oADAMBAAIRAxEAPwD8f/2sf+TpvjJ/2OfiH/04z18/19AftY/8nTfGT/sc/EP/AKcZ6+f6AP/Z";

    public static byte[] TwoByOneJpeg => Convert.FromBase64String(TwoByOneJpegBase64);

    public const string DateTimeOriginal = "2024:05:06 07:08:09";

    /// <summary>
    /// <see cref="TwoByOneJpeg"/> with an Exif APP1 segment: Orientation 6 (rotate 90° to
    /// display), DateTimeOriginal <see cref="DateTimeOriginal"/>, and GPS 40°26'46"N 79°58'56"W.
    /// </summary>
    public static byte[] OrientedJpeg => WithExif(TwoByOneJpeg, orientation: 6, DateTimeOriginal,
        latitude: (40, 26, 46), latitudeRef: 'N', longitude: (79, 58, 56), longitudeRef: 'W');

    public const double Latitude = 40 + 26 / 60.0 + 46 / 3600.0;
    public const double Longitude = -(79 + 58 / 60.0 + 56 / 3600.0);

    // ---- library-specific ----

    /// <summary>An 8x4 image: the left half red, the right half blue.</summary>
    public static SKBitmap Halves()
    {
        var image = new SKBitmap(new SKImageInfo(8, 4, SKColorType.Rgba8888, SKAlphaType.Premul));
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                image.SetPixel(x, y, x < 4 ? new SKColor(255, 0, 0) : new SKColor(0, 0, 255));
        return image;
    }

    public static byte[] HalvesPng()
    {
        using var image = Halves();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static (byte R, byte G, byte B) Pixel(SKBitmap image, int x, int y)
    {
        var pixel = image.GetPixel(x, y);
        return (pixel.Red, pixel.Green, pixel.Blue);
    }

    public static string Mime(SKEncodedImageFormat format) => format.ToMimeType();

    public static (int Width, int Height, string Mime) Decode(byte[] bytes)
    {
        using var codec = SKCodec.Create(new MemoryStream(bytes));
        return (codec.Info.Width, codec.Info.Height, codec.EncodedFormat.ToMimeType());
    }

    /// <summary>Decodes without orienting, as <c>TryReadImage</c> does.</summary>
    public static SKBitmap Load(byte[] bytes) => SKBitmap.Decode(bytes);

    // ---- library-independent ----

    public static bool IsRed((byte R, byte G, byte B) p) => p.R > 200 && p.G < 60 && p.B < 60;
    public static bool IsBlue((byte R, byte G, byte B) p) => p.B > 200 && p.R < 60 && p.G < 60;
    public static bool IsLight((byte R, byte G, byte B) p) => p.R > 200 && p.G > 200 && p.B > 200;
    public static bool IsDark((byte R, byte G, byte B) p) => p.R < 60 && p.G < 60 && p.B < 60;

    /// <summary>
    /// Splices a big-endian Exif APP1 segment in after the JFIF APP0. Layout of the TIFF block:
    /// IFD0 at 8 (Orientation, ExifIFD pointer, GPS pointer), Exif IFD at 50
    /// (DateTimeOriginal), its 20-byte string at 68, GPS IFD at 88, latitude rationals at 142,
    /// longitude rationals at 166.
    /// </summary>
    public static byte[] WithExif(byte[] jpeg, ushort orientation, string dateTimeOriginal,
        (uint D, uint M, uint S) latitude, char latitudeRef,
        (uint D, uint M, uint S) longitude, char longitudeRef)
    {
        const uint ifd0 = 8, exifIfd = 50, dateAt = 68, gpsIfd = 88, latitudeAt = 142, longitudeAt = 166;
        var tiff = new List<byte>();
        void U16(int v) { tiff.Add((byte)(v >> 8)); tiff.Add((byte)v); }
        void U32(uint v) { U16((int)(v >> 16)); U16((int)(v & 0xFFFF)); }
        void Entry(int tag, int type, uint count, uint valueOrOffset) { U16(tag); U16(type); U32(count); U32(valueOrOffset); }
        void At(uint offset) { if (tiff.Count != offset) throw new InvalidOperationException($"EXIF layout drifted: {tiff.Count} != {offset}"); }

        tiff.AddRange("MM"u8.ToArray()); U16(42); U32(ifd0);
        At(ifd0);
        U16(3);
        Entry(0x0112, 3, 1, (uint)orientation << 16);
        Entry(0x8769, 4, 1, exifIfd);
        Entry(0x8825, 4, 1, gpsIfd);
        U32(0);
        At(exifIfd);
        U16(1);
        Entry(0x9003, 2, 20, dateAt);
        U32(0);
        At(dateAt);
        tiff.AddRange(Encoding.ASCII.GetBytes(dateTimeOriginal)); tiff.Add(0);
        At(gpsIfd);
        U16(4);
        Entry(0x0001, 2, 2, (uint)latitudeRef << 24);
        Entry(0x0002, 5, 3, latitudeAt);
        Entry(0x0003, 2, 2, (uint)longitudeRef << 24);
        Entry(0x0004, 5, 3, longitudeAt);
        U32(0);
        At(latitudeAt);
        foreach (var part in new[] { latitude.D, latitude.M, latitude.S }) { U32(part); U32(1); }
        At(longitudeAt);
        foreach (var part in new[] { longitude.D, longitude.M, longitude.S }) { U32(part); U32(1); }

        var payload = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var length = payload.Length + 2;
        var app1 = new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)length }.Concat(payload);

        var app0End = 4 + ((jpeg[4] << 8) | jpeg[5]);
        return jpeg.Take(app0End).Concat(app1).Concat(jpeg.Skip(app0End)).ToArray();
    }
}
