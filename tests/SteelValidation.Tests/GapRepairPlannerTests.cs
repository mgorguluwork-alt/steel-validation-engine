using SteelValidation.Core.Numbering;

namespace SteelValidation.Tests;

public sealed class GapRepairPlannerTests
{
    private static NumberingReportPart Part(int id, string? partPosition, bool? upToDate = true, string? assemblyPosition = null) =>
        new NumberingReportPart(id, partPosition, assemblyPosition, upToDate);

    private static IReleaseStateSource AllNotReleased(params int[] ids) =>
        new MapReleaseStateSource(ids.Select(id => new KeyValuePair<int, FabricationReleaseState>(id, FabricationReleaseState.NotReleased)));

    private static IReleaseStateSource States(params (int Id, FabricationReleaseState State)[] states) =>
        new MapReleaseStateSource(states.Select(s => new KeyValuePair<int, FabricationReleaseState>(s.Id, s.State)));

    private static GapRepairPlan Plan(IEnumerable<NumberingReportPart> parts, IReleaseStateSource states) =>
        GapRepairPlanner.Plan(parts, states, isWholeModel: true);

    [Fact]
    public void A_Selection_Never_Gets_A_Proposal_Because_A_Free_Looking_Number_May_Be_In_Use_Outside_It()
    {
        // The model holds C1..C4; only C1 and C4 are selected. Proposing C4 to
        // C2 from the selection would duplicate the unselected C2.
        var selected = new[] { Part(1, "C1"), Part(4, "C4") };

        var plan = GapRepairPlanner.Plan(selected, AllNotReleased(1, 4), isWholeModel: false);

        var series = Assert.Single(plan.Series);
        Assert.Equal(GapRepairDecision.BlockedPartialInput, series.Decision);
        Assert.Empty(series.Changes);
        Assert.False(plan.HasProposals);
        Assert.False(plan.RequiresDatedSaveAs);
    }

    [Fact]
    public void A_Selection_Still_Reports_Released_Objects_For_Investigation()
    {
        var selected = new[] { Part(1, "C1"), Part(4, "C4") };

        var plan = GapRepairPlanner.Plan(selected, States((4, FabricationReleaseState.Released)), isWholeModel: false);

        var series = Assert.Single(plan.Series);
        Assert.Equal(GapRepairDecision.BlockedReleased, series.Decision);
        Assert.Equal(new[] { 4 }, series.BlockingObjectIds);
    }

    [Fact]
    public void Without_A_Release_Source_Every_Gap_Is_Blocked_And_Nothing_Is_Proposed()
    {
        var parts = new[] { Part(1, "C1"), Part(2, "C2"), Part(3, "C4") };

        var plan = Plan(parts, UnknownReleaseStateSource.Instance);

        var series = Assert.Single(plan.Series);
        Assert.Equal(GapRepairDecision.BlockedUnknownReleaseState, series.Decision);
        Assert.Empty(series.Changes);
        Assert.Equal(new[] { 1, 2, 3 }, series.BlockingObjectIds);
        Assert.False(plan.HasProposals);
        Assert.False(plan.RequiresDatedSaveAs);
        Assert.True(plan.IsDryRun);
    }

