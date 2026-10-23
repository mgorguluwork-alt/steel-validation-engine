using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace SteelValidation.Core.Model
{
    /// <summary>
    /// The outcome of checking a part length against a configured maximum.
    /// </summary>
    public enum PartLengthStatus
    {
        Ok,
        TooLong,
        LengthUnknown,
        NoLimit
    }

    /// <summary>
    /// A deterministic result from <see cref="PartLengthRule" />.
    /// </summary>
    public sealed class PartLengthResult
    {
        internal PartLengthResult(
            PartLengthStatus status,
            string partName,
            double? lengthMm,
            double? limitMm,
            string message)
        {
            Status = status;
            PartName = partName;
            LengthMm = lengthMm;
            LimitMm = limitMm;
            Message = message;
        }

        public PartLengthStatus Status { get; }

        public string PartName { get; }

        public double? LengthMm { get; }

        public double? LimitMm { get; }

        /// <summary>
        /// True only when the configured maximum is exceeded.
        /// </summary>
        public bool IsWarning => Status == PartLengthStatus.TooLong;

        /// <summary>
        /// A stable, human-readable description of the result.
        /// </summary>
        public string Message { get; }
    }

    /// <summary>
    /// Checks supplied part lengths against named, configurable maximums.
    /// This class has no dependency on Tekla or mutable external configuration.
    /// </summary>
    public sealed class PartLengthRule
    {
        private static readonly IReadOnlyDictionary<string, double> DefaultLimits =
            new ReadOnlyDictionary<string, double>(
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ANGLE"] = 6000d,
                    ["COLUMN"] = 12000d
                });

        private readonly Dictionary<string, double> _limitsMm;

        /// <summary>
        /// The maximum lengths used when no configuration is supplied.
        /// </summary>
        public static IReadOnlyDictionary<string, double> DefaultMaxLengthsMm => DefaultLimits;

        /// <summary>
        /// Creates a length rule. A supplied configuration replaces the defaults;
        /// pass null to use the default ANGLE and COLUMN limits.
        /// </summary>
        public PartLengthRule(IDictionary<string, double>? maxLengthsMm = null)
        {
            _limitsMm = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<KeyValuePair<string, double>> source;
            if (maxLengthsMm == null)
                source = DefaultLimits;
            else
                source = maxLengthsMm;

            foreach (var configuredLimit in source)
            {
                var name = NormalizeName(configuredLimit.Key);
                if (name.Length == 0)
                    throw new ArgumentException("A length-limit name cannot be empty.", nameof(maxLengthsMm));

                if (double.IsNaN(configuredLimit.Value) ||
                    double.IsInfinity(configuredLimit.Value) ||
                    configuredLimit.Value < 0d)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(maxLengthsMm),
                        "A length limit must be a finite, non-negative value.");
                }

                if (_limitsMm.ContainsKey(name))
                {
                    throw new ArgumentException(
                        "Length-limit names must be unique after trimming.",
                        nameof(maxLengthsMm));
                }

                _limitsMm.Add(name, configuredLimit.Value);
            }
        }

        /// <summary>
        /// Gets the configured maximum for a name after trimming it and matching
        /// it with ordinal case-insensitivity.
        /// </summary>
        public bool TryGetLimit(string? partName, out double limitMm) =>
            _limitsMm.TryGetValue(NormalizeName(partName), out limitMm);

        /// <summary>
        /// Checks a supplied length without deriving or substituting any value.
        /// </summary>
        public PartLengthResult Check(string? partName, double? lengthMm)
        {
            var normalizedName = NormalizeName(partName);

            if (!lengthMm.HasValue)
            {
                return new PartLengthResult(
                    PartLengthStatus.LengthUnknown,
                    normalizedName,
                    null,
                    null,
                    "INFO - " + DisplayName(normalizedName) + " length is unknown");
            }

            if (!TryGetLimit(normalizedName, out var limitMm))
            {
                return new PartLengthResult(
                    PartLengthStatus.NoLimit,
                    normalizedName,
                    lengthMm,
                    null,
                    "INFO - " + DisplayName(normalizedName) +
                    " has no configured length limit");
            }

            if (lengthMm.Value > limitMm)
            {
                return new PartLengthResult(
                    PartLengthStatus.TooLong,
                    normalizedName,
                    lengthMm,
                    limitMm,
                    "WARNING - " + DisplayName(normalizedName) + " length " +
                    FormatMillimetres(lengthMm.Value) + " mm exceeds limit " +
                    FormatMillimetres(limitMm) + " mm");
            }

            return new PartLengthResult(
                PartLengthStatus.Ok,
                normalizedName,
                lengthMm,
                limitMm,
                "INFO - " + DisplayName(normalizedName) + " length is within limit " +
                FormatMillimetres(limitMm) + " mm");
        }

        private static string NormalizeName(string? partName) =>
            partName == null ? string.Empty : partName.Trim();

        private static string DisplayName(string normalizedName) =>
            normalizedName.Length == 0 ? "(unnamed part)" : normalizedName;

        private static string FormatMillimetres(double value)
        {
            if (value == 0d)
                return "0";

            const NumberStyles style = NumberStyles.Float;
            var concise = value.ToString("G15", CultureInfo.InvariantCulture);

            if (double.TryParse(
                concise,
                style,
                CultureInfo.InvariantCulture,
                out var roundTripped) &&
                roundTripped == value)
            {
                return concise;
            }

            return value.ToString("G17", CultureInfo.InvariantCulture);
        }
    }
}
