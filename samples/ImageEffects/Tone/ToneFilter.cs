using System.Runtime.InteropServices;
using Acme.Image.Foundation;
using Objo.Runtime.Abstractions;

namespace Acme.Image.Tone;

/// <summary>
/// A native integer gain effect over owned raw images. It preserves alpha,
/// row padding and the exact colour-space/profile declaration.
/// </summary>
[ObjoExport]
public sealed class ToneFilter : IDisposable
{
    private volatile bool _cancelRequested;
    private volatile bool _disposed;
    private Exception? _callbackFailure;

    /// <summary>Creates an effect with its own cancellation state.</summary>
    public ToneFilter() { }

    /// <summary>Raised from native processing with the completed percentage.</summary>
    [ObjoEvent(Coalescing = ObjoEventCoalescing.Latest, ParameterNames = ["percent"])]
    public event ObjoNotification<long>? ProgressChanged;

    /// <summary>Raised after a successful effect finishes.</summary>
    [ObjoEvent]
    public event ObjoNotification? Completed;

    /// <summary>Returns a new raw image after applying integer gain.</summary>
    /// <param name="source">The owned source image.</param>
    /// <param name="gainPercent">Gain from 0 through 400 percent.</param>
    /// <param name="mask">Optional matching 8-bit Grey mask.</param>
    /// <returns>A separate owned image preserving all descriptor metadata.</returns>
    [ObjoAsync]
    public RawBitmap Apply(RawBitmap source, long gainPercent, RawBitmap? mask) =>
        Run(source, gainPercent, mask, inPlace: false);

    /// <summary>Applies gain to a mutable image atomically on success.</summary>
    /// <param name="source">The exclusive mutable source image.</param>
    /// <param name="gainPercent">Gain from 0 through 400 percent.</param>
    /// <param name="mask">Optional matching 8-bit Grey mask.</param>
    /// <returns>The same image object, after success.</returns>
    [ObjoAsync]
    public RawBitmap ApplyInPlace(RawBitmap source, long gainPercent, RawBitmap? mask) =>
        Run(source, gainPercent, mask, inPlace: true);

    private RawBitmap Run(RawBitmap source, long gainPercent, RawBitmap? mask, bool inPlace)
    {
        RequireLive();
        var gain = ValidateGain(gainPercent);
        _callbackFailure = null;
        try
        {
            var result = NativeImageOperation.Apply(source, mask, inPlace,
                (input, output, maskPointer, layout, maskLayout) =>
                    Native.Gain(input, output, layout.Width, layout.Height, layout.Stride,
                        (int)layout.Channels, layout.BitsPerComponent,
                        layout.RowOrder == ObjoRowOrder.BottomUp ? 1 : 0,
                        (int)layout.AlphaMode,
                        maskPointer, maskLayout?.Stride ?? 0,
                        maskLayout?.RowOrder == ObjoRowOrder.BottomUp ? 1 : 0,
                        gain, OnProgress, nint.Zero));
            Completed?.Invoke();
            return result;
        }
        catch (OperationCanceledException) when (_callbackFailure is not null)
        {
            throw new InvalidOperationException("The native tone progress callback failed.", _callbackFailure);
        }
    }

    /// <summary>Requests cancellation; native work stops at the next row boundary.</summary>
    [ObjoSafeWhilePending]
    public void RequestCancel() => _cancelRequested = true;

    /// <summary>Releases the filter after in-flight work has settled.</summary>
    public void Dispose() => _disposed = true;

    private int OnProgress(int percent, nint context)
    {
        try
        {
            if (_cancelRequested) return 0;
            ProgressChanged?.Invoke(percent);
            return _cancelRequested ? 0 : 1;
        }
        catch (Exception exception)
        {
            _callbackFailure = exception;
            return 0;
        }
    }

    private void RequireLive()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ToneFilter));
    }

    private static int ValidateGain(long gainPercent) => gainPercent is >= 0 and <= 400 ?
        (int)gainPercent : throw new ArgumentOutOfRangeException(nameof(gainPercent));

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ProgressCallback(int percent, nint context);

    private static class Native
    {
        [DllImport("acme_effects", EntryPoint = "acme_effects_gain", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Gain(nint source, nint target, int width,
            int height, int stride, int channels, int bits, int bottomUp, int alphaMode,
            nint mask, int maskStride, int maskBottomUp, int gainPercent,
            ProgressCallback progress, nint context);
    }
}