    [Fact]
    public void Unreleased_Up_To_Date_Series_Gets_The_Rules_Example_Compaction()
    {
        // RULES NUM-003: C1, C2, C4, C5 -> C4 to C3, C5 to C4.
        var parts = new[] { Part(10, "C1"), Part(11, "C2"), Part(12, "C4"), Part(13, "C5") };

        var plan = Plan(parts, AllNotReleased(10, 11, 12, 13));

        var series = Assert.Single(plan.Series);
        Assert.Equal(GapRepairDecision.Proposed, series.Decision);
        Assert.Equal(NumberingRules.PartPositionKind, series.PositionKind);
        Assert.Equal("C", series.Prefix);
        Assert.Equal(new[] { 1, 2, 4, 5 }, series.NumbersInUse);
        Assert.Collection(
            series.Changes,
            change =>
            {
                Assert.Equal("C4", change.OldPosition);
                Assert.Equal("C3", change.NewPosition);
                Assert.Equal(new[] { 12 }, change.ObjectIds);
                Assert.Equal(GapRepairPlanner.Reason, change.Reason);
            },
            change =>
            {
                Assert.Equal("C5", change.OldPosition);
                Assert.Equal("C4", change.NewPosition);
                Assert.Equal(new[] { 13 }, change.ObjectIds);
            });
        Assert.Empty(series.BlockingObjectIds);
        Assert.True(plan.HasProposals);
        Assert.True(plan.RequiresDatedSaveAs);
        Assert.True(plan.IsDryRun);
    }

    [Fact]
    public void One_Released_Object_Blocks_The_Whole_Series()
    {
        var parts = new[] { Part(1, "C1"), Part(2, "C2"), Part(3, "C4") };

        var plan = Plan(parts, States(
            (1, FabricationReleaseState.Released),
            (2, FabricationReleaseState.NotReleased),
            (3, FabricationReleaseState.NotReleased)));

        var series = Assert.Single(plan.Series);
        Assert.Equal(GapRepairDecision.BlockedReleased, series.Decision);
        Assert.Empty(series.Changes);
        Assert.Equal(new[] { 1 }, series.BlockingObjectIds);
        Assert.False(plan.RequiresDatedSaveAs);
    }

    [Fact]
    public void Released_Takes_Precedence_Over_Unknown()
    {
        var parts = new[] { Part(1, "C1"), Part(2, "C3") };

        var plan = Plan(parts, States((2, FabricationReleaseState.Released)));

        var series = Assert.Single(plan.Series);
        Assert.Equal(GapRepairDecision.BlockedReleased, series.Decision);
        Assert.Equal(new[] { 2 }, series.BlockingObjectIds);
    }

