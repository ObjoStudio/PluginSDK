using Objo.Runtime.Abstractions;

namespace Acme.Image.Foundation;

/// <summary>Interleaved raw-image channel arrangement.</summary>
[ObjoExport]
public enum RawChannels
{
    /// <summary>One greyscale component.</summary>
    Grey = 1,
    /// <summary>Three colour components.</summary>
    Rgb = 3,
    /// <summary>Three colour components and alpha.</summary>
    Rgba = 4
}

/// <summary>Storage order of the raw image rows.</summary>
[ObjoExport]
public enum RawRowOrder
{
    /// <summary>First storage row is the top pixel row.</summary>
    TopDown = 0,
    /// <summary>First storage row is the bottom pixel row.</summary>
    BottomUp = 1
}

/// <summary>Alpha representation of an RGBA raw image.</summary>
[ObjoExport]
public enum RawAlpha
{
    /// <summary>No alpha channel.</summary>
    None = 0,
    /// <summary>Colour components are independent of alpha.</summary>
    Straight = 1,
    /// <summary>Colour components have already been scaled by alpha.</summary>
    Premultiplied = 2
}

/// <summary>Declared colour interpretation of the raw components.</summary>
[ObjoExport]
public enum RawColourSpace
{
    /// <summary>No colour interpretation is declared.</summary>
    Unspecified = 0,
    /// <summary>sRGB encoded colour components.</summary>
    Srgb = 1,
    /// <summary>Linear RGB colour components.</summary>
    LinearRgb = 2
}

/// <summary>
/// An owned raw image that preserves 8- and 16-bit components, row padding,
/// orientation, alpha and optional ICC bytes across plugin calls. Native
/// effects borrow its bytes through exclusive SDK leases; Pixels is a copy.
/// </summary>
[ObjoExport]
public sealed class RawBitmap : IDisposable
{
    private readonly ObjoImageBuffer _image;
    private int _disposed;

    /// <summary>Copies raw pixels into a new owned image.</summary>
    /// <param name="width">Pixel width.</param>
    /// <param name="height">Pixel height.</param>
    /// <param name="stride">Bytes per storage row, including padding.</param>
    /// <param name="channels">Grey, RGB or RGBA channel arrangement.</param>
    /// <param name="bits">Eight or sixteen bits per component.</param>
    /// <param name="rowOrder">Top-down or bottom-up storage order.</param>
    /// <param name="alpha">Alpha representation; None for Grey or RGB.</param>
    /// <param name="colourSpace">Declared colour interpretation.</param>
    /// <param name="mutable">Whether effects may modify this image in place.</param>
    /// <param name="pixels">Exact stride times height raw bytes.</param>
    /// <param name="iccProfile">ICC profile bytes, retained exactly; use an empty buffer when absent.</param>
    public RawBitmap(long width, long height, long stride, RawChannels channels,
        long bits, RawRowOrder rowOrder, RawAlpha alpha,
        RawColourSpace colourSpace, bool mutable, byte[] pixels, byte[] iccProfile)
    {
        var descriptor = new ObjoImageDescriptor(checked((int)width), checked((int)height),
            checked((int)stride), (ObjoPixelChannels)channels, checked((int)bits),
            (ObjoRowOrder)rowOrder, (ObjoAlphaMode)alpha,
            (ObjoColourSpace)colourSpace, mutable, iccProfile);
        _image = new ObjoImageBuffer(descriptor, pixels);
    }

    private RawBitmap(ObjoImageBuffer image) => _image = image;

    /// <summary>Transfers one newly owned SDK buffer into a shared raw image.</summary>
    [ObjoIgnore]
    public static RawBitmap FromOwnedBuffer(ObjoImageBuffer image) => new(image);

    /// <summary>SDK-only access for independently built effect wrappers.</summary>
    [ObjoIgnore]
    public ObjoImageBuffer Image => Volatile.Read(ref _disposed) == 0 ? _image :
        throw new ObjectDisposedException(nameof(RawBitmap));

    /// <summary>The pixel width.</summary>
    public long Width => Image.Descriptor.Width;

    /// <summary>The pixel height.</summary>
    public long Height => Image.Descriptor.Height;

    /// <summary>Bytes per storage row, including padding.</summary>
    public long Stride => Image.Descriptor.Stride;

    /// <summary>Eight or sixteen component bits.</summary>
    public long Bits => Image.Descriptor.BitsPerComponent;

    /// <summary>The channel arrangement.</summary>
    public RawChannels Channels => (RawChannels)Image.Descriptor.Channels;

    /// <summary>The row orientation.</summary>
    public RawRowOrder RowOrder => (RawRowOrder)Image.Descriptor.RowOrder;

    /// <summary>The alpha representation.</summary>
    public RawAlpha Alpha => (RawAlpha)Image.Descriptor.AlphaMode;

    /// <summary>The colour interpretation.</summary>
    public RawColourSpace ColourSpace => (RawColourSpace)Image.Descriptor.ColourSpace;

    /// <summary>Whether the image accepts exclusive write leases.</summary>
    public bool Mutable => Image.Descriptor.Mutable;

    /// <summary>A copy of the optional ICC profile.</summary>
    public byte[] IccProfile => Image.Descriptor.IccProfile;

    /// <summary>A copy of the raw pixel bytes; effect chains use the owned image directly.</summary>
    public byte[] Pixels => Image.Snapshot();

    /// <summary>Releases the image after active native leases finish.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _image.Dispose();
    }
}
