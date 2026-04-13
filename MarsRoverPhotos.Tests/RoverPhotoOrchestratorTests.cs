using MarsRoverPhotos.Models;
using MarsRoverPhotos.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MarsRoverPhotos.Tests;

public class RoverPhotoOrchestratorTests : IDisposable
{
    private readonly Mock<INasaApiService> _nasaApiMock = new();
    private readonly Mock<IImageStorageService> _imageMock = new();
    private readonly IDateParserService _dateParser = new DateParserService();
    private readonly string _tempFile = Path.GetTempFileName();

    private RoverPhotoOrchestrator CreateSut() =>
        new(_dateParser, _nasaApiMock.Object, _imageMock.Object,
            NullLogger<RoverPhotoOrchestrator>.Instance);

    [Fact]
    public async Task ProcessDatesFileAsync_MixedDates_CorrectlySegregatesValidAndInvalid()
    {
        // dates.txt lines from the exercise
        await File.WriteAllLinesAsync(_tempFile, [
            "02/27/17",
            "June 2, 2018",
            "Jul-13-2016",
            "April 31, 2018"    // invalid
        ]);

        var samplePhotos = new List<NasaPhoto>
        {
            new(1, "http://example.com/1.jpg", "2017-02-27",
                new NasaRover("Curiosity"), new NasaCamera("FHAZ"))
        };

        _nasaApiMock
            .Setup(x => x.GetPhotosAsync(It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((samplePhotos, (string?)null));

        _imageMock
            .Setup(x => x.DownloadPhotosAsync(It.IsAny<DateOnly>(), It.IsAny<List<NasaPhoto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var report = await CreateSut().ProcessDatesFileAsync(_tempFile);

        Assert.Equal(4, report.TotalDatesRead);
        Assert.Equal(3, report.ValidDates);
        Assert.Equal(1, report.InvalidDates);
        Assert.Equal(3, report.TotalPhotosDownloaded);
    }

    [Fact]
    public async Task ProcessDatesFileAsync_ApiFailure_RecordsErrorButContinues()
    {
        await File.WriteAllTextAsync(_tempFile, "02/27/17\nJune 2, 2018\n");

        _nasaApiMock
            .SetupSequence(x => x.GetPhotosAsync(It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, "Network error: connection refused"))   // first date fails
            .ReturnsAsync((new List<NasaPhoto>                          // second succeeds
            {
                new(2, "http://example.com/2.jpg", "2018-06-02",
                    new NasaRover("Curiosity"), new NasaCamera("FHAZ"))
            }, (string?)null));

        _imageMock
            .Setup(x => x.DownloadPhotosAsync(It.IsAny<DateOnly>(), It.IsAny<List<NasaPhoto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var report = await CreateSut().ProcessDatesFileAsync(_tempFile);

        Assert.Equal(2, report.ValidDates);
        Assert.Equal(1, report.TotalPhotosDownloaded);
        Assert.NotNull(report.Results[0].ApiError);
        Assert.Null(report.Results[1].ApiError);
    }

    [Fact]
    public async Task ProcessDatesFileAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            CreateSut().ProcessDatesFileAsync("/nonexistent/path/dates.txt"));
    }

    [Fact]
    public async Task ProcessDatesFileAsync_AllInvalidDates_NoApiCallsMade()
    {
        await File.WriteAllLinesAsync(_tempFile, ["April 31, 2018", "not-a-date"]);

        var report = await CreateSut().ProcessDatesFileAsync(_tempFile);

        Assert.Equal(0, report.ValidDates);
        Assert.Equal(2, report.InvalidDates);
        Assert.Equal(0, report.TotalPhotosDownloaded);
        _nasaApiMock.VerifyNoOtherCalls();
    }

    public void Dispose() => File.Delete(_tempFile);
}
