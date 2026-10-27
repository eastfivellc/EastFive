using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

using Xunit;

using EastFive.Images;

using static EastFive.Api.Tests.Images.ImageFixtures;

namespace EastFive.Api.Tests.Images;

/// <summary>
/// Characterizes EastFive.Core's image extensions (decode, encode, data URIs, resize/crop/aspect,
/// EXIF) so the image library behind them can be swapped with the assertions unchanged.
/// </summary>
public class ImageExtensionsTests
{
    // ---- decode ----

    [Fact]
    public void TryReadImage_Png_ReadsSizeAndFormat()
    {
        Assert.True(HalvesPng().TryReadImage(out Image image, out IImageFormat format));
        using (image)
        {
            Assert.Equal((8, 4), (image.Width, image.Height));
            Assert.Equal("image/png", Mime(format));
        }
    }

    [Fact]
    public void TryReadImage_Jpeg_ReadsSizeAndFormat()
    {
        Assert.True(TwoByOneJpeg.TryReadImage(out Image image, out IImageFormat format));
        using (image)
        {
            Assert.Equal((2, 1), (image.Width, image.Height));
            Assert.Equal("image/jpeg", Mime(format));
            Assert.True(IsLight(Pixel(image, 0, 0)));
            Assert.True(IsDark(Pixel(image, 1, 0)));
        }
    }

    [Fact]
    public void TryReadImage_NotAnImage_IsFalse()
    {
        Assert.False("not an image at all"u8.ToArray().TryReadImage(out Image _, out IImageFormat _));
    }

    [Fact]
    public async Task TryReadImageAsync_Stream_Decodes()
    {
        using var stream = new MemoryStream(HalvesPng());

        var (success, image, format) = await stream.TryReadImageAsync();

        using (image)
        {
            Assert.True(success);
            Assert.Equal((8, 4), (image.Width, image.Height));
            Assert.Equal("image/png", Mime(format));
        }
    }

    [Fact]
    public async Task TryReadImageAsync_Callbacks_Decodes()
    {
        var size = await HalvesPng().TryReadImageAsync(
            (image, format) => { using (image) return (image.Width, image.Height, Mime(format)); },
            () => (0, 0, "failed"));

        Assert.Equal((8, 4, "image/png"), size);
    }

    [Fact]
    public async Task TryReadImageMetadata_ReadsSizeWithoutDecoding()
    {
        var info = await HalvesPng().TryReadImageMetadata();

        Assert.Equal((8, 4), (info.Width, info.Height));
    }

    // ---- encode ----

    [Theory]
    [InlineData("image/png", "image/png")]
    [InlineData("image/jpeg", "image/jpeg")]
    [InlineData("image/webp", "image/webp")]
    [InlineData("IMAGE/PNG", "image/png")]
    public void ParseImageEncoder_MapsSupportedTypes(string requested, string expected)
    {
        Assert.Equal(expected, Mime(requested.ParseImageEncoder()));
    }

