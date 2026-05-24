# Photo Browser — Design & Setup Guide

## Overview

A three-part system for browsing a local JPEG photo collection:

| Project | Technology | Role |
|---|---|---|
| `PhotoIndexer` | C# .NET 10 CLI | Scans folders, extracts EXIF, writes index |
| `PhotoWeb` | F# ASP.NET Core 10 | REST API, serves photos from disk |
| `photo-ui` | React 18 + TypeScript + Vite | Browser UI |

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  User runs: PhotoIndexer index <folder>                      │
│                                                              │
│  Scanner.cs  ──EXIF──►  SqliteStore.cs ──► photos.db        │
│                    └──► Store.cs       ──► index.json        │
└──────────────────────────────────────────────────────────────┘
                              │
                         photos.db
                              │
┌──────────────────────────────────────────────────────────────┐
│  PhotoWeb  (F#  ASP.NET Core  :5000)                        │
│                                                              │
│  PhotoDb.fs  ◄──SQLite──  photos.db                         │
│  Program.fs  ──REST──►  /api/photos                         │
│                          /api/dates                          │
│                          /api/lenses                         │
│                          /api/image?path=...                 │
└──────────────────────────────────────────────────────────────┘
                              │
                        HTTP (proxied)
                              │
┌──────────────────────────────────────────────────────────────┐
│  photo-ui  (Vite dev server  :5173)                         │
│                                                              │
│  App.tsx                                                     │
│  ├── DateTree.tsx   (left sidebar — year/month/day tree)    │
│  └── PhotoGrid.tsx  (right pane — image tiles + lens filter) │
└──────────────────────────────────────────────────────────────┘
```

---

## Database Strategy

**SQLite** — embedded, no server, single file.

**Location:** `%APPDATA%\PhotoIndexer\photos.db`
(same folder as the existing `index.json`)

### Schema

```sql
CREATE TABLE photos (
    full_path        TEXT PRIMARY KEY NOT NULL,
    relative_path    TEXT NOT NULL,
    root_folder      TEXT NOT NULL,
    size_bytes       INTEGER NOT NULL,
    last_modified    TEXT NOT NULL,
    date_taken       TEXT,           -- ISO 8601: 2019-04-03T21:37:53
    lens_model       TEXT,
    f_number         REAL,
    exposure_time_ms REAL,
    iso_speed        INTEGER,
    focal_length_mm  REAL
);

CREATE INDEX idx_date ON photos(date_taken);
CREATE INDEX idx_lens ON photos(lens_model);
CREATE INDEX idx_root ON photos(root_folder);
```

### Why SQLite

- Embedded — no installation or server process needed
- The three indexes make date-range and lens-filter queries instant even
  with tens of thousands of photos
- The existing JSON store (`index.json`) is kept in sync so all CLI
  commands (`find`, `list`, `show`, `dump`) continue to work unchanged
- The F# API opens the database in **read-only** mode, so there is no
  write contention when the indexer runs simultaneously

---

## EXIF Fields Indexed

| Field | EXIF Tag | Notes |
|---|---|---|
| Date Taken | `0x9003` SubIFD DateTimeOriginal | Stored as ISO 8601 text |
| Lens Model | `0xA434` SubIFD LensModel | Falls back to MakerNote "Lens Type", then "Lens Specification" if empty or "N/A" |
| F-Number | `0x829D` SubIFD FNumber | Stored as decimal (e.g. 2.8) |
| Exposure Time | `0x829A` SubIFD ExposureTime | Converted to milliseconds |
| ISO Speed | `0x8827` SubIFD ISOSpeedRatings | Integer |
| Focal Length | `0x920A` SubIFD FocalLength | Stored in mm as decimal |

---

## UI Design

### Layout

```
┌──────────────┬─────────────────────────────────────────────┐
│  Browse      │  [Lens: All lenses ▾]                       │
│  ──────────  │  ─────────────────────────────────────────  │
│  All Photos  │                                             │
│  ▾ 2026      │  ┌───────┐ ┌───────┐ ┌───────┐ ┌───────┐  │
│    ▾ May     │  │       │ │       │ │       │ │       │  │
│      9       │  │ photo │ │ photo │ │ photo │ │ photo │  │
│  ▾ 2019      │  │       │ │       │ │       │ │       │  │
│    ▸ April   │  └───────┘ └───────┘ └───────┘ └───────┘  │
│  ▸ 2010      │  ┌───────┐ ┌───────┐ ...                   │
│              │  │       │ │       │                        │
│              │  └───────┘ └───────┘                        │
└──────────────┴─────────────────────────────────────────────┘
  sidebar 220px         content (flex-grow)
```

### Left Sidebar — Date Tree

- Root node **All Photos** — loads every indexed photo
- Expands to **Year** nodes (e.g. 2026, 2019, 2010)
- Each year expands to **Month** nodes (January, February …)
- Each month expands to **Day** nodes (1, 2, 3 …)
- Most recent year is auto-expanded on load
- Active selection is highlighted in blue
- Photos are always sorted **descending by timestamp**

### Right Pane — Display Area

- **Lens filter dropdown** at the top — populated from distinct lens
  models in the index; selecting one re-fetches photos for that lens
- **Photo grid** — `auto-fill` CSS grid, tiles are 190 px minimum width,
  aspect ratio 4:3
- Tile **tooltip** shows lens, f-stop, focal length, ISO, exposure on hover
- Tile **caption** shows the date taken (or filename if date unavailable)
- Clicking a tile opens the full-resolution JPEG in a **new browser tab**
- Images use `loading="lazy"` so only visible tiles are fetched
- A faded placeholder appears if a file has been moved or deleted

---

## API Endpoints (PhotoWeb)

All endpoints are on `http://localhost:5000`.

| Method | Path | Query params | Response |
|---|---|---|---|
| GET | `/api/photos` | `date` (YYYY-MM-DD or "all"), `lens` | `Photo[]` JSON |
| GET | `/api/dates` | — | Year/month/day tree JSON |
| GET | `/api/lenses` | — | `string[]` of distinct lens names |
| GET | `/api/image` | `path` (full path, URL-encoded) | JPEG binary |

The `/api/image` endpoint verifies the requested path exists in the
database before serving it — this prevents arbitrary file access.

### Photo JSON shape

```json
{
  "fullPath": "D:\\backup\\pictures\\2019-04-03\\IMG_0002.JPG",
  "relativePath": "2019-04-03\\IMG_0002.JPG",
  "sizeBytes": 5730930,
  "lastModified": "2019-04-04T04:37:54",
  "dateTaken": "2019-04-03T21:37:53",
  "lensModel": "Canon EF 28-70mm f/2.8L or Sigma or Tamron Lens",
  "fNumber": 2.8,
  "exposureTimeMs": 8.0,
  "isoSpeed": 100,
  "focalLengthMm": 75.0
}
```

---

## File Structure

```
c:\code\photos\
│
├── photos.sln                     Solution (PhotoIndexer + PhotoWeb)
│
├── PhotoIndexer\                  C# CLI
│   ├── Models.cs                  PhotoEntry, FolderIndex, IndexStore
│   ├── Scanner.cs                 EXIF extraction (MetadataExtractor)
│   ├── Store.cs                   JSON persistence
│   ├── SqliteStore.cs             SQLite persistence (NEW)
│   └── Program.cs                 CLI commands: index list show find dump
│
├── PhotoWeb\                      F# Web API
│   ├── PhotoDb.fs                 SQLite queries (read-only)
│   ├── Program.fs                 Minimal API endpoints
│   └── Properties\
│       └── launchSettings.json    Binds to http://localhost:5000
│
├── photo-ui\                      React + TypeScript
│   ├── index.html
│   ├── vite.config.ts             Proxies /api → localhost:5000
│   ├── tsconfig.json
│   └── src\
│       ├── main.tsx
│       ├── index.css
│       ├── App.tsx                Root layout (sidebar + content)
│       ├── App.css                All component styles (dark theme)
│       ├── types.ts               Photo, YearNode, MonthNode interfaces
│       ├── api.ts                 fetch wrappers + imageUrl helper
│       └── components\
│           ├── DateTree.tsx       Collapsible year/month/day tree
│           └── PhotoGrid.tsx      Lazy-loaded tile grid
│
└── doc\
    └── OVERVIEW.md                This document
```

---

## Setup & Running

### Prerequisites

- .NET 10 SDK — https://dot.net
- Node.js LTS — https://nodejs.org

### Step 1 — Install npm packages (once)

```
cd c:\code\photos\photo-ui
npm install
```

### Step 2 — Index your photos

Run for every folder you want in the browser. Can be re-run at any time
to pick up new photos; existing entries for that folder are replaced.

```
dotnet run --project PhotoIndexer -- index "D:\backup\pictures"
dotnet run --project PhotoIndexer -- index "C:\Users\manda\Pictures"
```

### Step 3 — Start the API (terminal 1)

```
dotnet run --project PhotoWeb
```

API is ready when you see: `Now listening on: http://localhost:5000`

### Step 4 — Start the UI (terminal 2)

```
cd photo-ui
npm run dev
```

Open `http://localhost:5173` in a browser.

---

## CLI Reference (PhotoIndexer)

| Command | Description |
|---|---|
| `index <folder>` | Scan folder recursively, write EXIF to JSON + SQLite |
| `list` | Print all indexed files as CSV (stdout) |
| `show <folder>` | Show summary for one indexed root folder |
| `find <file>` | Look up a specific file by full path |
| `dump <file>` | Print every raw EXIF tag in a JPEG (diagnostic) |

CSV columns output by `list`:
`FullPath, DateTaken, LastModified, SizeBytes, LensModel, FNumber, ExposureTimeMs, IsoSpeed, FocalLengthMm`
