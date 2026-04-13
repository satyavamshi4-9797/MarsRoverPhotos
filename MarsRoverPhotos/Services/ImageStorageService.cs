using MarsRoverPhotos.Models;
using Microsoft.Extensions.Logging;

namespace MarsRoverPhotos.Services;

public interface IImageStorageService
{
    Task<int> DownloadPhotosAsync(
        DateOnly date,
        List<NasaPhoto> photos,
        CancellationToken ct = default);
}

public class ImageStorageService : IImageStorageService
{
    private readonly HttpClient _http;
    private readonly ILogger<ImageStorageService> _logger;
    private readonly string _baseFolder;

    public ImageStorageService(HttpClient http, ILogger<ImageStorageService> logger)
    {
        _http = http;
        _logger = logger;
        _baseFolder = Path.Combine(Directory.GetCurrentDirectory(), "photos");
    }

    public async Task<int> DownloadPhotosAsync(
        DateOnly date,
        List<NasaPhoto> photos,
        CancellationToken ct = default)
    {
        var folder = Path.Combine(_baseFolder, date.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(folder);

        var downloaded = 0;

        foreach (var photo in photos)
        {
            var fileName = $"{photo.Id}.jpg";
            var filePath = Path.Combine(folder, fileName);

            // Skip if already exists — no re-download
            if (File.Exists(filePath))
            {
                _logger.LogDebug("Photo {Id} already exists, skipping", photo.Id);
                downloaded++; // still counts as "available"
                continue;
            }

            try
            {
                _logger.LogInformation("Downloading photo {Id} from {Url}", photo.Id, photo.ImgSrc);
                using var request = new HttpRequestMessage(HttpMethod.Get, photo.ImgSrc);
                request.Headers.Add("User-Agent", "MarsRoverPhotosApp/1.0");
                var response = await _http.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                await File.WriteAllBytesAsync(filePath, bytes, ct);
                downloaded++;
                _logger.LogInformation("Saved photo {Id} to {Path}", photo.Id, filePath);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Failed to download photo {Id}: {Message}", photo.Id, ex.Message);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Download of photo {Id} was cancelled", photo.Id);
            }
        }

        return downloaded;
    }
}
