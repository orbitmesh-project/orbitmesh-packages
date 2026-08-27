using System.Reflection;
using System.Text.Json;

namespace OrbitMesh.DayInfo.Utils;

public static class NameDayUtils
{
    private static readonly Dictionary<string, string> NameDays = Load();

    // Reads the data.gouv.fr "saints et fêtes du calendrier" dataset (saints.json, shape
    // {"month": {"day": [names...]}}).
    private static Dictionary<string, string> Load()
    {
        var result = new Dictionary<string, string>();
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().First(n => n.EndsWith("saints.json", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var document = JsonDocument.Parse(stream);
        foreach (var monthProperty in document.RootElement.EnumerateObject())
        {
            foreach (var dayProperty in monthProperty.Value.EnumerateObject())
            {
                var names = dayProperty.Value.EnumerateArray().Select(n => n.GetString());
                result[$"{int.Parse(dayProperty.Name):D2}/{int.Parse(monthProperty.Name):D2}"] = string.Join(", ", names);
            }
        }
        return result;
    }

    public static string GetNameDay() => GetNameDay(DateTime.Now);

    // "/" in ToString("dd/MM") is the culture-dependent date separator, not literal - build the key
    // manually or it silently stops matching on non-"/"-separator cultures.
    public static string GetNameDay(DateTime date) =>
        NameDays.GetValueOrDefault($"{date.Day:D2}/{date.Month:D2}", string.Empty);
}
