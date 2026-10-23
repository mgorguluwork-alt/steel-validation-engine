using SteelValidation.Abstractions;
using SteelValidation.Core.Numbering;
using SteelValidation.Core.Reporting;

namespace SteelValidation.Tests;

public sealed class QaReportBuilderTests
{
    private static readonly DateTime Timestamp = new(2026, 9, 28, 13, 5, 9);

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("line1\r\nline2", "\"line1\r\nline2\"")]
    [InlineData("\"", "\"\"\"\"")]
    public void Csv_Fields_Are_Quoted_And_Escaped(string? field, string expected)
    {
        Assert.Equal(expected, QaReportBuilder.EscapeCsvField(field));
    }

    [Fact]
    public void Csv_Record_Keeps_Commas_Quotes_And_Newlines_Inside_One_Field()
    {
        var geometry = new GeometryCheckResult
        {
            Findings =
            {
                Finding("SHORT_OR_ZERO_LENGTH", "WARNING", "Too short, \"check\"\nnow", 7)
            }
        };

        var csv = QaReportBuilder.Build(null, geometry, null, "M", Timestamp).Csv;

        Assert.Equal(
            "Check,Rule,Status,ObjectId,Name,Profile,Value,Message\r\n" +
            "GEOMETRY,SHORT_OR_ZERO_LENGTH,WARNING,7,,,,\"Too short, \"\"check\"\"\nnow\"\r\n",
            csv);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    [InlineData("fr-FR")]
    public void Numbers_Use_The_Invariant_Culture(string culture)
    {
        using var scope = new CultureScope(culture);

        var model = new ModelCheckResult
        {
            LengthFindings =
            {
                new ModelLengthFinding
                {
                    ObjectId = 12345,
                    Name = "ANGLE",
                    Profile = "L50*5",
                    LengthMm = 6000.5,
                    LimitMm = 6000,
                    Status = "TooLong",
                    Message = "m"
                }
            }
        };

        var numbering = new NumberingReportResult();
        numbering.PartPositions.Gaps.Add(new NumberingGap
        {
            PositionKind = "PART",
            Prefix = "P",
            HighestNumber = 1500,
            MissingCount = 1200,
            MissingNumbers = { 1, 2 }
        });

        var report = QaReportBuilder.Build(model, null, numbering, "M", Timestamp);
        var lines = SplitRecords(report.Csv);

        Assert.Equal("MODEL,LENGTH,TooLong,12345,ANGLE,L50*5,6000.5,m", lines[1]);
        Assert.Contains("1200 missing between 1 and 1500: 1, 2 (first 2 listed)", lines[2]);
        Assert.Contains("Tarih: 2026-09-28 13:05:09", report.Summary);
        Assert.Equal("QA-report-20260928-130509", QaReportBuilder.FileBaseName(Timestamp));
    }

    [Fact]
    public void Unknown_Length_Is_Written_As_An_Empty_Value()
    {
        var model = new ModelCheckResult
        {
            LengthFindings =
            {
                new ModelLengthFinding { ObjectId = 1, Status = "LengthUnknown", LengthMm = null }
            }
        };

        var row = Assert.Single(QaReportBuilder.BuildRows(model, null, null));
        Assert.Equal(string.Empty, row.Value);
    }

    [Fact]
    public void Rows_Are_Ordered_By_Check_Then_Object_Id_Then_Rule()
    {
        var model = new ModelCheckResult
        {
            NameEmptyItems = { new ModelCheckItem { ObjectId = 30, Status = "ERROR - NAME EMPTY" } },
            LengthFindings =
            {
                new ModelLengthFinding { ObjectId = 30, Status = "Ok" },
                new ModelLengthFinding { ObjectId = 10, Status = "Ok" }
            },
            ConsistencyFindings =
            {
                new ModelConsistencyFinding { ObjectId = 10, Status = "Ok" },
                new ModelConsistencyFinding { ObjectId = 30, Status = "NameEmpty" }
            }
        };

        var geometry = new GeometryCheckResult
        {
            Findings =
            {
                Finding("DUPLICATE_BEAM", "WARNING", "d", 9, 4),
                Finding("EMPTY_PROFILE", "WARNING", "e", 4),
                Finding("UNKNOWN_LENGTH", "UNKNOWN", "u", 2)
            }
        };

        var numbering = new NumberingReportResult();
        numbering.PartPositions.Findings.Add(NumberingFinding("MISSING_POSITION", 8));
        numbering.PartPositions.Gaps.Add(new NumberingGap { PositionKind = "PART", Prefix = "P", HighestNumber = 3, MissingCount = 1, MissingNumbers = { 2 } });
        numbering.AssemblyPositions.Findings.Add(NumberingFinding("DUPLICATE_POSITION", 3, 8));
        numbering.Parts.Add(new NumberingPartItem { ObjectId = 5, NumberingStatus = NumberingRules.NotUpToDateStatus, IsNumberingUpToDate = false });
        numbering.Parts.Add(new NumberingPartItem { ObjectId = 6, NumberingStatus = NumberingRules.UpToDateStatus, IsNumberingUpToDate = true });

        var rows = QaReportBuilder.BuildRows(model, geometry, numbering);

        Assert.Equal(
            new[]
            {
                "MODEL|10|CONSISTENCY",
                "MODEL|10|LENGTH",
                "MODEL|30|CONSISTENCY",
                "MODEL|30|LENGTH",
                "MODEL|30|NAME_EMPTY",
                "GEOMETRY|2|UNKNOWN_LENGTH",
                "GEOMETRY|4;9|DUPLICATE_BEAM",
                "GEOMETRY|4|EMPTY_PROFILE",
                "NUMBERING|3;8|DUPLICATE_POSITION",
                "NUMBERING|5|NUMBERING_STATUS",
                "NUMBERING|8|MISSING_POSITION",
                "NUMBERING||NUMBERING_GAP"
            },
            rows.Select(row => row.Check + "|" + string.Join(";", row.ObjectIds) + "|" + row.Rule));

        var first = QaReportBuilder.Build(model, geometry, numbering, "M", Timestamp);
        var second = QaReportBuilder.Build(model, geometry, numbering, "M", Timestamp);
        Assert.Equal(first.Csv, second.Csv);
        Assert.Equal(first.Summary, second.Summary);
    }

    [Fact]
    public void Equal_Sort_Keys_Keep_Source_Order()
    {
        var geometry = new GeometryCheckResult
        {
            Findings =
            {
                Finding("R", "WARNING", "first", 1),
                Finding("R", "WARNING", "second", 1)
            }
        };

        var rows = QaReportBuilder.BuildRows(null, geometry, null);

        Assert.Equal(new[] { "first", "second" }, rows.Select(row => row.Message));
    }

    [Fact]
    public void Missing_Model_Check_Is_Reported_As_Not_Run()
    {
        var report = QaReportBuilder.Build(null, SomeGeometry(), SomeNumbering(), "M", Timestamp);

        Assert.Equal(new[] { "MODEL" }, report.NotRunChecks);
        Assert.DoesNotContain(report.Rows, row => row.Check == "MODEL");
        Assert.Contains("MODEL (isim / profil / uzunluk): CALISTIRILMADI", report.Summary);
        Assert.Contains("GEOMETRI: 1 sorun (1 kayit incelendi)", report.Summary);
    }

    [Fact]
    public void Missing_Geometry_Check_Is_Reported_As_Not_Run()
    {
        var report = QaReportBuilder.Build(SomeModel(), null, SomeNumbering(), "M", Timestamp);

        Assert.Equal(new[] { "GEOMETRY" }, report.NotRunChecks);
        Assert.DoesNotContain(report.Rows, row => row.Check == "GEOMETRY");
        Assert.Contains("GEOMETRI: CALISTIRILMADI", report.Summary);
    }

    [Fact]
    public void Missing_Numbering_Report_Is_Reported_As_Not_Run()
    {
        var report = QaReportBuilder.Build(SomeModel(), SomeGeometry(), null, "M", Timestamp);

        Assert.Equal(new[] { "NUMBERING" }, report.NotRunChecks);
        Assert.DoesNotContain(report.Rows, row => row.Check == "NUMBERING");
        Assert.Contains("NUMARALANDIRMA: CALISTIRILMADI", report.Summary);
    }

    [Fact]
    public void All_Missing_Gives_A_Header_Only_Csv_And_No_Findings()
    {
        var report = QaReportBuilder.Build(null, null, null, null, Timestamp);

        Assert.Empty(report.Rows);
        Assert.Equal(new[] { "MODEL", "GEOMETRY", "NUMBERING" }, report.NotRunChecks);
        Assert.Equal("Check,Rule,Status,ObjectId,Name,Profile,Value,Message\r\n", report.Csv);
        Assert.Contains("Model: (bilinmiyor)", report.Summary);
        Assert.Contains("SONUC: Sorun bulunmadi.", report.Summary);
        Assert.Contains("MODEL (isim / profil / uzunluk): CALISTIRILMADI", report.Summary);
        Assert.Contains("GEOMETRI: CALISTIRILMADI", report.Summary);
        Assert.Contains("NUMARALANDIRMA: CALISTIRILMADI", report.Summary);
    }

    [Fact]
    public void A_Check_That_Ran_With_No_Findings_Is_Not_Reported_As_Not_Run()
    {
        var report = QaReportBuilder.Build(new ModelCheckResult(), null, null, "M", Timestamp);

        Assert.DoesNotContain("MODEL", report.NotRunChecks);
        Assert.Contains("MODEL (isim / profil / uzunluk): sorun yok (0 kayit incelendi)", report.Summary);
    }

    [Fact]
    public void Summary_Counts_Findings_Per_Check_And_Status()
    {
        var model = new ModelCheckResult
        {
            NameEmptyItems = { new ModelCheckItem { ObjectId = 1, Status = "ERROR - NAME EMPTY" } },
            LengthFindings =
            {
                new ModelLengthFinding { ObjectId = 1, Status = "Ok" },
                new ModelLengthFinding { ObjectId = 2, Status = "TooLong" }
            },
            ConsistencyFindings =
            {
                new ModelConsistencyFinding { ObjectId = 1, Status = "NameEmpty" },
                new ModelConsistencyFinding { ObjectId = 2, Status = "Ok" }
            }
        };

        var geometry = new GeometryCheckResult
        {
            Findings =
            {
                Finding("UNKNOWN_LENGTH", "UNKNOWN", "u", 1),
                Finding("EMPTY_PROFILE", "WARNING", "e", 2),
                Finding("DUPLICATE_BEAM", "WARNING", "d", 2, 3)
            }
        };

        var report = QaReportBuilder.Build(model, geometry, SomeNumbering(), "Plant, \"A\"", Timestamp);

        Assert.Contains("STEEL VALIDATION ENGINE - KALITE RAPORU", report.Summary);
        Assert.Contains("SONUC: 10 sorun bulundu.", report.Summary);
        Assert.Contains("GEOMETRI: 3 sorun (3 kayit incelendi)", report.Summary);
        Assert.Contains("Tum ayrintilar ayni isimli CSV dosyasinda.", report.Summary);
    }

    [Fact]
    public void Csv_Has_One_Data_Record_Per_Finding()
    {
        var report = QaReportBuilder.Build(SomeModel(), SomeGeometry(), SomeNumbering(), "M", Timestamp);

        // Every field here is single-line, so records equal lines.
        Assert.Equal(report.Rows.Count + 1, SplitRecords(report.Csv).Length);
        Assert.Equal(1 + 1 + 2, report.Rows.Count);
    }

    [Fact]
    public void Build_Does_Not_Change_The_Input_Results()
    {
        var geometry = new GeometryCheckResult
        {
            Findings = { Finding("DUPLICATE_BEAM", "WARNING", "d", 9, 4) }
        };

        QaReportBuilder.Build(null, geometry, null, "M", Timestamp);

        Assert.Equal(new[] { 9, 4 }, geometry.Findings[0].ObjectIds);
    }

    [Fact]
    public void Write_Creates_Both_Files_And_Never_Overwrites()
    {
        var folder = Path.Combine(Path.GetTempPath(), "qa-report-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var report = QaReportBuilder.Build(SomeModel(), null, null, "M", Timestamp);

            var files = QaReportFiles.Write(folder, report, Timestamp);

            Assert.Equal(Path.Combine(folder, "QA-report-20260928-130509.csv"), files.CsvPath);
            Assert.Equal(Path.Combine(folder, "QA-report-20260928-130509.txt"), files.SummaryPath);
            Assert.Equal(report.Csv, File.ReadAllText(files.CsvPath));
            Assert.Equal(report.Summary, File.ReadAllText(files.SummaryPath));

            File.WriteAllText(files.SummaryPath, "operator notes");
            File.Delete(files.CsvPath);

            Assert.Throws<IOException>(() => QaReportFiles.Write(folder, report, Timestamp));
            Assert.False(File.Exists(files.CsvPath), "No CSV may be written when the summary already exists.");
            Assert.Equal("operator notes", File.ReadAllText(files.SummaryPath));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData(@"C:\Models\Plant", @"C:\Models\Plant", true)]
    [InlineData(@"C:\Models\Plant\", @"C:\Models\Plant", true)]
    [InlineData(@"C:\Models\Plant\Reports", @"C:\Models\Plant", true)]
    [InlineData(@"c:\models\plant\reports", @"C:\Models\Plant\", true)]
    [InlineData(@"C:\Models\Plant\..\Plant\Reports", @"C:\Models\Plant", true)]
    [InlineData(@"C:\Models\Plant2", @"C:\Models\Plant", false)]
    [InlineData(@"C:\Models", @"C:\Models\Plant", false)]
    [InlineData(@"D:\Reports", @"C:\Models\Plant", false)]
    [InlineData(@"D:\Reports", "", false)]
    public void Folder_Inside_Model_Folder_Is_Detected(string path, string folder, bool expected)
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Equal(expected, QaReportFiles.IsInsideFolder(path, folder));
    }

    private static ModelCheckResult SomeModel() => new()
    {
        LengthFindings = { new ModelLengthFinding { ObjectId = 1, Status = "Ok", LengthMm = 100 } }
    };

    private static GeometryCheckResult SomeGeometry() => new()
    {
        Findings = { Finding("EMPTY_PROFILE", "WARNING", "e", 2) }
    };

    private static NumberingReportResult SomeNumbering()
    {
        var numbering = new NumberingReportResult();
        numbering.PartPositions.Findings.Add(NumberingFinding("MISSING_POSITION", 3));
        numbering.Parts.Add(new NumberingPartItem { ObjectId = 3, NumberingStatus = NumberingRules.NotUpToDateStatus, IsNumberingUpToDate = false });
        numbering.Parts.Add(new NumberingPartItem { ObjectId = 4, NumberingStatus = NumberingRules.UpToDateStatus, IsNumberingUpToDate = true });
        return numbering;
    }

    private static GeometryCheckFinding Finding(string rule, string status, string message, params int[] ids) => new()
    {
        Rule = rule,
        Status = status,
        Message = message,
        ObjectIds = ids.ToList()
    };

    private static NumberingFinding NumberingFinding(string rule, params int[] ids) => new()
    {
        Rule = rule,
        Status = NumberingRules.WarningStatus,
        PositionKind = "PART",
        ObjectIds = ids.ToList(),
        Message = "n"
    };

    private static string[] SplitRecords(string csv) =>
        csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
}
