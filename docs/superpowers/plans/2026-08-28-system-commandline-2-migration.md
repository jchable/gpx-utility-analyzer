# System.CommandLine 2.x Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate `cli/src/GpxAnalyzer.Cli` from `System.CommandLine 2.0.0-beta4.22272.1` to the stable `2.0.11`, with zero change to CLI behaviour, guarded by a characterization test suite built first.

**Architecture:** The CLI exe is a thin `System.CommandLine` command layer (`Program.cs` + `Commands/*.cs`) over `GpxAnalyzer.Cli.Core`, which holds all computation. Only the command layer touches `System.CommandLine`, so the migration is confined to 6 files; `Core`, the API and the AI analyzer are untouched. Because the beta4 and 2.x APIs are mutually exclusive (no shared spelling for defaults or handlers), the migration is one atomic transaction across those 6 files, fenced by golden-file tests that pin today's stdout byte-for-byte.

**Tech Stack:** .NET 9.0, C# 13, Native AOT (`PublishAot`), `System.CommandLine 2.0.11`, xUnit 2.x, `Microsoft.NET.Test.Sdk 17.*`.

**Spec:** GitHub PR #57 (`chore(deps): Bump Google.GenAI, Microsoft.NET.Test.Sdk and System.CommandLine`) + this plan is self-contained.

## Global Constraints

- Target version is **`System.CommandLine 2.0.11`** — confirmed latest stable on nuget.org (`dotnet package search System.CommandLine --exact-match`: 2.0.0 … 2.0.11), and the exact version PR #57 proposes.
- Reference implementation to copy idioms from: `ai-analyzer/src/GpxAiAnalyzer/Program.cs` and `ai-analyzer/src/GpxAiAnalyzer/Commands/AnalyzeCommand.cs` — already on `2.*` with the target API.
- .NET 9.0 target framework; do not change `TargetFramework`.
- `PublishAot=true` must stay true and must not gain new IL2xxx/IL3xxx warnings. `System.CommandLine 2.0.11` ships `[AssemblyMetadata("IsTrimmable","True")]` and has **zero** package dependencies on `net8.0`, so it is AOT-friendly.
- **No behaviour change is allowed.** stdout for every command/flag combination must be byte-identical before and after. (Verified during plan authoring: all 10 characterization cases below are byte-identical between beta4 and 2.0.11.)
- The only accepted user-visible delta is `--help` layout, documented in Task 9.
- All 217 existing tests in `cli/tests/GpxAnalyzer.Cli.Tests/` must keep passing (`dotnet test` currently reports `217/217`).
- Commit messages MUST NOT contain a `Co-Authored-By` trailer (project rule).
- Do not edit `ai-analyzer/**` or `ui/**` in this work.
- Tests must run offline: every characterization invocation passes `--dem-auto-download false`, because `analyze`/`split`/`merge`/`benchmark` default to `--dem-auto-download true` and would otherwise hit the network for SRTM tiles.

---

## File Structure

| File | Status | Responsibility |
|------|--------|----------------|
| `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/CliRunner.cs` | **Create** | Locates the built `gpx-analyzer` binary, runs it out-of-process in a throwaway working directory seeded with `testdata/*.gpx`, captures stdout/stderr/exit code. |
| `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/Golden.cs` | **Create** | Golden-file compare/update helper + output normalizers (line endings, benchmark timings, non-ASCII box drawing). |
| `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/CliGoldenTests.cs` | **Create** | 10 behavioural golden tests + 1 exit-code test across all 4 commands. Must NOT change across the migration. |
| `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/CliHelpGoldenTests.cs` | **Create** | 5 `--help` golden tests. Deliberately re-baselined in Task 9 so the help delta is a reviewed, committed artifact. |
| `cli/tests/GpxAnalyzer.Cli.Tests/testdata/golden/*.txt` | **Create** | 15 golden files (generated, then committed). Read from and written to the *source* tree, not `bin/`. |
| `cli/src/GpxAnalyzer.Cli/GpxAnalyzer.Cli.csproj` | **Modify** (lines 16–21) | Package pin `2.0.0-beta4.22272.1` → `2.0.11`; drop the stale "pinned to beta4" comment. |
| `cli/src/GpxAnalyzer.Cli/Program.cs` | **Modify** (whole file, 16 lines) | Root command wiring: recursive `--format`, `Subcommands.Add`, `Parse(args).InvokeAsync()`. |
| `cli/src/GpxAnalyzer.Cli/Commands/SharedFlags.cs` | **Modify** (lines 1–40) | `BuildConfigFromContext(InvocationContext)` → `BuildConfigFromParseResult(ParseResult)`; 18 value reads. |
| `cli/src/GpxAnalyzer.Cli/Commands/BenchmarkCommand.cs` | **Modify** (lines 1–49) | 11 options + 1 argument + handler + 12 value reads. |
| `cli/src/GpxAnalyzer.Cli/Commands/AnalyzeCommand.cs` | **Modify** (lines 1–59) | 20 options + 1 argument + handler + 4 value reads + `SharedFlags` call. |
| `cli/src/GpxAnalyzer.Cli/Commands/SplitCommand.cs` | **Modify** (lines 1–66) | 20 options + 1 argument + handler + 5 value reads + `SharedFlags` call. |
| `cli/src/GpxAnalyzer.Cli/Commands/MergeCommand.cs` | **Modify** (lines 1–58, 107–110) | 20 options + 1 argument + handler + 5 value reads + `SharedFlags` call. |

Nothing else in the repository changes. `cli/tests/GpxAnalyzer.Cli.Tests/GpxAnalyzer.Cli.Tests.csproj` needs **no** edit: it already has `<ProjectReference Include="..\..\src\GpxAnalyzer.Cli\GpxAnalyzer.Cli.csproj" />` (so building the tests builds the CLI) and `<Content Include="testdata\**\*" CopyToOutputDirectory="PreserveNewest" />` (which harmlessly also picks up the new `testdata/golden/` files).

### The 5 API change patterns

Verified by reflection over `System.CommandLine.dll` 2.0.11 and by compiling the real migration.

| # | beta4 | 2.0.11 | Count |
|---|-------|--------|-------|
| 1 | `new Option<T>("--x", () => def, "desc")` | `new Option<T>("--x") { Description = "desc", DefaultValueFactory = _ => def }` — the only `Option<T>` ctor is `(string name, params string[] aliases)`; `Description` comes from `Symbol`; `DefaultValueFactory` is `Func<ArgumentResult, T>` | 72 |
| 2 | `cmd.SetHandler((InvocationContext ctx) => …)` | `cmd.SetAction((ParseResult parseResult) => …)` — binds the `Action<ParseResult>` overload; all 4 handlers are synchronous and use bare `return;`, so no `Func<…>` overload is selected | 4 |
| 3 | `ctx.ParseResult.GetValueForOption(o)` / `GetValueForArgument(a)` | `parseResult.GetValue(o)` / `parseResult.GetRequiredValue(a)` | 40 + 4 |
| 4 | `opt.AddAlias("-f")`; `root.AddGlobalOption(o)`; `root.AddCommand(c)` | `new Option<string>("--format", "-f")`; `o.Recursive = true;` + `root.Options.Add(o)`; `root.Subcommands.Add(c)` | 4 + 1 + 4 |
| 5 | `await rootCommand.InvokeAsync(args)` | `await rootCommand.Parse(args).InvokeAsync()` | 1 |

Also changed: `new Argument<T>("name", "desc")` → `new Argument<T>("name") { Description = "desc" }` (the only `Argument<T>` ctor is `(string name)`) — 4 occurrences.

**Two traps found while validating this plan:**

- `parseResult.GetValue<T>(Argument<T>)` returns `T?`, whereas beta4's `GetValueForArgument` returned `T`. Using `GetValue` for the 4 arguments introduces 4 new `CS8604` nullable warnings. Use **`GetRequiredValue`** (returns non-nullable `T`) for arguments; keep `GetValue` + `?? "default"` for options, which is already the existing pattern.
- `Required` vs `IsRequired` is **not relevant here**: this CLI declares no required *options*. Required-ness is expressed only through argument arity (`ArgumentArity.OneOrMore` on `files`, implicit `ExactlyOne` on `file`), which is unchanged in 2.x. `Option.Required` exists in 2.0.11 but stays unused.

### Why there is no green build between Task 2 and Task 7

beta4 has no `DefaultValueFactory` property and 2.0.11 has no `InvocationContext`, so no source file can compile against both. Tasks 2–6 therefore end with a **deliberately red build whose error set shrinks by exactly the file just migrated**; that shrinking error count is the checkpoint. The single commit lands at the end of Task 7. Exact expected counts (verified on a probe migration):

| After task | `dotnet build` errors | Breakdown |
|---|---|---|
| 2 (csproj + `Program.cs`) | **1** | `SharedFlags.cs(12,56)` |
| 3 (`SharedFlags.cs`) | **86** | Analyze 23, Benchmark 16, Merge 24, Split 23 |
| 4 (`BenchmarkCommand.cs`) | **70** | Analyze 23, Merge 24, Split 23 |
| 5 (`AnalyzeCommand.cs`) | **47** | Merge 24, Split 23 |
| 6 (`SplitCommand.cs`) | **24** | Merge 24 |
| 7 (`MergeCommand.cs`) | **0** | build succeeds |

---

### Task 1: Characterization safety net (golden-file tests for the command layer)

The command layer has **zero** test coverage today: all 217 tests in `cli/tests/GpxAnalyzer.Cli.Tests/` exercise `GpxAnalyzer.Cli.Core` only (`grep -r "GpxAnalyzer.Cli.Commands" cli/tests/` returns nothing). Nothing would catch a wrong default value, a lost alias or a swapped option after the migration. This task builds that net first. **Do not start Task 2 until Task 1 is committed and green.**

**Files:**
- Create: `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/CliRunner.cs`
- Create: `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/Golden.cs`
- Create: `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/CliGoldenTests.cs`
- Create: `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/CliHelpGoldenTests.cs`
- Create: `cli/tests/GpxAnalyzer.Cli.Tests/testdata/golden/` (15 `.txt` files, generated in Step 4)
- Test: the new files *are* the tests.

