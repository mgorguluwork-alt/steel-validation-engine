using System.Globalization;

namespace SteelValidation.Tests;

/// <summary>
/// Pins CurrentCulture for one test so culture-sensitive formatting is
/// repeatable on any machine.
/// </summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

    public CultureScope(string name) =>
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}
