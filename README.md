# RewindFix

RewindFix is an open-source, experimental crash analysis and candidate-fix pipeline for .NET applications.

The goal is simple:

> Capture a production crash, locate the failing code, understand the root cause, generate a candidate fix, validate it, publish a fix branch, and open a pull request for review.

## Current Status

RewindFix is an early proof of concept. Crash capture, storage, a narrow rule-based fix generator, and an isolated validation pipeline are implemented. Git branch push and pull request creation are implemented, but publication depends on validation and credentials; the current sample does not pass validation after the generated fix. This is not a general-purpose automatic bug fixer.

Currently implemented:

- ASP.NET Core exception capture
- Structured crash reports
- Non-blocking crash delivery through a bounded in-memory queue and background sender
- Separate ingest and admin API keys using `X-RewindFix-Key`
- Source-path containment checks against a configured repository root
- MySQL persistence with EF Core
- Crash history endpoints
- Stack trace parsing
- Source file, method, and line detection
- Source context extraction around the failing line
- Rule-based root-cause analysis
- Suggested fix generation
- Structured code patch generation
- Safe patch preview returned as JSON without modifying the source tree
- One isolated Git worktree shared by patch application, build, tests, and publication
- Automatic `dotnet build` validation
- Existing test discovery and `dotnet test` validation
- Git worktree creation
- Generated fix branches and commits
- Automatic branch push to GitHub
- Automatic pull request creation through the GitHub API
- Best-effort cleanup of temporary worktrees and local branches in `finally`
- Two-minute timeouts per restore, build, test-project, and Git process
- Configurable automation levels: `Analyze`, `Patch`, `Validate`, and `Publish`

## Pipeline

Crash capture and delivery run automatically after an unhandled exception. Analysis, preview, validation, and publication are explicitly requested through admin API endpoints; ingestion does not start automatic repair.

1. Capture an exception, enqueue a crash report, and rethrow the original exception.
2. Send the report in the background and persist it in MySQL.
3. Parse the stack trace and read source context inside `RepositoryRoot`.
4. Generate a candidate patch when the supported rule matches.
5. Create a temporary branch/worktree from `origin/<BaseBranch>` and apply the patch there.
6. Restore, build, and run discovered test projects in that same worktree.
7. In `Publish` mode, commit and push only after validation succeeds, then request a GitHub pull request.
8. Attempt to remove the temporary worktree and local branch. Pushed remote branches remain.

A build or test failure stops publication. No pull request is merged automatically.

## Automation Levels

RewindFix can be configured to stop at different stages of the pipeline.

### `Analyze`

Allows the analysis endpoint, which can also return a generated patch as data. Dedicated preview, validation, and publication endpoints are blocked.

### `Patch`

Allows RewindFix to generate and return a patch preview. The preview is returned as structured JSON and does not create a `.rewindfix.cs` file beside the original source file.

### `Validate`

Allows patch generation plus isolated build and test validation. Git publication is blocked.

### `Publish`

Runs the full workflow:

Analysis and patch generation → temporary branch/worktree → build → tests → commit → push → pull request. Each later stage requires the preceding stage to succeed.

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

For this specific supported pattern, RewindFix can capture the resulting `NullReferenceException`, locate the failing source line, read the surrounding code, identify a likely null dereference, and generate a candidate patch such as:

```csharp
return customerName?.ToUpper() ?? string.Empty;
```

This is a text-based rule for a nearby `variable = null;` assignment and a target line containing `variable!.ToUpper()`. It is not semantic C# analysis or AI. The empty-string fallback is a candidate behavior that requires human review.

Before publication, the generated patch is applied in an isolated Git worktree and validated with build and discovered existing tests.

In `Publish` mode, a successful validation can be followed by commit, push, and pull request creation. The current sample tests expect HTTP 500 from `/api/crash`; the candidate fix returns HTTP 200, so those tests reject it and publication stops. These tests currently verify the broken baseline and delivery timing, not the desired post-fix behavior.