**Interfaces:**
- Consumes: the built executable `cli/src/GpxAnalyzer.Cli/bin/{Debug|Release}/net9.0/{rid}/gpx-analyzer[.exe]`; the four fixtures `cli/tests/GpxAnalyzer.Cli.Tests/testdata/{small,two-segments,with-extensions,with-gps-quality}.gpx`.
- Produces:
  - `internal static CliResult CliRunner.Run(params string[] args)`
  - `internal sealed record CliResult(string StdOut, string StdErr, int ExitCode)`
  - `internal static string CliRunner.GoldenDir { get; }`
  - `internal static string Golden.Normalize(string text)`
  - `internal static string Golden.NormalizeBenchmark(string text)`
  - `internal static void Golden.Verify(string name, string actual)`

**Design decisions (each one is load-bearing — do not simplify them away):**

1. **Out-of-process, not in-process.** `Program.cs` uses top-level statements, so its entry point is `<Program>$.<Main>$` and is not callable. The test spawns the real binary.
2. **The copy of `gpx-analyzer.exe` that lands in the *test* output directory does not run** — it is a self-contained apphost without the runtime beside it and dies with `A fatal error was encountered. The library 'hostpolicy.dll' … Failed to run as a self-contained app.` The runner must therefore locate the binary under the *CLI project's own* `bin/<Configuration>/net9.0/<rid>/`, which does work.
3. **Run in a throwaway working directory** seeded with copies of the fixtures, and pass **relative** paths. The CLI echoes the path it was given into the JSON `filename` field, so an absolute path would bake `E:\…` vs `/home/runner/…` into the golden.
4. **`--dem-auto-download false` on every invocation.** Default is `true`, which calls `DemSource.CreateAuto(...)` and downloads SRTM tiles over the network.
5. **Goldens live in the source tree**, resolved from `AppContext.BaseDirectory` up three levels, so `UPDATE_GOLDEN=1` rewrites the committed files rather than a stale `bin/` copy.
6. **Line endings are normalized on both sides.** The repo has no `.gitattributes`, so goldens may check out CRLF on Windows and LF on Linux.
7. **The benchmark table needs its own normalizer.** Its last column is `{ms}ms` per run and the footer is `Completed {0} runs in {1:F1}s (avg: {2}ms/run)`; column widths are computed from the widest cell, so a `0ms` vs `11ms` run shifts every border. Worse, on Windows the box-drawing characters are emitted in the OEM code page (verified: bytes `da c4 c4 c2 …`, i.e. cp437/850) while Linux emits UTF-8. The normalizer replaces timings, strips every non-ASCII byte, then collapses runs of spaces.
8. **Help goldens are a separate class** because `--help` output legitimately changes in 2.x (see Task 9); the behavioural goldens must not.

**Steps:**

- [ ] **Step 1: Write the runner.** Create `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/CliRunner.cs`:

```csharp
using System.Diagnostics;
using System.Text;

namespace GpxAnalyzer.Cli.Tests.Characterization;

internal sealed record CliResult(string StdOut, string StdErr, int ExitCode);

/// <summary>
/// Runs the built gpx-analyzer executable out-of-process inside a throwaway working
/// directory seeded with the testdata GPX fixtures, so that every path the CLI echoes
/// back is relative and therefore identical on Windows and Linux.
/// </summary>
internal static class CliRunner
{
    // AppContext.BaseDirectory = <proj>/bin/<Configuration>/net9.0/
    private static readonly string TestProjectDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

    private static readonly string Configuration =
        new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

    private static readonly string TestDataDir = Path.Combine(TestProjectDir, "testdata");

    internal static string GoldenDir => Path.Combine(TestDataDir, "golden");

    private static readonly Lazy<string> Executable = new(FindExecutable);

    private static string FindExecutable()
    {
        // <repo>/cli/tests/GpxAnalyzer.Cli.Tests -> <repo>/cli/src/GpxAnalyzer.Cli/bin/<cfg>/net9.0
        var binDir = Path.GetFullPath(Path.Combine(
            TestProjectDir, "..", "..", "src", "GpxAnalyzer.Cli", "bin", Configuration, "net9.0"));

        if (!Directory.Exists(binDir))
            throw new InvalidOperationException(
                $"gpx-analyzer output directory not found: {binDir}. " +
                "Build it first: dotnet build cli/src/GpxAnalyzer.Cli/");

        // The build puts the apphost under a RID subfolder (win-x64, linux-x64, ...)
        // because the project is SelfContained.
        var name = OperatingSystem.IsWindows() ? "gpx-analyzer.exe" : "gpx-analyzer";
        var matches = Directory.GetFiles(binDir, name, SearchOption.AllDirectories);
        if (matches.Length == 0)
            throw new InvalidOperationException($"'{name}' not found under {binDir}.");

        return matches[0];
    }

    internal static CliResult Run(params string[] args)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "gpx-char-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            foreach (var gpx in Directory.GetFiles(TestDataDir, "*.gpx"))
                File.Copy(gpx, Path.Combine(workDir, Path.GetFileName(gpx)));

            var psi = new ProcessStartInfo(Executable.Value)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            using var process = Process.Start(psi)!;
            // Drain both pipes concurrently: split prints one stderr line per segment
            // and a full pipe buffer would deadlock a sequential ReadToEnd.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(120_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"gpx-analyzer {string.Join(' ', args)} timed out after 120s.");
            }

            return new CliResult(
                stdoutTask.GetAwaiter().GetResult(),
                stderrTask.GetAwaiter().GetResult(),
                process.ExitCode);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* best effort */ }
        }
    }
}
```

- [ ] **Step 2: Write the golden helper.** Create `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/Golden.cs`:

```csharp
using System.Text.RegularExpressions;

namespace GpxAnalyzer.Cli.Tests.Characterization;

internal static class Golden
{
    private static readonly Regex Millis = new(@"\d+ms", RegexOptions.Compiled);
    private static readonly Regex Seconds = new(@"in \d+[.,]\d+s", RegexOptions.Compiled);
    private static readonly Regex NonAscii = new(@"[^\x20-\x7E\n]", RegexOptions.Compiled);
    private static readonly Regex SpaceRuns = new(@"[ ]{2,}", RegexOptions.Compiled);

    /// <summary>CRLF/LF agnostic, single trailing newline.</summary>
    internal static string Normalize(string text) =>
        text.Replace("\r\n", "\n").TrimEnd('\n') + "\n";

    /// <summary>
    /// The benchmark table embeds per-run wall-clock timings, its column widths are
    /// derived from those timings, and its box-drawing characters are emitted in the
    /// OEM code page on Windows but UTF-8 on Linux. Strip all three sources of noise.
    /// </summary>
    internal static string NormalizeBenchmark(string text)
    {
        var t = Normalize(text);
        t = Millis.Replace(t, "<ms>");
        t = Seconds.Replace(t, "in <s>");
        t = NonAscii.Replace(t, "");
        t = SpaceRuns.Replace(t, " ");
        return string.Join('\n', t.Split('\n').Select(line => line.TrimEnd()));
    }

    /// <summary>
    /// Compares <paramref name="actual"/> against testdata/golden/{name}.txt.
    /// Set UPDATE_GOLDEN=1 to (re)write the golden instead of asserting.
    /// </summary>
    internal static void Verify(string name, string actual)
    {
        Directory.CreateDirectory(CliRunner.GoldenDir);
        var path = Path.Combine(CliRunner.GoldenDir, name + ".txt");

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(path, Normalize(actual));
            return;
        }

        Assert.True(File.Exists(path),
            $"Golden file missing: {path}. Regenerate with UPDATE_GOLDEN=1 and review the diff.");

        Assert.Equal(Normalize(File.ReadAllText(path)), Normalize(actual));
    }
}
```

- [ ] **Step 3: Write the behavioural tests (they will fail — no goldens exist yet).** Create `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/CliGoldenTests.cs`:

```csharp
namespace GpxAnalyzer.Cli.Tests.Characterization;

/// <summary>
/// Characterization tests: they pin the CURRENT stdout of the CLI command layer
/// byte-for-byte. They exist to fence the System.CommandLine 2.x migration and must
/// keep passing unchanged across it. If one of them fails after a command-layer edit,
/// the edit changed behaviour.
///
/// Every invocation passes --dem-auto-download false: the default is true, which
/// downloads SRTM tiles over the network.
/// </summary>
public class CliGoldenTests
{
    [Fact]
    public void Analyze_JsonDefaults_MatchesGolden()
    {
        var r = CliRunner.Run("--format", "json", "analyze", "--dem-auto-download", "false", "small.gpx");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("analyze-default", r.StdOut);
    }

    [Fact]
    public void Analyze_TextFormatter_MatchesGolden()
    {
        var r = CliRunner.Run("analyze", "--dem-auto-download", "false", "small.gpx");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("analyze-text", r.StdOut);
    }

    [Fact]
    public void Analyze_ManyFlagsAndGlobalOptionAfterSubcommand_MatchesGolden()
    {
        var r = CliRunner.Run(
            "analyze", "--format", "json", "--dem-auto-download", "false",
            "--preset", "trail", "--smoothing", "heavy", "--track-smoothing", "light",
            "--elevation-algo", "douglas-peucker", "--dp-epsilon", "1.5",
            "--elevation-threshold", "1", "--max-hr", "190", "--max-speed", "8",
            "with-extensions.gpx");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("analyze-flags", r.StdOut);
    }

    [Fact]
    public void Analyze_SegmentsAlgoAndStopOverrides_MatchesGolden()
    {
        var r = CliRunner.Run(
            "--format", "json", "analyze", "--dem-auto-download", "false",
            "--elevation-algo", "segments", "--seg-min-length", "100", "--seg-max-deviation", "1",
            "--stop-speed", "0.5", "--stop-duration", "30",
            "two-segments.gpx");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("analyze-segments", r.StdOut);
    }

    [Fact]
    public void Analyze_ShortFormatAliasAndFixAnomalies_MatchesGolden()
    {
        var r = CliRunner.Run("-f", "json", "analyze", "--dem-auto-download", "false",
            "--fix-anomalies", "with-gps-quality.gpx");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("analyze-fix-anomalies", r.StdOut);
    }

    [Fact]
    public void Analyze_ExportEnriched_MatchesGoldenAndReportsExport()
    {
        var r = CliRunner.Run("--format", "json", "analyze", "--dem-auto-download", "false",
            "--export", "exported", "--enrich", "small.gpx");
        Assert.Equal(0, r.ExitCode);
        // The export path is built with Path.Combine, so it is platform-dependent:
        // assert on the invariant part only, and golden just the stdout.
        Assert.Contains("_processed.gpx", r.StdErr);
        Golden.Verify("analyze-export", r.StdOut);
    }

    [Fact]
    public void Split_TwelveHours_MatchesGolden()
    {
        var r = CliRunner.Run("--format", "json", "split", "two-segments.gpx",
            "--interval", "12h", "--output-dir", "out", "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("split-12h", r.StdOut);
    }

    [Fact]
    public void Split_ThirtyMinutesWithPrefix_MatchesGolden()
    {
        var r = CliRunner.Run("--format", "json", "split", "small.gpx",
            "--interval", "30m", "--output-dir", "out2", "--prefix", "chunk",
            "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("split-30m", r.StdOut);
    }

    [Fact]
    public void Merge_WithAnalyze_MatchesGolden()
    {
        var r = CliRunner.Run("--format", "json", "merge", "small.gpx", "two-segments.gpx",
            "--output", "out/merged.gpx", "--analyze", "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("merge-analyze", r.StdOut);
    }

    [Fact]
    public void Merge_ShortOutputAliasAndNoSort_MatchesGolden()
    {
        var r = CliRunner.Run("--format", "json", "merge", "small.gpx", "two-segments.gpx",
            "-o", "out/m2.gpx", "--analyze", "--sort", "false", "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("merge-nosort", r.StdOut);
    }

    [Fact]
    public void Benchmark_VaryPreset_MatchesGolden()
    {
        var r = CliRunner.Run("benchmark", "small.gpx", "--vary", "preset",
            "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        Golden.Verify("benchmark-vary-preset", Golden.NormalizeBenchmark(r.StdOut));
    }

    [Fact]
    public void Benchmark_ReducedSortedVerbose_MatchesGolden()
    {
        var r = CliRunner.Run("benchmark", "small.gpx", "--sort", "elev-gain", "-v",
            "--dem-auto-download", "false");
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Running", r.StdErr);   // -v alias reached the handler
        Golden.Verify("benchmark-reduced", Golden.NormalizeBenchmark(r.StdOut));
    }

    [Fact]
    public void Analyze_MissingRequiredArgument_ExitsOneAndReportsIt()
    {
        var r = CliRunner.Run("analyze");
        Assert.Equal(1, r.ExitCode);
        Assert.Contains("Required argument missing for command: 'analyze'.", r.StdOut + r.StdErr);
    }
}
```

