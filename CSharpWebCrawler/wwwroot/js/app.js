let currentCrawlerId = "";
let currentView = "create"; // 'create' or 'detail'
let allSearchResults = [];
let searchPage = 1;
const pageSize = 10;

async function fetchCrawlers() {
    try {
        const response = await fetch('/crawlers');
        const crawlers = await response.json();
        
        const list = document.getElementById('crawler-sidebar-list');
        
        let html = '';
        if (crawlers.length === 0) {
            html += '<div class="bg-offline">No crawlers found.</div>';
        } else {
            crawlers.forEach(c => {
                const date = new Date(c.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                const isActive = (c.id === currentCrawlerId && currentView === "detail") ? 'active' : '';
                const statusBadge = c.isFinished ? '<span class="badge-status" style="background:var(--secondary)"><i class="fas fa-check-circle"></i> FINISHED</span>' : 
                                  (c.isPaused ? '<span class="badge-status bg-paused"><i class="fas fa-pause"></i> PAUSED</span>' : 
                                  '<span class="badge-status bg-running"><i class="fas fa-sync fa-spin"></i> RUNNING</span>');
                html += `
                    <div class="crawler-item ${isActive}" onclick="selectCrawler('${c.id}')">
                        <div class="crawler-origin">${c.originUrl}</div>
                        <div class="crawler-meta">
                            <span><i class="far fa-clock"></i> ${date} &nbsp; • &nbsp; <i class="far fa-file-alt"></i> ${c.visitedCount} pgs</span>
                            <span>${statusBadge}</span>
                        </div>
                    </div>
                `;
            });
        }
        
        list.innerHTML = html;
        
        if (crawlers.length > 0 && !currentCrawlerId && currentView !== "create") {
            selectCrawler(crawlers[0].id);
        }
    } catch(e) {
        console.error("Failed to fetch crawlers", e);
    }
}

function showCreateView() {
    currentCrawlerId = "";
    currentView = "create";
    document.getElementById('view-create').style.display = 'block';
    document.getElementById('view-detail').style.display = 'none';
    closeSearch();
    fetchCrawlers(); 
}

function selectCrawler(id) {
    currentView = "detail";
    currentCrawlerId = id;
    document.getElementById('view-create').style.display = 'none';
    document.getElementById('view-detail').style.display = 'block';
    closeSearch();
    
    fetchCrawlers(); 
    
    document.getElementById('live-logs').innerHTML = '<div style="color: #64748b;">Connecting...</div>';
    fetchStatus();
}

async function fetchStatus() {
    if (currentView !== "detail" || !currentCrawlerId) return;

    try {
        const response = await fetch(`/status?crawlerId=${currentCrawlerId}`);
        if (!response.ok) throw new Error(response.statusText);
        
        const data = await response.json();
        
        const badgeHTML = data.isFinished ? '<span class="badge-status" style="background:var(--secondary)"><i class="fas fa-check-circle"></i> FINISHED</span>' : 
                          (data.isPaused ? '<span class="badge-status bg-paused"><i class="fas fa-pause"></i> PAUSED</span>' : 
                          '<span class="badge-status bg-running"><i class="fas fa-sync fa-spin"></i> RUNNING</span>');
        
        document.getElementById('detail-origin').innerText = data.originUrl;
        document.getElementById('detail-created').innerText = `Started At: ${new Date(data.createdAt).toLocaleString()}`;
        document.getElementById('detail-badges').innerHTML = badgeHTML;
        
        document.getElementById('queue-depth').innerText = data.queueDepth;
        document.getElementById('visited-count').innerText = data.visitedCount;
        document.getElementById('indexed-words').innerText = data.indexedWordCount;
        
        const bp = document.getElementById('back-pressure');
        bp.innerText = data.isBackPressure ? "TRUE" : "FALSE";
        bp.style.color = data.isBackPressure ? 'var(--danger)' : 'var(--success)';

        const logContainer = document.getElementById('live-logs');
        if (logContainer && data.recentLogs && data.recentLogs.length > 0) {
            logContainer.innerHTML = data.recentLogs.map(log => {
                let color = '#4ade80';
                if (log.includes('Error')) color = '#f87171';
                return `<div style="color: ${color}; margin-bottom: 2px;">> ${log}</div>`;
            }).reverse().join('');
        }

        document.getElementById('btn-pause').style.display = (data.isPaused || data.isFinished) ? 'none' : 'flex';
        document.getElementById('btn-resume').style.display = (!data.isPaused || data.isFinished) ? 'none' : 'flex';
        
    } catch (e) {
        console.error("Fetch status error", e);
    }
}

async function startIndexing() {
    const urlInput = document.getElementById('crawl-url');
    const url = urlInput.value;
    const depth = parseInt(document.getElementById('crawl-depth').value) || 2;
    const maxQueue = parseInt(document.getElementById('max-queue').value) || 10000;
    const maxUrls = parseInt(document.getElementById('max-urls').value) || 1000;
    
    if (!url) return;

    const msg = document.getElementById('crawl-msg');
    msg.innerText = "Starting...";
    msg.style.color = "var(--text-muted)";

    try {
        const response = await fetch('/index', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ origin: url, k: depth, maxQueueDepth: maxQueue, maxUrls: maxUrls })
        });
        
        const result = await response.json();
        
        if (response.ok) {
            msg.innerText = "Success!";
            msg.style.color = "var(--success)";
            selectCrawler(result.crawlerId);
        } else {
            msg.innerText = result.message || "An error occurred";
            msg.style.color = "var(--danger)";
            msg.style.fontWeight = "600";
            if (response.status === 409) {
                // Highlight the existing one in the sidebar if possible
                // For now, the message already says what to do.
            }
        }
    } catch (e) {
        console.error(e);
        msg.innerText = "Connection failed.";
        msg.style.color = "var(--danger)";
    }
}

