using SteelValidation.Core.Clash;

namespace SteelValidation.Tests;

public sealed class ClashRulesTests
{
    [Theory]
    [InlineData(ClashKind.IsInside, "INSIDE", "HIGH", "Object is inside another object")]
    [InlineData(ClashKind.ExactMatch, "EXACT MATCH", "HIGH", "Objects occupy the same geometry")]
    [InlineData(ClashKind.CutThrough, "CUT THROUGH", "HIGH", "Object cuts through another object")]
    [InlineData(ClashKind.ComplexClash, "COMPLEX CLASH", "HIGH", "Complex geometric clash")]
    [InlineData(ClashKind.MinDistance, "MIN DISTANCE", "MEDIUM", "Minimum distance violation")]
    [InlineData(ClashKind.FailedSolid, "FAILED SOLID", "MEDIUM", "Solid geometry could not be checked")]
    [InlineData(ClashKind.FailedTest, "FAILED TEST", "MEDIUM", "Clash test failed")]
    [InlineData(ClashKind.Invalid, "INVALID", "LOW", "Unknown clash condition")]
    public void Kind_Maps_To_Type_Severity_And_Description(
        ClashKind kind, string typeText, string severity, string description)
    {
        Assert.Equal(typeText, ClashRules.GetTypeText(kind));
        Assert.Equal(severity, ClashRules.GetSeverity(kind));
        Assert.Equal(description, ClashRules.BuildDescription(kind, 5.0));
    }

    [Fact]
    public void Clash_Is_High_And_Uses_Overlap_In_Description()
    {
        using var culture = new CultureScope("en-US");

        Assert.Equal("CLASH", ClashRules.GetTypeText(ClashKind.Clash));
        Assert.Equal("HIGH", ClashRules.GetSeverity(ClashKind.Clash));
        Assert.Equal("12.35 mm overlap", ClashRules.BuildDescription(ClashKind.Clash, 12.3456));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-3.0)]
    public void Clash_Without_Positive_Overlap_Is_Geometric_Conflict(double overlap)
    {
        Assert.Equal("Geometric conflict", ClashRules.BuildDescription(ClashKind.Clash, overlap));
    }

    [Fact]
    public void Overlap_Uses_Current_Culture_As_Before()
    {
        using var culture = new CultureScope("tr-TR");

        Assert.Equal("12,35 mm overlap", ClashRules.BuildDescription(ClashKind.Clash, 12.3456));
    }

    [Fact]
    public void Overlap_Is_Rounded_To_Two_Decimals()
    {
        using var culture = new CultureScope("en-US");

        Assert.Equal("1 mm overlap", ClashRules.BuildDescription(ClashKind.Clash, 1.0));
        Assert.Equal("0.01 mm overlap", ClashRules.BuildDescription(ClashKind.Clash, 0.0051));
    }

    [Fact]
    public void Every_Known_Kind_Has_Its_Own_Text()
    {
        var known = Enum.GetValues<ClashKind>().Where(kind => kind != ClashKind.Invalid).ToArray();

        Assert.All(known, kind => Assert.NotEqual("INVALID", ClashRules.GetTypeText(kind)));
        Assert.All(known, kind =>
            Assert.NotEqual("Unknown clash condition", ClashRules.BuildDescription(kind, 1.0)));
        Assert.Equal(known.Length, known.Select(ClashRules.GetTypeText).Distinct().Count());
    }

    [Fact]
    public void Unknown_Value_Falls_Back_To_Invalid_Low()
    {
        var unknown = (ClashKind)999;

        Assert.Equal("INVALID", ClashRules.GetTypeText(unknown));
        Assert.Equal("LOW", ClashRules.GetSeverity(unknown));
        Assert.Equal("Unknown clash condition", ClashRules.BuildDescription(unknown, 1.0));
    }
}
