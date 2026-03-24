# Goal Description

The objective is to implement a highly scalable, single-machine C# Web Crawler. The new system will prioritize language-native features (e.g., bypassing heavyweight 3rd-party libraries for core features where native `HttpClient`, `Regex` / `ReadOnlySpan<char>`, and thread-safe collections suffice). It will expose both `index` and `search` capabilities, allow concurrent searching while indexing, include back-pressure mechanisms (max rate and queue depth), and optionally support resumability. Alongside the code, several deliverables (PRD, Recommendation, fully documented README) will be generated.

## User Review Required

- **Framework Choice:** The plan proposes using an **ASP.NET Core Web API (Minimal APIs)** project. This allows us to easily serve a simple HTML UI and provide the necessary API endpoints (`/index`, `/search`, `/status`) in a single self-contained application. Are you comfortable with this choice?
- **Resumability & Storage:** We will periodically flush the visited URLs queue and inverted index to local JSON files to support basic interruption/resumptions. Is this acceptable?
- **HTML Parsing approach:** To avoid "fully featured libraries" like HtmlAgilityPack, we will use compiled `Regex` to extract `href` links and basic text from HTML content. Is this strict adherence to native libraries what you envisioned?

## Proposed Changes

### [Core Project Initialization]
We will create a new directory (e.g., `CSharpWebCrawler`). In it, we will initialize a standard `dotnet` API project.

#### [NEW] `CSharpWebCrawler/CrawlerApp.csproj`
The core C# project definition. No external HTML parsing or crawling NuGet packages will be added.

### [Indexer Engine]
The Indexer handles background scraping operations with limits.

#### [NEW] `CSharpWebCrawler/Services/IndexerService.cs`
- Manages a thread-safe `ConcurrentQueue` of URLs to visit alongside their depth.
- **Back-pressure:** Uses `SemaphoreSlim(k)` to enforce a maximum rate of concurrent work, and rejects new index requests if the queue depth exceeds a configured threshold.
- Tracks seen URLs using `ConcurrentDictionary<string, byte>` (a thread-safe HashSet alternative).
- **Resumability:** Saves current queue and visited state to disk gracefully when the application stops, reloading them on startup.

#### [NEW] `CSharpWebCrawler/Utils/HtmlParser.cs`
- Uses `HttpClient` to fetch pages.
- Native `Regex` to extract all `<a href="...">` tags and basic word content for indexing.

### [Search Engine]
The Search engine indexes content and processes queries concurrently.

#### [NEW] `CSharpWebCrawler/Services/SearchService.cs`
- **Inverted Index:** Maintains a `ConcurrentDictionary<string, List<SearchResult>>` mapping words to the URLs where they were found.
- Safely mutated by the `IndexerService` while being read by incoming `/search` requests.
- Yields results structured as `(relevant_url, origin_url, depth)`.

### [API & User Interface]
Web UI and Endpoints.

#### [NEW] `CSharpWebCrawler/Program.cs`
- Hosts the required API endpoints:
  - `POST /index` -> Accepts `{origin, k}`
  - `GET /search` -> Accepts `?query=...`
  - `GET /status` -> Returns indexing progress, queue size, back-pressure status.
- Serves static files.

#### [NEW] `CSharpWebCrawler/wwwroot/index.html`
#### [NEW] `CSharpWebCrawler/wwwroot/js/app.js`
- Simple UI (HTML/JS/CSS) to initiate indexing, invoke search, and display real-time status.

### [Documentation and Deliverables]
The specific markdown documents requested.

#### [NEW] `CSharpWebCrawler/README.md`
- Documentation on how to run and use the project.
#### [NEW] `CSharpWebCrawler/product_prd.md`
- Product Requirements Document for the AI.
#### [NEW] `CSharpWebCrawler/recommendation.md`
- Next steps for deploying into a production environment.

## Verification Plan

### Automated Tests
* We will verify rate limiting by spawning index tasks and asserting they do not exceed concurrent limits.
* We will verify standard search relevance against known indexed HTML strings.

### Manual Verification
* Run the localhost web server.
* Submit an index task via the UI for a well-known site (like `wikipedia.org/wiki/Main_Page` with depth 2).
* Concurrently search for terms and verify results appear as the crawler works.
* Validate the UI correctly shows the system status (Queue depth, back pressure).
