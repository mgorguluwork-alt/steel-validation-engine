using SteelValidation.Core.Numbering;

namespace SteelValidation.Tests;

public sealed class OwnerDeclaredReleaseStateTests
{
    private static readonly DateTime Declared = new DateTime(2026, 9, 29, 18, 53, 1, DateTimeKind.Utc);

    private static OwnerDeclaredReleaseStateSource NothingReleased() =>
        new OwnerDeclaredReleaseStateSource(
            FabricationReleaseState.NotReleased,
            "New model 1",
            "Mehmet",
            Declared,
            "R&D test model; nothing released; no single part, assembly or GA drawings");

    [Fact]
    public void Every_Object_Gets_The_Declared_State_And_The_Declaration_Is_Kept()
    {
        var source = NothingReleased();

        Assert.Equal(FabricationReleaseState.NotReleased, source.GetState(1));
        Assert.Equal(FabricationReleaseState.NotReleased, source.GetState(int.MaxValue));
        Assert.Equal("Mehmet", source.DeclaredBy);
        Assert.Equal(Declared, source.DeclaredAtUtc);
        Assert.Equal("New model 1", source.ModelName);
    }

    [Theory]
    [InlineData("New model 1", true)]
    [InlineData("  New model 1  ", true)]
    [InlineData("New model", false)]
    [InlineData("new model 1", false)]
    [InlineData("New model 12", false)]
    [InlineData("New model 1_2026-09-29", true)]
    [InlineData("New model 1_2026-13-40", false)]
    [InlineData("New model 1_copy", false)]
    [InlineData(null, false)]
    public void A_Declaration_Applies_Only_To_The_Model_It_Names(string? model, bool applies)
    {
        Assert.Equal(applies, NothingReleased().AppliesTo(model));
    }

    [Fact]
    public void With_The_Owner_Declaration_An_Up_To_Date_Gap_Is_Proposed()
    {
        var parts = new[]
        {
            new NumberingReportPart(1, "C1", null, true),
            new NumberingReportPart(2, "C3", null, true)
        };

        var plan = GapRepairPlanner.Plan(parts, NothingReleased(), isWholeModel: true);

        var change = Assert.Single(Assert.Single(plan.Series).Changes);
        Assert.Equal(("C3", "C2"), (change.OldPosition, change.NewPosition));
        Assert.True(plan.RequiresDatedSaveAs);
    }

    [Fact]
    public void An_Incomplete_Declaration_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() => new OwnerDeclaredReleaseStateSource(
            FabricationReleaseState.NotReleased, " ", "Mehmet", Declared, "reason"));
        Assert.Throws<ArgumentException>(() => new OwnerDeclaredReleaseStateSource(
            FabricationReleaseState.NotReleased, "New model 1", "", Declared, "reason"));
        Assert.Throws<ArgumentException>(() => new OwnerDeclaredReleaseStateSource(
            FabricationReleaseState.NotReleased, "New model 1", "Mehmet", new DateTime(2026, 9, 29), "reason"));
        Assert.Throws<ArgumentException>(() => new OwnerDeclaredReleaseStateSource(
            FabricationReleaseState.NotReleased, "New model 1", "Mehmet", Declared, " "));
    }
}
