using SteelValidation.Core.Numbering;

namespace SteelValidation.Tests;

/// <summary>
/// Tekla appends "(?)" to a position whose numbering is out of date. Seen in
/// the live model on 2026-09-30 ("P-1(?)", "1(?)", "ROD-3001(?)"); the report
/// used to call these "unparsed".
/// </summary>
public sealed class OutOfDatePositionTests
{
    [Theory]
    [InlineData("P-1(?)", "P", 1)]
    [InlineData("1(?)", "", 1)]
    [InlineData("ROD-3001(?)", "ROD", 3001)]
    [InlineData("RB-7 (?)", "RB", 7)]
    [InlineData("  PL/31(?)  ", "PL", 31)]
    public void The_Out_Of_Date_Marker_Is_Recognised_And_The_Number_Is_Still_Read(
        string input, string prefix, int number)
    {
        var position = NumberingPosition.Parse(input);

        Assert.True(position.IsOutOfDate);
        Assert.True(position.IsParsed);
        Assert.Equal(prefix, position.Prefix);
        Assert.Equal(number, position.Number);
        Assert.Equal(input.Trim(), position.Position);
    }

    [Theory]
    [InlineData("B/12", false, true)]
    [InlineData("(?)", true, false)]
    [InlineData("B(?)", true, false)]
    [InlineData("B?", false, false)]
    [InlineData("B/12(", false, false)]
    public void Only_A_Trailing_Marker_Counts_And_A_Missing_Number_Stays_Unparsed(
        string input, bool outOfDate, bool parsed)
    {
        var position = NumberingPosition.Parse(input);

        Assert.Equal(outOfDate, position.IsOutOfDate);
        Assert.Equal(parsed, position.IsParsed);
    }

    [Fact]
    public void Out_Of_Date_Positions_Are_Reported_As_Such_And_Stay_Out_Of_The_Gap_Rule()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.PartPositionKind,
            new[]
            {
                new KeyValuePair<int, string?>(1, "P-1(?)"),
                new KeyValuePair<int, string?>(2, "P-2"),
                new KeyValuePair<int, string?>(3, "P-4")
            });

        Assert.Equal(1, report.OutOfDatePositionCount);
        Assert.Equal(0, report.UnparsedPositionCount);
        var finding = Assert.Single(report.Findings, f => f.Rule == NumberingRules.OutOfDatePositionRule);
        Assert.Equal("P-1(?)", finding.Position);
        Assert.Equal(new[] { 1 }, finding.ObjectIds);
        Assert.Equal(NumberingRules.WarningStatus, finding.Status);

        // Same gap result as before the marker was recognised: only the
        // current numbers 2 and 4 count, so 1 and 3 are missing.
        var gap = Assert.Single(report.Gaps);
        Assert.Equal(2, gap.MissingCount);
        Assert.Equal(new[] { 1, 3 }, gap.MissingNumbers);
    }

    [Fact]
    public void An_Out_Of_Date_Assembly_Position_Blocks_Gap_Repair_Even_On_An_Up_To_Date_Part()
    {
        // In the live model 1389 of 1454 assembly positions carry "(?)"
        // while most parts report numbering up to date.
        var parts = new[]
        {
            new NumberingReportPart(1, "B1", "A-1", true),
            new NumberingReportPart(2, "B2", "A-3(?)", true)
        };
        var states = new MapReleaseStateSource(new[]
        {
            new KeyValuePair<int, FabricationReleaseState>(1, FabricationReleaseState.NotReleased),
            new KeyValuePair<int, FabricationReleaseState>(2, FabricationReleaseState.NotReleased)
        });

        var plan = GapRepairPlanner.Plan(parts, states, isWholeModel: true);

        var series = Assert.Single(plan.Series);
        Assert.Equal(NumberingRules.AssemblyPositionKind, series.PositionKind);
        Assert.Equal(GapRepairDecision.BlockedNumberingStale, series.Decision);
        Assert.Equal(new[] { 2 }, series.BlockingObjectIds);
        Assert.False(plan.HasProposals);
    }

    [Fact]
    public void A_Current_Part_Series_Is_Not_Proposed_While_Numbering_Elsewhere_Is_Out_Of_Date()
    {
        // Qwen review: the part series P1, P3 is itself current, but another
        // object's assembly position is "(?)", so numbering must run first.
        var parts = new[]
        {
            new NumberingReportPart(1, "P1", "A-1", true),
            new NumberingReportPart(2, "P3", "A-2", true),
            new NumberingReportPart(3, "Q1", "A-3(?)", true)
        };
        var states = new MapReleaseStateSource(new[] { 1, 2, 3 }.Select(id =>
            new KeyValuePair<int, FabricationReleaseState>(id, FabricationReleaseState.NotReleased)));

        var plan = GapRepairPlanner.Plan(parts, states, isWholeModel: true);

        var partSeries = Assert.Single(plan.Series, s => s.PositionKind == NumberingRules.PartPositionKind);
        Assert.Equal(GapRepairDecision.BlockedModelNumberingNotUpToDate, partSeries.Decision);
        Assert.Empty(partSeries.Changes);
        Assert.False(plan.HasProposals);
    }
}
