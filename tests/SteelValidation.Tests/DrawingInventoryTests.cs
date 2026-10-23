using SteelValidation.Core.Drawings;
using SteelValidation.Core.Numbering;

namespace SteelValidation.Tests;

public sealed class DrawingInventoryTests
{
    private static NumberingReportPart Part(int id, string? position, string? assembly = "A-1", bool? upToDate = true) =>
        new NumberingReportPart(id, position, assembly, upToDate);

    private static DrawingRecord Drawing(
        DrawingKind kind,
        int? target = null,
        string status = DrawingInventoryRules.UpToDateStatusName,
        bool issued = false,
        bool issuedButModified = false,
        string mark = "") =>
        new DrawingRecord(kind, mark, "d", status, issued, issuedButModified, false, false, false, target);

    [Fact]
    public void A_Model_Without_Drawings_Lists_Every_Current_Part_Position_As_Uncovered()
    {
        var parts = new[] { Part(1, "P-2"), Part(2, "P-1"), Part(3, "P-1") };

        var report = DrawingInventoryRules.Build(Array.Empty<DrawingRecord>(), parts);

        Assert.Equal(0, report.TotalDrawings);
        Assert.All(report.CountByKind.Values, count => Assert.Equal(0, count));
        Assert.Equal(2, report.PartPositionCount);
        Assert.Equal(0, report.PartPositionsWithSinglePartDrawing);
        Assert.Equal(new[] { "P-1", "P-2" }, report.PartPositionsWithoutSinglePartDrawing);
        Assert.False(report.NumberingBlocksDrawingCreation);
    }

    [Fact]
    public void A_Single_Part_Drawing_Covers_Its_Position_For_All_Identical_Parts()
    {
        var parts = new[] { Part(1, "P-1"), Part(2, "P-1"), Part(3, "P-2") };
        var drawings = new[] { Drawing(DrawingKind.SinglePart, target: 2) };

        var report = DrawingInventoryRules.Build(drawings, parts);

        Assert.Equal(1, report.PartPositionsWithSinglePartDrawing);
        Assert.Equal(new[] { "P-2" }, report.PartPositionsWithoutSinglePartDrawing);
        Assert.Equal(1, report.CountByKind[DrawingKind.SinglePart]);
    }

    [Fact]
    public void Out_Of_Date_And_Issued_But_Modified_Drawings_Need_Attention()
    {
        var drawings = new[]
        {
            Drawing(DrawingKind.Assembly, status: "PartsWereModified", mark: "[A-2]"),
            Drawing(DrawingKind.SinglePart, issued: true, issuedButModified: true, mark: "[P-1]"),
            Drawing(DrawingKind.GeneralArrangement, mark: "[G-1]"),
            Drawing(DrawingKind.SinglePart, issued: true, mark: "[P-2]")
        };

        var report = DrawingInventoryRules.Build(drawings, Array.Empty<NumberingReportPart>());

        Assert.Equal(4, report.TotalDrawings);
        Assert.Equal(1, report.NotUpToDateCount);
        Assert.Equal(2, report.IssuedCount);
        Assert.Equal(1, report.IssuedButModifiedCount);
        Assert.Equal(new[] { "[P-1]", "[A-2]" }, report.Attention.Select(d => d.Mark));
    }

    [Theory]
    [InlineData("P-1(?)", "A-1", true)]
    [InlineData("P-1", "A-1(?)", true)]
    [InlineData("P-1", "", true)]
    [InlineData("P-1", "A-1", false)]
    public void Missing_Or_Out_Of_Date_Numbering_Blocks_Drawing_Creation(string part, string assembly, bool blocks)
    {
        var report = DrawingInventoryRules.Build(Array.Empty<DrawingRecord>(), new[] { Part(1, part, assembly) });

        Assert.Equal(blocks, report.NumberingBlocksDrawingCreation);
    }

    [Fact]
    public void A_Part_Whose_Numbering_Is_Not_Up_To_Date_Blocks_Drawing_Creation()
    {
        var report = DrawingInventoryRules.Build(Array.Empty<DrawingRecord>(), new[] { Part(1, "P-1", upToDate: false) });

        Assert.True(report.NumberingBlocksDrawingCreation);
    }

    [Fact]
    public void Out_Of_Date_Positions_Are_Not_Counted_As_Part_Positions()
    {
        var parts = new[] { Part(1, "P-1"), Part(2, "P-3(?)") };

        var report = DrawingInventoryRules.Build(Array.Empty<DrawingRecord>(), parts);

        Assert.Equal(1, report.PartPositionCount);
        Assert.Equal(new[] { "P-1" }, report.PartPositionsWithoutSinglePartDrawing);
    }

