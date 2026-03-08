#!/usr/bin/env python3
"""Check Crawl4AI availability across all connection methods (pip, Docker, MCP)."""

import subprocess
import sys
import json
import urllib.request
import urllib.error


def check_pip_install():
    """Check if crawl4ai is installed as a Python package."""
    try:
        import crawl4ai
        version = getattr(crawl4ai, "__version__", "unknown")
        return {"available": True, "version": version}
    except ImportError:
        return {"available": False}


def check_docker_running(base_url="http://localhost:11235"):
    """Check if a Crawl4AI Docker container is running and healthy."""
    try:
        req = urllib.request.Request(f"{base_url}/health", method="GET")
        with urllib.request.urlopen(req, timeout=5) as resp:
            data = json.loads(resp.read().decode())
            return {
                "available": True,
                "url": base_url,
                "version": data.get("version", "unknown"),
                "status": data.get("status", "unknown"),
            }
    except (urllib.error.URLError, OSError, json.JSONDecodeError):
        return {"available": False, "url": base_url}


def check_docker_image():
    """Check if the crawl4ai Docker image is pulled locally."""
    try:
        result = subprocess.run(
            ["docker", "images", "unclecode/crawl4ai", "--format", "{{.Tag}}"],
            capture_output=True, text=True, timeout=10,
        )
        tags = [t.strip() for t in result.stdout.strip().split("\n") if t.strip()]
        return {"available": bool(tags), "tags": tags}
    except (FileNotFoundError, subprocess.TimeoutExpired):
        return {"available": False, "docker_installed": False}


def check_mcp_endpoint(base_url="http://localhost:11235"):
    """Check if the MCP SSE endpoint is available (built into Docker server)."""
    try:
        req = urllib.request.Request(f"{base_url}/mcp/schema", method="GET")
        with urllib.request.urlopen(req, timeout=5) as resp:
            data = json.loads(resp.read().decode())
            tools = data.get("tools", [])
            return {
                "available": True,
                "url": f"{base_url}/mcp/sse",
                "tools_count": len(tools),
            }
    except (urllib.error.URLError, OSError, json.JSONDecodeError):
        return {"available": False}


def main():
    results = {
        "pip": check_pip_install(),
        "docker_running": check_docker_running(),
        "docker_image": check_docker_image(),
        "mcp": check_mcp_endpoint(),
    }

    available = []
    if results["pip"]["available"]:
        available.append(f"pip (v{results['pip']['version']})")
    if results["docker_running"]["available"]:
        available.append(f"Docker API ({results['docker_running']['url']})")
    if results["mcp"]["available"]:
        available.append(f"MCP ({results['mcp']['url']})")

    if available:
        print(f"Crawl4AI available via: {', '.join(available)}")
    else:
        print("Crawl4AI is NOT available. Setup required.")
        if results["docker_image"]["available"]:
            print(f"  Docker image found (tags: {results['docker_image']['tags']}) but container not running.")
            print("  Run: docker run -d -p 11235:11235 --name crawl4ai --shm-size=1g unclecode/crawl4ai:latest")

    # Machine-readable output
    print(f"\n{json.dumps(results, indent=2)}")
    return 0 if available else 1


if __name__ == "__main__":
    sys.exit(main())
