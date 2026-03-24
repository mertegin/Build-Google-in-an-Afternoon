using CrawlerApp.Services;
using CrawlerApp.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddHttpClient("CrawlerClient", client => 
{
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36 CSharpWebCrawler/1.0");
    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli
});
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<IndexerService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<IndexerService>());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/index", async (HttpContext context, IndexerService indexer) =>
{
    var request = await context.Request.ReadFromJsonAsync<IndexRequest>();
    if (request == null || string.IsNullOrWhiteSpace(request.Origin)) return Results.BadRequest();
    
    int depth = request.K > 0 ? request.K : 2;
    indexer.SetLimits(request.MaxQueueDepth, request.MaxUrls);
    
    var existingId = indexer.GetExistingCrawlerId(request.Origin);
    if (existingId != null)
    {
        return Results.Conflict(new { crawlerId = existingId, message = "You have already crawled this site. To crawl it again, please delete the previous crawl first." });
    }
    
    string id = indexer.CreateCrawler(request.Origin);
    var enqueued = indexer.Enqueue(id, request.Origin, depth);
    return Results.Ok(new { success = enqueued, crawlerId = id, message = enqueued ? "Crawler instance created" : "Failed to start" });
});

app.MapGet("/crawlers", (IndexerService indexer) =>
{
    var list = indexer.State.Crawlers.Values.Select(c => new {
        id = c.Id,
        originUrl = c.OriginUrl,
        createdAt = c.CreatedAt,
        visitedCount = c.ProcessedCount,
        queueDepth = c.Queue.Count,
        wordCount = c.SearchIndex.Count,
        isPaused = c.IsPaused,
        isFinished = c.IsFinished
    }).OrderByDescending(c => c.createdAt).ToList();
    return Results.Ok(list);
});

app.MapGet("/search", (string? crawlerId, string? query, string? sortBy, IndexerService indexer, SearchService searchService) =>
{
    if (string.IsNullOrEmpty(query)) 
        return Results.BadRequest(new { message = "Query required" });

    List<SearchResult> results;
    if (string.IsNullOrEmpty(crawlerId))
    {
        results = searchService.SearchAll(indexer.State.Crawlers, query);
    }
    else
    {
        if (!indexer.State.Crawlers.TryGetValue(crawlerId, out var instance))
            return Results.NotFound(new { message = "Crawler not found" });
        results = searchService.Search(instance, query);
    }

    // Default sorting is by frequency (relevance)
    return Results.Ok(results);
});

app.MapGet("/searchAll", (string? query, IndexerService indexer, SearchService searchService) =>
{
    if (string.IsNullOrEmpty(query)) 
        return Results.BadRequest(new { message = "Query required" });

    var results = searchService.SearchAll(indexer.State.Crawlers, query);
    return Results.Ok(results);
});

app.MapGet("/status", (string? crawlerId, IndexerService indexer) =>
{
    if (string.IsNullOrEmpty(crawlerId) || !indexer.State.Crawlers.TryGetValue(crawlerId, out var instance)) 
        return Results.NotFound(new { message = "Crawler not found" });

    var status = new
    {
        originUrl = instance.OriginUrl,
        createdAt = instance.CreatedAt,
        queueDepth = instance.Queue.Count,
        visitedCount = instance.ProcessedCount,
        indexedWordCount = instance.SearchIndex.Count,
        isBackPressure = instance.Queue.Count >= 10000,
        isPaused = instance.IsPaused,
        isFinished = instance.IsFinished,
        recentLogs = instance.RecentLogs.ToArray()
    };
    return Results.Ok(status);
});

app.MapPost("/pause", (string crawlerId, IndexerService indexer) =>
{
    if (string.IsNullOrEmpty(crawlerId) || !indexer.State.Crawlers.TryGetValue(crawlerId, out var instance)) 
        return Results.NotFound(new { message = "Crawler not found" });
    instance.IsPaused = true;
    instance.RecentLogs.Enqueue($"[{DateTime.UtcNow:HH:mm:ss}] ⏸️ Paused crawler.");
    return Results.Ok(new { message = "Crawler paused" });
});

app.MapPost("/resume", (string crawlerId, IndexerService indexer) =>
{
    if (string.IsNullOrEmpty(crawlerId) || !indexer.State.Crawlers.TryGetValue(crawlerId, out var instance)) 
        return Results.NotFound(new { message = "Crawler not found" });
    instance.IsPaused = false;
    instance.RecentLogs.Enqueue($"[{DateTime.UtcNow:HH:mm:ss}] ▶️ Resumed crawler.");
    return Results.Ok(new { message = "Crawler resumed" });
});

app.MapDelete("/crawler", (string crawlerId, IndexerService indexer) =>
{
    if (string.IsNullOrEmpty(crawlerId)) return Results.BadRequest();
    var deleted = indexer.DeleteCrawler(crawlerId);
    return deleted ? Results.Ok(new { message = "Crawler deleted" }) : Results.NotFound();
});

app.Run();

public class IndexRequest
{
    public string Origin { get; set; } = string.Empty;
    public int K { get; set; }
    public int MaxQueueDepth { get; set; }
    public int MaxUrls { get; set; }
}
