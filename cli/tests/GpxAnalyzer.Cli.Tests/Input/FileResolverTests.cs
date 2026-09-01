using GpxAnalyzer.Cli.Core.Input;

namespace GpxAnalyzer.Cli.Tests.Input;

public class FileResolverTests
{
    [Fact]
    public void ResolveFiles_BareGlobInCurrentDirectory_FindsTheFiles()
    {
        // A bare glob with no directory component, resolved against the current
        // directory. The tests must NOT change the process working directory:
        // xUnit runs test classes in parallel and the CWD is process-wide, so
        // mutating it breaks every sibling test that resolves testdata/
        // relatively. Unique names keep this test isolated instead.
        string tag = $"fr_{Guid.NewGuid():N}";
        string[] created = [$"{tag}_a.gpx", $"{tag}_b.gpx", $"{tag}_notes.txt"];
        foreach (var f in created) File.WriteAllText(f, "<gpx/>");

        try
        {
            // PowerShell and cmd.exe do not expand globs, so this literal
            // pattern is what actually reaches the CLI on Windows.
            var files = FileResolver.ResolveFiles([$"{tag}_*.gpx"]);

            Assert.Equal(2, files.Count);
            Assert.All(files, f => Assert.EndsWith(".gpx", f, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            foreach (var f in created) File.Delete(f);
        }
    }

    [Fact]
    public void ResolveFiles_GlobWithDirectory_StillWorks()
    {
        var tmp = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(tmp.FullName, "a.gpx"), "<gpx/>");
            var files = FileResolver.ResolveFiles([Path.Combine(tmp.FullName, "*.gpx")]);
            Assert.Single(files);
        }
        finally { tmp.Delete(recursive: true); }
    }
}
