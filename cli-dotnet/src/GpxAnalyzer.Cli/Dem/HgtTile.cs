using System.Buffers.Binary;

namespace GpxAnalyzer.Cli.Dem;

/// <summary>
/// Represents a loaded SRTM HGT tile.
/// </summary>
public sealed class HgtTile
{
    public const int Srtm1Size = 3601;
    public const int Srtm3Size = 1201;
    public const short VoidValue = -32768;

    public int LatOrigin { get; }
    public int LonOrigin { get; }
    public int GridSize { get; }
    public short[] Data { get; }

    private HgtTile(int latOrigin, int lonOrigin, int gridSize, short[] data)
    {
        LatOrigin = latOrigin;
        LonOrigin = lonOrigin;
        GridSize = gridSize;
        Data = data;
    }

    /// <summary>
    /// Returns the HGT filename stem for a given lat/lon, e.g. "N48W003".
    /// </summary>
    public static string TileKey(double lat, double lon)
    {
        int latInt = (int)Math.Floor(lat);
        int lonInt = (int)Math.Floor(lon);
        char ns = latInt >= 0 ? 'N' : 'S';
        char ew = lonInt >= 0 ? 'E' : 'W';
        if (latInt < 0) latInt = -latInt;
        if (lonInt < 0) lonInt = -lonInt;
        return string.Create(7, (ns, latInt, ew, lonInt), static (span, state) =>
        {
            span[0] = state.ns;
            state.latInt.TryFormat(span[1..], out _, "D2");
            span[3] = state.ew;
            state.lonInt.TryFormat(span[4..], out _, "D3");
        });
    }

    /// <summary>
    /// Loads an HGT tile from a file.
    /// </summary>
    public static HgtTile Load(string path)
    {
        var fileInfo = new FileInfo(path);
        long fileSize = fileInfo.Length;
        long totalSamples = fileSize / 2;
        int gridSize = (int)Math.Sqrt(totalSamples);

        if (gridSize != Srtm1Size && gridSize != Srtm3Size)
            throw new InvalidOperationException(
                $"Invalid HGT file size {fileSize} bytes (expected SRTM1 or SRTM3)");

        byte[] rawBytes = File.ReadAllBytes(path);
        var data = new short[gridSize * gridSize];
        ReadOnlySpan<byte> span = rawBytes;
        for (int i = 0; i < data.Length; i++)
            data[i] = BinaryPrimitives.ReadInt16BigEndian(span.Slice(i * 2, 2));

        var (latOrigin, lonOrigin) = ParseFilename(Path.GetFileName(path));

        return new HgtTile(latOrigin, lonOrigin, gridSize, data);
    }

    /// <summary>
    /// Returns bilinearly interpolated elevation at lat/lon.
    /// Returns (elevation, true) on success, (0, false) if void or out of bounds.
    /// </summary>
    public (double Elevation, bool Ok) GetElevation(double lat, double lon)
    {
        double row = (GridSize - 1) * (LatOrigin + 1.0 - lat);
        double col = (GridSize - 1) * (lon - LonOrigin);

        if (row < 0 || row > GridSize - 1 || col < 0 || col > GridSize - 1)
            return (0, false);

        int r0 = (int)Math.Floor(row);
        int c0 = (int)Math.Floor(col);
        int r1 = r0 + 1;
        int c1 = c0 + 1;

        if (r1 >= GridSize) r1 = GridSize - 1;
        if (c1 >= GridSize) c1 = GridSize - 1;

        short q11 = Get(r0, c0);
        short q12 = Get(r0, c1);
        short q21 = Get(r1, c0);
        short q22 = Get(r1, c1);

        if (q11 == VoidValue || q12 == VoidValue || q21 == VoidValue || q22 == VoidValue)
            return (0, false);

        double dr = row - r0;
        double dc = col - c0;

        double top = q11 * (1 - dc) + q12 * dc;
        double bot = q21 * (1 - dc) + q22 * dc;
        return (top * (1 - dr) + bot * dr, true);
    }

    public short Get(int row, int col) => Data[row * GridSize + col];

    /// <summary>
    /// Parses lat/lon origin from HGT filename like "N48W003.hgt".
    /// </summary>
    public static (int Lat, int Lon) ParseFilename(string name)
    {
        ReadOnlySpan<char> stem = Path.GetFileNameWithoutExtension(name.AsSpan());
        if (stem.Length != 7)
            throw new FormatException($"Invalid HGT filename: {name}");

        char ns = char.ToUpperInvariant(stem[0]);
        int lat = int.Parse(stem.Slice(1, 2));
        char ew = char.ToUpperInvariant(stem[3]);
        int lon = int.Parse(stem.Slice(4, 3));

        if (ns == 'S') lat = -lat;
        if (ew == 'W') lon = -lon;

        return (lat, lon);
    }
}
