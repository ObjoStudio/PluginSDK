using Acme.Foundation;
using Objo.Runtime.Abstractions;

namespace Acme.Creator;

/// <summary>Creates and reads storage owned by the shared foundation.</summary>
[ObjoExport]
public static class Factory
{
    /// <summary>Creates storage with an initial amount.</summary>
    /// <param name="amount">The starting amount.</param>
    public static Storage NewStorage(double amount) => new(amount);

    /// <summary>Reads storage created by this or another plugin.</summary>
    /// <param name="storage">The shared storage object.</param>
    public static double ReadAmount(Storage storage) => storage.Amount;
}
