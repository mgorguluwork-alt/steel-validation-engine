using SteelValidation.Core.Model;

namespace SteelValidation.Tests;

public sealed class PartLengthRuleTests
{
    [Fact]
    public void Default_Limits_Are_Angle_And_Column()
    {
        var rule = new PartLengthRule();

        Assert.Equal(6000d, PartLengthRule.DefaultMaxLengthsMm["ANGLE"]);
        Assert.Equal(12000d, PartLengthRule.DefaultMaxLengthsMm["COLUMN"]);
        Assert.True(rule.TryGetLimit("ANGLE", out var angleLimit));
        Assert.Equal(6000d, angleLimit);
        Assert.True(rule.TryGetLimit("COLUMN", out var columnLimit));
        Assert.Equal(12000d, columnLimit);
        Assert.False(rule.TryGetLimit("BEAM", out _));
    }

    [Fact]
    public void Custom_Limits_Replace_Default_Limits()
    {
        var configuredLimits = new Dictionary<string, double>
        {
            ["BEAM"] = 8000d
        };
        var rule = new PartLengthRule(configuredLimits);

        Assert.True(rule.TryGetLimit("BEAM", out var beamLimit));
        Assert.Equal(8000d, beamLimit);
        Assert.False(rule.TryGetLimit("ANGLE", out _));
        Assert.Equal(PartLengthStatus.NoLimit, rule.Check("ANGLE", 6001d).Status);
    }

    [Fact]
    public void Length_Above_Limit_Produces_A_Clear_Warning()
    {
        var result = new PartLengthRule().Check("ANGLE", 6500d);

        Assert.Equal(PartLengthStatus.TooLong, result.Status);
        Assert.True(result.IsWarning);
        Assert.Equal("WARNING - ANGLE length 6500 mm exceeds limit 6000 mm", result.Message);
        Assert.Contains("ANGLE", result.Message);
        Assert.Contains("6500", result.Message);
        Assert.Contains("6000", result.Message);
    }

    [Fact]
    public void Length_Exactly_At_Limit_Does_Not_Produce_A_Warning()
    {
        var result = new PartLengthRule().Check("ANGLE", 6000d);

        Assert.Equal(PartLengthStatus.Ok, result.Status);
        Assert.False(result.IsWarning);
    }

    [Fact]
    public void Length_Below_Limit_Does_Not_Produce_A_Warning()
    {
        var result = new PartLengthRule().Check("COLUMN", 11999d);

        Assert.Equal(PartLengthStatus.Ok, result.Status);
        Assert.False(result.IsWarning);
    }

    [Fact]
    public void Name_Without_A_Configured_Limit_Does_Not_Produce_A_Warning()
    {
        var result = new PartLengthRule().Check("BEAM", 15000d);

        Assert.Equal(PartLengthStatus.NoLimit, result.Status);
        Assert.False(result.IsWarning);
        Assert.Null(result.LimitMm);
    }

    [Fact]
    public void Name_Matching_Is_Whitespace_Trimmed_And_Case_Insensitive()
    {
        using var culture = new CultureScope("tr-TR");
        var rule = new PartLengthRule(new Dictionary<string, double>
        {
            ["GIRT"] = 5000d
        });

        var result = rule.Check("  girt  ", 5001d);

        Assert.Equal(PartLengthStatus.TooLong, result.Status);
        Assert.Equal("girt", result.PartName);
        Assert.Equal(5000d, result.LimitMm);
    }

    [Fact]
    public void Unknown_Length_Is_Reported_Without_Substitution()
    {
        var result = new PartLengthRule().Check(" ANGLE ", null);

        Assert.Equal(PartLengthStatus.LengthUnknown, result.Status);
        Assert.False(result.IsWarning);
        Assert.Equal("ANGLE", result.PartName);
        Assert.Null(result.LengthMm);
        Assert.Null(result.LimitMm);
        Assert.Contains("unknown", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Messages_Format_Whole_Millimetres_Without_Decimals()
    {
        var result = new PartLengthRule().Check("ANGLE", 6001d);

        Assert.Equal(
            "WARNING - ANGLE length 6001 mm exceeds limit 6000 mm",
            result.Message);
    }

    [Fact]
    public void Messages_Format_Meaningful_Decimals_Without_Binary_Artifacts()
    {
        var result = new PartLengthRule().Check("ANGLE", 6000.1d);

        Assert.Equal(
            "WARNING - ANGLE length 6000.1 mm exceeds limit 6000 mm",
            result.Message);
    }

    [Fact]
    public void Messages_Use_Invariant_Decimal_Formatting_For_Configured_Limits()
    {
        using var culture = new CultureScope("de-DE");
        var rule = new PartLengthRule(new Dictionary<string, double>
        {
            ["BEAM"] = 5000.5d
        });

        var withinLimit = rule.Check("BEAM", 5000.5d);
        var tooLong = rule.Check("BEAM", 5000.6d);

        Assert.Equal(
            "INFO - BEAM length is within limit 5000.5 mm",
            withinLimit.Message);
        Assert.Equal(
            "WARNING - BEAM length 5000.6 mm exceeds limit 5000.5 mm",
            tooLong.Message);
    }
}
