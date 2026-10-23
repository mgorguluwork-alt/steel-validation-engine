using SteelValidation.Core.Model;

namespace SteelValidation.Tests;

public sealed class ModelRulesTests
{
    [Theory]
    [InlineData("KIRIS", "HEA200", "KIRIS HEA200")]
    [InlineData("  KIRIS ", " HEA200  ", "KIRIS HEA200")]
    [InlineData("", "HEA200", "HEA200")]
    [InlineData(null, " IPE300 ", "IPE300")]
    [InlineData("KOLON", "", "KOLON")]
    [InlineData(" KOLON ", null, "KOLON")]
    [InlineData("", "", "Beam")]
    [InlineData("   ", "  ", "Beam")]
    [InlineData(null, null, "Beam")]
    public void Part_Description_Prefers_Name_And_Profile(
        string? name, string? profile, string expected)
    {
        Assert.Equal(expected, PartDescription.Describe(name, profile, "Beam"));
    }

    [Theory]
    [InlineData("CONCRETE", true)]
    [InlineData("C30/37 concrete", true)]
    [InlineData("Concrete_Undefined", true)]
    [InlineData("S355JR", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Concrete_Materials_Are_Excluded(string? material, bool excluded)
    {
        Assert.Equal(excluded, ModelCheckRules.IsExcludedMaterial(material));
    }

    [Fact]
    public void Concrete_Exclusion_Holds_Under_Turkish_Culture()
    {
        using var culture = new CultureScope("tr-TR");

        Assert.True(ModelCheckRules.IsExcludedMaterial("concrete"));
        Assert.False(ModelCheckRules.IsExcludedMaterial("S235"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("KIRIS", false)]
    public void Missing_Name_Is_Detected(string? name, bool missing)
    {
        Assert.Equal(missing, ModelCheckRules.IsNameMissing(name));
    }

    [Fact]
    public void Name_Empty_Status_Text_Is_Stable()
    {
        Assert.Equal("ERROR - NAME EMPTY", ModelCheckRules.NameEmptyStatus);
    }
}
