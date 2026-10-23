using SteelValidation.Abstractions;
using SteelValidation.Core.Clash;

namespace SteelValidation.Tests;

public sealed class AbstractionsContractTests
{
    [Fact]
    public void New_Clash_Result_Is_Empty_With_Unknown_Native_Count()
    {
        var result = new ClashCheckResult();

        Assert.NotNull(result.Items);
        Assert.Empty(result.Items);
        Assert.Equal(-1, result.NativeClashCount);
        Assert.Equal(0, result.TotalClashes);
    }

    [Fact]
    public void New_Model_Check_Result_Has_Empty_Item_Lists()
    {
        var result = new ModelCheckResult();

        Assert.NotNull(result.NameEmptyItems);
        Assert.Empty(result.NameEmptyItems);
        Assert.NotNull(result.LengthFindings);
        Assert.Empty(result.LengthFindings);
        Assert.Equal(0, result.LengthWarningCount);
        Assert.NotNull(result.ConsistencyFindings);
        Assert.Empty(result.ConsistencyFindings);
        Assert.Equal(0, result.ConsistencyMismatchCount);
    }

    [Fact]
    public void Model_Check_Results_Do_Not_Share_Item_Lists()
    {
        var first = new ModelCheckResult();
        var second = new ModelCheckResult();

        first.NameEmptyItems.Add(new ModelCheckItem());
        first.LengthFindings.Add(new ModelLengthFinding());
        first.ConsistencyFindings.Add(new ModelConsistencyFinding());

        Assert.Empty(second.NameEmptyItems);
        Assert.Empty(second.LengthFindings);
        Assert.Empty(second.ConsistencyFindings);
    }

    [Fact]
    public void Clash_Results_Do_Not_Share_Item_Lists()
    {
        var first = new ClashCheckResult();
        var second = new ClashCheckResult();

        first.Items.Add(new ClashCheckItem());

        Assert.Empty(second.Items);
    }

    [Fact]
    public void New_Geometry_Check_Result_Has_Empty_Independent_Findings()
    {
        var first = new GeometryCheckResult();
        var second = new GeometryCheckResult();

        Assert.NotNull(first.Findings);
        Assert.Empty(first.Findings);
        Assert.Equal(0, first.PartsChecked);
        Assert.Equal(0, first.PartsNotChecked);
        Assert.Equal(0, first.UnknownLengthCount);
        Assert.Equal(0, first.ShortOrZeroLengthCount);
        Assert.Equal(0, first.EmptyProfileCount);
        Assert.Equal(0, first.DuplicateBeamGroupCount);

        first.Findings.Add(new GeometryCheckFinding());
        Assert.Empty(second.Findings);
    }

    [Theory]
    [InlineData(typeof(ClashRules))]
    [InlineData(typeof(ClashCheckResult))]
    [InlineData(typeof(GeometryCheckResult))]
    [InlineData(typeof(NumberingReportResult))]
    [InlineData(typeof(SteelValidation.Core.Numbering.NumberingRules))]
    public void Tested_Layers_Do_Not_Reference_Tekla(Type type)
    {
        var references = type.Assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty);

        Assert.DoesNotContain(references,
            name => name.StartsWith("Tekla.", StringComparison.OrdinalIgnoreCase));
    }
}