    [Fact]
    public void One_Unknown_Release_State_Blocks_An_Otherwise_Unreleased_Series()
    {
        var parts = new[] { Part(1, "C1"), Part(2, "C3"), Part(3, "C4") };

        var plan = Plan(parts, States(
            (1, FabricationReleaseState.NotReleased),
            (3, FabricationReleaseState.NotReleased)));

        var series = Assert.Single(plan.Series);
        Assert.Equal(GapRepairDecision.BlockedUnknownReleaseState, series.Decision);
        Assert.Equal(new[] { 2 }, series.BlockingObjectIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void Stale_Or_Unknown_Numbering_Status_Blocks_The_Series(bool? upToDate)
    {
        var parts = new[] { Part(1, "C1"), Part(2, "C3", upToDate) };

        var plan = Plan(parts, AllNotReleased(1, 2));

        var series = Assert.Single(plan.Series);
        Assert.Equal(GapRepairDecision.BlockedNumberingStale, series.Decision);
        Assert.Empty(series.Changes);
        Assert.Equal(new[] { 2 }, series.BlockingObjectIds);
    }

    [Fact]
    public void A_Series_Without_A_Gap_Is_Not_Listed()
    {
        var parts = new[] { Part(1, "C1"), Part(2, "C2"), Part(3, "C3") };

        var plan = Plan(parts, AllNotReleased(1, 2, 3));

        Assert.Empty(plan.Series);
        Assert.False(plan.HasProposals);
    }

    [Fact]
    public void Identical_Parts_Sharing_A_Position_Move_Together()
    {
        var parts = new[] { Part(1, "C1"), Part(5, "C3"), Part(4, "C3") };

        var plan = Plan(parts, AllNotReleased(1, 4, 5));

        var change = Assert.Single(Assert.Single(plan.Series).Changes);
        Assert.Equal("C3", change.OldPosition);
        Assert.Equal("C2", change.NewPosition);
        Assert.Equal(new[] { 4, 5 }, change.ObjectIds);
    }

    [Theory]
    [InlineData("C/1", "C/4", "C/2")]
    [InlineData("B-1", "B-3", "B-2")]
    [InlineData("PL/001", "PL/007", "PL/002")]
    [InlineData("K1", "K10", "K2")]
    public void The_New_Position_Keeps_The_Separator_And_Zero_Padding(
        string first, string oldPosition, string expectedNewPosition)
    {
        var parts = new[] { Part(1, first), Part(2, oldPosition) };

        var plan = Plan(parts, AllNotReleased(1, 2));

        var change = Assert.Single(Assert.Single(plan.Series).Changes);
        Assert.Equal(oldPosition, change.OldPosition);
        Assert.Equal(expectedNewPosition, change.NewPosition);
    }

    [Fact]
    public void Missing_Unparsed_And_Zero_Positions_Take_No_Part()
    {
        var parts = new[]
        {
            Part(1, "C1"), Part(2, "C3"),
            Part(3, null), Part(4, "   "), Part(5, "C"), Part(6, "C0")
        };

        var plan = Plan(parts, AllNotReleased(1, 2));

        var series = Assert.Single(plan.Series);
        Assert.Equal(GapRepairDecision.Proposed, series.Decision);
        Assert.Equal(new[] { 1, 3 }, series.NumbersInUse);
        var change = Assert.Single(series.Changes);
        Assert.Equal(new[] { 2 }, change.ObjectIds);
    }

    [Fact]
    public void Part_And_Assembly_Positions_Are_Planned_As_Separate_Series()
    {
        var parts = new[]
        {
            Part(1, "p1", assemblyPosition: "A1"),
            Part(2, "p3", assemblyPosition: "A2")
        };

        var plan = Plan(parts, AllNotReleased(1, 2));

        var series = Assert.Single(plan.Series);
        Assert.Equal(NumberingRules.PartPositionKind, series.PositionKind);
        Assert.Equal("p", series.Prefix);
    }

    [Fact]
    public void Cyrillic_And_Latin_Prefixes_Are_Different_Series()
    {
        // The model has 317 parts with a Cyrillic "К" prefix; it only looks
        // like the Latin "K". The planner must never merge the two.
        var parts = new[] { Part(1, "K1"), Part(2, "K2"), Part(3, "К3") };

        var plan = Plan(parts, AllNotReleased(1, 2, 3));

        var series = Assert.Single(plan.Series);
        Assert.Equal("К", series.Prefix);
        var change = Assert.Single(series.Changes);
        Assert.Equal("К3", change.OldPosition);
        Assert.Equal("К1", change.NewPosition);
    }

    [Fact]
    public void Series_Are_Ordered_By_Kind_Then_Prefix()
    {
        var parts = new[]
        {
            Part(1, "C1", assemblyPosition: "B1"), Part(2, "C3", assemblyPosition: "B3"),
            Part(3, "A1", assemblyPosition: "Z1"), Part(4, "A3", assemblyPosition: "Z3")
        };

        var plan = Plan(parts, AllNotReleased(1, 2, 3, 4));

        Assert.Equal(
            new[] { "PART:A", "PART:C", "ASSEMBLY:B", "ASSEMBLY:Z" },
            plan.Series.Select(series => series.PositionKind + ":" + series.Prefix));
    }

    [Fact]
    public void Map_Source_Treats_Missing_Objects_As_Unknown()
    {
        var source = States((1, FabricationReleaseState.NotReleased));

        Assert.Equal(FabricationReleaseState.NotReleased, source.GetState(1));
        Assert.Equal(FabricationReleaseState.Unknown, source.GetState(2));
    }

    [Fact]
    public void An_Object_Listed_Twice_Is_Rejected()
    {
        // Qwen review round 2: without this, object 7 could be in two
        // contradictory changes (C3 to C2 and C5 to C3).
        var parts = new[] { Part(1, "C1"), Part(7, "C3"), Part(7, "C5") };

        Assert.Throws<ArgumentException>(() => Plan(parts, AllNotReleased(1, 7)));
    }

    [Fact]
    public void Empty_Input_Gives_An_Empty_Plan_That_Proposes_Nothing()
    {
        var plan = Plan(Array.Empty<NumberingReportPart>(), AllNotReleased());

        Assert.Empty(plan.Series);
        Assert.False(plan.HasProposals);
        Assert.False(plan.RequiresDatedSaveAs);
    }

    [Fact]
    public void Null_Arguments_And_Null_Entries_Are_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => GapRepairPlanner.Plan(null!, UnknownReleaseStateSource.Instance, isWholeModel: true));
        Assert.Throws<ArgumentNullException>(() => GapRepairPlanner.Plan(Array.Empty<NumberingReportPart>(), null!, isWholeModel: true));
        Assert.Throws<ArgumentException>(() => GapRepairPlanner.Plan(new NumberingReportPart[] { null! }, UnknownReleaseStateSource.Instance, isWholeModel: true));
    }

