namespace Acme.Foundation;

/// <summary>
/// A mutable value shared by Creator and Mutator. Both plugins use this
/// foundation package's Storage type, so changes remain visible through
/// either plugin.
/// </summary>
[Objo.Runtime.Abstractions.ObjoExport]
public class Storage
{
    private long _mutations;

    /// <summary>Creates storage holding <paramref name="amount"/>.</summary>
    /// <param name="amount">The initially stored amount.</param>
    public Storage(double amount) => Amount = amount;

    /// <summary>
    /// The currently stored amount.
    /// </summary>
    public double Amount { get; private set; }

    /// <summary>
    /// The number of mutations applied through <see cref="Add"/>.
    /// </summary>
    public long Mutations => _mutations;

    /// <summary>
    /// Applies <paramref name="delta"/> to the stored amount and returns this
    /// same storage so consumers can chain or alias it.
    /// </summary>
    /// <param name="delta">The amount to add.</param>
    /// <returns>This storage, after the mutation.</returns>
    public Storage Add(double delta)
    {
        Amount += delta;
        _mutations++;
        return this;
    }
}
