# DeepSeek Worker MCP

This folder contains a minimal MCP worker that forwards bounded text tasks to an OpenAI-compatible API endpoint.

## Expected environment variables

- `BASE_URL`
- `API_KEY`
- `MODEL`
- `DEFAULT_EFFORT`

## Local install

```powershell
cd tools\deepseek-worker
npm install
```

## Codex config snippet

```toml
[mcp_servers.deepseek_worker]
command = "node"
args = ["C:\\Users\\TD-997\\Documents\\NewAGV\\tools\\deepseek-worker\\server.mjs"]

[mcp_servers.deepseek_worker.env]
BASE_URL = "https://api.apipro.me/v1"
MODEL = "deepseek-v4-flash"
DEFAULT_EFFORT = "xhigh"
API_KEY = "YOUR_ROTATED_KEY_HERE"
```
