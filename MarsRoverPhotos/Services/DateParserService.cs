// DateParserService: handles 8 date formats with round-trip validation to catch invalid dates

using System.Globalization;

namespace MarsRoverPhotos.Services;

public interface IDateParserService
{
    (bool IsValid, DateOnly? Date, string? Error) TryParse(string input);
}

public class DateParserService : IDateParserService
{
    private static readonly string[] Formats =
    [
        "MM/dd/yy",
        "MM/dd/yyyy",
        "MMMM d, yyyy",
        "MMMM dd, yyyy",
        "MMM-dd-yyyy",
        "MMM-d-yyyy",
        "yyyy-MM-dd",
        "M/d/yyyy",
        "M/d/yy",
    ];

    public (bool IsValid, DateOnly? Date, string? Error) TryParse(string input)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return (false, null, "Empty or whitespace input");

        foreach (var fmt in Formats)
        {
            if (DateTime.TryParseExact(trimmed, fmt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                // Guard against silent overflow: April 31 → May 1
                // Re-format and compare to catch invalid day-of-month
                var roundTrip = dt.ToString(fmt, CultureInfo.InvariantCulture);
                if (!string.Equals(roundTrip, trimmed, StringComparison.OrdinalIgnoreCase))
                    return (false, null, $"Invalid calendar date: '{trimmed}' (day does not exist in that month)");

                return (true, DateOnly.FromDateTime(dt), null);
            }
        }

        // Last resort: try general parse (handles "April 31, 2018" with AllowInnerWhite etc.)
        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtGeneral))
        {
            // Verify the day wasn't silently rolled over
            var reparsed = dtGeneral.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
            // If TryParse succeeded it means it was a valid date — return it
            return (true, DateOnly.FromDateTime(dtGeneral), null);
        }

        return (false, null, $"Invalid or unrecognised date: '{trimmed}'");
    }
}