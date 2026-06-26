<p align="center">
  <img src="assets/full-logo-light.png" alt=".logscope logo" width="440">
</p>

<h1 align="center">.logscope</h1>

A read-only, fully offline log analysis tool for SDETs and other technical users. Inspect, parse, filter, search, and monitor sensitive plain-text log files on Windows — without ever modifying them or sending data anywhere.

> **Current version: v0.4.0**

## Download

Grab the latest `LogScope.exe` from the [Releases page](https://github.com/Tishinator/.logscope/releases). Single self-contained executable — no installer, no .NET runtime required, no admin rights.

## What it does

### Opening files
- **Open a file or a folder** — a single `.log`, or a whole directory scanned recursively for `.log` files.
- **Format auto-detection** — when opening an unknown file, .logscope detects the likely format and prompts you to Accept, Revise in the wizard, or fall back to Raw. You always keep raw access.
- **Configurable extensions** — add `.txt`, `.csv`, or any custom extension from Settings ▸ File Extensions.
- **Workspace auto-refresh** — the file tree updates automatically when files are added or removed from the workspace.

### Viewing
- **Table & raw views** — parsed fields as columns, or the original text exactly as stored. Physical line numbers are always preserved.
- **Show/hide columns** — toggle any parsed column; the layout persists per profile.
- **Multiline events** — stack traces and continuation lines fold into the event above them; select a row to expand its detail.

### Parsing profiles
- **Delimiter & regex** — tab-delimited, comma, `|`, `||`, or any custom separator; or named-group regex.
- **Field semantic types** — assign Timestamp, Level, Module, Message, Thread, Device ID, Test Case, Run ID, or Result to any field. Level sorts by severity order (with custom levels), not alphabetically.
- **Parser wizard** — guided setup with a live preview of the first 50 lines; validates before saving.
- **Profile assignment** — assign a profile to a directory (applies recursively) or override per file.
- **Import / export** — share profiles as local JSON files.
- **Multiline rule** — define a "new event" regex so continuation lines (stack traces, wrapped messages) attach to the preceding event rather than becoming new rows.

### Filtering & search
- **Filter** — by text or regex, any field, time range (when a Timestamp field is mapped), or flagged events only. Include and exclude conditions can be combined. Scope to a specific column with the field-scope picker.
- **Filter presets** — save the current filter as a named preset, optionally scoped to the active profile so it only appears when that profile is in use.
- **Search** — case-sensitive, whole-word, regex, column-specific, with next/prev navigation. "Search all rows" mode finds matches even when they are hidden by the current filter, and reports them without clearing your filter.
- **Regex error feedback** — invalid filter or search patterns show a red border and inline error message instead of crashing.

### Visual rules
- **Color rules** — built-in rules tint ERROR/FATAL rows red and WARN rows amber. Author your own rules by field value, message substring, regex, or timestamp range — with configurable priority. Rules can color entire rows or individual fields.
- **Flag rules** — `ERROR`, `WARN`, `FATAL`, `FAIL`, `ASSERT`, `TIMEOUT`, `EXCEPTION` are flagged out of the box. Customize or add your own field-value or regex rules.
- **Flagged indicators** — badge counts appear in the file tree, tab headers, and the status bar. Each location is independently toggleable from Settings.

### Streaming
- **Live tail** — enable streaming on any tab to follow a log that is actively being written. New lines are parsed, filtered, and color-ruled incrementally within ~1 second.
- **Partial-write safety** — lines are only emitted once a newline arrives; a write split across multiple flushes is held in a buffer and assembled correctly.
- **CRLF support** — `\r\n` line endings split across separate writes are handled correctly.
- **File rotation / truncation** — when a file is truncated or replaced, .logscope detects it, reloads from the beginning, and shows a reset notice.
- **Multiline streaming** — continuation lines (stack traces, etc.) fold into the preceding event row even when delivered in separate streaming batches.
- **Auto-follow** — the view scrolls to the tail automatically. Scroll up manually to pause following; a **New entries: N** badge appears. Click it to jump back to live.

### Side-by-side comparison
- **Split view** — right-click any tab ▸ **Split with active tab**; or View ▸ **Split all open logs**.
- **Draggable splitter** — drag the divider between panes to resize them.
- **Scroll-linked sync** — scrolling one pane syncs the other automatically when synchronization is enabled.
- **Sync modes** — line-number (default) or nearest-timestamp. Sync is toggleable per pane. Falls back to line sync with a notice when a log has no parsed timestamp.

### Large files
- **Background loading** — files are parsed on a background thread; a progress bar and Cancel button appear while loading.
- **Chunked paging** — files over 500,000 lines show a **Load next 100k rows** button in the status bar to page in more content on demand, backed by a byte-offset line index.

### Other
- **Sort & restore** — click any column header to sort; the toolbar always offers Restore original file order.
- **External actions** — Reveal in Explorer, Open in default editor, Copy rows / raw text / line references.
- **Encoding** — auto-detects UTF-8 / UTF-16 LE/BE and falls back to Windows-1252 with a warning; force an encoding from the toolbar at any time.
- **Window position** — window size, position, and split layout persist across sessions.

## Safety guarantees

- **Read-only.** Source log files and their folders are never modified, renamed, deleted, or written to.
- **Offline.** No telemetry, no network requests, no local server or listening port of any kind.
- **Out-of-workspace state.** All profiles, settings, presets, and indexes are stored under `%APPDATA%\logscope` — never inside the selected workspace.

## Project layout

```
src/
  LogScope.Core/        — parsing, filtering, search, color/flag rules, streaming, workspace (no UI)
  LogScope.App/         — WPF desktop UI (MVVM) on top of Core
tests/
  LogScope.Core.Tests/  — xUnit + FluentAssertions (179 tests)
```

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) on Windows.

```sh
dotnet build LogScope.sln -c Release
dotnet test  tests/LogScope.Core.Tests
dotnet run   --project src/LogScope.App
```

Produce a standalone exe:

```sh
dotnet publish src/LogScope.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Built with TDD

The Core engine was built test-first, one behavior at a time. Every parser, filter, search, rule engine, and streaming watcher has tests written before the implementation.
