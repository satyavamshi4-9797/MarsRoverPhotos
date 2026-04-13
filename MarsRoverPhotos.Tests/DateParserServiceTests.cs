using MarsRoverPhotos.Services;
using Xunit;

namespace MarsRoverPhotos.Tests;

public class DateParserServiceTests
{
    private readonly IDateParserService _sut = new DateParserService();

    // ── Valid formats ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("02/27/17",       2017, 2, 27)]
    [InlineData("June 2, 2018",   2018, 6, 2)]
    [InlineData("Jul-13-2016",    2016, 7, 13)]
    [InlineData("2021-09-15",     2021, 9, 15)]
    [InlineData("12/31/99",       1999, 12, 31)]
    public void TryParse_ValidDate_ReturnsCorrectDateOnly(string input, int year, int month, int day)
    {
        var (isValid, date, error) = _sut.TryParse(input);

        Assert.True(isValid, $"Expected '{input}' to be valid but got error: {error}");
        Assert.NotNull(date);
        Assert.Null(error);
        Assert.Equal(new DateOnly(year, month, day), date);
    }

    // ── Invalid calendar dates ───────────────────────────────────────────────

    [Theory]
    [InlineData("April 31, 2018")]   // April only has 30 days
    [InlineData("February 30, 2020")] // Feb never has 30 days
    [InlineData("November 31, 2021")] // November only has 30 days
    public void TryParse_InvalidCalendarDate_ReturnsInvalid(string input)
    {
        var (isValid, date, error) = _sut.TryParse(input);

        Assert.False(isValid, $"Expected '{input}' to be invalid");
        Assert.Null(date);
        Assert.NotNull(error);
        Assert.Contains("does not exist", error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Unrecognised formats ─────────────────────────────────────────────────

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("32/01/2020")]
    [InlineData("2020.01.01")]
    public void TryParse_UnrecognisedFormat_ReturnsInvalid(string input)
    {
        var (isValid, date, error) = _sut.TryParse(input);

        Assert.False(isValid);
        Assert.Null(date);
        Assert.NotNull(error);
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_EmptyOrWhitespace_ReturnsInvalid(string input)
    {
        var (isValid, date, error) = _sut.TryParse(input);

        Assert.False(isValid);
        Assert.Null(date);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_DateWithLeadingTrailingSpaces_IsHandled()
    {
        var (isValid, date, _) = _sut.TryParse("  02/27/17  ");

        Assert.True(isValid);
        Assert.Equal(new DateOnly(2017, 2, 27), date);
    }
}
