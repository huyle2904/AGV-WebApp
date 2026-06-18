using System.Text.Json.Nodes;

namespace NewAGV.Worker.Services;

internal static class JsonObjectExtensions
{
    public static string? StringValue(this JsonObject? json, string propertyName)
        => json is not null &&
           json.TryGetPropertyValue(propertyName, out var value) &&
           value is not null
            ? value.GetValue<string?>()
            : null;

    public static double? DoubleValue(this JsonObject? json, string propertyName)
        => json is not null &&
           json.TryGetPropertyValue(propertyName, out var value) &&
           value is not null &&
           double.TryParse(value.ToString(), out var number)
            ? number
            : null;

    public static int? IntValue(this JsonObject? json, string propertyName)
        => json is not null &&
           json.TryGetPropertyValue(propertyName, out var value) &&
           value is not null &&
           int.TryParse(value.ToString(), out var number)
            ? number
            : null;

    public static bool? BoolValue(this JsonObject? json, string propertyName)
        => json is not null &&
           json.TryGetPropertyValue(propertyName, out var value) &&
           value is not null &&
           bool.TryParse(value.ToString(), out var boolean)
            ? boolean
            : null;

    public static JsonArray? ArrayValue(this JsonObject? json, string propertyName)
        => json is not null &&
           json.TryGetPropertyValue(propertyName, out var value)
            ? value as JsonArray
            : null;

    public static JsonObject? ObjectValue(this JsonObject? json, string propertyName)
        => json is not null &&
           json.TryGetPropertyValue(propertyName, out var value)
            ? value as JsonObject
            : null;

    public static IReadOnlyList<string> StringArrayValue(this JsonObject? json, string propertyName)
    {
        var array = json.ArrayValue(propertyName);
        if (array is null)
        {
            return [];
        }

        return array
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();
    }
}
