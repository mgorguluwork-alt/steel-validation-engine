using SteelValidation.Abstractions;
using SteelValidation.Core.Numbering;

namespace SteelValidation.Tests;

public sealed class NumberingRulesTests
{
    [Theory]
    [InlineData("B/12", "B", 12)]
    [InlineData("B-12", "B", 12)]
    [InlineData("B12", "B", 12)]
    [InlineData("C/1", "C", 1)]
    [InlineData("  B/12  ", "B", 12)]
    [InlineData("PL/007", "PL", 7)]
    [InlineData("12", "", 12)]
    [InlineData("A-1/3", "A-1", 3)]
    public void Parser_Splits_Prefix_And_Number_With_Optional_Separator(
        string input, string expectedPrefix, int expectedNumber)
    {
        var position = NumberingPosition.Parse(input);

        Assert.True(position.IsParsed);
        Assert.False(position.IsMissing);
        Assert.Equal(input.Trim(), position.Position);
        Assert.Equal(expectedPrefix, position.Prefix);
        Assert.Equal(expectedNumber, position.Number);
    }

    [Theory]
    [InlineData("B")]
    [InlineData("B/")]
    [InlineData("?")]
    [InlineData("12B")]
    [InlineData("B/99999999999")]
    [InlineData("B/١٢")]
    public void Parser_Reports_Positions_Without_Usable_Trailing_Number_As_Unparsed(
        string input)
    {
        var position = NumberingPosition.Parse(input);

        Assert.False(position.IsParsed);
        Assert.False(position.IsMissing);
        Assert.Null(position.Prefix);
        Assert.Null(position.Number);
    }

    [Fact]
    public void Parser_Accepts_Int_MaxValue_And_Rejects_Numbers_Beyond_It()
    {
        var atLimit = NumberingPosition.Parse("B/2147483647");
        var beyondLimit = NumberingPosition.Parse("B/2147483648");

        Assert.True(atLimit.IsParsed);
        Assert.Equal("B", atLimit.Prefix);
        Assert.Equal(int.MaxValue, atLimit.Number);
        Assert.False(beyondLimit.IsParsed);
        Assert.Null(beyondLimit.Prefix);
        Assert.Null(beyondLimit.Number);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Parser_Reports_Empty_Or_Whitespace_Positions_As_Missing(string? input)
    {
        var position = NumberingPosition.Parse(input);

        Assert.True(position.IsMissing);
        Assert.False(position.IsParsed);
    }

    [Fact]
    public void Missing_Position_Rule_Reports_Empty_And_Whitespace_Positions()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.PartPositionKind,
            Positions((3, "   "), (1, null), (2, ""), (4, "B/1")));

