# rimdex

Search engine for RimWorld Workshop mods.

```text
┌───────────────────────────────────────────────┐
│ $ rimdex search "storage limit"               │
│ [                                             │
│   {                                           │
│     "title": "Storage Item Limiter",          │
│     "url": "https://steamcommunity.com/...",  │
│     "summary": "Enables storage units...",    │
│     "subscriptions": 727,                     │
│     "views": 2686                             │
│   }                                           │
│ ]                                             │
└───────────────────────────────────────────────┘
```

## Install

### macOS & Linux

```sh
curl -fsSL https://raw.githubusercontent.com/realloon/rimdex/main/install.sh | sh
```

### Windows (PowerShell)

```powershell
irm https://raw.githubusercontent.com/realloon/rimdex/main/install.ps1 | iex
```

## Configure

rimdex uses an embedding model. Configure it with:

```sh
rimdex config set \
  --api-key "YOUR_API_KEY" \
  --base-url "YOUR_BASE_URL" \
  --model "YOUR_MODEL"
```

## Build from source

```sh
git clone https://github.com/realloon/rimdex.git
cd rimdex
dotnet publish
```
