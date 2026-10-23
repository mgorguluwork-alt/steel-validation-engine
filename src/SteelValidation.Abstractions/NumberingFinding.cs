using System.Collections.Generic;

namespace SteelValidation.Abstractions
{
    /// <summary>
    /// A read-only numbering observation. ObjectIds contains every model
    /// object involved in the observation, ordered by identifier.
    /// </summary>
    public class NumberingFinding
    {
        public string Rule { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string PositionKind { get; set; } = string.Empty;

        public string Position { get; set; } = string.Empty;

        public List<int> ObjectIds { get; set; } = new List<int>();

        public string Message { get; set; } = string.Empty;
    }
}