- [ ] **Step 4: Write the help tests.** Create `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/CliHelpGoldenTests.cs`:

```csharp
namespace GpxAnalyzer.Cli.Tests.Characterization;

/// <summary>
/// Golden tests for --help output. Unlike CliGoldenTests these ARE expected to change
/// when System.CommandLine changes major version: the help renderer belongs to the
/// library, not to this codebase. Re-baseline them deliberately (UPDATE_GOLDEN=1) and
/// review the committed diff.
/// </summary>
public class CliHelpGoldenTests
{
    [Theory]
    [InlineData("help-root")]
    [InlineData("help-analyze")]
    [InlineData("help-split")]
    [InlineData("help-merge")]
    [InlineData("help-benchmark")]
    public void Help_MatchesGolden(string name)
    {
        string[] args = name == "help-root"
            ? ["--help"]
            : [name["help-".Length..], "--help"];

        var r = CliRunner.Run(args);
        Assert.Equal(0, r.ExitCode);
        Golden.Verify(name, r.StdOut);
    }
}
```

- [ ] **Step 5: Run the new tests and watch them fail.** The CLI binary must exist first.

```bash
dotnet build cli/src/GpxAnalyzer.Cli/
dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "FullyQualifiedName~Characterization"
```

Expected: **17 failed, 1 passed** (`Analyze_MissingRequiredArgument_ExitsOneAndReportsIt` passes; the 12 behavioural + 5 help golden tests fail). Each failure reads:

```
Assert.True() Failure
Expected: True
Actual:   False
Golden file missing: …\cli\tests\GpxAnalyzer.Cli.Tests\testdata\golden\analyze-default.txt. Regenerate with UPDATE_GOLDEN=1 and review the diff.
```

If instead every test fails with `gpx-analyzer output directory not found` or `'gpx-analyzer.exe' not found`, the CLI was not built — run the `dotnet build` line above and retry.

- [ ] **Step 6: Generate the goldens.**

```powershell
$env:UPDATE_GOLDEN = '1'
dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "FullyQualifiedName~Characterization"
Remove-Item Env:\UPDATE_GOLDEN
```

(bash equivalent: `UPDATE_GOLDEN=1 dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "FullyQualifiedName~Characterization"`)

Expected: **18 passed**. Then confirm 15 files were written:

```bash
ls cli/tests/GpxAnalyzer.Cli.Tests/testdata/golden/
```

Expected file list: `analyze-default.txt`, `analyze-export.txt`, `analyze-fix-anomalies.txt`, `analyze-flags.txt`, `analyze-segments.txt`, `analyze-text.txt`, `benchmark-reduced.txt`, `benchmark-vary-preset.txt`, `help-analyze.txt`, `help-benchmark.txt`, `help-merge.txt`, `help-root.txt`, `help-split.txt`, `merge-analyze.txt`, `merge-nosort.txt`.

- [ ] **Step 7: Sanity-check the goldens before trusting them.** They are the specification for the rest of this plan, so read them.

```bash
head -5 cli/tests/GpxAnalyzer.Cli.Tests/testdata/golden/analyze-default.txt
grep -c . cli/tests/GpxAnalyzer.Cli.Tests/testdata/golden/analyze-default.txt
grep -rlP '[^\x00-\x7F]' cli/tests/GpxAnalyzer.Cli.Tests/testdata/golden/ ; echo "exit=$?"
```

Expected: `analyze-default.txt` starts with `{`, `"filename": "small.gpx",` on line 2 (a bare filename — **not** an absolute path; if you see `E:\…` or `/tmp/…` the working-directory isolation is broken, fix it before continuing), roughly 147 lines, and the `grep -rlP` finds **no** file containing non-ASCII bytes (`exit=1`).

- [ ] **Step 8: Prove the goldens are stable, not lucky.** Run the suite three more times without `UPDATE_GOLDEN` and confirm the goldens are untouched.

```bash
dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "FullyQualifiedName~Characterization"
dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "FullyQualifiedName~Characterization"
dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "FullyQualifiedName~Characterization"
git status --porcelain cli/tests/GpxAnalyzer.Cli.Tests/testdata/golden/
```

Expected: `18 passed` three times, and `git status --porcelain` shows only untracked (`??`) entries — no modifications between runs. A flapping benchmark golden means `NormalizeBenchmark` is not stripping enough; fix it before continuing.

- [ ] **Step 9: Run the whole suite.**

```bash
dotnet test cli/tests/GpxAnalyzer.Cli.Tests/
```

Expected: `Passed! - Failed: 0, Passed: 235, Skipped: 0, Total: 235` (217 existing + 18 new).

- [ ] **Step 10: Commit the safety net.**

```bash
git add cli/tests/GpxAnalyzer.Cli.Tests/Characterization cli/tests/GpxAnalyzer.Cli.Tests/testdata/golden
git commit -m "test(cli): add characterization golden tests for the CLI command layer

The command layer (Program.cs + Commands/*.cs) had zero coverage: all 217
existing tests exercise GpxAnalyzer.Cli.Core only. These 18 tests run the built
gpx-analyzer binary out-of-process in a throwaway working directory and pin the
stdout of analyze/split/merge/benchmark byte-for-byte, so the upcoming
System.CommandLine 2.x migration can be verified as behaviour-preserving.

Every invocation passes --dem-auto-download false to stay offline."
```

---

### Task 2: Bump System.CommandLine to 2.0.11 and migrate Program.cs

**Files:**
- Modify: `cli/src/GpxAnalyzer.Cli/GpxAnalyzer.Cli.csproj` lines 16–21
- Modify: `cli/src/GpxAnalyzer.Cli/Program.cs` (whole file)
- Test: build error count only (see "Why there is no green build" above)

**Interfaces:**
- Consumes: `AnalyzeCommand.Create(Option<string>)`, `SplitCommand.Create(Option<string>)`, `MergeCommand.Create(Option<string>)`, `BenchmarkCommand.Create()` — signatures unchanged by this task.
- Produces: an `int` process exit code from `await rootCommand.Parse(args).InvokeAsync()`.

**Steps:**

- [ ] **Step 1: Bump the package reference.** In `cli/src/GpxAnalyzer.Cli/GpxAnalyzer.Cli.csproj`, replace

```xml
  <ItemGroup>
    <!-- Pinned to beta4: the CLI command layer uses the beta4 API
         (SetHandler / InvocationContext / GetValueForOption). Upgrading to the
         stable 2.0.x line is a separate, breaking API migration. -->
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
  </ItemGroup>
```

with

```xml
  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.11" />
  </ItemGroup>
```

- [ ] **Step 2: Confirm the restore resolves and see the first failure.**

```bash
dotnet build cli/src/GpxAnalyzer.Cli/ -v q -nologo
```

Expected tail: `1 Error(s)`, and the error is

```
Commands/SharedFlags.cs(12,56): error CS0246: The type or namespace name 'InvocationContext' could not be found (are you missing a using directive or an assembly reference?)
```

Only **one** error appears even though five files are still on the beta4 API: `InvocationContext` is in a method *signature*, so Roslyn fails the declaration phase and never binds method bodies. This is expected and is the reason Task 3 comes next.

- [ ] **Step 3: Migrate `Program.cs`.** Replace the whole file with:

```csharp
using System.CommandLine;
using GpxAnalyzer.Cli.Commands;

var formatOption = new Option<string>("--format", "-f")
{
    Description = "Output format: text or json",
    DefaultValueFactory = _ => "text",
    Recursive = true,
};

var rootCommand = new RootCommand("Analyze GPX files: distance, elevation, stops, and more");
rootCommand.Options.Add(formatOption);

rootCommand.Subcommands.Add(AnalyzeCommand.Create(formatOption));
rootCommand.Subcommands.Add(SplitCommand.Create(formatOption));
rootCommand.Subcommands.Add(MergeCommand.Create(formatOption));
rootCommand.Subcommands.Add(BenchmarkCommand.Create());

return await rootCommand.Parse(args).InvokeAsync();
```

Previous content, for reference:

