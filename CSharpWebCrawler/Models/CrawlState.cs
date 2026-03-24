using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CrawlerApp.Models;

public class CrawlerInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OriginUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPaused { get; set; } = false;
    public int ActiveRequests;
    public int ProcessedCount;
    public bool IsFinished => Queue.IsEmpty && ActiveRequests == 0 && Visited.Count > 0;
    public ConcurrentQueue<CrawlRequest> Queue { get; set; } = new();
    public ConcurrentDictionary<string, byte> Visited { get; set; } = new();
    public ConcurrentDictionary<string, ConcurrentDictionary<string, SearchResult>> SearchIndex { get; set; } = new();
    public ConcurrentQueue<string> RecentLogs { get; set; } = new();
}

public class CrawlState
{
    public ConcurrentDictionary<string, CrawlerInstance> Crawlers { get; set; } = new();
}

public class CrawlRequest
{
    public string CrawlerId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string OriginUrl { get; set; } = string.Empty;
    public int Depth { get; set; }
}

public class SearchResult
{
    public string RelevantUrl { get; set; } = string.Empty;
    public string OriginUrl { get; set; } = string.Empty;
    public int Depth { get; set; }
    public int Frequency { get; set; }
}
