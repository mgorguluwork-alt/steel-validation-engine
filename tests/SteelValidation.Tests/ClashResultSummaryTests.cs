using SteelValidation.Abstractions;
using SteelValidation.Core.Clash;

namespace SteelValidation.Tests;

public sealed class ClashResultSummaryTests
{
    private static ClashCheckResult ResultWith(params string?[] severities)
    {
        var result = new ClashCheckResult();
        foreach (var severity in severities)
            result.Items.Add(new ClashCheckItem { Severity = severity! });
        return result;
    }

    [Fact]
    public void Counts_Each_Severity_And_Total()
    {
        var result = ResultWith("HIGH", "HIGH", "MEDIUM", "LOW", "HIGH");

        ClashResultSummary.Summarize(result);

        Assert.Equal(5, result.TotalClashes);
        Assert.Equal(3, result.HighCount);
        Assert.Equal(1, result.MediumCount);
        Assert.Equal(1, result.LowCount);
    }

    [Fact]
    public void Unrecognised_Or_Missing_Severity_Counts_As_Low()
    {
        var result = ResultWith(null, "", "high", "CRITICAL");

        ClashResultSummary.Summarize(result);

        Assert.Equal(4, result.TotalClashes);
        Assert.Equal(0, result.HighCount);
        Assert.Equal(0, result.MediumCount);
        Assert.Equal(4, result.LowCount);
    }

    [Fact]
    public void Empty_Result_Is_All_Zero()
    {
        var result = new ClashCheckResult();

        ClashResultSummary.Summarize(result);

        Assert.Equal(0, result.TotalClashes);
        Assert.Equal(0, result.HighCount + result.MediumCount + result.LowCount);
    }

    [Fact]
    public void Summarizing_Twice_Does_Not_Double_Count()
    {
        var result = ResultWith("HIGH", "MEDIUM");

        ClashResultSummary.Summarize(result);
        ClashResultSummary.Summarize(result);

        Assert.Equal(2, result.TotalClashes);
        Assert.Equal(1, result.HighCount);
        Assert.Equal(1, result.MediumCount);
        Assert.Equal(0, result.LowCount);
    }

    [Fact]
    public void Leaves_Native_Clash_Count_Untouched()
    {
        var result = ResultWith("HIGH");
        result.NativeClashCount = 616;

        ClashResultSummary.Summarize(result);

        Assert.Equal(616, result.NativeClashCount);
    }

    [Fact]
    public void Rule_Severities_Round_Trip_Through_The_Summary()
    {
        var result = new ClashCheckResult();
        foreach (var kind in Enum.GetValues<ClashKind>())
            result.Items.Add(new ClashCheckItem { Severity = ClashRules.GetSeverity(kind) });

        ClashResultSummary.Summarize(result);

        Assert.Equal(5, result.HighCount);
        Assert.Equal(3, result.MediumCount);
        Assert.Equal(1, result.LowCount);
    }
}
