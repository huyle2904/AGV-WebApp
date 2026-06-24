using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NewAGV.Web.Services;

public sealed class MapBackgroundService(
    IWebHostEnvironment environment,
    IOptions<MapBackgroundOptions> options,
    ILogger<MapBackgroundService> logger)
{
    private readonly object _sync = new();
    private MapBackgroundDefinition? _cachedMap;
    private string? _cachedPath;
    private DateTimeOffset _cachedLastWriteUtc;

    public async Task<MapBackgroundDefinition?> GetAsync(CancellationToken cancellationToken = default)
    {
        var configuredPath = options.Value.SourcePath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        var fullPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));

        if (!File.Exists(fullPath))
        {
            logger.LogWarning("Map background file not found: {Path}", fullPath);
            return null;
        }

        var lastWriteUtc = File.GetLastWriteTimeUtc(fullPath);

        lock (_sync)
        {
            if (_cachedMap is not null &&
                string.Equals(_cachedPath, fullPath, StringComparison.OrdinalIgnoreCase) &&
                _cachedLastWriteUtc == lastWriteUtc)
            {
                return _cachedMap;
            }
        }

        await using var stream = File.OpenRead(fullPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var map = Parse(document.RootElement);

        lock (_sync)
        {
            _cachedMap = map;
            _cachedPath = fullPath;
            _cachedLastWriteUtc = lastWriteUtc;
        }

        return map;
    }

    private static MapBackgroundDefinition Parse(JsonElement root)
    {
        var header = root.TryGetProperty("header", out var headerElement)
            ? headerElement
            : default;

        var mapName = GetString(header, "mapName") ?? "AGV Map";
        var minPos = header.TryGetProperty("minPos", out var minPosElement) ? minPosElement : default;
        var maxPos = header.TryGetProperty("maxPos", out var maxPosElement) ? maxPosElement : default;

        var bounds = new MapBackgroundBounds(
            GetDouble(minPos, "x"),
            GetDouble(minPos, "y"),
            GetDouble(maxPos, "x"),
            GetDouble(maxPos, "y"));

        var points = root.TryGetProperty("normalPosList", out var pointArray) && pointArray.ValueKind == JsonValueKind.Array
            ? pointArray.EnumerateArray().Select(ReadPoint).ToArray()
            : [];

        var lines = root.TryGetProperty("advancedLineList", out var lineArray) && lineArray.ValueKind == JsonValueKind.Array
            ? lineArray.EnumerateArray()
                .Select(ReadLine)
                .Where(line => line is not null)
                .Cast<MapBackgroundLine>()
                .ToArray()
            : [];

        var markers = root.TryGetProperty("advancedPointList", out var markerArray) && markerArray.ValueKind == JsonValueKind.Array
            ? markerArray.EnumerateArray().Select(ReadMarker).ToArray()
            : [];

        return new MapBackgroundDefinition(mapName, bounds, points, lines, markers);
    }

    private static MapBackgroundPoint ReadPoint(JsonElement point)
        => new(GetDouble(point, "x"), GetDouble(point, "y"));

    private static MapBackgroundLine? ReadLine(JsonElement entry)
    {
        if (!entry.TryGetProperty("line", out var line) || line.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var start = line.TryGetProperty("startPos", out var startElement) ? startElement : default;
        var end = line.TryGetProperty("endPos", out var endElement) ? endElement : default;

        return new MapBackgroundLine(
            GetDouble(start, "x"),
            GetDouble(start, "y"),
            GetDouble(end, "x"),
            GetDouble(end, "y"));
    }

    private static MapBackgroundMarker ReadMarker(JsonElement marker)
    {
        var pos = marker.TryGetProperty("pos", out var posElement) ? posElement : default;
        return new MapBackgroundMarker(
            GetString(marker, "instanceName") ?? "marker",
            GetDouble(pos, "x"),
            GetDouble(pos, "y"));
    }

    private static string? GetString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static double GetDouble(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == JsonValueKind.Number &&
           property.TryGetDouble(out var value)
            ? value
            : 0;
}