```csharp
using System.CommandLine;
using GpxAnalyzer.Cli.Commands;

var formatOption = new Option<string>("--format", () => "text", "Output format: text or json");
formatOption.AddAlias("-f");

var rootCommand = new RootCommand("Analyze GPX files: distance, elevation, stops, and more");
rootCommand.AddGlobalOption(formatOption);

rootCommand.AddCommand(AnalyzeCommand.Create(formatOption));
rootCommand.AddCommand(SplitCommand.Create(formatOption));
rootCommand.AddCommand(MergeCommand.Create(formatOption));
rootCommand.AddCommand(BenchmarkCommand.Create());

return await rootCommand.InvokeAsync(args);
```

`Recursive = true` is the 2.x replacement for `AddGlobalOption`: it is what keeps `--format`/`-f` accepted both before and after the subcommand name (`gpx-analyzer --format json analyze …` **and** `gpx-analyzer analyze --format json …`), which the existing goldens exercise in both positions.

- [ ] **Step 4: Rebuild and confirm the error set did not grow.**

```bash
dotnet build cli/src/GpxAnalyzer.Cli/ -v q -nologo
```

Expected: still exactly `1 Error(s)`, still `Commands/SharedFlags.cs(12,56): error CS0246`. No error may mention `Program.cs`. Do **not** commit yet — the build is red by design until Task 7.

---

### Task 3: Migrate SharedFlags.cs

**Files:**
- Modify: `cli/src/GpxAnalyzer.Cli/Commands/SharedFlags.cs` lines 1–40
- Test: build error count

**Interfaces:**
- Consumed by: `AnalyzeCommand`, `SplitCommand`, `MergeCommand` (3 call sites).
- Before: `internal static ComputeConfig SharedFlags.BuildConfigFromContext(InvocationContext ctx, Option<string> presetOpt, …, Option<bool>? fixAnomaliesOpt = null)`
- After: `internal static ComputeConfig SharedFlags.BuildConfigFromParseResult(ParseResult parseResult, Option<string> presetOpt, …, Option<bool>? fixAnomaliesOpt = null)`
- `BuildConfig(string preset, …)` (line 42 onward) is **unchanged** — do not touch it.

The method is renamed because "Context" no longer names anything in 2.x. It is `internal static` with 3 compiler-checked call sites, so the rename is safe.

**Steps:**

- [ ] **Step 1: Replace lines 1–40 of `Commands/SharedFlags.cs`.** Before:

```csharp
using System.CommandLine;
using System.CommandLine.Invocation;
using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Dem;
using GpxAnalyzer.Cli.Core.Elevation;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

internal static class SharedFlags
{
    public static ComputeConfig BuildConfigFromContext(InvocationContext ctx,
        Option<string> presetOpt, Option<double> stopSpeedOpt, Option<double> stopDurationOpt,
        Option<double> elevThresholdOpt, Option<string> smoothingOpt,
        Option<string> demDirOpt, Option<string> demCacheOpt, Option<bool> demAutoOpt,
        Option<int> demMaxMemOpt, Option<bool> demSkipValOpt,
        Option<string> elevAlgoOpt, Option<string> trackSmoothOpt,
        Option<double> dpEpsOpt, Option<double> segMinLenOpt, Option<double> segMaxDevOpt,
        Option<int> maxHrOpt, Option<double> maxSpeedOpt, Option<bool>? fixAnomaliesOpt = null)
    {
        return BuildConfig(
            ctx.ParseResult.GetValueForOption(presetOpt) ?? "hiking",
            ctx.ParseResult.GetValueForOption(stopSpeedOpt),
            ctx.ParseResult.GetValueForOption(stopDurationOpt),
            ctx.ParseResult.GetValueForOption(elevThresholdOpt),
            ctx.ParseResult.GetValueForOption(smoothingOpt) ?? "medium",
            ctx.ParseResult.GetValueForOption(demDirOpt) ?? "",
            ctx.ParseResult.GetValueForOption(demCacheOpt) ?? "",
            ctx.ParseResult.GetValueForOption(demAutoOpt),
            ctx.ParseResult.GetValueForOption(demMaxMemOpt),
            ctx.ParseResult.GetValueForOption(demSkipValOpt),
            ctx.ParseResult.GetValueForOption(elevAlgoOpt) ?? "threshold",
            ctx.ParseResult.GetValueForOption(trackSmoothOpt) ?? "none",
            ctx.ParseResult.GetValueForOption(dpEpsOpt),
            ctx.ParseResult.GetValueForOption(segMinLenOpt),
            ctx.ParseResult.GetValueForOption(segMaxDevOpt),
            ctx.ParseResult.GetValueForOption(maxHrOpt),
            ctx.ParseResult.GetValueForOption(maxSpeedOpt),
            fixAnomaliesOpt != null && ctx.ParseResult.GetValueForOption(fixAnomaliesOpt));
    }
```

After:

```csharp
using System.CommandLine;
using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Dem;
using GpxAnalyzer.Cli.Core.Elevation;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

internal static class SharedFlags
{
    public static ComputeConfig BuildConfigFromParseResult(ParseResult parseResult,
        Option<string> presetOpt, Option<double> stopSpeedOpt, Option<double> stopDurationOpt,
        Option<double> elevThresholdOpt, Option<string> smoothingOpt,
        Option<string> demDirOpt, Option<string> demCacheOpt, Option<bool> demAutoOpt,
        Option<int> demMaxMemOpt, Option<bool> demSkipValOpt,
        Option<string> elevAlgoOpt, Option<string> trackSmoothOpt,
        Option<double> dpEpsOpt, Option<double> segMinLenOpt, Option<double> segMaxDevOpt,
        Option<int> maxHrOpt, Option<double> maxSpeedOpt, Option<bool>? fixAnomaliesOpt = null)
    {
        return BuildConfig(
            parseResult.GetValue(presetOpt) ?? "hiking",
            parseResult.GetValue(stopSpeedOpt),
            parseResult.GetValue(stopDurationOpt),
            parseResult.GetValue(elevThresholdOpt),
            parseResult.GetValue(smoothingOpt) ?? "medium",
            parseResult.GetValue(demDirOpt) ?? "",
            parseResult.GetValue(demCacheOpt) ?? "",
            parseResult.GetValue(demAutoOpt),
            parseResult.GetValue(demMaxMemOpt),
            parseResult.GetValue(demSkipValOpt),
            parseResult.GetValue(elevAlgoOpt) ?? "threshold",
            parseResult.GetValue(trackSmoothOpt) ?? "none",
            parseResult.GetValue(dpEpsOpt),
            parseResult.GetValue(segMinLenOpt),
            parseResult.GetValue(segMaxDevOpt),
            parseResult.GetValue(maxHrOpt),
            parseResult.GetValue(maxSpeedOpt),
            fixAnomaliesOpt != null && parseResult.GetValue(fixAnomaliesOpt));
    }
```

Two things changed beyond the mechanical rename: `using System.CommandLine.Invocation;` is deleted (`ParseResult` lives in `System.CommandLine`), and every `ctx.ParseResult.GetValueForOption(x)` became `parseResult.GetValue(x)`. The `?? "default"` fallbacks stay: `GetValue<T>` returns `T?`.

- [ ] **Step 2: Rebuild and confirm the second error wave.**

```bash
dotnet build cli/src/GpxAnalyzer.Cli/ -v q -nologo
```

Expected: `86 Error(s)`, all inside the four not-yet-migrated command files. Break it down to be sure:

```bash
dotnet build cli/src/GpxAnalyzer.Cli/ -v q -nologo 2>&1 \
  | grep -oE "(AnalyzeCommand|SplitCommand|MergeCommand|BenchmarkCommand)\.cs\([0-9]+,[0-9]+\): error CS[0-9]+" \
  | sort -u | sed 's/\.cs.*//' | uniq -c
```

Expected:

```
     23 AnalyzeCommand
     16 BenchmarkCommand
     24 MergeCommand
     23 SplitCommand
```

The codes you will see are `CS1729` (`Argument<T>` has no 2-argument constructor), `CS1660` (`() => x` cannot convert to `string` — the removed `getDefaultValue` parameter), `CS1061` (`AddAlias` no longer exists) and `CS0246` (`InvocationContext`). Zero errors may mention `Program.cs` or `SharedFlags.cs`. Still no commit.

---

### Task 4: Migrate BenchmarkCommand.cs

Smallest command and the only one that does not call `SharedFlags`, so it is the cleanest first full command migration.

**Files:**
- Modify: `cli/src/GpxAnalyzer.Cli/Commands/BenchmarkCommand.cs` lines 1–49
- Test: build error count

**Interfaces:**
- `public static Command BenchmarkCommand.Create()` — signature unchanged.
- `private static List<BenchmarkCombination> GenerateVaryCombos(string axes)` (line 157 onward) — unchanged, do not touch.
- Everything from `var wallClock = Stopwatch.StartNew();` (old line 51) to the end of the handler is unchanged.

**Steps:**

- [ ] **Step 1: Replace lines 1–49.** Before:

```csharp
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using GpxAnalyzer.Cli.Core.Benchmark;
using GpxAnalyzer.Cli.Core.Dem;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class BenchmarkCommand
{
    public static Command Create()
    {
        var fileArg = new Argument<string>("file", "GPX file to benchmark");
        var outputOpt = new Option<string>("--output", () => "", "CSV output file");
        outputOpt.AddAlias("-o");
        var fullOpt = new Option<bool>("--full", () => false, "Full Cartesian product (~1248 runs)");
        var varyOpt = new Option<string>("--vary", () => "", "Axes to vary (comma-separated: preset,elev-algo,elev-smoothing,track-smoothing,dem,elev-params)");
        var verboseOpt = new Option<bool>("--verbose", () => false, "Print progress to stderr");
        verboseOpt.AddAlias("-v");
        var sortOpt = new Option<string>("--sort", () => "", $"Sort by column ({string.Join(", ", BenchmarkOutput.ValidSortColumns)})");
        var maxHrOpt = new Option<int>("--max-hr", () => 0, "Max HR for zone calculation");
        var demDirOpt = new Option<string>("--dem-dir", () => "", "SRTM .hgt directory");
        var demCacheOpt = new Option<string>("--dem-cache", () => "", "DEM cache directory");
        var demAutoOpt = new Option<bool>("--dem-auto-download", () => true, "Auto-download missing tiles");
        var demMaxMemOpt = new Option<int>("--dem-max-memory", () => 0, "Max memory for DEM (MB, 0=unlimited)");
        var demSkipValOpt = new Option<bool>("--dem-skip-validation", () => false, "Skip tile validation");

        var cmd = new Command("benchmark", "Run multi-configuration benchmark on a GPX file")
        {
            fileArg, outputOpt, fullOpt, varyOpt, verboseOpt, sortOpt,
            maxHrOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt, demSkipValOpt
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var file = ctx.ParseResult.GetValueForArgument(fileArg);
            var output = ctx.ParseResult.GetValueForOption(outputOpt) ?? "";
            var full = ctx.ParseResult.GetValueForOption(fullOpt);
            var vary = ctx.ParseResult.GetValueForOption(varyOpt) ?? "";
            var verbose = ctx.ParseResult.GetValueForOption(verboseOpt);
            var sort = ctx.ParseResult.GetValueForOption(sortOpt) ?? "";
            var maxHr = ctx.ParseResult.GetValueForOption(maxHrOpt);
            var demDir = ctx.ParseResult.GetValueForOption(demDirOpt) ?? "";
            var demCache = ctx.ParseResult.GetValueForOption(demCacheOpt) ?? "";
            var demAuto = ctx.ParseResult.GetValueForOption(demAutoOpt);
            var demMaxMem = ctx.ParseResult.GetValueForOption(demMaxMemOpt);
            var demSkipVal = ctx.ParseResult.GetValueForOption(demSkipValOpt);
```

