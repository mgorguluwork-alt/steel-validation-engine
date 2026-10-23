using System.Collections.Generic;

namespace SteelValidation.Abstractions
{
    public class ModelCheckResult
    {
        public int TotalParts { get; set; }
        public int NameEmptyCount { get; set; }
        public int LengthWarningCount { get; set; }
        public int ConsistencyMismatchCount { get; set; }

        public List<ModelCheckItem> NameEmptyItems { get; set; }
            = new List<ModelCheckItem>();

        public List<ModelLengthFinding> LengthFindings { get; set; }
            = new List<ModelLengthFinding>();

        public List<ModelConsistencyFinding> ConsistencyFindings { get; set; }
            = new List<ModelConsistencyFinding>();
    }
}