        Assert.Equal(3, report.MissingPositionCount);
        var missing = report.Findings
            .Where(finding => finding.Rule == NumberingRules.MissingPositionRule)
            .ToList();
        Assert.Equal(new[] { 1, 2, 3 }, missing.Select(finding => Assert.Single(finding.ObjectIds)));
        Assert.All(missing, finding =>
        {
            Assert.Equal(NumberingRules.WarningStatus, finding.Status);
            Assert.Equal(NumberingRules.PartPositionKind, finding.PositionKind);
        });
    }

    [Fact]
    public void Unparsed_Positions_Are_Reported_As_Unknown_And_Not_Used_For_Gaps()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.PartPositionKind,
            Positions((1, "B"), (2, "B/3")));

        var unparsed = Assert.Single(report.Findings,
            finding => finding.Rule == NumberingRules.UnparsedPositionRule);
        Assert.Equal(NumberingRules.UnknownStatus, unparsed.Status);
        Assert.Equal("B", unparsed.Position);
        Assert.Equal(1, Assert.Single(unparsed.ObjectIds));
        Assert.Contains("not guessed", unparsed.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, report.UnparsedPositionCount);

        var gap = Assert.Single(report.Gaps);
        Assert.Equal(new[] { 1, 2 }, gap.MissingNumbers);
    }

    [Fact]
    public void Gap_Rule_Reports_Missing_Numbers_Per_Prefix()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.PartPositionKind,
            Positions(
                (1, "B/1"), (2, "B-4"), (3, "B6"),
                (4, "C/1"), (5, "C/2"),
                (6, "A/3")));

        Assert.Equal(2, report.Gaps.Count);

        var a = report.Gaps[0];
        Assert.Equal("A", a.Prefix);
        Assert.Equal(3, a.HighestNumber);
        Assert.Equal(2, a.MissingCount);
        Assert.Equal(new[] { 1, 2 }, a.MissingNumbers);
        Assert.False(a.IsTruncated);

        var b = report.Gaps[1];
        Assert.Equal("B", b.Prefix);
        Assert.Equal(6, b.HighestNumber);
        Assert.Equal(3, b.MissingCount);
        Assert.Equal(new[] { 2, 3, 5 }, b.MissingNumbers);
        Assert.Equal(NumberingRules.PartPositionKind, b.PositionKind);

        Assert.Equal(5, report.TotalMissingNumberCount);
    }

    [Fact]
    public void Gap_Rule_Ignores_Duplicates_And_Zero_When_Counting()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.PartPositionKind,
            Positions((1, "B/0"), (2, "B/3"), (3, "B/3"), (4, "B/1")));

        var gap = Assert.Single(report.Gaps);
        Assert.Equal(1, gap.MissingCount);
        Assert.Equal(new[] { 2 }, gap.MissingNumbers);
    }

    [Fact]
    public void Gap_Rule_Treats_Prefixes_Case_Sensitively()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.PartPositionKind,
            Positions((1, "b/2"), (2, "B/1")));

        var gap = Assert.Single(report.Gaps);
        Assert.Equal("b", gap.Prefix);
        Assert.Equal(new[] { 1 }, gap.MissingNumbers);
    }

    [Fact]
    public void Gap_List_Is_Capped_At_Default_Of_Fifty_But_Count_Is_Complete()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.PartPositionKind,
            Positions((1, "B/1000")));

        var gap = Assert.Single(report.Gaps);
        Assert.Equal(999, gap.MissingCount);
        Assert.Equal(NumberingReportOptions.DefaultMaxMissingNumbersPerPrefix, gap.MissingNumbers.Count);
        Assert.Equal(Enumerable.Range(1, 50), gap.MissingNumbers);
        Assert.True(gap.IsTruncated);
        Assert.Equal(999, report.TotalMissingNumberCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(1000)]
    public void Gap_List_Cap_Is_Configurable(int cap)
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.PartPositionKind,
            Positions((1, "B/2"), (2, "B/5000")),
            new NumberingReportOptions(cap));

        var gap = Assert.Single(report.Gaps);
        Assert.Equal(4998, gap.MissingCount);
        Assert.Equal(
            new[] { 1 }.Concat(Enumerable.Range(3, cap - 1)),
            gap.MissingNumbers);
    }

    [Fact]
    public void Gap_Rule_Handles_Very_High_Numbers_Without_Enumerating_The_Range()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.PartPositionKind,
            Positions((1, "B/" + int.MaxValue)));

        var gap = Assert.Single(report.Gaps);
        Assert.Equal(int.MaxValue - 1, gap.MissingCount);
        Assert.Equal(50, gap.MissingNumbers.Count);
    }

    [Fact]
    public void Gap_Rule_Reports_No_Gap_For_Complete_Series()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.PartPositionKind,
            Positions((1, "B/2"), (2, "B/1"), (3, "C/1")));

        Assert.Empty(report.Gaps);
        Assert.Equal(0, report.TotalMissingNumberCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Gap_List_Cap_Outside_One_To_Thousand_Throws(int cap)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new NumberingReportOptions(cap));

        Assert.Equal("maxMissingNumbersPerPrefix", exception.ParamName);
    }

    [Fact]
    public void Default_Options_Cap_Is_Fifty()
    {
        Assert.Equal(50, new NumberingReportOptions().MaxMissingNumbersPerPrefix);
    }

    [Fact]
    public void Duplicate_Positions_Are_Information_Only()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.AssemblyPositionKind,
            Positions((30, "B/1"), (10, "B/1"), (20, " B/1 "), (40, "B/2"), (50, "X"), (60, "X")));

        var duplicates = report.Findings
            .Where(finding => finding.Rule == NumberingRules.DuplicatePositionRule)
            .ToList();

        Assert.Equal(2, report.DuplicatePositionGroupCount);
        Assert.Equal(2, duplicates.Count);
        Assert.All(duplicates, finding =>
        {
            Assert.Equal(NumberingRules.InfoStatus, finding.Status);
            Assert.Equal(NumberingRules.AssemblyPositionKind, finding.PositionKind);
            Assert.Contains("information only", finding.Message, StringComparison.OrdinalIgnoreCase);
        });

        Assert.Equal("B/1", duplicates[0].Position);
        Assert.Equal(new[] { 10, 20, 30 }, duplicates[0].ObjectIds);
        Assert.Equal("X", duplicates[1].Position);
        Assert.Equal(new[] { 50, 60 }, duplicates[1].ObjectIds);

        Assert.DoesNotContain(report.Findings, finding => finding.Status == NumberingRules.WarningStatus);
    }

    [Fact]
    public void The_Same_Object_Twice_Is_Not_A_Duplicate()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.PartPositionKind,
            Positions((1, "B/1"), (1, "B/1")));

        Assert.Equal(0, report.DuplicatePositionGroupCount);
    }

    [Fact]
    public void Findings_Order_Is_Missing_Then_Unparsed_Then_Duplicates()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.PartPositionKind,
            Positions((5, "B/1"), (4, "?"), (3, ""), (2, "B/1"), (1, "Z")));

        Assert.Equal(
            new[]
            {
                NumberingRules.MissingPositionRule,
                NumberingRules.UnparsedPositionRule,
                NumberingRules.UnparsedPositionRule,
                NumberingRules.DuplicatePositionRule
            },
            report.Findings.Select(finding => finding.Rule));
        Assert.Equal(new[] { 1 }, report.Findings[1].ObjectIds);
        Assert.Equal(new[] { 4 }, report.Findings[2].ObjectIds);
    }

    [Fact]
    public void Report_Applies_Rules_To_Part_And_Assembly_Positions_Separately()
    {
        var result = NumberingRules.BuildReport(new[]
        {
            new NumberingReportPart(2, "p/3", "", true),
            new NumberingReportPart(1, "p/1", "A/2", false),
            new NumberingReportPart(3, "", "A/2", null)
        });

        Assert.Equal(3, result.PartsRead);
        Assert.Equal(50, result.MaxMissingNumbersPerPrefix);

        Assert.Equal(NumberingRules.PartPositionKind, result.PartPositions.PositionKind);
        Assert.Equal(1, result.PartPositions.MissingPositionCount);
        Assert.Equal(3, Assert.Single(result.PartPositions.Findings).ObjectIds.Single());
        var partGap = Assert.Single(result.PartPositions.Gaps);
        Assert.Equal("p", partGap.Prefix);
        Assert.Equal(new[] { 2 }, partGap.MissingNumbers);

        Assert.Equal(NumberingRules.AssemblyPositionKind, result.AssemblyPositions.PositionKind);
        Assert.Equal(1, result.AssemblyPositions.MissingPositionCount);
        Assert.Equal(1, result.AssemblyPositions.DuplicatePositionGroupCount);
        var assemblyGap = Assert.Single(result.AssemblyPositions.Gaps);
        Assert.Equal("A", assemblyGap.Prefix);
        Assert.Equal(new[] { 1 }, assemblyGap.MissingNumbers);
    }

    [Fact]
    public void Report_Records_Numbering_Up_To_Date_State_Without_Guessing()
    {
        var result = NumberingRules.BuildReport(new[]
        {
            new NumberingReportPart(3, "B/1", "A/1", null),
            new NumberingReportPart(1, "B/1", "A/1", true),
            new NumberingReportPart(2, "B/2", "A/2", false)
        });

        Assert.Equal(1, result.NumberingUpToDateCount);
        Assert.Equal(1, result.NumberingNotUpToDateCount);
        Assert.Equal(1, result.NumberingStatusUnknownCount);

        Assert.Equal(new[] { 1, 2, 3 }, result.Parts.Select(part => part.ObjectId));
        Assert.Equal(
            new[]
            {
                NumberingRules.UpToDateStatus,
                NumberingRules.NotUpToDateStatus,
                NumberingRules.UpToDateUnknownStatus
            },
            result.Parts.Select(part => part.NumberingStatus));
        Assert.Null(result.Parts[2].IsNumberingUpToDate);
    }

    [Fact]
    public void Report_Keeps_Null_Positions_As_Empty_Strings()
    {
        var result = NumberingRules.BuildReport(new[]
        {
            new NumberingReportPart(1, null, null, true)
        });

        var part = Assert.Single(result.Parts);
        Assert.Equal(string.Empty, part.PartPosition);
        Assert.Equal(string.Empty, part.AssemblyPosition);
        Assert.Equal(1, result.PartPositions.MissingPositionCount);
        Assert.Equal(1, result.AssemblyPositions.MissingPositionCount);
    }

    [Fact]
    public void Report_Is_Deterministic_For_Any_Input_Order()
    {
        var parts = new[]
        {
            new NumberingReportPart(5, "B/4", "A/1", true),
            new NumberingReportPart(2, "B/1", "A/1", true),
            new NumberingReportPart(9, "", "A/3", false),
            new NumberingReportPart(7, "?", "", null)
        };

        var first = NumberingRules.BuildReport(parts);
        var second = NumberingRules.BuildReport(parts.Reverse());

        Assert.Equal(Describe(first), Describe(second));
    }

    [Fact]
    public void Empty_Input_Produces_An_Empty_Report()
    {
        var result = NumberingRules.BuildReport(Array.Empty<NumberingReportPart>());

        Assert.Equal(0, result.PartsRead);
        Assert.Empty(result.Parts);
        Assert.Empty(result.PartPositions.Findings);
        Assert.Empty(result.PartPositions.Gaps);
        Assert.Empty(result.AssemblyPositions.Findings);
        Assert.Empty(result.AssemblyPositions.Gaps);
    }

    [Fact]
    public void Null_Inputs_Are_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => NumberingRules.BuildReport(null!));
        Assert.Throws<ArgumentException>(
            () => NumberingRules.BuildReport(new NumberingReportPart[] { null! }));
        Assert.Throws<ArgumentNullException>(
            () => NumberingRules.CheckPositions(NumberingRules.PartPositionKind, null!));
    }

    [Fact]
    public void New_Numbering_Result_Has_Empty_Independent_Lists()
    {
        var first = new NumberingReportResult();
        var second = new NumberingReportResult();

        first.Parts.Add(new NumberingPartItem());
        first.PartPositions.Findings.Add(new NumberingFinding());
        first.AssemblyPositions.Gaps.Add(new NumberingGap());

        Assert.Empty(second.Parts);
        Assert.Empty(second.PartPositions.Findings);
        Assert.Empty(second.AssemblyPositions.Gaps);
        Assert.NotSame(first.PartPositions, first.AssemblyPositions);
    }

    private static IEnumerable<KeyValuePair<int, string?>> Positions(
        params (int ObjectId, string? Position)[] entries) =>
        entries.Select(entry => new KeyValuePair<int, string?>(entry.ObjectId, entry.Position));

    private static string Describe(NumberingReportResult result)
    {
        var lines = new List<string>
        {
            string.Join(";", result.Parts.Select(part =>
                part.ObjectId + "|" + part.PartPosition + "|" + part.AssemblyPosition + "|" + part.NumberingStatus))
        };

        foreach (var report in new[] { result.PartPositions, result.AssemblyPositions })
        {
            lines.AddRange(report.Findings.Select(finding =>
                finding.Rule + "|" + finding.Position + "|" + string.Join(",", finding.ObjectIds) + "|" + finding.Message));
            lines.AddRange(report.Gaps.Select(gap =>
                gap.Prefix + "|" + gap.MissingCount + "|" + string.Join(",", gap.MissingNumbers)));
        }

        return string.Join("\n", lines);
    }

    [Fact]
    public void Gap_Rule_Counts_From_The_Tekla_Series_Start_Number()
    {
        // Live model, 2026-09-30: ROD assemblies start at 3001. Counting from
        // 1 reported 3000 false missing numbers.
        var parts = new[]
        {
            new NumberingReportPart(1, "P1", "ROD-3001", true, 1, 3001),
            new NumberingReportPart(2, "P2", "ROD-3002", true, 1, 3001),
            new NumberingReportPart(3, "P3", "ROD-3004", true, 1, 3001),
        };

        var result = NumberingRules.BuildReport(parts);

        var gap = Assert.Single(result.AssemblyPositions.Gaps);
        Assert.Equal("ROD", gap.Prefix);
        Assert.Equal(3001, gap.StartNumber);
        Assert.Equal(1, gap.MissingCount);
        Assert.Equal(new[] { 3003 }, gap.MissingNumbers);
        Assert.Empty(result.PartPositions.Gaps);
    }

    [Fact]
    public void Gap_Rule_Uses_The_Lowest_Start_In_A_Prefix_And_Ignores_Numbers_Below_It()
    {
        var parts = new[]
        {
            new NumberingReportPart(1, "B5", null, true, partStartNumber: 5),
            new NumberingReportPart(2, "B7", null, true, partStartNumber: 7),
            new NumberingReportPart(3, "B2", null, true, partStartNumber: 7),
        };

        var result = NumberingRules.BuildReport(parts);

        // Lowest start is 5; B2 lies below it and is not a gap reason.
        var gap = Assert.Single(result.PartPositions.Gaps);
        Assert.Equal(5, gap.StartNumber);
        Assert.Equal(new[] { 6 }, gap.MissingNumbers);
    }

    [Fact]
    public void Gap_Rule_Ignores_Start_Numbers_Below_One()
    {
        // Live model: secondary parts report assembly start 0.
        var parts = new[]
        {
            new NumberingReportPart(1, "R1", "ROD-3001", true, 1, 3001),
            new NumberingReportPart(2, "PL1", "ROD-3001", true, 1, 0),
            new NumberingReportPart(3, "R1", "ROD-3002", true, 1, 3001),
        };

        var result = NumberingRules.BuildReport(parts);

        Assert.Empty(result.AssemblyPositions.Gaps);
    }

    [Fact]
    public void A_Series_That_Does_Not_Start_At_One_Is_A_Warning()
    {
        // Owner decision (2026-09-30): every series starts at 1.
        var parts = new[]
        {
            new NumberingReportPart(7, "R1", "ROD-3001", true, 1, 3001),
            new NumberingReportPart(3, "PL1", "ROD-3001", true, 1, null),
            new NumberingReportPart(5, "R1", "ROD-3001", true, 1, 3001),
            new NumberingReportPart(9, "P1", "HB-1", true, 1, 1),
        };

        var result = NumberingRules.BuildReport(parts);

        var finding = Assert.Single(
            result.AssemblyPositions.Findings,
            item => item.Rule == NumberingRules.SeriesStartNotOneRule);
        Assert.Equal(NumberingRules.WarningStatus, finding.Status);
        Assert.Equal("ROD", finding.Position);
        Assert.Equal(new[] { 5, 7 }, finding.ObjectIds);
        Assert.Contains("starts at 3001", finding.Message);
        Assert.DoesNotContain(
            result.PartPositions.Findings,
            item => item.Rule == NumberingRules.SeriesStartNotOneRule);
    }

    [Fact]
    public void Gap_Rule_Starts_At_One_When_No_Start_Number_Was_Read()
    {
        var report = NumberingRules.CheckPositions(
            NumberingRules.AssemblyPositionKind,
            Positions((1, "ROD-3001"), (2, "ROD-3002")));

        var gap = Assert.Single(report.Gaps);
        Assert.Equal(1, gap.StartNumber);
        Assert.Equal(3000, gap.MissingCount);
    }
}
