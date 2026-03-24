using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CrawlerApp.Utils;

public class HtmlParser
{
    private static readonly Regex _linkRegex = new Regex(@"href\s*=\s*(?:[""'](?<url>[^""']+)[""']|(?<url>[^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _wordRegex = new Regex(@"(?<=>)[^<]+(?=<)", RegexOptions.Compiled);
    private static readonly Regex _cleanLettersRegex = new Regex(@"\b[a-zA-Z]{3,}\b", RegexOptions.Compiled);

    public static List<string> ExtractLinks(string html, string baseUrl)
    {
        var links = new List<string>();
        if (string.IsNullOrWhiteSpace(html)) return links;

        var matches = _linkRegex.Matches(html);
        foreach (Match match in matches)
        {
            var href = match.Groups["url"].Value;
            if (string.IsNullOrWhiteSpace(href)) continue;
            
            // Handle relative / absolute URLs
            if (Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri))
            {
                if (!uri.IsAbsoluteUri)
                {
                    if (Uri.TryCreate(new Uri(baseUrl), uri, out var absoluteUri))
                    {
                        if (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps)
                        {
                            links.Add(absoluteUri.ToString());
                        }
                    }
                }
                else if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                {
                    links.Add(uri.ToString());
                }
            }
        }
        return links;
    }

    public static List<string> ExtractWords(string html)
    {
        var words = new List<string>();
        if (string.IsNullOrWhiteSpace(html)) return words;

        // Try to strip scripts and styles before word extraction
        html = Regex.Replace(html, @"<(script|style)[^>]*?>.*?</\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var textNodes = _wordRegex.Matches(html);
        foreach (Match node in textNodes)
        {
            var wordMatches = _cleanLettersRegex.Matches(node.Value);
            foreach (Match wordMatch in wordMatches)
            {
                words.Add(wordMatch.Value.ToLowerInvariant());
            }
        }
        return words;
    }
}
