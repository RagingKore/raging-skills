# Common Use Cases

## Table of Contents

- [Documentation to Markdown](#documentation-to-markdown)
- [E-commerce Product Monitoring](#e-commerce-product-monitoring)
- [News Aggregation](#news-aggregation)
- [Research & Data Collection](#research--data-collection)

## Documentation to Markdown

```python
# Convert entire documentation site to clean markdown
async with AsyncWebCrawler() as crawler:
    result = await crawler.arun("https://docs.example.com")

    # Save as markdown for LLM consumption
    with open("docs.md", "w") as f:
        f.write(result.markdown)
```

## E-commerce Product Monitoring

```python
# Generate schema once for product pages
# Then monitor prices/availability without LLM costs
schema = load_json("product_schema.json")
products = await crawler.arun_many(product_urls,
    config=CrawlerRunConfig(extraction_strategy=JsonCssExtractionStrategy(schema)))
```

## News Aggregation

```python
# Crawl multiple news sources concurrently
news_urls = ["https://news1.com", "https://news2.com", "https://news3.com"]
results = await crawler.arun_many(news_urls, max_concurrent=5)

# Extract articles with Fit Markdown
for result in results:
    if result.success:
        # Get only relevant content
        article = result.markdown.fit_markdown
```

## Research & Data Collection

```python
from crawl4ai.content_filter_strategy import BM25ContentFilter
from crawl4ai.markdown_generation_strategy import DefaultMarkdownGenerator

# Academic paper collection with focused extraction
bm25_filter = BM25ContentFilter(
    user_query="machine learning transformers",
    bm25_threshold=1.0
)
md_generator = DefaultMarkdownGenerator(content_filter=bm25_filter)

config = CrawlerRunConfig(markdown_generator=md_generator)
result = await crawler.arun(url, config=config)
filtered_content = result.markdown.fit_markdown
```
