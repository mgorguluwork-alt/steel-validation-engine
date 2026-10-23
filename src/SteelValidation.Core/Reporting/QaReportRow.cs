using System.Collections.Generic;

namespace SteelValidation.Core.Reporting
{
    /// <summary>
    /// One finding of a QA report, taken unchanged from a check result.
    /// Fields that the source result does not provide stay empty.
    /// </summary>
    public sealed class QaReportRow
    {
        internal QaReportRow(
            string check,
            string rule,
            string status,
            IReadOnlyList<int> objectIds,
            string name,
            string profile,
            string value,
            string message)
        {
            Check = check;
            Rule = rule;
            Status = status;
            ObjectIds = objectIds;
            Name = name;
            Profile = profile;
            Value = value;
            Message = message;
        }

        /// <summary>MODEL, GEOMETRY or NUMBERING.</summary>
        public string Check { get; }

        public string Rule { get; }

        public string Status { get; }

        /// <summary>Every object involved, ascending. May be empty.</summary>
        public IReadOnlyList<int> ObjectIds { get; }

        public string Name { get; }

        public string Profile { get; }

        public string Value { get; }

        public string Message { get; }
    }
}
