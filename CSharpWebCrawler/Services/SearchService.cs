using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CrawlerApp.Models;

namespace CrawlerApp.Services;

public class SearchService
{
    public void IndexWords(CrawlerInstance instance, IEnumerable<string> words, SearchResult resultContext)
    {
        var wordCounts = words.GroupBy(w => w).ToDictionary(g => g.Key, g => g.Count());

        foreach (var kvp in wordCounts)
        {
            var word = kvp.Key;
            var count = kvp.Value;
            var wordDocs = instance.SearchIndex.GetOrAdd(word, _ => new ConcurrentDictionary<string, SearchResult>());
            
            wordDocs[resultContext.RelevantUrl] = new SearchResult
            {
                RelevantUrl = resultContext.RelevantUrl,
                OriginUrl = resultContext.OriginUrl,
                Depth = resultContext.Depth,
                Frequency = count
            };
        }
    }

    public List<SearchResult> Search(CrawlerInstance instance, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResult>();

        var queryWord = query.ToLowerInvariant().Trim();
        if (instance.SearchIndex.TryGetValue(queryWord, out var results))
        {
            return results.Values.OrderByDescending(r => r.Frequency).ToList();
        }

        return new List<SearchResult>();
    }

    public List<SearchResult> SearchAll(ConcurrentDictionary<string, CrawlerInstance> crawlers, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResult>();

        var results = new List<SearchResult>();
        foreach (var instance in crawlers.Values)
        {
            results.AddRange(Search(instance, query));
        }

        return results
            .GroupBy(r => r.RelevantUrl)
            .Select(g => new SearchResult
            {
                RelevantUrl = g.Key,
                OriginUrl = g.First().OriginUrl,
                Depth = g.Min(x => x.Depth),
                Frequency = g.Sum(x => x.Frequency)
            })
            .OrderByDescending(r => r.Frequency).ToList();
    }
}