async function searchQuery() {
    const query = document.getElementById('search-query').value;
    if (!query) return;

    document.getElementById('search-results-overlay').style.display = 'block';
    const container = document.getElementById('search-results');
    container.innerHTML = "Searching globally...";
    document.getElementById('search-pagination').style.display = 'none';

    try {
        const response = await fetch(`/searchAll?query=${encodeURIComponent(query)}`);
        allSearchResults = await response.json();
        
        document.getElementById('search-count').innerText = `(${allSearchResults.length})`;
        searchPage = 1;
        displayResults();
    } catch (e) {
        container.innerHTML = "Global search failed.";
    }
}

function displayResults() {
    const container = document.getElementById('search-results');
    const pagination = document.getElementById('search-pagination');
    
    if (allSearchResults.length === 0) {
        container.innerHTML = "No results found.";
        pagination.style.display = 'none';
        return;
    }

    const totalPages = Math.ceil(allSearchResults.length / pageSize);
    const start = (searchPage - 1) * pageSize;
    const end = start + pageSize;
    const pageData = allSearchResults.slice(start, end);

    let html = "";
    pageData.forEach(r => {
        html += `
            <div class="result-item">
                <a href="${r.relevantUrl}" target="_blank">${r.relevantUrl}</a>
                <div class="result-meta">
                    <span style="color:var(--primary); font-weight:600;">Origin: ${r.originUrl}</span>
                    <span>Depth: ${r.depth}</span>
                    <span class="badge">Relevance: ${r.frequency}</span>
                </div>
            </div>
        `;
    });
    container.innerHTML = html;

    // Update Pagination UI
    if (totalPages > 1) {
        pagination.style.display = 'flex';
        document.getElementById('current-page').innerText = searchPage;
        document.getElementById('total-pages').innerText = totalPages;
        document.getElementById('prev-page').disabled = (searchPage === 1);
        document.getElementById('next-page').disabled = (searchPage === totalPages);
    } else {
        pagination.style.display = 'none';
    }
}

function changePage(delta) {
    const totalPages = Math.ceil(allSearchResults.length / pageSize);
    searchPage += delta;
    if (searchPage < 1) searchPage = 1;
    if (searchPage > totalPages) searchPage = totalPages;
    displayResults();
    // Scroll to top of results
    document.getElementById('search-results-overlay').scrollIntoView({ behavior: 'smooth' });
}

function closeSearch() {
    document.getElementById('search-results-overlay').style.display = 'none';
    allSearchResults = [];
    searchPage = 1;
}

async function pauseCrawler() {
    if (!currentCrawlerId) return;
    await fetch(`/pause?crawlerId=${currentCrawlerId}`, { method: 'POST' });
}

async function resumeCrawler() {
    if (!currentCrawlerId) return;
    await fetch(`/resume?crawlerId=${currentCrawlerId}`, { method: 'POST' });
}

async function deleteCrawler() {
    if (!currentCrawlerId) return;
    if (!confirm("Are you sure you want to delete this crawler and all its indexed data?")) return;
    await fetch(`/crawler?crawlerId=${currentCrawlerId}`, { method: 'DELETE' });
    showCreateView();
}

showCreateView();
fetchCrawlers();

setInterval(() => {
    fetchCrawlers();
    fetchStatus();
}, 2000);
