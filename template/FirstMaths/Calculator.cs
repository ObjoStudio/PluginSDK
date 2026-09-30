using Objo.Runtime.Abstractions;

namespace Example.Maths;

/// <summary>A calculator that keeps a running integer total.</summary>
[ObjoExport]
public sealed class Calculator
{
    /// <summary>Creates a calculator with an initial total.</summary>
    /// <param name="start">The starting value.</param>
    public Calculator(long start) => Total = start;

    /// <summary>The current total.</summary>
    public long Total { get; private set; }

    /// <summary>Raised after Add changes the total.</summary>
    [ObjoEvent(ParameterNames = ["total"])]
    public event ObjoNotification<long>? ValueChanged;

    /// <summary>Adds an amount to the total and raises ValueChanged.</summary>
    /// <param name="amount">The amount to add.</param>
    public void Add(long amount)
    {
        Total = checked(Total + amount);
        ValueChanged?.Invoke(Total);
    }
}
