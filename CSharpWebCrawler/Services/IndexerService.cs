using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CrawlerApp.Models;
using CrawlerApp.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CrawlerApp.Services;

public class IndexerService : BackgroundService
{
    private readonly ILogger<IndexerService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SearchService _searchService;

    // Concurrency limits
    private readonly SemaphoreSlim _rateLimiter;
    private int _maxQueueDepthLimit;
    private int _maxUrlsLimit;
    private const string StateFilePath = "crawler_state.json";
    
    private readonly JsonSerializerOptions _jsonOptions = new() { IncludeFields = true };
    private DateTime _lastSaveTime = DateTime.UtcNow;

    public CrawlState State { get; private set; } = new();

    public IndexerService(ILogger<IndexerService> logger, IHttpClientFactory httpClientFactory, SearchService searchService)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("CrawlerClient");
        _searchService = searchService;
        
        // Settings could be injected via IConfiguration
        _rateLimiter = new SemaphoreSlim(10); // Max 10 concurrent requests
        _maxQueueDepthLimit = 10000;
        _maxUrlsLimit = 10000;
    }

    public void SetLimits(int maxQueueDepth, int maxUrls)
    {
        if (maxQueueDepth > 0) _maxQueueDepthLimit = maxQueueDepth;
        if (maxUrls > 0) _maxUrlsLimit = maxUrls;
    }

    private string NormalizeUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;
        var uri = new Uri(url);
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
    }

    public string? GetExistingCrawlerId(string originUrl)
    {
        var normalized = NormalizeUrl(originUrl);
        foreach (var c in State.Crawlers.Values)
        {
            if (NormalizeUrl(c.OriginUrl) == normalized) return c.Id;
        }
        return null;
    }

    public string CreateCrawler(string originUrl)
    {
        var instance = new CrawlerInstance { OriginUrl = originUrl };
        State.Crawlers[instance.Id] = instance;
        return instance.Id;
    }

    public bool Enqueue(string crawlerId, string url, int depth, string originUrl = "")
    {
        if (!State.Crawlers.TryGetValue(crawlerId, out var instance)) return false;

        if (instance.Visited.Count >= _maxUrlsLimit)
        {
            _logger.LogDebug("Max URL limit reached. Rejecting {Url}", url);
            return false;
        }

        if (instance.Queue.Count >= _maxQueueDepthLimit)
        {
            _logger.LogDebug("Queue full (back pressure). Rejecting {Url}", url);
            return false;
        }

        if (instance.Visited.TryAdd(url, 0))
        {
            instance.Queue.Enqueue(new CrawlRequest { CrawlerId = crawlerId, Url = url, Depth = depth, OriginUrl = string.IsNullOrEmpty(originUrl) ? url : originUrl });
            return true;
        }
        return false;
    }

    public bool DeleteCrawler(string id)
    {
        return State.Crawlers.TryRemove(id, out _);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await LoadStateAsync();
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await SaveStateAsync();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = new List<Task>();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                bool hasWork = false;
                foreach (var instance in State.Crawlers.Values)
                {
                    if (instance.IsPaused) continue;

                    if (instance.Queue.TryDequeue(out var request))
                    {
                        hasWork = true;
                        Interlocked.Increment(ref instance.ActiveRequests);
                        await _rateLimiter.WaitAsync(stoppingToken);
                        
                        var runTask = Task.Run(async () =>
                        {
                            try
                            {
                                await ProcessUrlAsync(instance, request, stoppingToken);
                            }
                            finally
                            {
                                _rateLimiter.Release();
                                Interlocked.Decrement(ref instance.ActiveRequests);
                            }
                        }); // Do not pass stoppingToken here to prevent it from throwing OperationCanceledException immediately if canceled before running

                        tasks.Add(runTask);
                    }
                }

                tasks.RemoveAll(t => t.IsCompleted);

                if (DateTime.UtcNow - _lastSaveTime > TimeSpan.FromSeconds(15))
                {
                    await SaveStateAsync();
                    _lastSaveTime = DateTime.UtcNow;
                }

                if (!hasWork)
                {
                    await Task.Delay(500, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful cancellation requested (e.g., Ctrl+C)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in IndexerService loop.");
        }
        finally
        {
            try 
            {
                await Task.WhenAll(tasks);
            }
            catch { /* Ignore remaining task failures on shutdown */ }
        }
    }

    private async Task ProcessUrlAsync(CrawlerInstance instance, CrawlRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogTrace("Crawling {Url} at Depth {Depth} for Crawler {Id}", request.Url, request.Depth, instance.Id);
            instance.RecentLogs.Enqueue($"[{DateTime.UtcNow:HH:mm:ss}] ⏳ Crawling: {request.Url}");
            while (instance.RecentLogs.Count > 200) instance.RecentLogs.TryDequeue(out _);

            var html = await _httpClient.GetStringAsync(request.Url, cancellationToken);
            
            var links = HtmlParser.ExtractLinks(html, request.Url);
            var words = HtmlParser.ExtractWords(html);

            // Linkteki kelimeleri de aramalarda bulabilmek için URL'i de parçalayıp kelime listesine ekliyoruz.
            var urlWords = System.Text.RegularExpressions.Regex.Matches(request.Url, @"[a-zA-Z]{3,}").Select(m => m.Value.ToLowerInvariant());
            words.AddRange(urlWords);

            // Index words
            _searchService.IndexWords(instance, words, new SearchResult 
            { 
                RelevantUrl = request.Url, 
                OriginUrl = request.OriginUrl, 
                Depth = request.Depth 
            });

            // Enqueue new links
            if (request.Depth > 0)
            {
                foreach (var link in links)
                {
                    Enqueue(instance.Id, link, request.Depth - 1, request.OriginUrl);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to crawl {Url}: {Error}", request.Url, ex.Message);
            // instance.RecentLogs.Enqueue($"[{DateTime.UtcNow:HH:mm:ss}] ❌ Error: {request.Url} ({ex.Message})");
            while (instance.RecentLogs.Count > 200) instance.RecentLogs.TryDequeue(out _);
        }
        finally
        {
            Interlocked.Increment(ref instance.ProcessedCount);
        }
    }

    private async Task LoadStateAsync()
    {
        if (File.Exists(StateFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(StateFilePath);
                var state = JsonSerializer.Deserialize<CrawlState>(json, _jsonOptions);
                if (state != null)
                {
                    State = state;
                    
                    // Legacy data migration: If ProcessedCount is 0 but Visited is not, recovery from Visited.Count
                    foreach (var instance in State.Crawlers.Values)
                    {
                        if (instance.ProcessedCount == 0 && instance.Visited.Count > 0)
                        {
                            instance.ProcessedCount = instance.Visited.Count;
                        }
                    }

                    _logger.LogInformation("Loaded previously saved crawler state. Active Crawlers: {Count}", State.Crawlers.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load crawler state");
            }
        }
    }

    private async Task SaveStateAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(State, _jsonOptions);
            await File.WriteAllTextAsync(StateFilePath, json);
            _logger.LogInformation("Saved crawler state.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save crawler state");
        }
    }
}