After:

```csharp
using System.CommandLine;
using System.Diagnostics;
using GpxAnalyzer.Cli.Core.Benchmark;
using GpxAnalyzer.Cli.Core.Dem;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class BenchmarkCommand
{
    public static Command Create()
    {
        var fileArg = new Argument<string>("file") { Description = "GPX file to benchmark" };
        var outputOpt = new Option<string>("--output", "-o") { Description = "CSV output file", DefaultValueFactory = _ => "" };
        var fullOpt = new Option<bool>("--full") { Description = "Full Cartesian product (~1248 runs)", DefaultValueFactory = _ => false };
        var varyOpt = new Option<string>("--vary") { Description = "Axes to vary (comma-separated: preset,elev-algo,elev-smoothing,track-smoothing,dem,elev-params)", DefaultValueFactory = _ => "" };
        var verboseOpt = new Option<bool>("--verbose", "-v") { Description = "Print progress to stderr", DefaultValueFactory = _ => false };
        var sortOpt = new Option<string>("--sort") { Description = $"Sort by column ({string.Join(", ", BenchmarkOutput.ValidSortColumns)})", DefaultValueFactory = _ => "" };
        var maxHrOpt = new Option<int>("--max-hr") { Description = "Max HR for zone calculation", DefaultValueFactory = _ => 0 };
        var demDirOpt = new Option<string>("--dem-dir") { Description = "SRTM .hgt directory", DefaultValueFactory = _ => "" };
        var demCacheOpt = new Option<string>("--dem-cache") { Description = "DEM cache directory", DefaultValueFactory = _ => "" };
        var demAutoOpt = new Option<bool>("--dem-auto-download") { Description = "Auto-download missing tiles", DefaultValueFactory = _ => true };
        var demMaxMemOpt = new Option<int>("--dem-max-memory") { Description = "Max memory for DEM (MB, 0=unlimited)", DefaultValueFactory = _ => 0 };
        var demSkipValOpt = new Option<bool>("--dem-skip-validation") { Description = "Skip tile validation", DefaultValueFactory = _ => false };

        var cmd = new Command("benchmark", "Run multi-configuration benchmark on a GPX file")
        {
            fileArg, outputOpt, fullOpt, varyOpt, verboseOpt, sortOpt,
            maxHrOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt, demSkipValOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var file = parseResult.GetRequiredValue(fileArg);
            var output = parseResult.GetValue(outputOpt) ?? "";
            var full = parseResult.GetValue(fullOpt);
            var vary = parseResult.GetValue(varyOpt) ?? "";
            var verbose = parseResult.GetValue(verboseOpt);
            var sort = parseResult.GetValue(sortOpt) ?? "";
            var maxHr = parseResult.GetValue(maxHrOpt);
            var demDir = parseResult.GetValue(demDirOpt) ?? "";
            var demCache = parseResult.GetValue(demCacheOpt) ?? "";
            var demAuto = parseResult.GetValue(demAutoOpt);
            var demMaxMem = parseResult.GetValue(demMaxMemOpt);
            var demSkipVal = parseResult.GetValue(demSkipValOpt);
```

Note the `--sort` description keeps its interpolated `$"…{string.Join(", ", …)}…"` string verbatim; a naive find-and-replace mangles it because it contains a quoted `", "`.

Note also `GetRequiredValue(fileArg)`, not `GetValue(fileArg)`: `GetValue` would return `string?` and produce `CS8604` at `GpxParser.ParseFile(file)`.

- [ ] **Step 2: Rebuild and confirm 15 errors disappeared.**

```bash
dotnet build cli/src/GpxAnalyzer.Cli/ -v q -nologo
```

Expected: `70 Error(s)`, none of them in `BenchmarkCommand.cs`. Verify with the same breakdown command as Task 3 Step 2; expected output:

```
     23 AnalyzeCommand
     24 MergeCommand
     23 SplitCommand
```

Still no commit.

---

### Task 5: Migrate AnalyzeCommand.cs

**Files:**
- Modify: `cli/src/GpxAnalyzer.Cli/Commands/AnalyzeCommand.cs` lines 1–59
- Test: build error count

**Interfaces:**
- `public static Command AnalyzeCommand.Create(Option<string> formatOption)` — unchanged.
- `private static void AnalyzeFile(string path, IFormatter formatter, ComputeConfig cfg, string exportDir, bool enrich)` (line 77 onward) — unchanged, do not touch.
- Calls `SharedFlags.BuildConfigFromParseResult(...)` produced by Task 3.

**Steps:**

- [ ] **Step 1: Replace lines 1–59.** Before:

```csharp
using System.CommandLine;
using System.CommandLine.Invocation;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Input;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class AnalyzeCommand
{
    public static Command Create(Option<string> formatOption)
    {
        var filesArg = new Argument<string[]>("files", "GPX files, directories, or glob patterns")
        {
            Arity = ArgumentArity.OneOrMore
        };
        var presetOpt = new Option<string>("--preset", () => "hiking", "Stop detection preset: hiking, trail, cycling");
        var stopSpeedOpt = new Option<double>("--stop-speed", () => 0, "Override max speed for stops (m/s)");
        var stopDurationOpt = new Option<double>("--stop-duration", () => 0, "Override min duration for stops (seconds)");
        var elevThresholdOpt = new Option<double>("--elevation-threshold", () => 2.0, "Min elevation change (meters)");
        var smoothingOpt = new Option<string>("--smoothing", () => "medium", "Elevation smoothing: none, light, medium, heavy");
        var demDirOpt = new Option<string>("--dem-dir", () => "", "SRTM .hgt directory");
        var demCacheOpt = new Option<string>("--dem-cache", () => "", "DEM cache directory");
        var demAutoOpt = new Option<bool>("--dem-auto-download", () => true, "Auto-download missing tiles");
        var demMaxMemOpt = new Option<int>("--dem-max-memory", () => 0, "Max memory for DEM (MB, 0=unlimited)");
        var demSkipValOpt = new Option<bool>("--dem-skip-validation", () => false, "Skip tile validation");
        var elevAlgoOpt = new Option<string>("--elevation-algo", () => "threshold", "Algorithm: threshold, douglas-peucker, segments");
        var trackSmoothOpt = new Option<string>("--track-smoothing", () => "none", "GPS lat/lon smoothing: none, light, medium, heavy");
        var dpEpsOpt = new Option<double>("--dp-epsilon", () => 3.0, "Douglas-Peucker epsilon (meters)");
        var segMinLenOpt = new Option<double>("--seg-min-length", () => 200.0, "Segments min length (meters)");
        var segMaxDevOpt = new Option<double>("--seg-max-deviation", () => 2.0, "Segments max RMS residual (meters)");
        var exportOpt = new Option<string>("--export", () => "", "Export preprocessed GPX directory");
        var enrichOpt = new Option<bool>("--enrich", () => false, "Include computed metrics in export");
        var maxHrOpt = new Option<int>("--max-hr", () => 0, "Max HR for zone calculation");
        var maxSpeedOpt = new Option<double>("--max-speed", () => 0, "GPS outlier removal threshold (m/s)");
        var fixAnomaliesOpt = new Option<bool>("--fix-anomalies", () => false, "Apply automatic anomaly corrections");

        var cmd = new Command("analyze", "Analyze GPX files: distance, elevation, speed, pace, stops")
        {
            filesArg, presetOpt, stopSpeedOpt, stopDurationOpt, elevThresholdOpt,
            smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
            demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt,
            segMaxDevOpt, exportOpt, enrichOpt, maxHrOpt, maxSpeedOpt, fixAnomaliesOpt
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var files = ctx.ParseResult.GetValueForArgument(filesArg);
            var format = ctx.ParseResult.GetValueForOption(formatOption) ?? "text";
            var export = ctx.ParseResult.GetValueForOption(exportOpt) ?? "";
            var enrich = ctx.ParseResult.GetValueForOption(enrichOpt);

            var formatter = FormatterFactory.Create(format, GpxAnalyzer.Cli.Output.JsonContext.Default.Options);
            var resolvedFiles = FileResolver.ResolveFiles(files);
            var cfg = SharedFlags.BuildConfigFromContext(ctx, presetOpt, stopSpeedOpt, stopDurationOpt,
                elevThresholdOpt, smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
                demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt, segMaxDevOpt,
                maxHrOpt, maxSpeedOpt, fixAnomaliesOpt);
```

After:

