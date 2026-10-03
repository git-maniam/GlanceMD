using System.Text.Json;
using GlanceMD.Core;
using Windows.Storage;

namespace GlanceMD_App.Services;

internal sealed record AppSettings(
    string Theme,
    double Zoom,
    bool RecentHistoryEnabled,
    IReadOnlyList<RecentDocument> RecentDocuments)
{
    public static AppSettings Default { get; } = new("Default", 1.0, true, []);
}

internal sealed class AppSettingsService
{
    private const string SettingsKey = "settings-v1";
    private readonly ApplicationDataContainer _container = ApplicationData.Current.LocalSettings;

    public AppSettings Load()
    {
        if (_container.Values[SettingsKey] is not string json)
        {
            return AppSettings.Default;
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.Default;
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings) =>
        _container.Values[SettingsKey] = JsonSerializer.Serialize(settings);
}
