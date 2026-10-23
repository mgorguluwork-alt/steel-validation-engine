using SteelValidation.Core.Model;

namespace SteelValidation.Tests;

public sealed class PartConsistencyRuleTests
{
    [Theory]
    [InlineData("ANGLE", "L50*5")]
    [InlineData("ANGLE", "l100x10")]
    [InlineData("BASE PLATE", "PL10*200")]
    [InlineData("GUSSET PLATE", "pl12")]
    [InlineData("TIE ROD", "D16")]
    [InlineData("SAG ROD", "RD16")]
    [InlineData("ANCHOR BOLT", "ROD16")]
    public void Default_Rules_Match_Expected_Profiles(string name, string profile)
    {
        var result = new PartConsistencyRule().Check(name, profile, "S275");

        Assert.Equal(PartConsistencyStatus.Ok, result.Status);
        Assert.False(result.IsMismatch);
    }

    [Theory]
    [InlineData("ANGLE", "HEA200")]
    [InlineData("BASE PLATE", "L50*5")]
    [InlineData("TIE ROD", "PL10")]
    [InlineData("ANGLE", "XL50*5")]
    public void Invalid_Profile_Is_A_Mismatch(string name, string profile)
    {
        var result = new PartConsistencyRule().Check(name, profile, "S275");

        Assert.Equal(PartConsistencyStatus.ProfileMismatch, result.Status);
        Assert.True(result.IsMismatch);
        Assert.Contains(name, result.Message);
        Assert.Contains(profile, result.Message);
        Assert.NotNull(result.ExpectedProfilePattern);
        Assert.Contains(result.ExpectedProfilePattern!, result.Message);
    }

    [Fact]
    public void Matching_Trims_Outer_Whitespace_And_Ignores_Case()
    {
        var result = new PartConsistencyRule().Check(
            "  angle ",
            " l50*5 ",
            " S275 ");

        Assert.Equal(PartConsistencyStatus.Ok, result.Status);
        Assert.Equal("angle", result.PartName);
        Assert.Equal("l50*5", result.Profile);
        Assert.Equal("S275", result.Material);
    }

    [Theory]
    [InlineData("BEAM")]
    [InlineData("STAIR")]
    public void Names_Without_A_Configured_Rule_Are_Not_Checked(string name)
    {
        var result = new PartConsistencyRule().Check(name, "HEA200", "S275");

        Assert.Equal(PartConsistencyStatus.NotChecked, result.Status);
        Assert.False(result.IsMismatch);
        Assert.Null(result.ExpectedProfilePattern);
        Assert.Contains(name, result.Message);
        Assert.Contains("HEA200", result.Message);
        Assert.Contains("expected profile pattern", result.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_Or_Whitespace_Name_Is_Not_Guessed(string name)
    {
        var result = new PartConsistencyRule().Check(name, "L50*5", "S275");

        Assert.Equal(PartConsistencyStatus.NameEmpty, result.Status);
        Assert.False(result.IsMismatch);
        Assert.Equal(string.Empty, result.PartName);
        Assert.Contains("(unnamed part)", result.Message);
        Assert.Contains("L50*5", result.Message);
    }

    [Fact]
    public void Null_Name_Is_Not_Guessed()
    {
        var result = new PartConsistencyRule().Check(null, "L50*5", "S275");

        Assert.Equal(PartConsistencyStatus.NameEmpty, result.Status);
        Assert.False(result.IsMismatch);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_Or_Whitespace_Profile_Is_Unknown(string profile)
    {
        var result = new PartConsistencyRule().Check("ANGLE", profile, "S275");

        Assert.Equal(PartConsistencyStatus.ProfileUnknown, result.Status);
        Assert.False(result.IsMismatch);
        Assert.Equal(string.Empty, result.Profile);
        Assert.NotNull(result.ExpectedProfilePattern);
        Assert.Contains("(empty)", result.Message);
        Assert.Contains(result.ExpectedProfilePattern!, result.Message);
    }

    [Fact]
    public void Null_Profile_Is_Unknown()
    {
        var result = new PartConsistencyRule().Check("ANGLE", null, "S275");

        Assert.Equal(PartConsistencyStatus.ProfileUnknown, result.Status);
        Assert.False(result.IsMismatch);
    }

    [Fact]
    public void Optional_Material_Pattern_Is_Checked_When_Configured()
    {
        var rule = new PartConsistencyRule(
            new Dictionary<string, PartConsistencySpec>
            {
                ["ANGLE"] = new PartConsistencySpec(
                    @"^L\d+\*\d+$",
                    @"^S355$")
            });

        var match = rule.Check("ANGLE", "L50*5", "s355");
        var mismatch = rule.Check("ANGLE", "L50*5", "S275");
        var unknown = rule.Check("ANGLE", "L50*5", " ");

        Assert.Equal(PartConsistencyStatus.Ok, match.Status);
        Assert.Equal(PartConsistencyStatus.MaterialMismatch, mismatch.Status);
        Assert.True(mismatch.IsMismatch);
        Assert.Equal(PartConsistencyStatus.MaterialUnknown, unknown.Status);
        Assert.False(unknown.IsMismatch);
        Assert.Equal("^S355$", mismatch.ExpectedMaterialPattern);
        Assert.Contains("L50*5", mismatch.Message);
        Assert.Contains("^L\\d+\\*\\d+$", mismatch.Message);
        Assert.Contains("^S355$", mismatch.Message);
    }

    [Fact]
    public void Default_Rules_Do_Not_Require_A_Material()
    {
        var result = new PartConsistencyRule().Check("ANGLE", "L50*5", null);

        Assert.Equal(PartConsistencyStatus.Ok, result.Status);
        Assert.Null(result.ExpectedMaterialPattern);
    }

    [Fact]
    public void Custom_Configuration_Replaces_Default_Rules()
    {
        var rule = new PartConsistencyRule(
            new Dictionary<string, PartConsistencySpec>
            {
                ["BEAM"] = new PartConsistencySpec(@"^HEA\d+$")
            });

        Assert.Equal(
            PartConsistencyStatus.Ok,
            rule.Check("BEAM", "HEA200", "S275").Status);
        Assert.Equal(
            PartConsistencyStatus.NotChecked,
            rule.Check("ANGLE", "L50*5", "S275").Status);
    }

    [Fact]
    public void Configuration_Rejects_Empty_Names_Duplicates_And_Invalid_Patterns()
    {
        Assert.Throws<ArgumentException>(() => new PartConsistencyRule(
            new Dictionary<string, PartConsistencySpec>
            {
                [" "] = new PartConsistencySpec(@"^L.*$")
            }));

        Assert.Throws<ArgumentException>(() => new PartConsistencyRule(
            new Dictionary<string, PartConsistencySpec>
            {
                ["ANGLE"] = new PartConsistencySpec(@"^L.*$"),
                [" angle "] = new PartConsistencySpec(@"^L.*$")
            }));

        Assert.Throws<ArgumentException>(() => new PartConsistencyRule(
            new Dictionary<string, PartConsistencySpec>
            {
                ["ANGLE"] = new PartConsistencySpec("[")
            }));
    }
}