```csharp
using System.CommandLine;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Input;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class AnalyzeCommand
{
    public static Command Create(Option<string> formatOption)
    {
        var filesArg = new Argument<string[]>("files")
        {
            Description = "GPX files, directories, or glob patterns",
            Arity = ArgumentArity.OneOrMore
        };
        var presetOpt = new Option<string>("--preset") { Description = "Stop detection preset: hiking, trail, cycling", DefaultValueFactory = _ => "hiking" };
        var stopSpeedOpt = new Option<double>("--stop-speed") { Description = "Override max speed for stops (m/s)", DefaultValueFactory = _ => 0 };
        var stopDurationOpt = new Option<double>("--stop-duration") { Description = "Override min duration for stops (seconds)", DefaultValueFactory = _ => 0 };
        var elevThresholdOpt = new Option<double>("--elevation-threshold") { Description = "Min elevation change (meters)", DefaultValueFactory = _ => 2.0 };
        var smoothingOpt = new Option<string>("--smoothing") { Description = "Elevation smoothing: none, light, medium, heavy", DefaultValueFactory = _ => "medium" };
        var demDirOpt = new Option<string>("--dem-dir") { Description = "SRTM .hgt directory", DefaultValueFactory = _ => "" };
        var demCacheOpt = new Option<string>("--dem-cache") { Description = "DEM cache directory", DefaultValueFactory = _ => "" };
        var demAutoOpt = new Option<bool>("--dem-auto-download") { Description = "Auto-download missing tiles", DefaultValueFactory = _ => true };
        var demMaxMemOpt = new Option<int>("--dem-max-memory") { Description = "Max memory for DEM (MB, 0=unlimited)", DefaultValueFactory = _ => 0 };
        var demSkipValOpt = new Option<bool>("--dem-skip-validation") { Description = "Skip tile validation", DefaultValueFactory = _ => false };
        var elevAlgoOpt = new Option<string>("--elevation-algo") { Description = "Algorithm: threshold, douglas-peucker, segments", DefaultValueFactory = _ => "threshold" };
        var trackSmoothOpt = new Option<string>("--track-smoothing") { Description = "GPS lat/lon smoothing: none, light, medium, heavy", DefaultValueFactory = _ => "none" };
        var dpEpsOpt = new Option<double>("--dp-epsilon") { Description = "Douglas-Peucker epsilon (meters)", DefaultValueFactory = _ => 3.0 };
        var segMinLenOpt = new Option<double>("--seg-min-length") { Description = "Segments min length (meters)", DefaultValueFactory = _ => 200.0 };
        var segMaxDevOpt = new Option<double>("--seg-max-deviation") { Description = "Segments max RMS residual (meters)", DefaultValueFactory = _ => 2.0 };
        var exportOpt = new Option<string>("--export") { Description = "Export preprocessed GPX directory", DefaultValueFactory = _ => "" };
        var enrichOpt = new Option<bool>("--enrich") { Description = "Include computed metrics in export", DefaultValueFactory = _ => false };
        var maxHrOpt = new Option<int>("--max-hr") { Description = "Max HR for zone calculation", DefaultValueFactory = _ => 0 };
        var maxSpeedOpt = new Option<double>("--max-speed") { Description = "GPS outlier removal threshold (m/s)", DefaultValueFactory = _ => 0 };
        var fixAnomaliesOpt = new Option<bool>("--fix-anomalies") { Description = "Apply automatic anomaly corrections", DefaultValueFactory = _ => false };

        var cmd = new Command("analyze", "Analyze GPX files: distance, elevation, speed, pace, stops")
        {
            filesArg, presetOpt, stopSpeedOpt, stopDurationOpt, elevThresholdOpt,
            smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
            demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt,
            segMaxDevOpt, exportOpt, enrichOpt, maxHrOpt, maxSpeedOpt, fixAnomaliesOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var files = parseResult.GetRequiredValue(filesArg);
            var format = parseResult.GetValue(formatOption) ?? "text";
            var export = parseResult.GetValue(exportOpt) ?? "";
            var enrich = parseResult.GetValue(enrichOpt);

            var formatter = FormatterFactory.Create(format, GpxAnalyzer.Cli.Output.JsonContext.Default.Options);
            var resolvedFiles = FileResolver.ResolveFiles(files);
            var cfg = SharedFlags.BuildConfigFromParseResult(parseResult, presetOpt, stopSpeedOpt, stopDurationOpt,
                elevThresholdOpt, smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
                demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt, segMaxDevOpt,
                maxHrOpt, maxSpeedOpt, fixAnomaliesOpt);
```

The `foreach (var path in resolvedFiles)` block and everything after it stays exactly as it is.

- [ ] **Step 2: Rebuild and confirm 23 errors disappeared.**

```bash
dotnet build cli/src/GpxAnalyzer.Cli/ -v q -nologo
```

Expected: `47 Error(s)`, breakdown `24 MergeCommand` / `23 SplitCommand`, nothing in `AnalyzeCommand.cs`. Still no commit.

---

### Task 6: Migrate SplitCommand.cs

**Files:**
- Modify: `cli/src/GpxAnalyzer.Cli/Commands/SplitCommand.cs` lines 1–66
- Test: build error count

**Interfaces:**
- `public static Command SplitCommand.Create(Option<string> formatOption)` — unchanged.
- `private static TimeSpan ParseDuration(string s)` (line 106 onward) — unchanged, do not touch.

**Steps:**

- [ ] **Step 1: Replace lines 1–66.** Before (abbreviating only the 17 shared-flag `new Option<…>` lines, which are byte-identical to the AnalyzeCommand "Before" block above for `--preset` … `--max-speed`):

```csharp
using System.CommandLine;
using System.CommandLine.Invocation;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Split;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class SplitCommand
{
    public static Command Create(Option<string> formatOption)
    {
        var fileArg = new Argument<string>("file", "GPX file to split");
        var intervalOpt = new Option<string>("--interval", () => "24h", "Split interval (e.g. 24h, 12h, 30m)");
        var outputDirOpt = new Option<string>("--output-dir", () => "splits", "Output directory for split files");
        var prefixOpt = new Option<string>("--prefix", () => "segment", "Filename prefix for split files");
        …
        cmd.SetHandler((InvocationContext ctx) =>
        {
            var file = ctx.ParseResult.GetValueForArgument(fileArg);
            var interval = ctx.ParseResult.GetValueForOption(intervalOpt) ?? "24h";
            var outputDir = ctx.ParseResult.GetValueForOption(outputDirOpt) ?? "splits";
            var prefix = ctx.ParseResult.GetValueForOption(prefixOpt) ?? "segment";
            var format = ctx.ParseResult.GetValueForOption(formatOption) ?? "text";
            …
            var cfg = SharedFlags.BuildConfigFromContext(ctx, presetOpt, stopSpeedOpt, stopDurationOpt,
```

After — write this out in full:

```csharp
using System.CommandLine;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Split;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class SplitCommand
{
    public static Command Create(Option<string> formatOption)
    {
        var fileArg = new Argument<string>("file") { Description = "GPX file to split" };
        var intervalOpt = new Option<string>("--interval") { Description = "Split interval (e.g. 24h, 12h, 30m)", DefaultValueFactory = _ => "24h" };
        var outputDirOpt = new Option<string>("--output-dir") { Description = "Output directory for split files", DefaultValueFactory = _ => "splits" };
        var prefixOpt = new Option<string>("--prefix") { Description = "Filename prefix for split files", DefaultValueFactory = _ => "segment" };

        // Shared compute flags
        var presetOpt = new Option<string>("--preset") { Description = "Stop detection preset: hiking, trail, cycling", DefaultValueFactory = _ => "hiking" };
        var stopSpeedOpt = new Option<double>("--stop-speed") { Description = "Override max speed for stops (m/s)", DefaultValueFactory = _ => 0 };
        var stopDurationOpt = new Option<double>("--stop-duration") { Description = "Override min duration for stops (seconds)", DefaultValueFactory = _ => 0 };
        var elevThresholdOpt = new Option<double>("--elevation-threshold") { Description = "Min elevation change (meters)", DefaultValueFactory = _ => 2.0 };
        var smoothingOpt = new Option<string>("--smoothing") { Description = "Elevation smoothing: none, light, medium, heavy", DefaultValueFactory = _ => "medium" };
        var demDirOpt = new Option<string>("--dem-dir") { Description = "SRTM .hgt directory", DefaultValueFactory = _ => "" };
        var demCacheOpt = new Option<string>("--dem-cache") { Description = "DEM cache directory", DefaultValueFactory = _ => "" };
        var demAutoOpt = new Option<bool>("--dem-auto-download") { Description = "Auto-download missing tiles", DefaultValueFactory = _ => true };
        var demMaxMemOpt = new Option<int>("--dem-max-memory") { Description = "Max memory for DEM (MB, 0=unlimited)", DefaultValueFactory = _ => 0 };
        var demSkipValOpt = new Option<bool>("--dem-skip-validation") { Description = "Skip tile validation", DefaultValueFactory = _ => false };
        var elevAlgoOpt = new Option<string>("--elevation-algo") { Description = "Algorithm: threshold, douglas-peucker, segments", DefaultValueFactory = _ => "threshold" };
        var trackSmoothOpt = new Option<string>("--track-smoothing") { Description = "GPS lat/lon smoothing: none, light, medium, heavy", DefaultValueFactory = _ => "none" };
        var dpEpsOpt = new Option<double>("--dp-epsilon") { Description = "Douglas-Peucker epsilon (meters)", DefaultValueFactory = _ => 3.0 };
        var segMinLenOpt = new Option<double>("--seg-min-length") { Description = "Segments min length (meters)", DefaultValueFactory = _ => 200.0 };
        var segMaxDevOpt = new Option<double>("--seg-max-deviation") { Description = "Segments max RMS residual (meters)", DefaultValueFactory = _ => 2.0 };
        var maxHrOpt = new Option<int>("--max-hr") { Description = "Max HR for zone calculation", DefaultValueFactory = _ => 0 };
        var maxSpeedOpt = new Option<double>("--max-speed") { Description = "GPS outlier removal threshold (m/s)", DefaultValueFactory = _ => 0 };

        var cmd = new Command("split", "Split a GPX file by time interval")
        {
            fileArg, intervalOpt, outputDirOpt, prefixOpt,
            presetOpt, stopSpeedOpt, stopDurationOpt, elevThresholdOpt,
            smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
            demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt,
            segMaxDevOpt, maxHrOpt, maxSpeedOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var file = parseResult.GetRequiredValue(fileArg);
            var interval = parseResult.GetValue(intervalOpt) ?? "24h";
            var outputDir = parseResult.GetValue(outputDirOpt) ?? "splits";
            var prefix = parseResult.GetValue(prefixOpt) ?? "segment";
            var format = parseResult.GetValue(formatOption) ?? "text";

            var splitInterval = ParseDuration(interval);
            if (splitInterval <= TimeSpan.Zero)
            {
                Console.Error.WriteLine($"Error: invalid interval '{interval}'");
                return;
            }

            var formatter = FormatterFactory.Create(format, GpxAnalyzer.Cli.Output.JsonContext.Default.Options);
            var cfg = SharedFlags.BuildConfigFromParseResult(parseResult, presetOpt, stopSpeedOpt, stopDurationOpt,
                elevThresholdOpt, smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
                demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt, segMaxDevOpt,
                maxHrOpt, maxSpeedOpt);
```