## Project Structure

```text
RewindFix/
├── samples/
│   └── RewindFix.SampleApi/
├── src/
│   ├── RewindFix.Api/
│   └── RewindFix.AspNetCore/
└── tests/
    ├── RewindFix.Api.Tests/
    └── RewindFix.SampleApi.Tests/
```

### `RewindFix.AspNetCore`

The SDK that is installed in an ASP.NET Core application. It captures unhandled exceptions and sends structured crash reports to the RewindFix API.

### `RewindFix.Api`

The backend that stores crashes, analyzes source locations, generates candidate fixes, validates patches, and coordinates Git and GitHub publication.

### `RewindFix.SampleApi`

A deliberately broken sample application used to exercise the full RewindFix pipeline.

### `RewindFix.SampleApi.Tests`

Integration tests for the deliberately crashing endpoint and its response timing. They currently assert HTTP 500, so they also reject the generated candidate fix.

### `RewindFix.Api.Tests`

Tests that source context can be read inside the configured repository root and is rejected outside it.

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

The token is used for the GitHub REST request to create a pull request. Git fetch/push use the host's Git credentials separately, and commits require a configured Git author. Even `Validate` currently fetches `origin/<BaseBranch>` and therefore requires remote access. The GitHub owner/repository must match the configured checkout.

GitHub App authentication is not implemented.

## Requirements

- .NET 10 SDK
- MySQL 8+
- Git
- Access to the Git remote for validation; Git push credentials and a GitHub token for publication
- `dotnet-ef` 9.x for the EF Core 9 / Pomelo 9 database migrations

## Local Setup

Run commands from the repository root unless noted otherwise. Create a MySQL database named `rewindfix`, then configure local values using .NET User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:RewindFix" "Server=localhost;Port=3306;Database=rewindfix;User=YOUR_USER;Password=YOUR_PASSWORD;" --project src/RewindFix.Api
dotnet user-secrets set "ApiKeys:IngestKey" "YOUR_INGEST_KEY" --project src/RewindFix.Api
dotnet user-secrets set "ApiKeys:AdminKey" "YOUR_DIFFERENT_ADMIN_KEY" --project src/RewindFix.Api
dotnet user-secrets set "RewindFix:RepositoryRoot" "ABSOLUTE_PATH_TO_YOUR_CHECKOUT" --project src/RewindFix.Api
dotnet user-secrets set "RewindFix:AutomationLevel" "Analyze" --project src/RewindFix.Api
```

Override the checked-in development path and automation level before running. Start with `Analyze`; use `Validate` or `Publish` explicitly when ready. For source lookup, the stack trace must contain file/line information and its paths must resolve inside this checkout. Cross-machine source-path mapping is not implemented.

The sample reads `RewindFix:ApiKey` from configuration. For example, set the environment variable `RewindFix__ApiKey` to the same ingest key before launching it. Keep keys and credentials out of committed files.

Apply migrations from the API directory because the design-time factory reads configuration from the current directory:

```bash
cd src/RewindFix.Api
dotnet ef database update
cd ../..
```

Build and run the two applications in separate terminals:

```bash
dotnet build RewindFix.slnx
dotnet run --project src/RewindFix.Api --launch-profile http
```

```bash
dotnet run --project samples/RewindFix.SampleApi --launch-profile http
```

Trigger the deliberately broken endpoint with `GET http://localhost:5163/api/crash`. It should fail with HTTP 500 while the report is sent in the background.

## API Endpoints

All endpoints below require the `X-RewindFix-Key` header. Missing or invalid keys return HTTP 401; an insufficient automation level returns HTTP 403 for preview/validation/publication.

