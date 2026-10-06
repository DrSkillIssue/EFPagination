# EFPagination

EFPagination is keyset (seek, cursor) pagination for Entity Framework Core, shipped as NuGet packages. It targets
.NET 10 and EF Core 10.

## Where code lives

- `src/EFPagination` - the core package: definitions (`Definition/`), the cursor format (`Cursor/`), sorting
  (`Sorting/`), the fluent builder and executors, and the expression-tree machinery (`Internal/`).
- `src/EFPagination.AspNetCore` - ASP.NET Core integration: `PaginationRequest`, `PaginatedResponse<T>`, endpoint
  extensions.
- `src/EFPagination.Analyzers` - Roslyn analyzers (`KP0001`-`KP0004`), shipped as their own package. Targets
  `netstandard2.0`. Every rule is listed in `AnalyzerReleases.Unshipped.md` or `AnalyzerReleases.Shipped.md`.
- `test/EFPagination.Tests` - the one test project: unit, analyzer and integration tests.
- `benchmark/EFPagination.Benchmarks` - BenchmarkDotNet benchmarks. A performance claim in a PR cites a run of these.
- `samples` - a sample web app. `docs` - user docs; `README.md` is the package readme.

## Code rules

- `.claude/rules/` holds the code rules: `conventions.md` for every change under `src/`, `csharp.md` for C#,
  `extensions.md` for code that uses `Microsoft.Extensions.*`, and `docs.md` for XML documentation. Read the ones that
  apply before writing code. Test code follows "Tests" below, which wins where the two conflict.

## Verifying

CI (`.github/workflows/ci.yml`, `lint.yml`) runs these; run them before a PR:

- `dotnet build EFPagination.sln` - warnings fail the build (`TreatWarningsAsErrors`, `AnalysisLevel`
  latest-recommended).
- `dotnet format style EFPagination.sln --verify-no-changes` and `dotnet format analyzers EFPagination.sln
  --verify-no-changes`.
- `dotnet test EFPagination.sln`.
- Restore uses lock files (`packages.lock.json`, `--locked-mode` in CI). A package change updates the lock files in the
  same commit. Package versions live in `Directory.Packages.props`.

## Tests

- xUnit v2 with FluentAssertions (`.Should()`). Classes are `public class XTests` in namespace `EFPagination`.
- A test with no database (cursor encoding, fingerprints, analyzers, sort registry) is a plain class with `[Fact]` and
  `[Theory]`.
- A test that runs queries takes `[Collection(SqliteDatabaseCollection.Name)]` and `SqliteDatabaseFixture` in its
  constructor, and gets a `TestDbContext` from `fixture.BuildServices()`. Models live in `TestModels/`.
- A bug fix comes with a test that fails before the fix. A test of something that differs between processes (a
  fingerprint, a hash) asserts a fixed expected value, not one computed in the same process.
- Name tests `Member_Condition_Result`. Assert exact values; never weaken an assertion to make a test pass.
- No doc comments on test methods.
- Run one class with `dotnet test test/EFPagination.Tests --filter "FullyQualifiedName~EFPagination.<ClassName>"`.

## Pull requests

- Make a PR only when the developer asks for one. Work on the developer's current branch; when that is `main`, make a
  branch first.
- Conventional commit titles, plain language: `fix(cursor): a cursor decodes in every process that issued it`. PRs are
  squash-merged, so the PR title is the commit title on `main`.
- Body: follow `.github/pull_request_template.md`. The problem in a sentence or two, then how you fixed it, then
  `Closes #N`. Name any breaking change, and any change to the cursor format, in its own line.

## Work tracking

- Track active work in the GitHub issue that owns it. Each issue uses a form in `.github/ISSUE_TEMPLATE/`: `bug`,
  `task` or `research`. An issue filed with `gh` gets no form, so write each form field as `### <label>`, a blank
  line, then the value.
- A merged PR is the implementation record. Close or update its tracking issue when the work lands.
