using System.Runtime.InteropServices;
using Acme.Image.Foundation;
using Objo.Runtime.Abstractions;

namespace Acme.Image.Tone;

/// <summary>
/// Bounded PNG/JPEG data interoperability through the same staged native
/// library as ToneFilter. Raw effect chains keep their owned image buffers.
/// Encoded inputs with ICC profiles are rejected because this sample codec
/// cannot retain that metadata; decoding unprofiled files declares the colour
/// space Unspecified until the caller deliberately interprets it.
/// </summary>
[ObjoExport]
public static class ImageCodec
{
    /// <summary>Encodes an 8-bit unprofiled sRGB image as PNG bytes.</summary>
    /// <param name="image">Grey, RGB or straight-alpha RGBA input.</param>
    /// <returns>Owned encoded PNG bytes suitable for a file or WorkerMessage.</returns>
    [ObjoAsync]
    public static byte[] EncodePng(RawBitmap image)
    {
        var layout = RequireEncodable(image, jpeg: false);
        using var lease = image.Image.OpenRead();
        nint encoded = nint.Zero;
        var length = 0;
        try
        {
            var status = -1;
            lease.Pin((pointer, _) => status = Native.EncodePng(pointer,
                layout.Width, layout.Height, (int)layout.Channels, layout.Stride,
                layout.RowOrder == ObjoRowOrder.BottomUp ? 1 : 0,
                out encoded, out length));
            CheckStatus(status, "PNG encoding");
            return CopyResult(encoded, length);
        }
        finally { if (encoded != nint.Zero) Native.Free(encoded); }
    }

    /// <summary>Encodes an 8-bit unprofiled sRGB Grey or RGB image as JPEG bytes.</summary>
    /// <param name="image">Grey or RGB input; alpha is not accepted.</param>
    /// <param name="quality">Integer quality from 1 through 100.</param>
    /// <returns>Owned encoded JPEG bytes.</returns>
    [ObjoAsync]
    public static byte[] EncodeJpeg(RawBitmap image, long quality)
    {
        if (quality is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(quality));
        var layout = RequireEncodable(image, jpeg: true);
        using var lease = image.Image.OpenRead();
        nint encoded = nint.Zero;
        var length = 0;
        try
        {
            var status = -1;
            lease.Pin((pointer, _) => status = Native.EncodeJpeg(pointer,
                layout.Width, layout.Height, (int)layout.Channels, layout.Stride,
                layout.RowOrder == ObjoRowOrder.BottomUp ? 1 : 0,
                (int)quality, out encoded, out length));
            CheckStatus(status, "JPEG encoding");
            return CopyResult(encoded, length);
        }
        finally { if (encoded != nint.Zero) Native.Free(encoded); }
    }

    /// <summary>
    /// Decodes an unprofiled PNG or JPEG into an owned top-down image. Sixteen-bit
    /// PNG stays sixteen-bit; JPEG is eight-bit. Colour space is Unspecified.
    /// </summary>
    /// <param name="encoded">PNG or JPEG bytes.</param>
    /// <returns>An owned mutable raw image.</returns>
    [ObjoAsync]
    public static RawBitmap Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        nint pixels = nint.Zero;
        try
        {
            var status = Native.Decode(encoded, encoded.Length, out pixels,
                out var width, out var height, out var channels, out var bits);
            CheckStatus(status, "image decoding");
            var stride = checked(width * channels * (bits / 8));
            var length = checked(stride * height);
            if (length > ObjoImageDescriptor.MaximumPixelBytes)
                throw new InvalidDataException("Decoded image exceeds the raw-buffer limit.");
            var copied = CopyResult(pixels, length);
            return new RawBitmap(width, height, stride, (RawChannels)channels, bits,
                RawRowOrder.TopDown, channels == 4 ? RawAlpha.Straight : RawAlpha.None,
                RawColourSpace.Unspecified, true, copied, []);
        }
        finally { if (pixels != nint.Zero) Native.Free(pixels); }
    }

    private static ObjoImageDescriptor RequireEncodable(RawBitmap image, bool jpeg)
    {
        ArgumentNullException.ThrowIfNull(image);
        var layout = image.Image.Descriptor;
        if (layout.BitsPerComponent != 8)
            throw new NotSupportedException("This codec cannot encode 16-bit images without reducing precision.");
        if (layout.IccProfile.Length != 0)
            throw new NotSupportedException("This codec cannot encode an ICC profile without losing it.");
        if (layout.ColourSpace != ObjoColourSpace.Srgb)
            throw new NotSupportedException("Encoding requires an explicitly declared sRGB image.");
        if (layout.AlphaMode == ObjoAlphaMode.Premultiplied)
            throw new NotSupportedException("Encoding premultiplied alpha requires an explicit straight-alpha conversion.");
        if (jpeg && layout.Channels == ObjoPixelChannels.Rgba)
            throw new NotSupportedException("JPEG encoding cannot preserve an alpha channel.");
        return layout;
    }

    private static byte[] CopyResult(nint pointer, int length)
    {
        if (pointer == nint.Zero || length <= 0 || length > ObjoImageDescriptor.MaximumPixelBytes)
            throw new InvalidDataException("The native image codec returned an invalid buffer.");
        var result = new byte[length];
        Marshal.Copy(pointer, result, 0, length);
        return result;
    }

    private static void CheckStatus(int status, string operation)
    {
        if (status == 0) return;
        throw new InvalidDataException(status switch
        {
            -2 => $"{operation} refused an ICC profile that it cannot preserve.",
            -3 => $"{operation} exceeds the native image buffer limit.",
            _ => $"{operation} rejected malformed or unsupported image data."
        });
    }

    private static class Native
    {
        [DllImport("acme_effects", EntryPoint = "acme_codec_decode", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Decode(byte[] data, int length, out nint pixels,
            out int width, out int height, out int channels, out int bits);

        [DllImport("acme_effects", EntryPoint = "acme_codec_encode_png", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int EncodePng(nint pixels, int width, int height,
            int channels, int stride, int bottomUp, out nint encoded, out int length);

        [DllImport("acme_effects", EntryPoint = "acme_codec_encode_jpeg", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int EncodeJpeg(nint pixels, int width, int height,
            int channels, int stride, int bottomUp, int quality, out nint encoded, out int length);

        [DllImport("acme_effects", EntryPoint = "acme_codec_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Free(nint pointer);
    }
}
