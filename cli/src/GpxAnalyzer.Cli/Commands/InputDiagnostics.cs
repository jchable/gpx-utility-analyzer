namespace GpxAnalyzer.Cli.Commands;

/// <summary>
/// #136. `analyze`, `merge` and `split` all bind file names positionally, and `analyze` and
/// `merge` do it with OneOrMore arity, so System.CommandLine hands an unknown option to the
/// argument as a VALUE instead of rejecting it. Whatever consumed that value then failed deep
/// inside the handler - <c>FileResolver</c> threw straight out and the runtime printed a stack
/// trace. `benchmark`, whose file argument takes exactly one value, is rejected cleanly by the
/// parser instead, and that is the behaviour the other three have to match.
/// </summary>
internal static class InputDiagnostics
{
    /// <summary>
    /// A token bound to a file argument that is really a mistyped option: it starts with '-'
    /// and nothing on disk answers to that name. A file genuinely called "-weird.gpx" still
    /// works, and a bare "-" (the conventional stdin placeholder) is left alone.
    /// </summary>
    internal static bool LooksLikeAnOption(string token) =>
        token.Length > 1 && token[0] == '-'
        && !File.Exists(token) && !Directory.Exists(token);

    /// <summary>
    /// Prints one line naming the first token that cannot be a file and returns true, or
    /// returns false when every token is a plausible path.
    /// </summary>
    internal static bool ReportUnrecognizedOption(IEnumerable<string> fileTokens)
    {
        var bad = fileTokens.FirstOrDefault(LooksLikeAnOption);
        if (bad is null)
            return false;

        Console.Error.WriteLine(
            $"Error: unrecognized option '{bad}'. Run with --help to list the available options.");
        return true;
    }

    /// <summary>
    /// Resolves the file arguments, turning any resolver failure into one stderr line rather
    /// than an unhandled exception. Returns null when the run cannot continue.
    /// </summary>
    internal static List<string>? ResolveOrReport(string[] fileTokens)
    {
        if (ReportUnrecognizedOption(fileTokens))
            return null;

        try
        {
            return Core.Input.FileResolver.ResolveFiles(fileTokens);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return null;
        }
    }
}
