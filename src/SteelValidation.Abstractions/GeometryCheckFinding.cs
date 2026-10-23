using System.Collections.Generic;

namespace SteelValidation.Abstractions
{
    /// <summary>
    /// A read-only geometry-check observation. ObjectIds contains every model
    /// object involved in the observation, ordered by identifier.
    /// </summary>
    public class GeometryCheckFinding
    {
        public string Rule { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public List<int> ObjectIds { get; set; } = new List<int>();

        public string Message { get; set; } = string.Empty;
    }
}
