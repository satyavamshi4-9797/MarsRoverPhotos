# AI_NOTES.md — AI-Assisted Development Log

## 1. Tools Used

- **Claude (Anthropic)** — primary AI assistant for architecture design, code generation, debugging, and review

---

## 2. Prompts That Worked Best

### Prompt 1 — Project scaffolding with constraints
> "Build a .NET 8 Web API that reads dates from a text file, parses them in multiple formats, calls the NASA Mars Rover Photos API for each valid date, downloads up to 5 photos per date to a local folder structure, skips existing files, and returns a summary report. Use async/await throughout, inject config for the API key, and follow clean architecture with separate service classes."

**Why it worked:** Being explicit about the constraints up front (no re-downloads, async/await, DI for config) meant the AI produced production-ready patterns rather than a single-file prototype.

---

### Prompt 2 — Tricky date validation
> "In C#, `DateTime.TryParseExact` for `MMMM d, yyyy` silently adjusts April 31 to May 1 instead of returning false. Write a method that detects this and returns an error instead. Use a round-trip format comparison approach."

**Why it worked:** Naming the exact failure mode (silent date adjustment) focused the AI on the right solution immediately. Without that context it would have generated a naive `TryParseExact` call that misses the bug.

---

### Prompt 3 — Unit test coverage
> "Write xUnit tests for DateParserService covering: all valid formats from the requirements, invalid calendar dates (April 31, Feb 30), unrecognised formats, empty/whitespace input, and leading/trailing whitespace trimming. Use `[Theory]` + `[InlineData]` where possible."

**Why it worked:** Listing the exact test categories ensured complete coverage rather than just the happy-path cases.

---

### Prompt 4 — Real-world API outage handling
> "The NASA Mars Photos API is returning 404. Research whether the API is still live and if not, find a working alternative that returns real Mars images and allows automated downloads."

**Why it worked:** Treating the outage as a research problem rather than just a code problem led to discovering the NASA Image and Video Library API (images-api.nasa.gov) as a fully working replacement.

---

## 3. Example Where AI Generated Incorrect Code — and How I Fixed It

### Example 1 — Silent date overflow in C#

**Problem:** The AI initially generated the `TryParse` method using only `DateTime.TryParseExact` and returning `true` for `April 31, 2018`. C# internally adjusts the overflowed day to May 1 without signalling an error, so the method silently passed an invalid date downstream.

**AI's original (incorrect) logic:**
```csharp
if (DateTime.TryParseExact(trimmed, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
    return (true, DateOnly.FromDateTime(dt), null);  // ← April 31 becomes May 1 silently
```

**Fix applied:** After a successful parse, round-trip the result back to the same format string and compare it case-insensitively against the original input. If they differ, the date was adjusted and is therefore invalid:

```csharp
var roundTrip = dt.ToString(fmt, CultureInfo.InvariantCulture);
if (!string.Equals(roundTrip, trimmed, StringComparison.OrdinalIgnoreCase))
    return (false, null, $"Invalid calendar date: '{trimmed}' (day does not exist in that month)");
```

---

### Example 2 — NASA Mars Photos API no longer exists

**Problem:** The AI initially pointed to `api.nasa.gov/mars-photos` as the API endpoint. When running the application, every request returned 404. The AI assumed the API was still live based on its training data.

**Investigation:** After research, I discovered the NASA Mars Photos API was permanently retired and archived on **October 8, 2025** by its maintainer. The community Heroku mirror was also down. This was confirmed on the official NASA GitHub issues page.

**Fix applied:** Rather than hardcoding a workaround, I implemented a proper multi-source fallback strategy:
1. Try the official NASA Mars Photos API (in case it ever comes back)
2. Try the Heroku community mirror as a second option
3. Fall back to the **NASA Image and Video Library API** (`images-api.nasa.gov`) which is fully operational, requires no API key, and serves real NASA Mars/Curiosity rover images

The fallback uses targeted search queries varied by date to ensure different photos are returned for each date, with filtering logic to exclude diagrams and non-photo content.

**Key lesson:** External APIs can disappear. Production-quality code should always handle dependency failures gracefully and have a fallback strategy rather than hard-crashing.

---

## 4. Significant Changes Made After AI Output

| Area | AI Output | What I Changed | Why |
|------|-----------|----------------|-----|
| **Date validation** | Used `TryParseExact` only | Added round-trip comparison guard | Catch silent overflow (April 31 → May 1) |
| **Startup auto-run** | Printed report inside `Main()` | Moved to `app.Lifetime.ApplicationStarted` | Ensures DI is fully resolved before use |
| **Duplicate detection** | Checked only `File.Exists` | Kept same but logged "already exists" clearly | Better observability |
| **Error messages** | Generic exception messages | Capped API error body at 200 chars | Prevent log flooding from huge HTML error pages |
| **HttpClient config** | Single shared client | Separate named clients for API vs image downloads | Different timeout needs (30s vs 60s) |
| **API source** | Used NASA Mars Photos API only | Implemented 3-source fallback chain | Primary API was retired Oct 2025; app must be resilient |
| **Image filtering** | Returned all search results | Added keyword filter to skip diagrams/charts | NASA Image Library returns mixed content types |
| **Search queries** | Single fixed query | Varied query per date using year + camera type | Ensures different photos per date folder |