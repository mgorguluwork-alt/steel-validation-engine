using System;

namespace SteelValidation.Core.Numbering
{
    /// <summary>
    /// Explicit numbering-report settings.
    /// </summary>
    public sealed class NumberingReportOptions
    {
        public const int DefaultMaxMissingNumbersPerPrefix = 50;
        public const int MinMaxMissingNumbersPerPrefix = 1;
        public const int MaxMaxMissingNumbersPerPrefix = 1000;

        public NumberingReportOptions(
            int maxMissingNumbersPerPrefix = DefaultMaxMissingNumbersPerPrefix)
        {
            if (maxMissingNumbersPerPrefix < MinMaxMissingNumbersPerPrefix ||
                maxMissingNumbersPerPrefix > MaxMaxMissingNumbersPerPrefix)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxMissingNumbersPerPrefix),
                    maxMissingNumbersPerPrefix,
                    "The missing-number list cap must be between " +
                    MinMaxMissingNumbersPerPrefix + " and " +
                    MaxMaxMissingNumbersPerPrefix + ".");
            }

            MaxMissingNumbersPerPrefix = maxMissingNumbersPerPrefix;
        }

        /// <summary>
        /// The maximum number of missing numbers listed for one prefix. The
        /// total missing count is always reported in full.
        /// </summary>
        public int MaxMissingNumbersPerPrefix { get; }
    }
}
