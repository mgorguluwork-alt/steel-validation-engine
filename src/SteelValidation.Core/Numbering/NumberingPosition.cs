using System.Globalization;

namespace SteelValidation.Core.Numbering
{
    /// <summary>
    /// A Tekla position string split into a prefix and a number. A position
    /// without a trailing number is unparsed; its prefix and number stay
    /// unknown and are never guessed.
    /// </summary>
    public sealed class NumberingPosition
    {
        /// <summary>
        /// Tekla appends this to a position whose numbering is out of date
        /// (read from the live model, 2026-09-30: "P-1(?)", "ROD-3001(?)").
        /// </summary>
        public const string OutOfDateMarker = "(?)";

        private NumberingPosition(
            string position,
            string? prefix,
            int? number,
            bool isOutOfDate)
        {
            Position = position;
            Prefix = prefix;
            Number = number;
            IsOutOfDate = isOutOfDate;
        }

        /// <summary>The trimmed position string, including any "(?)" marker.</summary>
        public string Position { get; }

        public string? Prefix { get; }

        public int? Number { get; }

        public bool IsMissing => Position.Length == 0;

        public bool IsParsed => Number.HasValue;

        /// <summary>
        /// True when Tekla marked the position "(?)": the number shown is from
        /// an earlier numbering run and will change when numbering runs again.
        /// </summary>
        public bool IsOutOfDate { get; }

        /// <summary>
        /// Parses forms such as B/12, B-12, B12 and C/1, with or without Tekla's
        /// trailing "(?)" out-of-date marker. The separator ('/' or '-') between
        /// prefix and number is optional and not part of the prefix. The number
        /// is the trailing run of ASCII digits; a number that does not fit in an
        /// int is reported as unparsed.
        /// </summary>
        public static NumberingPosition Parse(string? position)
        {
            var trimmed = position == null ? string.Empty : position.Trim();
            if (trimmed.Length == 0)
                return new NumberingPosition(string.Empty, null, null, false);

            var isOutOfDate = trimmed.EndsWith(OutOfDateMarker, System.StringComparison.Ordinal);
            var core = isOutOfDate
                ? trimmed.Substring(0, trimmed.Length - OutOfDateMarker.Length).TrimEnd()
                : trimmed;

            var digitStart = core.Length;
            while (digitStart > 0 && IsAsciiDigit(core[digitStart - 1]))
                digitStart--;

            // No trailing number (e.g. "B", "B/", "12B", "(?)"): unparsed, not guessed.
            if (digitStart == core.Length)
                return new NumberingPosition(trimmed, null, null, isOutOfDate);

            // Validates the int range: a trailing number too large for an int
            // is unparsed rather than truncated or guessed.
            if (!int.TryParse(
                core.Substring(digitStart),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number))
            {
                return new NumberingPosition(trimmed, null, null, isOutOfDate);
            }

            var prefix = core.Substring(0, digitStart);
            if (prefix.Length > 0 && IsSeparator(prefix[prefix.Length - 1]))
                prefix = prefix.Substring(0, prefix.Length - 1);

            return new NumberingPosition(trimmed, prefix, number, isOutOfDate);
        }

        private static bool IsAsciiDigit(char value) =>
            value >= '0' && value <= '9';

        private static bool IsSeparator(char value) =>
            value == '/' || value == '-';
    }
}
