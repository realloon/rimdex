# rimdex

Search engine for RimWorld Workshop mods.

```text
┌───────────────────────────────────────────────────────┐
│ $ rimdex search "storage limit"                       │
│ [                                                     │
│   {                                                   │
│     "title": "Storage Item Limiter",                  │
│     "url": "https://steamcommunity.com/shared...",    │
│     "summary": "Enables storage units to have...",    │
│     "subscriptions": 727,                             │
│     "views": 2686                                     │
│   }                                                   │
│ ]                                                     │
└───────────────────────────────────────────────────────┘
```

## Install

### macOS & Linux

```sh
curl -fsSL https://raw.githubusercontent.com/realloon/rimdex/main/install.sh | sh
```

### Windows

Run in PowerShell:

```powershell
irm https://raw.githubusercontent.com/realloon/rimdex/main/install.ps1 | iex
```

### Manual

Download the latest release binary from [Releases](https://github.com/realloon/rimdex/releases) and place it into your `PATH` (e.g. `~/.local/bin`).

## Configure rimdex

rimdex uses an embedding model. Configure it with:

```sh
rimdex config set \
  --api-key "YOUR_API_KEY" \
  --base-url "https://api.openai.com/v1" \
  --model "text-embedding-3-small"
```

## Build from source

```sh
git clone https://github.com/realloon/rimdex.git
cd rimdex
dotnet publish
```