    [Fact]
    public void A_Drawing_Whose_Part_Changed_Is_Not_Reported_As_Missing()
    {
        // Live test 2026-10-05: P-13 had the drawing "[P.13]" whose part was
        // modified after numbering; the other P-13 parts were reported "no drawing".
        var parts = new[] { Part(1, "P-13(?)"), Part(2, "P-13"), Part(3, "R-1") };
        var drawings = new[] { Drawing(DrawingKind.SinglePart, target: 1, status: "PartsWereModified", mark: "[P.13]") };

        var report = DrawingInventoryRules.Build(drawings, parts);

        Assert.Equal(new[] { "P-13" }, report.PartPositionsWithChangedSinglePartDrawing);
        Assert.Equal(new[] { "R-1" }, report.PartPositionsWithoutSinglePartDrawing);
    }

    [Theory]
    [InlineData("[PL.26]")]
    [InlineData("PL.26")]
    [InlineData("PL-26")]
    public void A_Drawing_Mark_Alone_Shows_The_Drawing_Exists(string mark)
    {
        var parts = new[] { Part(1, "PL-26"), Part(2, "PL-3") };
        var drawings = new[] { Drawing(DrawingKind.SinglePart, target: null, mark: mark) };

        var report = DrawingInventoryRules.Build(drawings, parts);

        Assert.Equal(new[] { "PL-26" }, report.PartPositionsWithChangedSinglePartDrawing);
        Assert.Equal(new[] { "PL-3" }, report.PartPositionsWithoutSinglePartDrawing);
    }

    [Fact]
    public void A_Current_Drawing_Is_Neither_Missing_Nor_Changed()
    {
        var parts = new[] { Part(1, "PL-26") };
        var drawings = new[] { Drawing(DrawingKind.SinglePart, target: 1, mark: "PL.26") };

        var report = DrawingInventoryRules.Build(drawings, parts);

        Assert.Empty(report.PartPositionsWithChangedSinglePartDrawing);
        Assert.Empty(report.PartPositionsWithoutSinglePartDrawing);
        Assert.Equal(1, report.PartPositionsWithSinglePartDrawing);
    }

    [Fact]
    public void Null_Input_Is_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => DrawingInventoryRules.Build(null!, Array.Empty<NumberingReportPart>()));
        Assert.Throws<ArgumentNullException>(() => DrawingInventoryRules.Build(Array.Empty<DrawingRecord>(), null!));
        Assert.Throws<ArgumentException>(() => DrawingInventoryRules.Build(new DrawingRecord[] { null! }, Array.Empty<NumberingReportPart>()));
    }

    private static NumberingReportPart InAssembly(int id, string? position, string? assemblyPosition, int assemblyId) =>
        new NumberingReportPart(id, position, assemblyPosition, true, null, null, assemblyId);

    [Fact]
    public void Assembly_Positions_Without_An_Assembly_Drawing_Are_Listed()
    {
        // RB-1 has two assemblies (10, 11); a drawing of either covers the position.
        var parts = new[]
        {
            InAssembly(1, "P-1", "RB-1", 10), InAssembly(2, "P-1", "RB-1", 11),
            InAssembly(3, "P-2", "C-1", 20), InAssembly(4, "P-3", "C-2(?)", 30),
            Part(5, "P-4", "C-3") // assembly id not read: not counted either way
        };
        var drawings = new[] { Drawing(DrawingKind.Assembly, target: 11) };

        var report = DrawingInventoryRules.Build(drawings, parts);

        Assert.Equal(2, report.AssemblyPositionCount);
        Assert.Equal(new[] { "C-1" }, report.AssemblyPositionsWithoutAssemblyDrawing);
    }

    [Theory]
    [InlineData("DrawingIsUpToDate", "Güncel")]
    [InlineData("PartsWereModified", "Parçalar değişti")]
    [InlineData("NumberOfPartsInNumberingSeriesDecreased", "Adet azaldı")]
    [InlineData("NumberOfPartsInNumberingSeriesIncreased", "Adet arttı")]
    [InlineData("SomethingNew", "SomethingNew")]
    [InlineData(null, "Bilinmiyor")]
    public void Drawing_Status_Is_Described_In_Turkish_And_Unknown_Is_Not_Guessed(string? status, string expected) =>
        Assert.Equal(expected, DrawingInventoryRules.DescribeStatusTurkish(status));
}