Everything from `try` / `var doc = GpxParser.ParseFile(file);` onward is unchanged. The bare `return;` inside the handler is why the `Action<ParseResult>` overload of `SetAction` is the one selected — do not add a return value.

- [ ] **Step 2: Rebuild and confirm only MergeCommand is left.**

```bash
dotnet build cli/src/GpxAnalyzer.Cli/ -v q -nologo
```

Expected: `24 Error(s)`, all in `MergeCommand.cs`. Still no commit.

---

### Task 7: Migrate MergeCommand.cs, go green, verify no behaviour changed, commit

**Files:**
- Modify: `cli/src/GpxAnalyzer.Cli/Commands/MergeCommand.cs` lines 1–58 and 107–110
- Test: `cli/tests/GpxAnalyzer.Cli.Tests/Characterization/CliGoldenTests.cs` (must pass **unchanged**), plus the full 235-test suite

**Interfaces:**
- `public static Command MergeCommand.Create(Option<string> formatOption)` — unchanged.

**Steps:**

- [ ] **Step 1: Replace lines 1–58.** After:

```csharp
using System.CommandLine;
using GpxAnalyzer.Cli.Core.Gpx;
using GpxAnalyzer.Cli.Core.Input;
using GpxAnalyzer.Cli.Core.Merge;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Commands;

public static class MergeCommand
{
    public static Command Create(Option<string> formatOption)
    {
        var filesArg = new Argument<string[]>("files")
        {
            Description = "GPX files, directories, or glob patterns",
            Arity = ArgumentArity.OneOrMore
        };
        var outputOpt = new Option<string>("--output", "-o") { Description = "Output file path", DefaultValueFactory = _ => "merged.gpx" };
        var sortOpt = new Option<bool>("--sort") { Description = "Sort track points by time", DefaultValueFactory = _ => true };
        var analyzeOpt = new Option<bool>("--analyze") { Description = "Print statistics for merged result", DefaultValueFactory = _ => false };

        // Shared compute flags (for --analyze)
        var presetOpt = new Option<string>("--preset") { Description = "Stop detection preset: hiking, trail, cycling", DefaultValueFactory = _ => "hiking" };
        var stopSpeedOpt = new Option<double>("--stop-speed") { Description = "Override max speed for stops (m/s)", DefaultValueFactory = _ => 0 };
        var stopDurationOpt = new Option<double>("--stop-duration") { Description = "Override min duration for stops (seconds)", DefaultValueFactory = _ => 0 };
        var elevThresholdOpt = new Option<double>("--elevation-threshold") { Description = "Min elevation change (meters)", DefaultValueFactory = _ => 2.0 };
        var smoothingOpt = new Option<string>("--smoothing") { Description = "Elevation smoothing: none, light, medium, heavy", DefaultValueFactory = _ => "medium" };
        var demDirOpt = new Option<string>("--dem-dir") { Description = "SRTM .hgt directory", DefaultValueFactory = _ => "" };
        var demCacheOpt = new Option<string>("--dem-cache") { Description = "DEM cache directory", DefaultValueFactory = _ => "" };
        var demAutoOpt = new Option<bool>("--dem-auto-download") { Description = "Auto-download missing tiles", DefaultValueFactory = _ => true };
        var demMaxMemOpt = new Option<int>("--dem-max-memory") { Description = "Max memory for DEM (MB, 0=unlimited)", DefaultValueFactory = _ => 0 };
        var demSkipValOpt = new Option<bool>("--dem-skip-validation") { Description = "Skip tile validation", DefaultValueFactory = _ => false };
        var elevAlgoOpt = new Option<string>("--elevation-algo") { Description = "Algorithm: threshold, douglas-peucker, segments", DefaultValueFactory = _ => "threshold" };
        var trackSmoothOpt = new Option<string>("--track-smoothing") { Description = "GPS lat/lon smoothing: none, light, medium, heavy", DefaultValueFactory = _ => "none" };
        var dpEpsOpt = new Option<double>("--dp-epsilon") { Description = "Douglas-Peucker epsilon (meters)", DefaultValueFactory = _ => 3.0 };
        var segMinLenOpt = new Option<double>("--seg-min-length") { Description = "Segments min length (meters)", DefaultValueFactory = _ => 200.0 };
        var segMaxDevOpt = new Option<double>("--seg-max-deviation") { Description = "Segments max RMS residual (meters)", DefaultValueFactory = _ => 2.0 };
        var maxHrOpt = new Option<int>("--max-hr") { Description = "Max HR for zone calculation", DefaultValueFactory = _ => 0 };
        var maxSpeedOpt = new Option<double>("--max-speed") { Description = "GPS outlier removal threshold (m/s)", DefaultValueFactory = _ => 0 };

        var cmd = new Command("merge", "Merge multiple GPX files into one")
        {
            filesArg, outputOpt, sortOpt, analyzeOpt,
            presetOpt, stopSpeedOpt, stopDurationOpt, elevThresholdOpt,
            smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
            demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt,
            segMaxDevOpt, maxHrOpt, maxSpeedOpt
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var files = parseResult.GetRequiredValue(filesArg);
            var output = parseResult.GetValue(outputOpt) ?? "merged.gpx";
            var sort = parseResult.GetValue(sortOpt);
            var analyze = parseResult.GetValue(analyzeOpt);
            var format = parseResult.GetValue(formatOption) ?? "text";
```

- [ ] **Step 2: Fix the second `SharedFlags` call site (old lines 107–110), inside the `if (analyze)` block.** Before:

```csharp
                var cfg = SharedFlags.BuildConfigFromContext(ctx, presetOpt, stopSpeedOpt, stopDurationOpt,
                    elevThresholdOpt, smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
                    demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt, segMaxDevOpt,
                    maxHrOpt, maxSpeedOpt);
```

After:

```csharp
                var cfg = SharedFlags.BuildConfigFromParseResult(parseResult, presetOpt, stopSpeedOpt, stopDurationOpt,
                    elevThresholdOpt, smoothingOpt, demDirOpt, demCacheOpt, demAutoOpt, demMaxMemOpt,
                    demSkipValOpt, elevAlgoOpt, trackSmoothOpt, dpEpsOpt, segMinLenOpt, segMaxDevOpt,
                    maxHrOpt, maxSpeedOpt);
```

- [ ] **Step 3: Build green, with zero new warnings.**

```bash
dotnet build cli/src/GpxAnalyzer.Cli/ -v q -nologo --no-incremental
```

Expected: `Build succeeded.` with `0 Error(s)`. Warnings must be **only** the pre-existing `CS9057` analyzer-version warnings (one per project, caused by the OllamaSharp source generator). If you see `CS8604` ("possible null reference argument"), a `GetValueForArgument` was replaced with `GetValue` instead of `GetRequiredValue` — fix that before continuing.

- [ ] **Step 4: Confirm no beta4 API remains anywhere.**

```bash
grep -rn "InvocationContext\|SetHandler\|GetValueForOption\|GetValueForArgument\|AddGlobalOption\|AddAlias\|AddCommand" cli/src/GpxAnalyzer.Cli/
```

Expected: no output.

- [ ] **Step 5: Run the characterization suite — the moment of truth.**

```bash
dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "FullyQualifiedName~CliGoldenTests"
```

Expected: `Passed! - Failed: 0, Passed: 13, Skipped: 0, Total: 13`.

**Do not run `UPDATE_GOLDEN=1` here.** These 13 goldens are the contract. A failure means the migration changed behaviour; read the xUnit `Assert.Equal` diff, find which option lost its default or alias, and fix the source — never the golden.