    [Fact]
    public void ParseImageEncoder_NoType_IsPng()
    {
        Assert.Equal("image/png", Mime(((string)null!).ParseImageEncoder()));
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public void Save_WritesTheRequestedFormat(string mimeType)
    {
        using var image = Halves();

        // Called through the class: an instance Save(string path) on the image type would win.
        var (bytes, format) = ImageLoadingExtensions.Save(image, mimeType);

        Assert.Equal(mimeType, Mime(format));
        Assert.Equal((8, 4, mimeType), Decode(bytes));
    }

    [Fact]
    public async Task SaveAsync_WritesTheRequestedFormatToTheStream()
    {
        using var image = Halves();
        using var stream = new MemoryStream();

        var format = await image.SaveAsync(stream, "image/png");

        Assert.Equal("image/png", Mime(format));
        Assert.Equal((8, 4, "image/png"), Decode(stream.ToArray()));
    }

    [Fact]
    public async Task GetBytesAsync_DefaultsToJpeg()
    {
        using var image = Halves();

        var (bytes, format) = await image.GetBytesAsync();

        Assert.Equal("image/jpeg", Mime(format));
        Assert.Equal((8, 4, "image/jpeg"), Decode(bytes));
    }

    // ---- data URIs ----

    [Fact]
    public void Base64Encode_TryParseImage_RoundTrips()
    {
        using var image = Halves();

        var dataUri = image.Base64Encode("image/png");

        Assert.StartsWith("data:image/png;base64,", dataUri);
        Assert.True(dataUri.TryParseImage(out Image parsed, out IImageFormat format));
        using (parsed)
        {
            Assert.Equal((8, 4), (parsed.Width, parsed.Height));
            Assert.Equal("image/png", Mime(format));
            Assert.True(IsRed(Pixel(parsed, 0, 0)));
            Assert.True(IsBlue(Pixel(parsed, 7, 3)));
        }
    }

    [Fact]
    public void Base64Encode_DefaultsToJpeg()
    {
        using var image = Halves();

        Assert.StartsWith("data:image/jpeg;base64,", image.Base64Encode());
    }

    [Fact]
    public void TryParseImage_NotADataUri_IsFalse()
    {
        Assert.False("plainly not base64 image data".TryParseImage(out Image _, out IImageFormat _));
    }

    [Fact]
    public void GetMimeType_IsJpeg()
    {
        using var image = Halves();

        Assert.Equal("image/jpeg", image.GetMimeType());
    }

    // ---- resize / crop / aspect ----

    [Fact]
    public void ResizeImage_ByWidth_KeepsAspectAndColours()
    {
        using var image = Halves().ResizeImage(width: 4);

        Assert.Equal((4, 2), (image.Width, image.Height));
        Assert.True(IsRed(Pixel(image, 0, 0)));
        Assert.True(IsBlue(Pixel(image, 3, 1)));
    }

    [Fact]
    public void ResizeImage_ByHeight_KeepsAspect()
    {
        using var image = Halves().ResizeImage(height: 2);

        Assert.Equal((4, 2), (image.Width, image.Height));
    }

    [Fact]
    public void ResizeImage_NoSize_IsUnchanged()
    {
        using var image = Halves().ResizeImage();

        Assert.Equal((8, 4), (image.Width, image.Height));
    }

    [Fact]
    public void Crop_Pixels_KeepsTheRegion()
    {
        using var image = Halves().Crop(4, 0, 4, 4);

        Assert.Equal((4, 4), (image.Width, image.Height));
        Assert.True(IsBlue(Pixel(image, 0, 0)));
        Assert.True(IsBlue(Pixel(image, 3, 3)));
    }

    [Fact]
    public void Crop_Fractions_KeepsTheRegion()
    {
        using var image = Halves().Crop(0.0, 0.0, 0.5, 1.0);

        Assert.Equal((4, 4), (image.Width, image.Height));
        Assert.True(IsRed(Pixel(image, 0, 0)));
        Assert.True(IsRed(Pixel(image, 3, 3)));
    }

    [Fact]
    public void Scale_ByWidth_KeepsAspect()
    {
        using var image = Halves().Scale(width: 4);

        Assert.Equal((4, 2), (image.Width, image.Height));
    }

    [Fact]
    public void MaxAspect_CropsTheCentre()
    {
        using var image = Halves().MaxAspect(1.0);

        Assert.Equal((4, 4), (image.Width, image.Height));
        Assert.True(IsRed(Pixel(image, 0, 0)));
        Assert.True(IsBlue(Pixel(image, 3, 0)));
    }

    [Fact]
    public void ComputeMaxAspect_CentresTheCrop()
    {
        Assert.True(ImageSharpManipulationExtensions.ComputeMaxAspect(1.0, 8, 4,
            out var x, out var y, out var width, out var height));
        Assert.Equal((2, 0, 4, 4), (x, y, width, height));
    }

    /// <summary>
    /// MinAspect asks for a crop LARGER than the image (it reads like a letterbox, but crops),
    /// which is rejected rather than padded.
    /// </summary>
    [Fact]
    public void MinAspect_BeyondTheBounds_Throws()
    {
        using var image = Halves();

        Assert.ThrowsAny<ArgumentException>(() => image.MinAspect(1.0));
    }

    [Fact]
    public void ScaleAspect_StretchesToTheAspect()
    {
        using var image = Halves().ScaleAspect(1.0);

        Assert.Equal((8, 8), (image.Width, image.Height));
    }

    [Fact]
    public void AspectRatio_IsWidthOverHeight()
    {
        using var image = Halves();

        Assert.Equal(2.0, image.AspectRatio());
    }

    // ---- EXIF ----

    [Fact]
    public void ExifOrientation_IsRead()
    {
        using var image = Image.Load(OrientedJpeg);

        Assert.Equal(6, (int)image.ExifGetRotateFlip());
    }

    [Fact]
    public void FixOrientation_RotatesThePixels()
    {
        using var image = Image.Load(OrientedJpeg);

        var fix = image.FixOrientation();
        image.Mutate(ctx => fix(ctx));

        Assert.Equal((1, 2), (image.Width, image.Height));
    }

    /// <summary>
    /// Orientation 6 means "rotate 90° clockwise to display", which puts the stored LEFT (white)
    /// pixel on top. Today's port rotates the other way (ImageSharp's Rotate270) and puts the
    /// RIGHT (black) pixel on top. Pinned as measured, not as correct.
    /// </summary>
    [Fact]
    public void FixOrientation_Direction()
    {
        using var image = Image.Load(OrientedJpeg);

        var fix = image.FixOrientation();
        image.Mutate(ctx => fix(ctx));

        Assert.True(IsDark(Pixel(image, 0, 0)));
        Assert.True(IsLight(Pixel(image, 0, 1)));
    }

    [Fact]
    public void ExifTaken_IsDateTimeOriginal()
    {
        using var image = Image.Load(OrientedJpeg);

        Assert.Equal(new DateTime(2024, 5, 6, 7, 8, 9), image.ExifTaken());
    }

    [Fact]
    public void ExifLatLon_AreSignedDecimalDegrees()
    {
        using var image = Image.Load(OrientedJpeg);

        Assert.Equal(Latitude, image.ExifLat()!.Value, 6);
        Assert.Equal(Longitude, image.ExifLon()!.Value, 6);
    }
}
