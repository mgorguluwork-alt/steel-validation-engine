using System;
using System.Collections.Generic;
using System.Linq;
using SteelValidation.Core.Drawings;
using SteelValidation.Core.Evidence;
using SteelValidation.Core.Numbering;
using Xunit;

namespace SteelValidation.Tests;

public sealed class ExportVerificationTests
{
    private static readonly DateTime Now = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
    private const string Model = @"C:\MODELS\TEST_2026-10-19";
    private static readonly string Hash = new string('a', 64);

    private static EvidenceContext Ctx(string token = "T1") => new EvidenceContext(Model, token, Now, TimeSpan.FromMinutes(30), null);

    private static ExportRecord Pdf(string key = "D-1", bool exists = true, long size = 1000, string? hash = null, int ageMin = 2,
        string? model = Model, string token = "T1") =>
        new ExportRecord(ExportKind.DrawingPdf, key, key + ".pdf", exists, size, hash ?? Hash, Now.AddMinutes(-ageMin), model, token);

    private static string NcText(string mark = "PL.1", string profile = "PL10", string length = "200.0", bool start = true, bool end = true) =>
        (start ? "** header comment\r\nST\r\n" : "** header comment\r\n") +
        "  1\r\n  D1\r\n  1\r\n  " + mark + "\r\n  S235\r\n  1\r\n  " + profile + "\r\n  B\r\n  " + length + "\r\n" + (end ? "EN\r\n" : "");

    private static ExportRecord Nc(string key = "PL.1", string? text = null, long size = 500) =>
        new ExportRecord(ExportKind.NcFile, key, key + ".nc1", true, size, Hash, Now.AddMinutes(-2), Model, "T1", text ?? NcText());

    private static ExpectedExport ExpPdf(string key = "D-1") => new ExpectedExport(ExportKind.DrawingPdf, key);
    private static ExpectedExport ExpNc(string key = "PL.1", string profile = "PL10", double length = 200.0) =>
        new ExpectedExport(ExportKind.NcFile, key, profile, length);

    private static string Code(ExpectedExport e, ExportRecord r, EvidenceContext? c = null) =>
        ExportVerifier.Verify(new[] { e }, new[] { r }, c ?? Ctx()).Items.Single().Code;

    [Fact] public void Complete_And_Fresh_Exports_Are_Ok()
    {
        var report = ExportVerifier.Verify(new[] { ExpPdf(), ExpNc() }, new[] { Pdf(), Nc() }, Ctx());
        Assert.True(report.IsComplete);
        Assert.Equal(2, report.OkCount);
    }

    [Fact] public void Nothing_Expected_Is_Not_A_Positive_Verdict() =>
        Assert.False(ExportVerifier.Verify(Array.Empty<ExpectedExport>(), Array.Empty<ExportRecord>(), Ctx()).IsComplete);

    [Fact] public void Missing_Record_And_Missing_File_Are_Missing()
    {
        Assert.Equal(ExportCode.Missing, ExportVerifier.Verify(new[] { ExpPdf() }, Array.Empty<ExportRecord>(), Ctx()).Items.Single().Code);
        Assert.Equal(ExportCode.Missing, Code(ExpPdf(), Pdf(exists: false)));
    }

    [Fact] public void Empty_File_And_Bad_Hash_Are_Rejected()
    {
        Assert.Equal(ExportCode.EmptyFile, Code(ExpPdf(), Pdf(size: 0)));
        Assert.Equal(ExportCode.HashMissing, Code(ExpPdf(), Pdf(hash: "abc")));
        Assert.Equal(ExportCode.HashMissing, Code(ExpPdf(), new ExportRecord(ExportKind.DrawingPdf, "D-1", "x.pdf", true, 5, null, Now, Model, "T1")));
    }

    [Fact] public void Old_Export_Changed_Model_Or_No_Context_Is_Stale()
    {
        Assert.Equal(ExportCode.Stale, Code(ExpPdf(), Pdf(ageMin: 45)));
        Assert.Equal(ExportCode.Stale, Code(ExpPdf(), Pdf(token: "T0")));
        Assert.Equal(ExportCode.Stale, Code(ExpPdf(), Pdf(token: "")));
        Assert.Equal(ExportCode.Stale, ExportVerifier.Verify(new[] { ExpPdf() }, new[] { Pdf() }, null).Items.Single().Code);
    }

    [Fact] public void Export_Of_Another_Model_Is_WrongModel() =>
        Assert.Equal(ExportCode.WrongModel, Code(ExpPdf(), Pdf(model: @"C:\MODELS\OTHER_2026-10-19")));

    [Fact] public void Duplicate_And_Unexpected_Records_Are_Reported()
    {
        var dup = ExportVerifier.Verify(new[] { ExpPdf() }, new[] { Pdf(), Pdf() }, Ctx());
        Assert.Equal(ExportCode.Duplicate, dup.Items.Single().Code);

        var extra = ExportVerifier.Verify(new[] { ExpPdf() }, new[] { Pdf(), Pdf("D-9") }, Ctx());
        Assert.False(extra.IsComplete);
        Assert.Contains(extra.Items, i => i.Key == "D-9" && i.Code == ExportCode.Unexpected);
    }

