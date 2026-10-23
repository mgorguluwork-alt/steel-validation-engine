using System.Collections.Generic;

namespace SteelValidation.Abstractions
{
    public class ClashCheckResult
    {
        public int TotalClashes { get; set; }

        public int ScannedParts { get; set; }

        public int NativeClashCount { get; set; } = -1;

        public int HighCount { get; set; }

        public int MediumCount { get; set; }

        public int LowCount { get; set; }

        public List<ClashCheckItem> Items { get; set; }
            = new List<ClashCheckItem>();
    }
}

