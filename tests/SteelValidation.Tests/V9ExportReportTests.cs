using System;
using System.Linq;
using SteelValidation.Abstractions;
using SteelValidation.Core.Drawings;
using SteelValidation.Core.Evidence;
using SteelValidation.Core.Numbering;
using SteelValidation.Core.Reporting;
using Xunit;

namespace SteelValidation.Tests;

public sealed class V9ExportReportTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 20, 0, 0, DateTimeKind.Utc);
    private const string Model = "model-A";
    private static EvidenceContext Context(string model = Model, string token = "S1") => new(model, token, Now, TimeSpan.FromMinutes(30), null);
    private static ExportRecord Record(ExportKind kind, string key = "D1", string model = Model, string token = "S1", string? hash = null) =>
        new(kind, key, "output", true, 100, hash ?? new string('a', 64), Now.AddMinutes(-1), model, token,
            kind == ExportKind.NcFile ? "ST\norder\ndrawing\nphase\nD1\nS235\n1\nPL10\nB\n200\nEN\n" : null);
    private static ExpectedExport Expected(ExportKind kind) => new(kind, "D1", kind == ExportKind.NcFile ? "PL10" : null, kind == ExportKind.NcFile ? 200 : null);
    private static ExportVerificationReport AllExports(string model = Model) => ExportVerifier.Verify(
        Enum.GetValues(typeof(ExportKind)).Cast<ExportKind>().Select(Expected),
        Enum.GetValues(typeof(ExportKind)).Cast<ExportKind>().Select(k => Record(k, model: model)), Context(model));
    private static QaReport CleanQa() => QaReportBuilder.Build(new ModelCheckResult(), new GeometryCheckResult(), new NumberingReportResult(), Model, Now);
    private static DrawingReadinessReport CurrentDrawings()
    {
        var parts = new[] { new NumberingReportPart(1, "PL-1", "A-1", true, null, null, 100) };
        var drawings = new[] {
            new DrawingRecord(DrawingKind.SinglePart,"PL.1","single",DrawingInventoryRules.UpToDateStatusName,false,false,false,false,false,1),
            new DrawingRecord(DrawingKind.Assembly,"A.1","assembly",DrawingInventoryRules.UpToDateStatusName,false,false,false,false,false,100) };
        return DrawingReadinessRules.Build(DrawingInventoryRules.Build(drawings, parts), drawings);
    }

    [Theory]
    [InlineData(ExportKind.DrawingDwg)]
    [InlineData(ExportKind.MaterialList)]
    public void AddedKindsUseExistingMetadataAndFreshnessChecks(ExportKind kind)
    {
        Assert.True(ExportVerifier.Verify(new[] { Expected(kind) }, new[] { Record(kind) }, Context()).IsComplete);
        Assert.Equal(ExportCode.Missing, ExportVerifier.Verify(new[] { Expected(kind) }, Array.Empty<ExportRecord>(), Context()).Items.Single().Code);
        Assert.Equal(ExportCode.Stale, ExportVerifier.Verify(new[] { Expected(kind) }, new[] { Record(kind, token: "old") }, Context()).Items.Single().Code);
        Assert.Equal(ExportCode.WrongModel, ExportVerifier.Verify(new[] { Expected(kind) }, new[] { Record(kind, model: "other") }, Context()).Items.Single().Code);
    }

    [Fact]
    public void UnknownKindsAndBlankExpectedKeysAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpectedExport((ExportKind)999, "D1"));
        Assert.Throws<ArgumentException>(() => new ExpectedExport(ExportKind.DrawingDwg, " "));
    }

    [Fact]
    public void DuplicateExpectedEntriesCannotProduceCompleteReport()
    {
        var e = Expected(ExportKind.DrawingDwg);
        var report = ExportVerifier.Verify(new[] { e, e }, new[] { Record(ExportKind.DrawingDwg) }, Context());
        Assert.False(report.IsComplete);
        Assert.All(report.Items, i => Assert.Equal(ExportCode.Duplicate, i.Code));
    }

    [Fact]
    public void SixtyFourNonHexCharactersAreNotSha256Evidence()
    {
        var report = ExportVerifier.Verify(new[] { Expected(ExportKind.MaterialList) },
            new[] { Record(ExportKind.MaterialList, hash: new string('z', 64)) }, Context());
        Assert.Equal(ExportCode.HashMissing, report.Items.Single().Code);
    }

    [Fact]
    public void MissingStateTokenNeverVerifiesExports()
    {
        var report = ExportVerifier.Verify(new[] { Expected(ExportKind.DrawingDwg) }, new[] { Record(ExportKind.DrawingDwg, token: "") }, Context(token: ""));
        Assert.False(report.IsComplete);
        Assert.Equal(ExportCode.Stale, report.Items.Single().Code);
    }

    [Fact]
    public void MetadataCompleteNeverImpliesProductionReleaseByDefault()
    {
        var report = V9ResultReportBuilder.Build(Model, CleanQa(), AllExports(), CurrentDrawings(), true, Now);
        Assert.True(report.ExportMetadataComplete);
        Assert.False(report.IsReadyForRelease);
        Assert.Contains(V9ResultReportBuilder.ProductionVerificationMissing, report.Blockers);
        Assert.Contains("NC yalniz baslik", report.Document.Summary);
    }

    [Fact]
    public void MissingQaAndOwnerConfirmationRemainVisible()
    {
        var report = V9ResultReportBuilder.Build(Model, null, null, null, false, Now);
        Assert.False(report.IsReadyForRelease);
        Assert.Contains(V9ResultReportBuilder.QaMissing, report.Blockers);
        Assert.Contains(FinalDrawingGate.ReleaseNotConfirmed, report.Blockers);
        Assert.Contains(FinalDrawingGate.ExportsIncomplete, report.Blockers);
        Assert.Contains("CALISTIRILMADI", report.Document.Summary);
        Assert.Equal(3, report.Document.NotRunChecks.Count);
    }

    [Fact]
    public void ForeignModelReportCannotBeMergedIntoReadyResult()
    {
        var report = V9ResultReportBuilder.Build(Model, CleanQa(), AllExports("other"), CurrentDrawings(), true, Now, true);
        Assert.False(report.IsReadyForRelease);
        Assert.Contains(V9ResultReportBuilder.ExportModelMismatch, report.Blockers);
    }

    [Fact]
    public void PartialExportKindScopeCannotClaimCompleteV9()
    {
        var exports = ExportVerifier.Verify(new[] { Expected(ExportKind.DrawingPdf) }, new[] { Record(ExportKind.DrawingPdf) }, Context());
        var report = V9ResultReportBuilder.Build(Model, CleanQa(), exports, CurrentDrawings(), true, Now, true);
        Assert.False(report.IsReadyForRelease);
        Assert.Contains(report.Blockers, b => b == V9ResultReportBuilder.ExportKindMissing + ":DrawingDwg");
        Assert.Contains(report.Blockers, b => b == V9ResultReportBuilder.ExportKindMissing + ":MaterialList");
    }

    [Fact]
    public void CompleteExportEvidenceCannotOverrideUnboundQaAndDrawings()
    {
        var report = V9ResultReportBuilder.Build(Model, CleanQa(), AllExports(), CurrentDrawings(), true, Now, true);
        Assert.Equal(new[] { V9ResultReportBuilder.QaDrawingBindingUnverified }, report.Blockers);
        Assert.False(report.IsReadyForRelease);
        Assert.True(report.ExportMetadataComplete);
        Assert.Equal(5, report.Document.Rows.Count);
        Assert.Contains("EXPORT,DrawingDwg,PASS", report.Document.Csv);
        Assert.Contains("EXPORT,MaterialList,PASS", report.Document.Csv);
    }
    [Fact]
    public void ForeignQaCannotBeWaivedByCallerConfirmationBooleans()
    {
        var foreignQa = QaReportBuilder.Build(new ModelCheckResult(), new GeometryCheckResult(), new NumberingReportResult(), "other-model", Now);
        var report = V9ResultReportBuilder.Build(Model, foreignQa, AllExports(), CurrentDrawings(), true, Now, true);
        Assert.False(report.IsReadyForRelease);
        Assert.Contains(V9ResultReportBuilder.QaDrawingBindingUnverified, report.Blockers);
        Assert.Contains("serbest birakma yetkisi vermez", report.Document.Summary);
    }

}