    [Fact]
    public void A_Series_That_Starts_At_Its_Tekla_Start_Number_Has_No_Gap()
    {
        var parts = new[]
        {
            new NumberingReportPart(1, "P1", "ROD-3001", true, 1, 3001),
            new NumberingReportPart(2, "P1", "ROD-3002", true, 1, 3001),
        };

        var plan = Plan(parts, AllNotReleased(1, 2));

        Assert.Empty(plan.Series);
        Assert.False(plan.HasProposals);
    }

    [Fact]
    public void Compaction_Maps_Onto_The_Series_Start_And_Never_Moves_Rod_3001_To_1()
    {
        var parts = new[]
        {
            new NumberingReportPart(1, "P1", "ROD-3001", true, 1, 3001),
            new NumberingReportPart(2, "P1", "ROD-3002", true, 1, 3001),
            new NumberingReportPart(3, "P1", "ROD-3004", true, 1, 3001),
        };

        var plan = Plan(parts, AllNotReleased(1, 2, 3));

        var series = Assert.Single(plan.Series);
        Assert.Equal(GapRepairDecision.Proposed, series.Decision);
        Assert.Equal(3001, series.StartNumber);
        var change = Assert.Single(series.Changes);
        Assert.Equal("ROD-3004", change.OldPosition);
        Assert.Equal("ROD-3003", change.NewPosition);
        Assert.Equal(new[] { 3 }, change.ObjectIds);
    }

    [Fact]
    public void Numbers_Below_The_Series_Start_Take_No_Part()
    {
        var parts = new[]
        {
            new NumberingReportPart(1, "B2", null, true, partStartNumber: 5),
            new NumberingReportPart(2, "B5", null, true, partStartNumber: 5),
            new NumberingReportPart(3, "B6", null, true, partStartNumber: 5),
        };

        var plan = Plan(parts, AllNotReleased(1, 2, 3));

        Assert.Empty(plan.Series);
    }

    [Fact]
    public void Different_Start_Numbers_In_One_Series_Block_The_Proposal()
    {
        // Compacting onto the lowest start (1) would move B7 below its own
        // start of 5.
        var parts = new[]
        {
            new NumberingReportPart(1, "B1", null, true, partStartNumber: 1),
            new NumberingReportPart(2, "B5", null, true, partStartNumber: 5),
            new NumberingReportPart(3, "B7", null, true, partStartNumber: 5),
        };

        var plan = Plan(parts, AllNotReleased(1, 2, 3));

        var series = Assert.Single(plan.Series);
        Assert.Equal(GapRepairDecision.BlockedMixedStartNumbers, series.Decision);
        Assert.Equal(1, series.StartNumber);
        Assert.Equal(new[] { 2, 3 }, series.BlockingObjectIds);
        Assert.Empty(series.Changes);
        Assert.False(plan.HasProposals);
    }
}
