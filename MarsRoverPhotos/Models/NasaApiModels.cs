using System.Text.Json.Serialization;

namespace MarsRoverPhotos.Models;

// NASA API response shapes
public record NasaPhotosResponse(
    [property: JsonPropertyName("photos")] List<NasaPhoto> Photos
);

public record NasaPhoto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("img_src")] string ImgSrc,
    [property: JsonPropertyName("earth_date")] string EarthDate,
    [property: JsonPropertyName("rover")] NasaRover Rover,
    [property: JsonPropertyName("camera")] NasaCamera Camera
);

public record NasaRover(
    [property: JsonPropertyName("name")] string Name
);

public record NasaCamera(
    [property: JsonPropertyName("full_name")] string FullName
);

// App result models
public record DateProcessingResult(
    string OriginalInput,
    bool IsValid,
    DateOnly? ParsedDate,
    string? ValidationError,
    int PhotosDownloaded,
    string? ApiError
);

public record SummaryReport(
    DateTime GeneratedAt,
    int TotalDatesRead,
    int ValidDates,
    int InvalidDates,
    int TotalPhotosDownloaded,
    List<DateProcessingResult> Results
);
