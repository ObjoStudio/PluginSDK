using Acme.Foundation;
using Objo.Runtime.Abstractions;

namespace Acme.Mutator;

/// <summary>Changes storage supplied by a different plugin.</summary>
[ObjoExport]
public static class Shifter
{
    /// <summary>Adds a value and returns the same shared storage object.</summary>
    /// <param name="storage">The storage object to change.</param>
    /// <param name="delta">The amount to add.</param>
    public static Storage Boost(Storage storage, double delta) => storage.Add(delta);
}
