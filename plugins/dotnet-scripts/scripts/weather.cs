#!/usr/bin/env dotnet
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: weather <city>");
    Console.Error.WriteLine("Example: weather London");
    return 1;
}

var city = Uri.EscapeDataString(string.Join(" ", args));
var url = $"https://wttr.in/{city}?format=j1";

using var client = new HttpClient();
client.DefaultRequestHeaders.Add("User-Agent", "weather-cli/1.0");

try
{
    var json = await client.GetStringAsync(url);
    var weather = JsonSerializer.Deserialize(json, AppJsonContext.Default.WeatherResponse);

    if (weather?.CurrentCondition is not [var current, ..])
    {
        Console.Error.WriteLine("Error: no current conditions returned.");
        return 1;
    }

    var area = weather.NearestArea is [var a, ..] ? a : null;
    var locationName = area?.AreaName is [var n, ..] ? n.Value : city;
    var country = area?.Country is [var c, ..] ? c.Value : "";
    var description = current.WeatherDesc is [var d, ..] ? d.Value : "Unknown";

    Console.WriteLine($"Weather for {locationName}, {country}");
    Console.WriteLine($"  Temperature: {current.TempC}°C ({current.TempF}°F)");
    Console.WriteLine($"  Feels like:  {current.FeelsLikeC}°C ({current.FeelsLikeF}°F)");
    Console.WriteLine($"  Conditions:  {description}");
    Console.WriteLine($"  Humidity:    {current.Humidity}%");
    Console.WriteLine($"  Wind:        {current.WindspeedKmph} km/h {current.Winddir16Point}");
    Console.WriteLine($"  Visibility:  {current.Visibility} km");

    return 0;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Error fetching weather data: {ex.Message}");
    return 1;
}
catch (JsonException ex)
{
    Console.Error.WriteLine($"Error parsing weather data: {ex.Message}");
    return 1;
}

// --- Type declarations (after all top-level statements) ---

record WeatherResponse(
    [property: JsonPropertyName("current_condition")] List<CurrentCondition> CurrentCondition,
    [property: JsonPropertyName("nearest_area")] List<NearestArea> NearestArea
);

record CurrentCondition(
    [property: JsonPropertyName("temp_C")] string TempC,
    [property: JsonPropertyName("temp_F")] string TempF,
    [property: JsonPropertyName("FeelsLikeC")] string FeelsLikeC,
    [property: JsonPropertyName("FeelsLikeF")] string FeelsLikeF,
    [property: JsonPropertyName("weatherDesc")] List<WeatherValue> WeatherDesc,
    [property: JsonPropertyName("humidity")] string Humidity,
    [property: JsonPropertyName("windspeedKmph")] string WindspeedKmph,
    [property: JsonPropertyName("winddir16Point")] string Winddir16Point,
    [property: JsonPropertyName("visibility")] string Visibility
);

record NearestArea(
    [property: JsonPropertyName("areaName")] List<WeatherValue> AreaName,
    [property: JsonPropertyName("country")] List<WeatherValue> Country
);

record WeatherValue(
    [property: JsonPropertyName("value")] string Value
);

[JsonSerializable(typeof(WeatherResponse))]
[JsonSerializable(typeof(List<CurrentCondition>))]
[JsonSerializable(typeof(List<NearestArea>))]
[JsonSerializable(typeof(List<WeatherValue>))]
partial class AppJsonContext : JsonSerializerContext;
