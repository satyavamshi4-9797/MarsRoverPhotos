using MarsRoverPhotos.Models;
using Microsoft.Extensions.Logging;

namespace MarsRoverPhotos.Services;

public interface IRoverPhotoOrchestrator
{
    Task<SummaryReport> ProcessDatesFileAsync(string filePath, CancellationToken ct = default);
}

public class RoverPhotoOrchestrator : IRoverPhotoOrchestrator
{
    private readonly IDateParserService _dateParser;
    private readonly INasaApiService _nasaApi;
    private readonly IImageStorageService _imageStorage;
    private readonly ILogger<RoverPhotoOrchestrator> _logger;

    public RoverPhotoOrchestrator(
        IDateParserService dateParser,
        INasaApiService nasaApi,
        IImageStorageService imageStorage,
        ILogger<RoverPhotoOrchestrator> logger)
    {
        _dateParser = dateParser;
        _nasaApi = nasaApi;
        _imageStorage = imageStorage;
        _logger = logger;
    }

    public async Task<SummaryReport> ProcessDatesFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Dates file not found: {filePath}");

        var lines = await File.ReadAllLinesAsync(filePath, ct);
        var results = new List<DateProcessingResult>();

        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            _logger.LogInformation("Processing date line: {Line}", line);
            var (isValid, parsedDate, validationError) = _dateParser.TryParse(line);

            if (!isValid || parsedDate is null)
            {
                _logger.LogWarning("Invalid date '{Line}': {Error}", line, validationError);
                results.Add(new DateProcessingResult(line, false, null, validationError, 0, null));
                continue;
            }

            // Fetch from NASA API
            var (photos, apiError) = await _nasaApi.GetPhotosAsync(parsedDate.Value, ct: ct);

            if (apiError is not null || photos is null)
            {
                results.Add(new DateProcessingResult(line, true, parsedDate, null, 0, apiError));
                continue;
            }

            if (photos.Count == 0)
            {
                results.Add(new DateProcessingResult(
                    line, true, parsedDate, null, 0,
                    "No photos available from NASA API for this date"));
                continue;
            }

            // Download images
            var count = await _imageStorage.DownloadPhotosAsync(parsedDate.Value, photos, ct);
            results.Add(new DateProcessingResult(line, true, parsedDate, null, count, null));
        }

        return new SummaryReport(
            GeneratedAt: DateTime.UtcNow,
            TotalDatesRead: lines.Length,
            ValidDates: results.Count(r => r.IsValid),
            InvalidDates: results.Count(r => !r.IsValid),
            TotalPhotosDownloaded: results.Sum(r => r.PhotosDownloaded),
            Results: results
        );
    }
}
