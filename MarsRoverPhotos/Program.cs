using MarsRoverPhotos.Services;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.WriteIndented = true;
    });

// Named HttpClient for NASA API calls (with timeout)
builder.Services.AddHttpClient();
builder.Services.AddScoped<INasaApiService, NasaApiService>();

// Named HttpClient for image downloads (longer timeout for large files)
builder.Services.AddHttpClient<IImageStorageService, ImageStorageService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddSingleton<IDateParserService, DateParserService>();
builder.Services.AddScoped<IRoverPhotoOrchestrator, RoverPhotoOrchestrator>();

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// On startup, also run a console summary so the app is usable without a UI
app.Lifetime.ApplicationStarted.Register(async () =>
{
    using var scope = app.Services.CreateScope();
    var orchestrator = scope.ServiceProvider.GetRequiredService<IRoverPhotoOrchestrator>();

    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "dates.txt");
    if (!File.Exists(filePath))
    {
        Console.WriteLine($"[INFO] dates.txt not found at {filePath}. Skipping auto-run.");
        Console.WriteLine("[INFO] Use GET /api/roverphotos/process to trigger processing.");
        return;
    }

    Console.WriteLine("\n========== Mars Rover Photo Downloader ==========");
    try
    {
        var report = await orchestrator.ProcessDatesFileAsync(filePath);

        Console.WriteLine($"Generated at : {report.GeneratedAt:u}");
        Console.WriteLine($"Total lines  : {report.TotalDatesRead}");
        Console.WriteLine($"Valid dates  : {report.ValidDates}");
        Console.WriteLine($"Invalid dates: {report.InvalidDates}");
        Console.WriteLine($"Photos saved : {report.TotalPhotosDownloaded}");
        Console.WriteLine("\n--- Per-date breakdown ---");

        foreach (var r in report.Results)
        {
            if (!r.IsValid)
            {
                Console.WriteLine($"  [INVALID] \"{r.OriginalInput}\" → {r.ValidationError}");
            }
            else if (r.ApiError is not null)
            {
                Console.WriteLine($"  [API ERROR] {r.ParsedDate} → {r.ApiError}");
            }
            else
            {
                Console.WriteLine($"  [OK] {r.ParsedDate} → {r.PhotosDownloaded} photo(s) downloaded");
            }
        }

        Console.WriteLine("=================================================\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Failed to process dates: {ex.Message}");
    }
});

app.Run();
