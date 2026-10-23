using System.Collections.Generic;

namespace SteelValidation.Core.Reporting
{
    /// <summary>
    /// A built QA report: the ordered findings, the CSV text and the text
    /// summary. Checks whose result was missing are listed in NotRunChecks
    /// and contribute no rows.
    /// </summary>
    public sealed class QaReport
    {
        internal QaReport(
            IReadOnlyList<QaReportRow> rows,
            IReadOnlyList<string> notRunChecks,
            string csv,
            string summary)
        {
            Rows = rows;
            NotRunChecks = notRunChecks;
            Csv = csv;
            Summary = summary;
        }

        public IReadOnlyList<QaReportRow> Rows { get; }

        public IReadOnlyList<string> NotRunChecks { get; }

        public string Csv { get; }

        public string Summary { get; }
    }
}
