# Troubleshooting

## Table of Contents

- [JavaScript Not Loading](#javascript-not-loading)
- [Bot Detection Issues](#bot-detection-issues)
- [Content Extraction Problems](#content-extraction-problems)
- [Session/Auth Issues](#sessionauth-issues)

## JavaScript Not Loading

```python
config = CrawlerRunConfig(
    wait_for="css:.dynamic-content",  # Wait for specific element
    page_timeout=60000  # Increase timeout
)
```

## Bot Detection Issues

```python
browser_config = BrowserConfig(
    headless=False,  # Sometimes visible browsing helps
    viewport_width=1920,
    viewport_height=1080,
    user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
)
# Add delays between requests
await asyncio.sleep(random.uniform(2, 5))
```

## Content Extraction Problems

```python
# Debug what's being extracted
result = await crawler.arun(url)
print(f"HTML length: {len(result.html)}")
print(f"Markdown length: {len(result.markdown)}")
print(f"Links found: {len(result.links)}")

# Try different wait strategies
config = CrawlerRunConfig(
    wait_for="js:document.querySelector('.content') !== null"
)
```

## Session/Auth Issues

```python
# Verify session is maintained
config = CrawlerRunConfig(session_id="test_session")
result = await crawler.arun(url, config=config)
print(f"Session ID: {result.session_id}")
print(f"Cookies: {result.cookies}")
```
