using System.Runtime.InteropServices;
using Objo.Runtime.Abstractions;

namespace Acme.NativeMaths;

/// <summary>Adds integers with one call into a native C library.</summary>
[ObjoExport]
public sealed class Adder
{
    /// <summary>Creates an adder.</summary>
    public Adder()
    {
    }

    /// <summary>Returns the sum of two integers, computed by the native library.</summary>
    /// <param name="a">The first integer.</param>
    /// <param name="b">The second integer.</param>
    public long Add(long a, long b) => AcmeNativeAdd(a, b);

    [DllImport("acme_nativemaths", EntryPoint = "acme_nativemaths_add",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern long AcmeNativeAdd(long a, long b);
}
