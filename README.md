<p align="center">
  <img src="assets/full-logo-light.png" alt=".logscope logo" width="440">
</p>

<h1 align="center">.logscope</h1>

A read-only, fully offline log analysis tool for SDETs and other technical users. Inspect, parse, filter, search, and monitor sensitive plain-text log files on Windows — without ever modifying them or sending data anywhere.

> **Status:** MVP (v0.0.1). Core engine is fully test-covered; the desktop UI wires it together.

## Download

Grab the latest `LogScope.exe` from the [Releases page](https://github.com/Tishinator/.logscope/releases). It's a single self-contained executable — no installer, no .NET runtime to install, no admin rights required. Just download and run.

## What it does

- **Open a file or a folder** — a single `.log`, or a whole directory scanned recursively for `.log` files.
- **Table & raw views** — parsed fields as columns, or the original text exactly as stored. Physical line numbers are always preserved.
- **Automatic format detection** — unknown logs are inspected and a delimiter/raw profile is suggested; you always keep raw access if parsing falls back.
- **Parsing** — delimiter-based (`|`, `||`, tab, comma…) and regex (named capture groups) parsing into semantic fields.
- **Multiline events** — stack traces and continuation lines attach to the event above them, expandable in a details row.
- **Filter & search** — filter rows by text or regex, show only flagged events; search with case-sensitivity / whole-word / regex and jump between matches.
- **Color & flag rules** — error/warning rows are tinted and flagged out of the box (`ERROR`, `WARN`, `FATAL`, `FAIL`, `ASSERT`, `TIMEOUT`, `EXCEPTION`).
- **Streaming** — follow a log that is actively being appended; the view updates within ~1 second.
- **External actions** — reveal a file in Explorer or open it in your default editor.

- **Parser profiles** — create reusable delimited/regex profiles in a wizard with live preview; assign a profile to a directory (applies to subfolders) or override per file; import/export profiles as local files.
- **Encoding** — auto-detects UTF-8 / UTF-16 (LE/BE) and falls back to Windows-1252 (ANSI) with a warning; you can also force an encoding from the toolbar.
- **Filter presets** — save the current filter as a named preset and reapply it later.
- **Field types / templates** — assign a semantic type to each field (Timestamp, Level, Module, Message, Thread, Device ID, Test Case, Run ID, Result). The Level field sorts by a configurable severity order (with custom levels), not alphabetically.
- **Custom color rules** — author your own row/field visual rules (field value, message-contains, or regex → color, with priority) in Settings ▸ Color rules; they persist.
- **Side-by-side split view** — right-click a tab ▸ **Split with active tab** to view logs side by side (repeat on another tab to add it; or View ▸ Split all). Optional synchronization (off by default) aligns the panes by physical line number or nearest timestamp when you select a row; sync is toggleable per pane and falls back to line sync (with a notice) when a log has no timestamp field.
- **Collapsible workspace panel** — collapse the file tree accordion-style to give logs more room.
- **Persistence** — window size, included extensions, profile assignments, presets, and color rules are saved between sessions, under your app-data folder (never in the log directory). Reset from the Settings menu.

## Safety guarantees

- **Read-only.** Source log files and their folders are never modified, renamed, deleted, or written to.
- **Offline.** No telemetry, no network requests, no local server or listening port.
- **Out-of-workspace state.** All profiles, settings, and presets live under `%APPDATA%\logscope` — never inside the selected workspace.

## Requirement coverage

Implemented for this release: directory/single-file open, recursive `.log` tree with configurable
extensions, table & raw views with physical line numbers, raw fallback, format auto-detection,
delimiter & regex parsing, multiline event grouping, a parser wizard with preview, directory- and
file-level profile assignment, profile import/export, field/text/regex filtering, search
(case/whole-word/regex with next/prev), column sorting + restore-file-order, configurable color &
flag rules, flagged indicators (tab badges + status), near-real-time streaming, copy
(rows/raw/line-refs), reveal-in-Explorer / open-in-editor, hover metadata, encoding detection +
manual override, filter presets, field semantic types with custom level severity, side-by-side
synchronized comparison (line/timestamp), settings persistence, and a large-file safety cap backed
by a byte-offset line index.

Not yet implemented (planned): advanced cross-log synchronized search, and live scroll-linked
(not just selection-linked) synchronization.

## Project layout

```
src/
  LogScope.Core/   — parsing, filtering, search, color/flag rules, streaming, workspace (no UI)
  LogScope.App/    — WPF desktop UI (MVVM) on top of Core
tests/
  LogScope.Core.Tests/  — xUnit + FluentAssertions, 83 tests
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

The Core engine was built test-first, one behavior at a time. Every parser, filter, search, rule engine, and the streaming watcher has tests written before the implementation.
