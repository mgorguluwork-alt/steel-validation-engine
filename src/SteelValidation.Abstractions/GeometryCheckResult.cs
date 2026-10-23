using System.Collections.Generic;

namespace SteelValidation.Abstractions
{
    /// <summary>
    /// The complete, read-only result of a geometry check over a steel-part
    /// scope. PartsChecked and PartsNotChecked describe point availability for
    /// the duplicate-beam rule; all parts are still evaluated for length and
    /// profile findings.
    /// </summary>
    public class GeometryCheckResult
    {
        public int PartsChecked { get; set; }

        public int PartsNotChecked { get; set; }

        public int UnknownLengthCount { get; set; }

        public int ShortOrZeroLengthCount { get; set; }

        public int EmptyProfileCount { get; set; }

        public int DuplicateBeamGroupCount { get; set; }

        public List<GeometryCheckFinding> Findings { get; set; }
            = new List<GeometryCheckFinding>();
    }
}
