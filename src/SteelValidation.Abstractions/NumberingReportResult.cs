using System.Collections.Generic;

namespace SteelValidation.Abstractions
{
    /// <summary>
    /// The complete, read-only V4 numbering report over a steel-part scope.
    /// The report only describes numbering; it never renumbers parts or
    /// repairs gaps. Part positions and assembly positions are evaluated
    /// separately with the same rules.
    /// </summary>
    public class NumberingReportResult
    {
        public int PartsRead { get; set; }

        public int NumberingUpToDateCount { get; set; }

        public int NumberingNotUpToDateCount { get; set; }

        public int NumberingStatusUnknownCount { get; set; }

        public int MaxMissingNumbersPerPrefix { get; set; }

        public List<NumberingPartItem> Parts { get; set; }
            = new List<NumberingPartItem>();

        public NumberingPositionReport PartPositions { get; set; }
            = new NumberingPositionReport();

        public NumberingPositionReport AssemblyPositions { get; set; }
            = new NumberingPositionReport();
    }
}
