using System.Collections.Generic;

namespace SteelValidation.Abstractions
{
    /// <summary>
    /// Numbering rule results for one kind of position (part or assembly).
    /// </summary>
    public class NumberingPositionReport
    {
        public string PositionKind { get; set; } = string.Empty;

        public int MissingPositionCount { get; set; }

        public int UnparsedPositionCount { get; set; }

        /// <summary>Positions Tekla marked "(?)": numbering is out of date.</summary>
        public int OutOfDatePositionCount { get; set; }

        public int DuplicatePositionGroupCount { get; set; }

        public int TotalMissingNumberCount { get; set; }

        public List<NumberingFinding> Findings { get; set; }
            = new List<NumberingFinding>();

        public List<NumberingGap> Gaps { get; set; }
            = new List<NumberingGap>();
    }
}
