# Web Crawler Project Requirements
## C# Web Crawler & Search Engine

### 1. Overview
The goal of this project is to implement a high-performance web crawler and search engine in C# using ASP.NET Core Minimal APIs. The system is designed to be fully self-contained, using native .NET features for crawling, parsing, and indexing without heavy third-party dependencies.

### 2. Core Features
- **Intelligent Crawler Cluster:**
  - Background service managing multiple, independent crawler instances simultaneously.
  - Implements Breadth-First Search (BFS) with configurable depth and recursion limits.
  - Rate limiting via `SemaphoreSlim` to ensure polite crawling and resource stability.
  - **New:** Periodic state persistence (every 15s) ensures progress is never lost, even on unexpected shutdowns.
- **Advanced Search Engine:**
  - Real-time inverted index construction with word frequency scoring.
  - **New:** Enhanced indexing that also extracts keywords from URLs for better search coverage.
  - Localized search within individual crawler instances for pinpoint results.
- **Dashboard:**
  - Dynamic frontend with dedicated "Detail Views" for monitoring live crawler telemetry.
  - Real-time logging stream with filtered diagnostics for a cleaner operational view.
  - Accurate progress tracking that counts successfully processed URLs.

### 3. Implementation Details
- **Thread Safety:** Extensively uses `ConcurrentDictionary` and `ConcurrentQueue` for lock-free data integrity across parallel tasks.
- **Resumability:** Automated state serialization to `crawler_state.json` with legacy data migration logic for seamless version upgrades.
- **Minimalist Architecture:** Uses compiled Regex for high-speed HTML link and text extraction.

### 4. Project Milestones (Completed)
1. ✅ Setting up the base C# Web API.
2. ✅ Implementing the BFS crawler and Regex parser.
3. ✅ Adding the in-memory inverted index and URL keyword extraction.
4. ✅ Implementing multi-instance support and automated disk persistence.
5. ✅ Building the advanced multi-view dashboard with live telemetry.
6. ✅ Implementing data recovery and migration for long-running crawl states.