(`CliHelpGoldenTests` will still fail at this point — that is expected and is Task 9's job.)

- [ ] **Step 6: Run the whole CLI suite.**

```bash
dotnet test cli/tests/GpxAnalyzer.Cli.Tests/
```

Expected: `Failed: 5, Passed: 230, Total: 235` — the 5 failures are exactly the `CliHelpGoldenTests` `--help` goldens. If anything in `CliGoldenTests` or in the 217 pre-existing tests fails, stop and fix it.

- [ ] **Step 7: Build the whole solution the way CI does.**

```bash
dotnet restore cli/GpxAnalyzer.Cli.sln
dotnet build cli/GpxAnalyzer.Cli.sln --configuration Release --no-restore
```

Expected: `Build succeeded.`, `0 Error(s)`. This mirrors the `.NET CLI (build + test)` job in `.github/workflows/ci.yml`.

- [ ] **Step 8: Commit the migration.**

```bash
git add cli/src/GpxAnalyzer.Cli/GpxAnalyzer.Cli.csproj cli/src/GpxAnalyzer.Cli/Program.cs cli/src/GpxAnalyzer.Cli/Commands
git commit -m "feat(cli): migrate command layer to System.CommandLine 2.0.11

Replaces the beta4 API across Program.cs and the four command files:

- new Option<T>(\"--x\", () => def, \"desc\")
    -> new Option<T>(\"--x\") { Description = ..., DefaultValueFactory = _ => def }  (72)
- new Argument<T>(\"name\", \"desc\") -> new Argument<T>(\"name\") { Description = ... }  (4)
- SetHandler((InvocationContext ctx) => ...) -> SetAction((ParseResult pr) => ...)  (4)
- ctx.ParseResult.GetValueForOption(o) -> pr.GetValue(o)                            (40)
- ctx.ParseResult.GetValueForArgument(a) -> pr.GetRequiredValue(a)                  (4)
- AddAlias(\"-x\") -> alias in the Option constructor                                (4)
- AddGlobalOption(o) -> o.Recursive = true + root.Options.Add(o)
- AddCommand(c) -> root.Subcommands.Add(c)
- root.InvokeAsync(args) -> root.Parse(args).InvokeAsync()

SharedFlags.BuildConfigFromContext is renamed BuildConfigFromParseResult since
InvocationContext no longer exists.

GetRequiredValue (not GetValue) is used for arguments: GetValue<T> returns T? in
2.x and would introduce CS8604 at the four call sites.

Behaviour is unchanged: the 13 characterization goldens added in the previous
commit pass byte-for-byte. --help formatting changes are handled separately."
```

---

### Task 8: Verify Native AOT compatibility

The CLI publishes with `PublishAot=true`; a package that is not trim-safe would surface as new `IL2xxx`/`IL3xxx` warnings from ILCompiler.

**Files:**
- Modify: none (verification only)
- Test: the ILCompiler warning set

**Interfaces:** none.

**Baseline (measured on beta4 before the migration):** exactly **two** warnings, both pre-existing and both from `System.Text.Json`, not from `System.CommandLine`:

```
cli/src/GpxAnalyzer.Cli.Core/Output/JsonFormatter.cs(57): Trim analysis warning IL2026: … JsonSerializer.Serialize<JsonSummary>(…) which has 'RequiresUnreferencedCodeAttribute' …
cli/src/GpxAnalyzer.Cli.Core/Output/JsonFormatter.cs(57): AOT analysis warning IL3050: … JsonSerializer.Serialize<JsonSummary>(…) which has 'RequiresDynamicCodeAttribute' …
```

**Steps:**

- [ ] **Step 1: Publish with AOT and capture the warnings.**

```bash
dotnet publish cli/src/GpxAnalyzer.Cli/GpxAnalyzer.Cli.csproj -c Release 2>&1 | grep -E "IL[0-9]+|error|Build succeeded"
```

Expected: exactly the two `JsonFormatter.cs(57)` warnings above — `IL2026` and `IL3050` — and **no** warning whose message mentions `System.CommandLine`. Any new `IL2xxx`/`IL3xxx` naming `System.CommandLine` is a blocker: report it rather than suppressing it.

- [ ] **Step 2: Handle the native link step.** On a machine without the MSVC linker on `PATH`, the publish reaches `Generating native code`, prints the two warnings, then fails at the link step with:

```
error MSB3073: The command ""'vswhere.exe' is not recognized …;… link.exe" @"obj\Release\net9.0\win-x64\native\link.rsp"" exited with code 3.
```

This is a pre-existing local toolchain gap, **not** a migration regression — the ILCompiler analysis (the part that matters here) already ran. To get a full link, either run the command from a "Developer Command Prompt for VS 2022" / after `Import-Module …\Microsoft.VisualStudio.DevShell.dll; Enter-VsDevShell`, or accept the warnings-only check and let CI's Linux runner do the rest. Record which of the two you did.

- [ ] **Step 3: Smoke-test the published binary if the link succeeded.**

```bash
cli/src/GpxAnalyzer.Cli/bin/Release/net9.0/win-x64/publish/gpx-analyzer.exe --format json analyze --dem-auto-download false cli/tests/GpxAnalyzer.Cli.Tests/testdata/small.gpx
```

Expected: the same JSON as `cli/tests/GpxAnalyzer.Cli.Tests/testdata/golden/analyze-default.txt`, except the `"filename"` field, which echoes the path you passed. (On Linux substitute `bin/Release/net9.0/linux-x64/publish/gpx-analyzer`.) If the link step was skipped in Step 2, skip this step and say so.

- [ ] **Step 4: No commit** — this task changes no files. If Step 1 surfaced a `System.CommandLine`-attributed AOT warning, stop and escalate instead of continuing to Task 9.

---

### Task 9: Re-baseline the `--help` goldens and verify the docs

`--help` rendering belongs to `System.CommandLine`, so it legitimately changes. Measured diffs between beta4 and 2.0.11:

1. **Root help:** `--version` moves from before to after `-?, -h, --help`.
2. **Sub-command help:** `[default: …]` is no longer printed for `bool` options or for empty-string defaults. Concretely, `--dem-auto-download` loses its visible `[default: True]`, and `--dem-dir` / `--dem-cache` / `--export` lose their `[]`. Non-empty, non-boolean defaults (`[default: hiking]`, `[default: 2]`, `[default: text]`) are still shown.
3. **Recursive option placement:** `-f, --format` is now listed *after* `-?, -h, --help` instead of before it.
4. beta4 emitted two trailing blank lines; 2.0.11 emits one.

Nothing else changes. The four sub-command descriptions, the usage line, the argument section and every option name/description are identical.

**Files:**
- Modify: `cli/tests/GpxAnalyzer.Cli.Tests/testdata/golden/help-*.txt` (5 files, regenerated)
- Verify (expected: no edit needed): `docs/content/cli/analyze.md`, `docs/content/cli/split.md`, `docs/content/cli/merge.md`, `docs/content/cli/benchmark.md`, `docs/content/cli/elevation.md`, `docs/content/cli/recipes.md`, `cli/README.md`
- Test: `CliHelpGoldenTests`

**Interfaces:** none.

**Steps:**

- [ ] **Step 1: Look at the delta before accepting it.**

```bash
dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "FullyQualifiedName~CliHelpGoldenTests"
```

Expected: `Failed: 5`. Read the `Assert.Equal` diffs and confirm every difference is one of the four listed above. If a *description string* or an *option name* differs, that is a migration bug, not a renderer change — go back and fix the source.

- [ ] **Step 2: Re-baseline.**

```powershell
$env:UPDATE_GOLDEN = '1'
dotnet test cli/tests/GpxAnalyzer.Cli.Tests/ --filter "FullyQualifiedName~CliHelpGoldenTests"
Remove-Item Env:\UPDATE_GOLDEN
```

- [ ] **Step 3: Review the committed diff line by line.**

```bash
git diff cli/tests/GpxAnalyzer.Cli.Tests/testdata/golden/
```

Expected: only `help-*.txt` files changed, only in the four ways listed above.

- [ ] **Step 4: Verify the docs still tell the truth.** The `[default: …]` markers that disappeared from `--help` are documented independently in Docusaurus tables, so no rewrite should be needed — confirm it:

```bash
grep -n "dem-auto-download" docs/content/cli/*.md cli/README.md
```

Expected: `docs/content/cli/analyze.md:22`, `merge.md:23`, `split.md:23` and `benchmark.md:45` each carry a table row documenting the default as `true`, and `docs/content/cli/elevation.md` / `recipes.md` show the `--dem-auto-download=false` recipe. Since the equals form is what the docs recommend, confirm 2.x still accepts it (it does — verified during plan authoring, along with `-f`, `-o` and `-v`):

```bash
cli/src/GpxAnalyzer.Cli/bin/Debug/net9.0/win-x64/gpx-analyzer.exe -f json analyze cli/tests/GpxAnalyzer.Cli.Tests/testdata/small.gpx --dem-auto-download=false
```

Expected: valid JSON on stdout, exit code 0. If any documented default in those tables no longer matches the migrated source, fix the *docs* in this task. Note `cli/docs/INSTALL.md` and `cli/README.md` document install and architecture, not flag defaults, so they need no change.

- [ ] **Step 5: Full suite green.**

```bash
dotnet test cli/tests/GpxAnalyzer.Cli.Tests/
```

Expected: `Passed! - Failed: 0, Passed: 235, Skipped: 0, Total: 235`.

- [ ] **Step 6: Commit.**

```bash
git add cli/tests/GpxAnalyzer.Cli.Tests/testdata/golden
git commit -m "test(cli): re-baseline --help goldens for System.CommandLine 2.0.11

The help renderer belongs to System.CommandLine, so its output changed with the
major version. Four differences, all cosmetic:

- --version now sorts after -?, -h, --help on the root command
- [default: ...] is no longer printed for bool options or empty-string defaults
  (--dem-auto-download, --dem-dir, --dem-cache, --export)
- the recursive -f, --format option is listed after -?, -h, --help
- one trailing blank line instead of two

Option names, descriptions, arguments and usage lines are unchanged. The
defaults that vanished from --help remain documented in docs/content/cli/*.md."
```

---

### Task 10: Close out PR #57

**Files:** none in this repo working tree.

**Interfaces:** none.

PR #57 (`chore(deps): Bump Google.GenAI, Microsoft.NET.Test.Sdk and System.CommandLine`, dependabot) touches exactly three files:

| File | Bump |
|------|------|
| `cli/src/GpxAnalyzer.Cli/GpxAnalyzer.Cli.csproj` | System.CommandLine `2.0.0-beta4.22272.1` → `2.0.11` |
| `ai-analyzer/src/GpxAiAnalyzer.Core/GpxAiAnalyzer.Core.csproj` | Google.GenAI `1.13.0` → `1.19.0` |
| `ai-analyzer/tests/GpxAiAnalyzer.Tests/GpxAiAnalyzer.Tests.csproj` | Microsoft.NET.Test.Sdk `18.7.0` → `18.9.0` |

Its first change is now already on the branch with the same target version, so the rebase is a trivial no-op on that file and PR #57 reduces to the two `ai-analyzer` bumps.

Context worth remembering: commit `e3cb8d1` ("pin System.CommandLine back to beta4 (2.0.9 is a breaking API change)") reverted an earlier dependabot bump precisely because this migration had not been done. That is what this plan retires.

**Steps:**

- [ ] **Step 1: Push the branch and open the PR** for this migration; get it merged to `dev` first.
- [ ] **Step 2: Rebase PR #57** by commenting `@dependabot rebase` on <https://github.com/jchable/gpx-utility-analyzer/pull/57>.
- [ ] **Step 3: Confirm the rebased PR is down to the two `ai-analyzer` csproj files** and that CI's `.NET CLI (build + test)`, `.NET AI analyzer (build + test)` and `ASP.NET Core API (build + test)` jobs are green.
- [ ] **Step 4: Merge PR #57**, which lands Google.GenAI `1.19.0` and Microsoft.NET.Test.Sdk `18.9.0`.

---

## Rollback

The migration is one commit plus two test commits, so reverting is cheap.

- **Revert the migration only** (keeps the safety net, which is valuable on its own):

```bash
git log --oneline -4
git revert <sha-of "feat(cli): migrate command layer to System.CommandLine 2.0.11">
```

Then re-baseline the help goldens back to beta4 output (`UPDATE_GOLDEN=1 dotnet test … --filter CliHelpGoldenTests`) and commit that. The 13 behavioural goldens are version-agnostic and will pass again untouched — which is itself the proof that the revert restored the original behaviour.

- **Restore the pin by hand** if you only need the build back on beta4, in `cli/src/GpxAnalyzer.Cli/GpxAnalyzer.Cli.csproj`:

```xml
  <ItemGroup>
    <!-- Pinned to beta4: the CLI command layer uses the beta4 API
         (SetHandler / InvocationContext / GetValueForOption). Upgrading to the
         stable 2.0.x line is a separate, breaking API migration. -->
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
  </ItemGroup>
```

Note this only works together with reverting the six source files: the 2.x source does not compile against beta4.

- **Revert everything including the tests:**

```bash
git revert <sha-help-rebaseline> <sha-migration> <sha-characterization-tests>
```

- **If a rollback happens, also re-pin PR #57** — comment `@dependabot ignore System.CommandLine major version` so the bump does not reappear before the migration is retried.
