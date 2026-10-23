using System.Collections.Generic;

namespace SteelValidation.Abstractions
{
    /// <summary>
    /// Numbers missing between the series start number (Tekla's numbering
    /// series StartNumber, 1 when unknown) and the highest number in use for
    /// one position prefix. MissingNumbers is capped; MissingCount is always
    /// the full total. A gap is reported only; it is never repaired.
    /// </summary>
    public class NumberingGap
    {
        public string PositionKind { get; set; } = string.Empty;

        public string Prefix { get; set; } = string.Empty;

        public int StartNumber { get; set; } = 1;

        public int HighestNumber { get; set; }

        public int MissingCount { get; set; }

        public List<int> MissingNumbers { get; set; } = new List<int>();

        public bool IsTruncated => MissingCount > MissingNumbers.Count;
    }
}
