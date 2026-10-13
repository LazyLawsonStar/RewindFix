# RewindFix

RewindFix is an open-source crash analysis and automated fix platform for .NET applications.

The goal is simple:

> Capture a production crash, locate the failing code, understand the root cause, generate a candidate fix, validate it, and prepare a pull request.

## Current Status

RewindFix is an early proof of concept, but the core end-to-end pipeline is already working.

Currently implemented:

- ASP.NET Core exception capture
- Structured crash reports
- Remote crash delivery to the RewindFix API
- MySQL persistence with EF Core
- Crash history endpoints
- Stack trace parsing
- Source file, method, and line detection
- Source context extraction around the failing line
- Rule-based root-cause analysis
- Suggested fix generation
- Structured code patch generation
- Safe patch preview generation
- Temporary workspace validation
- Automatic `dotnet build` validation
- Existing test discovery and `dotnet test` validation
- Git worktree creation
- Generated fix branches and commits
- Validated fix flow up to a pull request

## How It Works

```text
ASP.NET Core Application
        |
        | exception
        v
RewindFix.AspNetCore SDK
        |
        | CrashReport
        v
RewindFix.Api
        |
        +--> MySQL persistence
        |
        +--> Stack trace parser
        |       |
        |       v
        |   Source location
        |       |
        |       v
        |   Source context
        |
        +--> Root cause analysis
        |
        +--> Suggested fix
        |
        +--> Code patch
        |
        +--> Temporary workspace
        |       |
        |       +--> dotnet build
        |       |
        |       +--> dotnet test
        |
        +--> Git branch + commit
        |
        v
   Pull Request
```

## Example

Given code like:

```csharp
string? customerName = null;

return customerName!.ToUpper();
```

RewindFix can capture the resulting `NullReferenceException`, locate the failing source line, read the surrounding code, identify a likely null dereference, and generate a candidate patch such as:

```csharp
return customerName?.ToUpper() ?? string.Empty;
```

The patch is applied in an isolated temporary workspace and validated with build and regression tests before it is considered ready for Git publication.

## Project Structure

```text
RewindFix/
├── samples/
│   └── RewindFix.SampleApi/
├── src/
│   ├── RewindFix.Api/
│   └── RewindFix.AspNetCore/
└── tests/
    └── RewindFix.SampleApi.Tests/
```

### `RewindFix.AspNetCore`

The SDK that is installed in an ASP.NET Core application. It captures unhandled exceptions and sends structured crash reports to the RewindFix API.

### `RewindFix.Api`

The backend that stores crashes, analyzes source locations, generates candidate fixes, validates patches, and coordinates the Git workflow.

### `RewindFix.SampleApi`

A deliberately broken sample application used to exercise the full RewindFix pipeline.

### `RewindFix.SampleApi.Tests`

Regression and integration tests used to verify that generated fixes actually improve application behavior instead of merely compiling.

## Requirements

- .NET 10 SDK
- MySQL 8+
- Git

## Build

```bash
dotnet build
```

## Run the RewindFix API

```bash
dotnet run --project src/RewindFix.Api
```

## Run the Sample Application

```bash
dotnet run --project samples/RewindFix.SampleApi
```

The sample crash endpoint is:

```text
GET http://localhost:5163/api/crash
```

Crash history is available from:

```text
GET http://localhost:5000/api/crashes
```

## Safety Principles

RewindFix follows several safety rules:

- RewindFix must not break the monitored application when the RewindFix server is unavailable.
- Generated patches are never considered valid only because they compile.
- Candidate fixes are validated in an isolated workspace.
- Regression tests are used when available.
- Production code should not be modified automatically without an explicit publication workflow.
- Secrets and repository credentials must never be committed to source control.

## Roadmap

- [x] Capture unhandled ASP.NET Core exceptions
- [x] Persist crash reports
- [x] Parse stack traces
- [x] Locate failing source code
- [x] Read source context
- [x] Rule-based root-cause analysis
- [x] Suggested fix generation
- [x] Patch generation
- [x] Build validation in an isolated workspace
- [x] Test validation
- [x] Generate Git branches and commits
- [ ] Native GitHub API integration inside RewindFix
- [ ] Automatic pull request publication
- [ ] GitHub App authentication
- [ ] Smarter source-code analysis
- [ ] AI-assisted root-cause analysis
- [ ] Regression test generation
- [ ] Multi-file patches
- [ ] Background processing and retry queues
- [ ] Sensitive-data redaction
- [ ] NuGet packaging for the ASP.NET Core SDK
- [ ] Production-ready configuration and observability

## Important Note

RewindFix is experimental and is not ready for production use yet. Generated fixes may be incomplete or semantically incorrect even when they compile and pass available tests.

## License

A license has not been selected yet.