    [Fact] public void Nc_Header_Is_Compared_With_The_Model()
    {
        Assert.Equal(ExportCode.Ok, Code(ExpNc(), Nc()));
        Assert.Equal(ExportCode.NcMarkMismatch, Code(ExpNc(), Nc(text: NcText(mark: "PL.2"))));
        Assert.Equal(ExportCode.NcProfileMismatch, Code(ExpNc(), Nc(text: NcText(profile: "PL12"))));
        Assert.Equal(ExportCode.NcLengthMismatch, Code(ExpNc(), Nc(text: NcText(length: "201.0"))));
        Assert.Equal(ExportCode.Ok, Code(ExpNc(), Nc(text: NcText(length: "200.4"))));
        Assert.Equal(ExportCode.Ok, Code(ExpNc(profile: "pl10"), Nc()));
    }

    [Fact] public void Nc_Structure_Problems_Are_Rejected()
    {
        Assert.Equal(ExportCode.NcNoStartBlock, Code(ExpNc(), Nc(text: NcText(start: false))));
        Assert.Equal(ExportCode.NcNoEndBlock, Code(ExpNc(), Nc(text: NcText(end: false))));
        Assert.Equal(ExportCode.NcUnparsable, Code(ExpNc(), Nc(text: "")));
        Assert.Equal(ExportCode.NcUnparsable, Code(ExpNc(), Nc(text: NcText(length: "abc"))));
    }

    [Fact] public void Nc_Without_A_Model_Expectation_Cannot_Be_Verified()
    {
        Assert.Equal(ExportCode.NcExpectationMissing, Code(new ExpectedExport(ExportKind.NcFile, "PL.1"), Nc()));
        Assert.Equal(ExportCode.NcExpectationMissing, Code(new ExpectedExport(ExportKind.NcFile, "PL.1", "PL10", null), Nc()));
    }

    [Fact] public void Header_Reader_Skips_Comments_And_Handles_Line_Endings()
    {
        var unix = NcHeaderReader.Read(NcText().Replace("\r\n", "\n"));
        Assert.Equal("PL.1", unix.PieceMark);
        Assert.Equal("PL10", unix.Profile);
        Assert.Equal(200.0, unix.LengthMm);
        Assert.True(unix.HasStartBlock && unix.HasEndBlock);
        Assert.False(NcHeaderReader.Read(null).HasStartBlock);
    }

    // ---- final gate

    private static DrawingReadinessReport CurrentDrawings()
    {
        var parts = new[] { new NumberingReportPart(1, "PL.1", "A.1", true, null, null, 100) };
        var drawings = new[]
        {
            new DrawingRecord(DrawingKind.SinglePart, "D-1", "d1", DrawingInventoryRules.UpToDateStatusName, false, false, false, false, false, 1),
            new DrawingRecord(DrawingKind.Assembly, "D-2", "d2", DrawingInventoryRules.UpToDateStatusName, false, false, false, false, false, 100)
        };
        return DrawingReadinessRules.Build(DrawingInventoryRules.Build(drawings, parts), drawings);
    }

    private static DrawingReadinessReport StaleDrawings()
    {
        var parts = new[] { new NumberingReportPart(1, "PL.1", "A.1", true, null, null, 100) };
        return DrawingReadinessRules.Build(DrawingInventoryRules.Build(Array.Empty<DrawingRecord>(), parts), Array.Empty<DrawingRecord>());
    }

    private static ExportVerificationReport GoodExports() => ExportVerifier.Verify(new[] { ExpPdf() }, new[] { Pdf() }, Ctx());
    private static ExportVerificationReport BadExports() => ExportVerifier.Verify(new[] { ExpPdf() }, Array.Empty<ExportRecord>(), Ctx());

    [Fact] public void Final_Gate_Needs_Current_Drawings_Complete_Exports_And_Owner_Confirmation()
    {
        Assert.True(CurrentDrawings().IsInventoryCurrent);
        Assert.True(FinalDrawingGate.IsReadyForRelease(CurrentDrawings(), GoodExports(), true));

        Assert.Equal(new[] { FinalDrawingGate.ReleaseNotConfirmed }, FinalDrawingGate.Blockers(CurrentDrawings(), GoodExports(), false));
        Assert.Equal(new[] { FinalDrawingGate.ExportsIncomplete }, FinalDrawingGate.Blockers(CurrentDrawings(), BadExports(), true));
        Assert.Equal(new[] { FinalDrawingGate.DrawingsNotCurrent }, FinalDrawingGate.Blockers(StaleDrawings(), GoodExports(), true));
    }

    [Fact] public void Final_Gate_Reports_Every_Blocker_And_Fails_Closed_On_Missing_Reports()
    {
        Assert.Equal(3, FinalDrawingGate.Blockers(StaleDrawings(), BadExports(), false).Count);
        Assert.Equal(3, FinalDrawingGate.Blockers(null, null, false).Count);
        Assert.False(FinalDrawingGate.IsReadyForRelease(null, GoodExports(), true));
        Assert.False(FinalDrawingGate.IsReadyForRelease(CurrentDrawings(), null, true));
    }
}
