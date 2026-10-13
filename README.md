# RewindFix

RewindFix is an open-source crash analysis and automated fix platform for .NET applications.

The goal is simple:

> Capture a production crash, locate the failing code, understand the root cause, generate a candidate fix, validate it, publish a fix branch, and open a pull request for review.

## Current Status

RewindFix is still an early proof of concept, but the core end-to-end pipeline is now working.

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
- Safe patch preview returned as JSON without modifying the source tree
- Temporary workspace validation
- Automatic `dotnet build` validation
- Existing test discovery and `dotnet test` validation
- Git worktree creation
- Generated fix branches and commits
- Automatic branch push to GitHub
- Automatic pull request creation through the GitHub API
- Configurable automation levels: `Analyze`, `Patch`, `Validate`, and `Publish`

## End-to-End Flow

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
        +--> Git branch
        |
        +--> Commit
        |
        +--> Push to GitHub
        |
        v
   Pull Request
```

## Automation Levels

RewindFix can be configured to stop at different stages of the pipeline.

### `Analyze`

Performs crash analysis only. Patch application, validation, and publication are blocked.

### `Patch`

Allows RewindFix to generate and return a patch preview. The preview is returned as structured JSON and does not create a `.rewindfix.cs` file beside the original source file.

### `Validate`

Allows patch generation plus isolated build and test validation. Git publication is blocked.

### `Publish`

Runs the full workflow:

```text
analysis
→ patch
→ build
→ tests
→ branch
→ commit
→ push
→ pull request
```

The automation level can be configured in `appsettings.Development.json`:

```json
{
  "RewindFix": {
    "AutomationLevel": "Validate"
  }
}
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

Before publication, the generated patch is applied in an isolated temporary workspace and validated with build and regression tests.

If the project is configured with `AutomationLevel = Publish`, RewindFix can then create a dedicated branch, commit the validated change, push the branch to GitHub, and open a pull request automatically.

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

The backend that stores crashes, analyzes source locations, generates candidate fixes, validates patches, and coordinates Git and GitHub publication.

### `RewindFix.SampleApi`

A deliberately broken sample application used to exercise the full RewindFix pipeline.

### `RewindFix.SampleApi.Tests`

Regression and integration tests used to verify that generated fixes actually improve application behavior instead of merely compiling.

## GitHub Integration

For local development, RewindFix uses a fine-grained GitHub personal access token to create pull requests.

The token must not be committed to source control. Store it with .NET User Secrets:

```bash
dotnet user-secrets set "GitHub:Token" "YOUR_TOKEN" --project src/RewindFix.Api
```

Example development configuration:

```json
{
  "RewindFix": {
    "AutomationLevel": "Publish"
  },
  "GitHub": {
    "Owner": "LazyLawsonStar",
    "Repository": "RewindFix",
    "BaseBranch": "main"
  }
}
```

For a production version, RewindFix should use GitHub App authentication rather than requiring users to create personal access tokens manually.

## Requirements

- .NET 10 SDK
- MySQL 8+
- Git
- GitHub credentials only when using `Publish`

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
- Patch preview should not modify the monitored source tree.
- Publication is controlled by an explicit automation level.
- `Publish` creates a branch and pull request; it does not automatically merge into `main`.
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
- [x] Safe patch preview
- [x] Build validation in an isolated workspace
- [x] Test validation
- [x] Generate Git branches and commits
- [x] Push generated fix branches to GitHub
- [x] Native GitHub API integration
- [x] Automatic pull request publication
- [x] Configurable automation levels
- [ ] Prevent duplicate branches and pull requests for the same crash/fix
- [ ] Automatic cleanup of temporary validation workspaces and Git worktrees
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
