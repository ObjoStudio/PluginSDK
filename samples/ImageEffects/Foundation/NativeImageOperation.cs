using Objo.Runtime.Abstractions;

namespace Acme.Image.Foundation;

/// <summary>A native effect invoked while all source, mask and destination leases are held.</summary>
public delegate int RawNativeEffect(nint source, nint target, nint mask,
    ObjoImageDescriptor sourceLayout, ObjoImageDescriptor? maskLayout);

/// <summary>
/// SDK-facing native call pattern shared by two independently built effects.
/// No VM objects, UI objects or Objo integer pointers enter this operation.
/// </summary>
public static class NativeImageOperation
{
    /// <summary>
    /// Runs one effect with exclusive mutation or a separate output owner.
    /// Mask dimensions and depth are checked before any native call.
    /// </summary>
    public static RawBitmap Apply(RawBitmap source, RawBitmap? mask, bool inPlace,
        RawNativeEffect effect)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(effect);
        if (inPlace && ReferenceEquals(source, mask))
            throw new ArgumentException("An in-place image cannot also be its own mask.");

        var sourceBuffer = source.Image;
        var layout = sourceBuffer.Descriptor;
        var maskBuffer = mask?.Image;
        var maskLayout = maskBuffer?.Descriptor;
        if (maskLayout is not null &&
            (maskLayout.Width != layout.Width || maskLayout.Height != layout.Height ||
             maskLayout.Channels != ObjoPixelChannels.Grey || maskLayout.BitsPerComponent != 8))
            throw new ArgumentException("A mask must be a matching 8-bit Grey image.", nameof(mask));

        using var maskLease = maskBuffer?.OpenRead();
        if (inPlace)
        {
            using var write = sourceBuffer.OpenWrite();
            // Blur reads neighbouring pixels. Keep the exclusive lease over the
            // full call, but use private input/output so cancellation cannot
            // publish half of an effect and neighbours never feed back.
            var original = new byte[layout.ByteLength];
            write.Read(bytes => bytes.CopyTo(original));
            using var sourceCopy = new ObjoImageBuffer(layout, original);
            using var sourceLease = sourceCopy.OpenRead();
            using var stagedOutput = new ObjoImageBuffer(layout, original);
            using (var stagedWrite = stagedOutput.OpenWrite())
                Invoke(sourceLease, stagedWrite, maskLease, layout, maskLayout, effect);
            var result = stagedOutput.Snapshot();
            write.Write(bytes => result.CopyTo(bytes));
            return source;
        }

        using var read = sourceBuffer.OpenRead();
        var output = ObjoImageBuffer.Allocate(layout);
        try
        {
            using (var write = output.OpenWrite())
            {
                // Native effects write every active component. Preserve only
                // row padding, so a new-result chain never copies the full
                // source image merely to cross a plugin boundary.
                CopyPadding(read, write, layout);
                Invoke(read, write, maskLease, layout, maskLayout, effect);
            }
            return RawBitmap.FromOwnedBuffer(output);
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    private static void CopyPadding(ObjoImageLease source, ObjoImageLease target,
        ObjoImageDescriptor layout)
    {
        var rowBytes = checked(layout.Width * (int)layout.Channels * (layout.BitsPerComponent / 8));
        var padding = layout.Stride - rowBytes;
        if (padding == 0) return;
        var saved = new byte[checked(padding * layout.Height)];
        source.Read(bytes =>
        {
            for (var row = 0; row < layout.Height; row++)
                bytes.Slice(row * layout.Stride + rowBytes, padding)
                    .CopyTo(saved.AsSpan(row * padding, padding));
        });
        target.Write(bytes =>
        {
            for (var row = 0; row < layout.Height; row++)
                saved.AsSpan(row * padding, padding)
                    .CopyTo(bytes.Slice(row * layout.Stride + rowBytes, padding));
        });
    }

    private static void Invoke(ObjoImageLease source, ObjoImageLease target,
        ObjoImageLease? mask, ObjoImageDescriptor layout,
        ObjoImageDescriptor? maskLayout, RawNativeEffect effect)
    {
        var status = -1;
        source.Pin((sourcePointer, _) => target.Pin((targetPointer, _) =>
        {
            if (mask is null)
                status = effect(sourcePointer, targetPointer, nint.Zero, layout, null);
            else
                mask.Pin((maskPointer, _) =>
                    status = effect(sourcePointer, targetPointer, maskPointer, layout, maskLayout));
        }));
        if (status == 1) throw new OperationCanceledException("Native image processing was cancelled.");
        if (status != 0) throw new InvalidOperationException($"Native image effect failed with status {status}.");
    }
}
