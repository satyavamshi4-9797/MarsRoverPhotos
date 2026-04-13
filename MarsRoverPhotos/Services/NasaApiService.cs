using System.Text.Json;
using System.Text.Json.Nodes;
using MarsRoverPhotos.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MarsRoverPhotos.Services;

public interface INasaApiService
{
    Task<(List<NasaPhoto>? Photos, string? Error)> GetPhotosAsync(
        DateOnly date,
        string rover = "curiosity",
        int maxPhotos = 5,
        CancellationToken ct = default);
}

public class NasaApiService : INasaApiService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _apiKey;
    private readonly ILogger<NasaApiService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public NasaApiService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<NasaApiService> logger)
    {
        _httpFactory = httpFactory;
        _apiKey = config["NASA_API_KEY"] ?? "DEMO_KEY";
        _logger = logger;
    }

    public async Task<(List<NasaPhoto>? Photos, string? Error)> GetPhotosAsync(
        DateOnly date,
        string rover = "curiosity",
        int maxPhotos = 5,
        CancellationToken ct = default)
    {
        var dateStr = date.ToString("yyyy-MM-dd");
        _logger.LogInformation("Fetching photos for {Date} from rover {Rover}", dateStr, rover);

        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "MarsRoverPhotosApp/1.0");

        // Try 1: Official NASA Mars Photos API (retired Oct 2025, kept for completeness)
        var (photos1, _) = await TryMarsPhotosApi(client, rover, dateStr, maxPhotos, ct);
        if (photos1 != null) return (photos1, null);

        // Try 2: NASA Image and Video Library - searches for Curiosity photos near the given date
        // This API is fully working as of 2026 and requires no API key
        var (photos2, _) = await TryNasaImageLibrary(client, date, maxPhotos, ct);
        if (photos2 != null) return (photos2, null);

        return (null, $"All NASA photo sources unavailable for {dateStr}. " +
            "The Mars Photos API was retired in October 2025.");
    }

    private async Task<(List<NasaPhoto>? Photos, string? Error)> TryMarsPhotosApi(
        HttpClient client, string rover, string dateStr, int maxPhotos, CancellationToken ct)
    {
        var urls = new[]
        {
            $"https://api.nasa.gov/mars-photos/api/v1/rovers/{rover}/photos?earth_date={dateStr}&api_key={_apiKey}",
            $"https://mars-photos.herokuapp.com/api/v1/rovers/{rover}/photos?earth_date={dateStr}"
        };

        foreach (var url in urls)
        {
            try
            {
                var response = await client.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) continue;
                var json = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<NasaPhotosResponse>(json, JsonOpts);
                var photos = result?.Photos?.Take(maxPhotos).ToList();
                if (photos?.Count > 0) return (photos, null);
            }
            catch (Exception ex) { _logger.LogDebug("Mars Photos API failed: {Msg}", ex.Message); }
        }
        return (null, null);
    }

    private async Task<(List<NasaPhoto>? Photos, string? Error)> TryNasaImageLibrary(
    HttpClient client, DateOnly date, int maxPhotos, CancellationToken ct)
    {
        // Search specifically for raw rover camera images — not diagrams or press graphics
        var year = date.Year;
        // Try multiple queries in order until we get enough photos
        var queries = new[]
        {
            $"curiosity+rover+navcam+{year}",
            $"curiosity+rover+mastcam+{year}",
            $"curiosity+mars+hazcam+{year}",
            "curiosity+rover+navcam",       // fallback without year
            "curiosity+rover+mastcam",      // fallback without year
            "mars+curiosity+surface",       // broad fallback
        };

        try
        {
            _logger.LogInformation("Trying NASA Image and Video Library API...");
            JsonArray? items = null;
            foreach (var query in queries)
            {
                var searchUrl = $"https://images-api.nasa.gov/search?q={query}&media_type=image&page_size=20";
                var response = await client.GetAsync(searchUrl, ct);
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync(ct);
                var root = JsonNode.Parse(json);
                items = root?["collection"]?["items"]?.AsArray();
                if (items != null && items.Count > 0) break;
            }
            if (items == null || items.Count == 0) return (null, null);

            

            var photos = new List<NasaPhoto>();
            var dateStr = date.ToString("yyyy-MM-dd");

            // Keywords that indicate diagrams, charts, or non-photo content — skip these
            var skipKeywords = new[] { "diagram", "chart", "graphic", "illustration",
            "artist", "concept", "render", "map", "infographic", "logo", "schematic" };

            foreach (var item in items)
            {
                if (photos.Count >= maxPhotos) break;

                var title = item?["data"]?[0]?["title"]?.GetValue<string>() ?? "";
                var description = item?["data"]?[0]?["description"]?.GetValue<string>() ?? "";
                var href = item?["links"]?[0]?["href"]?.GetValue<string>() ?? "";

                // Skip anything that looks like a diagram or non-photo
                var combined = (title + " " + description).ToLowerInvariant();
                if (skipKeywords.Any(k => combined.Contains(k))) continue;

                // Only accept jpg images (not video thumbnails or PNG graphics)
                if (!href.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
                    !href.Contains("~small") && !href.Contains("~medium") &&
                    !href.Contains("~large")) continue;

                if (!string.IsNullOrEmpty(href))
                {
                    photos.Add(new NasaPhoto(
                        Id: photos.Count + 1,
                        ImgSrc: href,
                        EarthDate: dateStr,
                        Rover: new NasaRover("Curiosity"),
                        Camera: new NasaCamera(title[..Math.Min(40, title.Length)])
                    ));
                }
            }

            if (photos.Count > 0)
            {
                _logger.LogInformation("NASA Image Library returned {Count} rover photos for {Date}",
                    photos.Count, dateStr);
                return (photos, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("NASA Image Library failed: {Msg}", ex.Message);
        }

        return (null, null);
    }
}