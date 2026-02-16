namespace GpxAnalyzer.Cli.Core.Input;

public static class FileResolver
{
    public static List<string> ResolveFiles(string[] args)
    {
        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var arg in args)
        {
            var resolved = ResolveArg(arg);
            foreach (var f in resolved)
            {
                string abs = Path.GetFullPath(f);
                if (seen.Add(abs))
                    files.Add(f);
            }
        }

        if (files.Count == 0)
            throw new InvalidOperationException(
                $"No GPX files found in arguments: {string.Join(", ", args)}");

        return files;
    }

    private static List<string> ResolveArg(string arg)
    {
        // Glob pattern
        if (arg.Contains('*') || arg.Contains('?') || arg.Contains('['))
        {
            string dir = Path.GetDirectoryName(arg) ?? ".";
            string pattern = Path.GetFileName(arg);
            if (Directory.Exists(dir))
            {
                var matches = Directory.GetFiles(dir, pattern);
                return FilterGpx(matches);
            }
            return [];
        }

        if (Directory.Exists(arg))
            return FindGpxInDir(arg);

        if (!IsGpx(arg))
            throw new InvalidOperationException($"{arg} is not a .gpx file");

        return [arg];
    }

    private static List<string> FindGpxInDir(string dir)
    {
        return Directory.EnumerateFiles(dir, "*.gpx", SearchOption.AllDirectories)
            .ToList();
    }

    private static bool IsGpx(string path)
        => Path.GetExtension(path).Equals(".gpx", StringComparison.OrdinalIgnoreCase);

    private static List<string> FilterGpx(string[] paths)
        => paths.Where(IsGpx).ToList();
}
