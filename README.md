<p align="center">
  <img src="assets/logscope-logo.png" alt=".logscope logo" width="280">
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

## Safety guarantees

- **Read-only.** Source log files and their folders are never modified, renamed, deleted, or written to.
- **Offline.** No telemetry, no network requests, no local server or listening port.

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
