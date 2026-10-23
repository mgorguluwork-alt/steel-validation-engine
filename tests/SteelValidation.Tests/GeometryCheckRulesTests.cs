using SteelValidation.Core.Geometry;

namespace SteelValidation.Tests;

public sealed class GeometryCheckRulesTests
{
    [Fact]
    public void Length_Rule_Reports_Short_And_Unknown_Without_Guessing()
    {
        var result = GeometryCheckRules.Check(new[]
        {
            Part(1, lengthMm: 0d),
            Part(2, lengthMm: 9.999d),
            Part(3, lengthMm: 10d),
            Part(4, lengthMm: 10.001d),
            Part(5, lengthMm: null)
        });

        Assert.Equal(2, result.ShortOrZeroLengthCount);
        Assert.Equal(1, result.UnknownLengthCount);
        Assert.Equal(new[] { 1, 2 }, result.Findings
            .Where(finding => finding.Rule == GeometryCheckRules.ShortOrZeroLengthRule)
            .Select(finding => Assert.Single(finding.ObjectIds)));

        var unknown = Assert.Single(result.Findings,
            finding => finding.Rule == GeometryCheckRules.UnknownLengthRule);
        Assert.Equal(GeometryCheckRules.UnknownStatus, unknown.Status);
        Assert.Equal(5, Assert.Single(unknown.ObjectIds));
        Assert.Contains("not assumed", unknown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Profile_Rule_Reports_Null_Empty_And_Whitespace_Only_Profiles()
    {
        var result = GeometryCheckRules.Check(new[]
        {
            Part(1, profile: null),
            Part(2, profile: string.Empty),
            Part(3, profile: "   "),
            Part(4, profile: "HEA300")
        });

        Assert.Equal(3, result.EmptyProfileCount);
        Assert.Equal(new[] { 1, 2, 3 }, result.Findings
            .Where(finding => finding.Rule == GeometryCheckRules.EmptyProfileRule)
            .Select(finding => Assert.Single(finding.ObjectIds)));
    }

    [Fact]
    public void Duplicate_Beams_Are_Found_In_Same_And_Reversed_Directions()
    {
        var result = GeometryCheckRules.Check(new[]
        {
            Part(20, start: Point(100d, 0d, 0d), end: Point(0d, 0d, 0d)),
            Part(10, start: Point(0d, 0d, 0d), end: Point(100d, 0d, 0d))
        });

        Assert.Equal(2, result.PartsChecked);
        Assert.Equal(0, result.PartsNotChecked);
        Assert.Equal(1, result.DuplicateBeamGroupCount);

        var duplicate = Assert.Single(result.Findings,
            finding => finding.Rule == GeometryCheckRules.DuplicateBeamRule);
        Assert.Equal(GeometryCheckRules.WarningStatus, duplicate.Status);
        Assert.Equal(new[] { 10, 20 }, duplicate.ObjectIds);
    }

    [Fact]
    public void Duplicate_Beam_Uses_Tolerance_Boundary_And_Normalized_Profile()
    {
        var result = GeometryCheckRules.Check(new[]
        {
            Part(12, profile: "  hea300  ",
                start: Point(1d, 0d, 0d), end: Point(101d, 0d, 0d)),
            Part(10, profile: "HEA300",
                start: Point(0d, 0d, 0d), end: Point(100d, 0d, 0d))
        });

        Assert.Equal(1, result.DuplicateBeamGroupCount);
        var duplicate = Assert.Single(result.Findings,
            finding => finding.Rule == GeometryCheckRules.DuplicateBeamRule);
        Assert.Equal(new[] { 10, 12 }, duplicate.ObjectIds);
    }

    [Fact]
    public void Parts_Without_Both_Endpoints_Are_Skipped_Only_For_Duplicate_Checking()
    {
        var result = GeometryCheckRules.Check(new[]
        {
            Part(1, start: Point(0d, 0d, 0d), end: Point(10d, 0d, 0d)),
            Part(2, start: null, end: null),
            Part(3, start: Point(0d, 0d, 0d), end: null)
        });

        Assert.Equal(1, result.PartsChecked);
        Assert.Equal(2, result.PartsNotChecked);
        Assert.Equal(0, result.DuplicateBeamGroupCount);
    }

    [Fact]
    public void Beams_Outside_The_Exact_Tolerance_Are_Not_Duplicates()
    {
        var result = GeometryCheckRules.Check(new[]
        {
            Part(1, profile: "HEA300",
                start: Point(0d, 0d, 0d), end: Point(100d, 0d, 0d)),
            Part(2, profile: "HEA300",
                start: Point(1.001d, 0d, 0d), end: Point(101.001d, 0d, 0d)),
            Part(3, profile: "HEB300",
                start: Point(0d, 0d, 0d), end: Point(100d, 0d, 0d))
        });

        Assert.Equal(0, result.DuplicateBeamGroupCount);
        Assert.DoesNotContain(result.Findings,
            finding => finding.Rule == GeometryCheckRules.DuplicateBeamRule);
    }

    [Fact]
    public void Output_Order_Is_Stable_Regardless_Of_Input_Order()
    {
        var parts = new[]
        {
            Part(30, profile: " ", lengthMm: null),
            Part(40, profile: string.Empty, start: Point(100d, 0d, 0d), end: Point(0d, 0d, 0d)),
            Part(10, profile: "HEA300", lengthMm: 9d),
            Part(20, profile: " ", start: Point(0d, 0d, 0d), end: Point(100d, 0d, 0d))
        };

        var forward = GeometryCheckRules.Check(parts);
        var reverse = GeometryCheckRules.Check(parts.Reverse());

        Assert.Equal(
            forward.Findings.Select(DescribeFinding),
            reverse.Findings.Select(DescribeFinding));
        Assert.Equal(new[]
        {
            "SHORT_OR_ZERO_LENGTH:10",
            "UNKNOWN_LENGTH:30",
            "EMPTY_PROFILE:20",
            "EMPTY_PROFILE:30",
            "EMPTY_PROFILE:40",
            "DUPLICATE_BEAM:20,40"
        }, forward.Findings.Select(DescribeFinding));
    }

    private static GeometryCheckPart Part(
        int id,
        string? profile = "HEA300",
        double? lengthMm = 10d,
        GeometryPointMm? start = null,
        GeometryPointMm? end = null) =>
        new(id, profile, lengthMm, start, end);

    private static GeometryPointMm Point(double x, double y, double z) =>
        new(x, y, z);

    private static string DescribeFinding(
        SteelValidation.Abstractions.GeometryCheckFinding finding) =>
        finding.Rule + ":" + string.Join(",", finding.ObjectIds);
}
