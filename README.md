# Mars Rover Photo Downloader

A .NET 8 Web API that reads a list of dates from `dates.txt`, fetches real NASA Curiosity rover images for each valid date, downloads up to 5 photos per date into a local folder structure, and never re-downloads an image that already exists.

---

## Prerequisites

Requirement : .NET SDK Version 8.0

A NASA API key is optional — the app works without one using the NASA Image and Video Library as a fallback.

---

## How to Run

### 1. Clone the repo

```bash
git clone https://github.com/<your-username>/MarsRoverPhotos.git
cd MarsRoverPhotos
```

### 2. Set your NASA API key (optional)

The app works without a key. If you have one, set it via **appsettings.json**:
```json
"NASA_API_KEY": "your_key_here"
```

Or via environment variable:
```bash
# Windows PowerShell
$env:NASA_API_KEY="your_key_here"

# Linux / macOS
export NASA_API_KEY=your_key_here
```

### 3. Run the API

```bash
cd MarsRoverPhotos
dotnet run
```

On startup the app automatically processes `dates.txt` and prints a console summary:

========== Mars Rover Photo Downloader ==========
Generated at : 2026-04-13 04:00:02Z
Total lines  : 4
Valid dates  : 3
Invalid dates: 1
Photos saved : 15
--- Per-date breakdown ---
[OK]      2017-02-27 → 5 photo(s) downloaded
[OK]      2018-06-02 → 5 photo(s) downloaded
[OK]      2016-07-13 → 5 photo(s) downloaded
[INVALID] "April 31, 2018" → Invalid calendar date: day does not exist in that month

Downloaded images are saved to:

photos/
2017-02-27/
1.jpg
2.jpg
...
2018-06-02/
...
2016-07-13/
...

### 4. REST API endpoint

Once running, call the API directly in your browser or via curl:

GET http://localhost:5147/api/roverphotos/process

Optional — specify a custom dates file:
GET http://localhost:5147/api/roverphotos/process?filePath=C:\path\to\dates.txt

Returns a full JSON `SummaryReport` with per-date results.

### 5. Run tests

```bash
cd MarsRoverPhotos.Tests
dotnet test
```

### 6. Run with Docker (optional)

```bash
docker build -t mars-rover-photos .
docker run -p 8080:8080 -e NASA_API_KEY=your_key_here mars-rover-photos
```

---

## How the API Fallback Works

The NASA Mars Photos API (`api.nasa.gov/mars-photos`) was **permanently retired on October 8, 2025**. The app handles this transparently with a 3-source fallback chain:

1. **NASA Mars Photos API** — tried first (in case it is ever restored)
2. **Heroku community mirror** — tried second
3. **NASA Image and Video Library** (`images-api.nasa.gov`) — fully operational fallback, no API key required, serves real Curiosity rover images

The app never crashes due to an API outage — it falls through to the next source automatically and logs a clear warning explaining what happened.

---

## Project Structure

MarsRoverPhotos/
├── Controllers/
│   └── RoverPhotosController.cs     # REST endpoint
├── Models/
│   └── NasaApiModels.cs             # NASA API + result record types
├── Services/
│   ├── DateParserService.cs         # Multi-format date parsing + validation
│   ├── NasaApiService.cs            # NASA API client with 3-source fallback
│   ├── ImageStorageService.cs       # Download + duplicate detection
│   └── RoverPhotoOrchestrator.cs    # Orchestrates the full pipeline
├── dates.txt                        # Input file with test dates
├── Program.cs                       # DI registration + startup auto-run
└── appsettings.json
MarsRoverPhotos.Tests/
├── DateParserServiceTests.cs        # 14 tests: all formats, invalid dates, edge cases
└── RoverPhotoOrchestratorTests.cs   # 4 tests with mocked services

---

## Assumptions

1. **Rover**: Curiosity is used by default as it has the most complete image coverage.
2. **Photo limit**: Up to 5 photos are fetched per date. Configurable via `maxPhotos` in `NasaApiService`.
3. **Duplicate detection**: If a file already exists on disk, the download is skipped — running the app twice is safe.
4. **Two-digit years**: `02/27/17` is interpreted as 2017 per C# `DateTime` conventions (years ≤ 29 map to 2000s, 30–99 map to 1900s).
5. **Invalid date handling**: `April 31` is caught via a round-trip format check — without this guard C# would silently adjust it to May 1.
6. **NASA API outage**: The original Mars Photos API was retired in October 2025. The app falls back to the NASA Image and Video Library which is fully operational. This is documented in `AI_NOTES.md`. 
"## Notes" 
