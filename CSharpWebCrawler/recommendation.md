# Future Recommendations
## How can we take this project further?

### Introduction
The current web crawler works great on a single machine, keeping data in RAM and saving it to a simple JSON file. But if we wanted to scale this up to a real-world level (like a mini-search engine for a campus or a specific niche), we would need to make some architectural changes. Here are some of my ideas on how to improve it:

### 1. Moving Beyond JSON
Right now, everything is in a `ConcurrentDictionary` and saved to `crawler_state.json`. As the data grows, this file is going to get really bulky and slow to load.
- **Recommendation:** We should move the data to a real database. Something like **Elasticsearch** would be perfect for the search part, or just a simple SQL database if we want to keep it structured. 
- **Queue Management:** Instead of keeping the BFS queue in memory, using a system like **Redis** would allow us to stop and start the crawler without worrying about RAM limits.

### 2. Scaling with Workers
Currently, the crawler runs inside the same app as the Web API. This means if the crawler gets heavy, the UI might lag.
- **Recommendation:** We could separate the "crawling" part from the "dashboard" part. By running multiple "worker" instances, we could crawl thousands of pages much faster by distributing the workload.

### 3. Smarter HTML Parsing
Using Regex to find links is fast, but it’s not very "smart." It can struggle with messy HTML or websites that use a lot of JavaScript (like React or Vue apps).
- **Recommendation:** It would be better to use a proper HTML library like **HtmlAgilityPack**. If we really wanted to be fancy, we could use **Playwright** to "see" the page just like a real user does.

### 4. Intelligent Reasoning with Chain-of-Thought (CoT)
To improve transparency and result quality, we can implement a reasoning-based approach for the system’s logic.
- **Explainable Search:** instead of just links, the system can explain *why* a result is relevant (e.g., "Found 5 matches in title + 10 in body").
- **Smart Link Prioritization:** Using a reasoning chain to decide which URLs in the BFS queue are most valuable to crawl next based on semantic relevance.
- **Query Intent Analysis:** Breaking down complex user queries into logical sub-steps to refine search results.