| Method | Path | Required key | Minimum automation level |
| --- | --- | --- | --- |
| POST | `/api/crashes` | Ingest or admin | Analyze |
| GET | `/api/crashes` | Admin | Analyze |
| GET | `/api/crashes/{id}` | Admin | Analyze |
| GET | `/api/crashes/{id}/analysis` | Admin | Analyze |
| POST | `/api/crashes/{id}/apply-preview` | Admin | Patch |
| POST | `/api/crashes/{id}/validate-patch` | Admin | Validate |
| POST | `/api/crashes/{id}/prepare-git-fix` | Admin | Publish |

For example:

```bash
curl -H "X-RewindFix-Key: YOUR_ADMIN_KEY" http://localhost:5000/api/crashes
```

## Verification Status and Known Limitations

The latest reported local verification on 15 October 2026 passed the build and all four existing tests on the unpatched baseline. The candidate-fix run passed build validation but stopped at the sample tests described above. Worktree and local-branch cleanup was observed in that run. This is not evidence that arbitrary fixes or every publication path work.

- **Narrow fix support:** only the demonstrated null/`ToUpper` pattern has an automatic replacement rule. Other exceptions may be stored but return no reliable fix.
- **Limited test coverage:** the four tests cover source-path checks and the crashing sample; API-key enforcement, patch correctness, pipeline failures, and GitHub publication do not yet have comprehensive automated coverage.
- **No tests is not a behavioral pass:** discovery uses project-name suffixes `.Tests`, `.Test`, and `.IntegrationTests`. If none are found, validation returns `Success = true` with `TestsFound = false` and can proceed to publication.
- **Best-effort delivery:** the SDK queue is in memory (default capacity 1000), drops new reports when full, and has no durable storage or retries. Failed sends and process restarts can lose reports. Default send timeout is three seconds.
- **Source and revision assumptions:** source lookup uses local stack-trace paths; validation starts from the remote base branch. There is no crash-to-commit mapping. The patch applier checks that the target line still matches, but this does not establish full revision compatibility.
- **Path checks are not a sandbox:** checks use normalized relative paths and do not resolve symlink/junction escapes. Builds and tests execute code with the API process's permissions; use only trusted repositories.
- **Cancellation is incomplete:** process helpers accept cancellation tokens and have per-process timeouts, but HTTP endpoints do not yet propagate request cancellation through the pipeline. There is no whole-pipeline deadline.
- **Cleanup and publication are not transactional:** cleanup failures are not surfaced reliably; remote branches remain after push, including when PR creation fails. Duplicate fix branches/PRs are not prevented.
- **Data handling is basic:** query strings, exception messages, and stack traces are stored without sensitive-data redaction. There is no multi-tenant isolation, rate limiting, pagination, or retention policy.
- **No dashboard or AI integration:** inspection is API-based; no browser viewer, AI analysis, generated regression tests, or multi-file fixes are implemented.

## Safety Principles

RewindFix follows several safety rules:

- RewindFix must not break the monitored application when the RewindFix server is unavailable.
- A failing discovered test blocks publication. If no tests are discovered, that absence is reported but does not currently block publication.
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
- [x] Best-effort cleanup of temporary worktrees and local branches
- [x] Separate ingest/admin API keys
- [x] Non-blocking SDK queue and background sender
- [x] Per-process build/test/Git timeouts
- [ ] End-to-end request cancellation and whole-pipeline deadline
- [ ] Tests for desired post-fix behavior and pipeline failure paths
- [ ] Stronger source isolation and crash-to-commit mapping
- [ ] GitHub App authentication
- [ ] Smarter source-code analysis
- [ ] AI-assisted root-cause analysis
- [ ] Regression test generation
- [ ] Multi-file patches
- [ ] Background repair jobs, durable crash delivery, and retry queues
- [ ] Sensitive-data redaction
- [ ] NuGet packaging for the ASP.NET Core SDK
- [ ] Production-ready configuration and observability

## Important Note

RewindFix is experimental and is not ready for production use yet. Generated fixes may be incomplete or semantically incorrect even when they compile and pass available tests.

## License

This project is licensed under the [MIT License](LICENSE).

SPDX-License-Identifier: MIT
