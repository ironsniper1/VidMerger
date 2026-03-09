using System.Text.Json;

namespace VidMerger;

public class YouTubeChannel
{
    public string Name { get; set; } = "";
    public string Url  { get; set; } = "";
}

public class AppSettings
{
    public List<YouTubeChannel> YouTubeChannels  { get; set; } = new();
    public string ActiveChannelUrl               { get; set; } = "";
    public string YouTubeClientId                { get; set; } = "";
    public string YouTubeClientSecret            { get; set; } = "";
    public string YouTubeProjectId               { get; set; } = "";
    public double LbryDefaultBid                 { get; set; } = 0.0001;

    public YouTubeChannel? ActiveChannel =>
        YouTubeChannels.FirstOrDefault(c => c.Url == ActiveChannelUrl);

    private static string SettingsPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
