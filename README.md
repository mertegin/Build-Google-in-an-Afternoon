# C# Web Crawler & Search Engine Cluster

A high-performance, multi-threaded web crawler and search engine built with **C#** and **ASP.NET Core Minimal APIs**. This project handles concurrent indexing of multiple web targets and provides a real-time dashboard fordata analysis.

## Key Features
- **Concurrent Cluster Management:** Start and monitor multiple independent crawlers simultaneously from a single dashboard.
- **Persistent States:** Automatic progress serialization (every 15s) allows the crawler to resume precisely where it left off after restarts.
- **Dashboard:** 
  - **Live Detail View:** Real-time metrics for queue depth, visited pages, indexed words, and back-pressure.
  - **Diagnostic Stream:** A terminal-style log for monitoring crawler activity and error handling.
- **In-Memory Search Engine:** High-speed inverted index construction using thread-safe `ConcurrentDictionary`.
  - **Scoped Search:** Search results are specific to the currently selected crawler, ensuring relevance to the target website.
- **Intelligent Indexing:** Extracts and scores terms from both HTML body content and URL paths for maximum coverage.
- **Resilient Workflows:** Rate-limited crawling via `SemaphoreSlim` to ensure polite resource usage.
- **Data Persistence:** All crawled data and indexing progress are stored in `crawler_state.json` within the project root.

## Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download) (or .NET 8+)
- Modern Web Browser

## Quick Start

1. **Clone & Navigate:**
   ```bash
   git clone <repo-url>
   cd CSharpWebCrawler
   ```

2. **Run Application:**
   ```bash
   dotnet run
   ```

3. **Access Dashboard:**
   Open `http://localhost:5244` in your browser.

4. **Initialize Indexing:**
   Go to the "Initialize" view, enter a target URL (e.g., `https://example.com`), set your depth, and click **Start Crawler**.
   
5. **Search Content:**
   Once crawling starts or finishes, select the crawler from the sidebar and use the search bar at the top. **Note:** The search functionality is specifically scoped to the website that the selected crawler has indexed.

## API Reference

| Endpoint | Method | Description |
| :--- | :---: | :--- |
| `/index` | `POST` | Deploys a new crawler instance. |
| `/crawlers` | `GET` | Lists all active and cached crawler instances. |
| `/status` | `GET` | Detailed telemetry and logs for a specific instance. |
| `/search` | `GET` | Flexible search (Scoped or Global) with `query` and `sortBy`. |
| `/pause`/`/resume` | `POST` | Control the execution flow of a crawler. |
| `/crawler` | `DELETE` | Erases an instance and purges its indexed data. |

## Data Storage
The application uses a singular JSON file for state persistence: `crawler_state.json`. This file contains the complete search index, visited URL history, and current crawler queues. It is automatically updated every 15 seconds during active crawling and saved upon application shutdown.

## Storage Location
All crawled data and indexing progress are stored in `crawler_state.json` within the project root directory.

## Why this project?
This crawler demonstrates a "native-first" approach to system design, primarily using .NET's built-in libraries for asynchronous task management and state serialization. It serves as a robust foundation for building large-scale search and data mining tools.

## To-Do
- [x] Documentation Update
    - [x] Clarify search scope in README.md
    - [x] Document storage location in README.md
