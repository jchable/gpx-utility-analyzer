using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Core.Benchmark;

public sealed record BenchmarkCombination
{
    public string Preset { get; init; } = "hiking";
    public string ElevAlgo { get; init; } = Stats.ElevationAlgo.Threshold;
    public string ElevSmoothing { get; init; } = "medium";
    public string TrackSmoothing { get; init; } = "none";
    public bool UseDem { get; init; } = true;
    public double Threshold { get; init; } = 2.0;
    public double DpEpsilon { get; init; } = 3.0;
    public double SegMinLen { get; init; } = 200.0;
    public double SegMaxDev { get; init; } = 2.0;

    public string ParamsLabel()
    {
        return ElevAlgo switch
        {
            Stats.ElevationAlgo.Threshold => $"t={Threshold:F1}",
            Stats.ElevationAlgo.DouglasPeucker => $"e={DpEpsilon:F1}",
            Stats.ElevationAlgo.Segments => $"l={SegMinLen:F0}/d={SegMaxDev:F1}",
            _ => "-"
        };
    }

    public string Label() =>
        $"{Preset}|{ElevAlgo}|{ElevSmoothing}|{TrackSmoothing}|{(UseDem ? "dem" : "nodem")}|{ParamsLabel()}";
}

public static class BenchmarkMatrix
{
    private static readonly string[] AllPresets = new[] { "hiking", "trail", "cycling" };
    private static readonly string[] AllElevAlgos = new[] { Stats.ElevationAlgo.Threshold, Stats.ElevationAlgo.DouglasPeucker, Stats.ElevationAlgo.Segments };
    private static readonly string[] AllElevSmoothings = new[] { "none", "light", "medium", "heavy" };
    private static readonly string[] AllTrackSmoothings = new[] { "none", "light", "medium", "heavy" };
    private static readonly double[] AllThresholds = new[] { 1.0, 2.0, 3.0, 5.0 };
    private static readonly double[] AllEpsilons = new[] { 1.5, 3.0, 5.0 };
    private static readonly double[] AllMinLens = new[] { 100.0, 200.0, 400.0 };
    private static readonly double[] AllMaxDevs = new[] { 1.0, 2.0 };

    public static BenchmarkCombination DefaultBase() => new();

    public static List<BenchmarkCombination> FullMatrix()
    {
        var combos = new List<BenchmarkCombination>();
        foreach (var preset in AllPresets)
        foreach (var algo in AllElevAlgos)
        foreach (var elevSmooth in AllElevSmoothings)
        foreach (var trackSmooth in AllTrackSmoothings)
        foreach (var useDem in new[] { true, false })
        {
            var paramSets = GetParamSets(algo);
            foreach (var (threshold, epsilon, minLen, maxDev) in paramSets)
            {
                combos.Add(new BenchmarkCombination
                {
                    Preset = preset,
                    ElevAlgo = algo,
                    ElevSmoothing = elevSmooth,
                    TrackSmoothing = trackSmooth,
                    UseDem = useDem,
                    Threshold = threshold,
                    DpEpsilon = epsilon,
                    SegMinLen = minLen,
                    SegMaxDev = maxDev,
                });
            }
        }
        return combos;
    }

    public static List<BenchmarkCombination> Reduced()
    {
        var baseCfg = DefaultBase();
        var seen = new HashSet<string>();
        var combos = new List<BenchmarkCombination>();

        void Add(BenchmarkCombination c)
        {
            if (seen.Add(c.Label())) combos.Add(c);
        }

        // Vary preset
        foreach (var p in AllPresets)
            Add(baseCfg with { Preset = p });

        // Vary elevation algo
        foreach (var a in AllElevAlgos)
            Add(baseCfg with { ElevAlgo = a });

        // Vary elevation smoothing
        foreach (var s in AllElevSmoothings)
            Add(baseCfg with { ElevSmoothing = s });

        // Vary track smoothing
        foreach (var s in AllTrackSmoothings)
            Add(baseCfg with { TrackSmoothing = s });

        // Vary DEM
        Add(baseCfg with { UseDem = true });
        Add(baseCfg with { UseDem = false });

        // Vary params
        foreach (var t in AllThresholds)
            Add(baseCfg with { ElevAlgo = Stats.ElevationAlgo.Threshold, Threshold = t });
        foreach (var e in AllEpsilons)
            Add(baseCfg with { ElevAlgo = Stats.ElevationAlgo.DouglasPeucker, DpEpsilon = e });
        foreach (var l in AllMinLens)
        foreach (var d in AllMaxDevs)
            Add(baseCfg with { ElevAlgo = Stats.ElevationAlgo.Segments, SegMinLen = l, SegMaxDev = d });

        return combos;
    }

    private static List<(double Threshold, double Epsilon, double MinLen, double MaxDev)> GetParamSets(string algo)
    {
        return algo switch
        {
            Stats.ElevationAlgo.Threshold => AllThresholds.Select(t => (t, 3.0, 200.0, 2.0)).ToList(),
            Stats.ElevationAlgo.DouglasPeucker => AllEpsilons.Select(e => (2.0, e, 200.0, 2.0)).ToList(),
            Stats.ElevationAlgo.Segments => (from l in AllMinLens from d in AllMaxDevs select (2.0, 3.0, l, d)).ToList(),
            _ => new List<(double, double, double, double)> { (2.0, 3.0, 200.0, 2.0) }
        };
    }
}
