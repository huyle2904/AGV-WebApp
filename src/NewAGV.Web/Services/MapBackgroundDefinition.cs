namespace NewAGV.Web.Services;

public sealed record MapBackgroundDefinition(
    string Name,
    MapBackgroundBounds Bounds,
    IReadOnlyList<MapBackgroundPoint> Points,
    IReadOnlyList<MapBackgroundLine> Lines,
    IReadOnlyList<MapBackgroundMarker> Markers);

public sealed record MapBackgroundBounds(double MinX, double MinY, double MaxX, double MaxY);

public sealed record MapBackgroundPoint(double X, double Y);

public sealed record MapBackgroundLine(double StartX, double StartY, double EndX, double EndY);

public sealed record MapBackgroundMarker(string Name, double X, double Y);
