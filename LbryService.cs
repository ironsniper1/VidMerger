using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VidMerger;

/// <summary>
/// Wraps the local LBRY daemon HTTP API at http://localhost:5279
/// </summary>
public class LbryService
{
    private const string ApiUrl = "http://localhost:5279";
    private readonly HttpClient _http = new();
    private readonly AppSettings _settings;

    public string ChannelId { get; private set; } = "";
    public string ChannelName { get; private set; } = "";

    public LbryService(AppSettings settings)
    {
        _settings = settings;
    }

    // ------------------------------------------------------------------ //
    // Connection check
    // ------------------------------------------------------------------ //

    public async Task<bool> IsRunningAsync()
    {
        try
        {
            var result = await CallAsync("status", new { });
            return result != null;
        }
        catch
        {
            return false;
        }
    }

    // ------------------------------------------------------------------ //
    // Channel selection
    // ------------------------------------------------------------------ //

    public async Task<List<(string Id, string Name)>> GetChannelsAsync()
    {
        var result = await CallAsync("claim_list", new
        {
            claim_type = new[] { "channel" },
            page_size = 50,
            resolve = true
        });

        var channels = new List<(string, string)>();
        var items = result?["result"]?["items"]?.AsArray();
        if (items == null) return channels;

        foreach (var item in items)
        {
            string id = item?["claim_id"]?.GetValue<string>() ?? "";
            string name = item?["name"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrEmpty(id))
                channels.Add((id, name));
        }
        return channels;
    }

    public void SetChannel(string channelId, string channelName)
    {
        ChannelId = channelId;
        ChannelName = channelName;
    }

    // ------------------------------------------------------------------ //
    // Video listing
    // ------------------------------------------------------------------ //

    public async Task<List<VideoItem>> GetChannelVideosAsync(IProgress<string>? progress = null)
    {
        var videos = new List<VideoItem>();
        int page = 1;
        int totalPages = 1;

        do
        {
            var result = await CallAsync("claim_list", new
            {
                claim_type = new[] { "stream" },
                channel_id = new[] { ChannelId },
                page_size = 20,
                page = page,
                resolve = true,
                order_by = "name"
            });

            var items = result?["result"]?["items"]?.AsArray();
            int totalItems = result?["result"]?["total_items"]?.GetValue<int>() ?? 0;
            totalPages = (int)Math.Ceiling(totalItems / 20.0);

            if (items == null) break;

            foreach (var item in items)
            {
                string? streamType = item?["value"]?["stream_type"]?.GetValue<string>();
                if (streamType != "video") continue;

                string id = item?["claim_id"]?.GetValue<string>() ?? "";
                string name = item?["name"]?.GetValue<string>() ?? "";
                string title = item?["value"]?["title"]?.GetValue<string>() ?? name;
                string desc = item?["value"]?["description"]?.GetValue<string>() ?? "";
                string thumbUrl = item?["value"]?["thumbnail"]?["url"]?.GetValue<string>() ?? "";

                var tags = new List<string>();
                var tagArr = item?["value"]?["tags"]?.AsArray();
                if (tagArr != null)
                    foreach (var t in tagArr)
                        tags.Add(t?.GetValue<string>() ?? "");

                var video = new VideoItem
                {
                    Id = id,
                    LbryClaimId = id,
                    LbryName = name,
                    LbryChannelId = ChannelId,
                    LbryBid = _settings.LbryDefaultBid,
                    Title = title,
                    Description = desc,
                    Tags = tags,
                    ThumbnailUrl = thumbUrl,
                    Platform = VideoPlatform.LBRY
                };

                videos.Add(video);
                progress?.Report($"Loaded: {title}");
            }

            page++;

        } while (page <= totalPages);

        return videos;
    }

    // ------------------------------------------------------------------ //
    // Upload
    // ------------------------------------------------------------------ //

    public async Task<bool> UploadVideoAsync(
        VideoItem video,
        string videoFilePath,
        string thumbnailPath,
        double bid,
        IProgress<string>? progress = null)
    {
        progress?.Report($"Uploading to LBRY: {video.Title}");

        // Build a valid URL name from the title
        string urlName = MakeValidName(video.Title);

        // Upload thumbnail to spee.ch first if available
        string thumbUrl = video.ThumbnailUrl;
        if (File.Exists(thumbnailPath))
        {
            string? uploaded = await UploadThumbnailAsync(thumbnailPath, urlName);
            if (uploaded != null)
                thumbUrl = uploaded;
        }

        var parameters = new Dictionary<string, object>
        {
            ["name"] = urlName,
            ["bid"] = bid.ToString("F4"),
            ["file_path"] = videoFilePath.Replace("\\", "/"),
            ["title"] = video.Title,
            ["description"] = video.Description,
            ["channel_id"] = ChannelId,
            ["languages"] = new[] { "en" },
            ["tags"] = video.Tags.ToArray()
        };

        if (!string.IsNullOrEmpty(thumbUrl))
            parameters["thumbnail_url"] = thumbUrl;

        var result = await CallAsync("stream_create", parameters);
        string? claimId = result?["result"]?["outputs"]?[0]?["claim_id"]?.GetValue<string>();

        if (string.IsNullOrEmpty(claimId))
            return false;

        video.LbryClaimId = claimId;
        video.Id = claimId;
        progress?.Report($"Upload submitted. Claim ID: {claimId}");

        // Poll until confirmed
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            if (await IsUploadedAsync(claimId))
            {
                progress?.Report("Upload confirmed on LBRY!");
                return true;
            }
            progress?.Report($"Waiting for confirmation... ({i + 1}/10)");
        }

        return false;
    }

    public async Task<bool> IsUploadedAsync(string claimId)
    {
        var result = await CallAsync("claim_list", new
        {
            claim_id = new[] { claimId },
            resolve = false
        });
        int total = result?["result"]?["total_items"]?.GetValue<int>() ?? 0;
        return total > 0;
    }

    // ------------------------------------------------------------------ //
    // Download
    // ------------------------------------------------------------------ //

    public async Task<string?> DownloadVideoAsync(
        VideoItem video,
        string outputFolder,
        IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(outputFolder);
        progress?.Report($"Requesting download: {video.Title}");

        var result = await CallAsync("get", new
        {
            uri = $"lbry://{video.LbryName}#{video.LbryClaimId}",
            download_directory = outputFolder.Replace("\\", "/"),
            save_file = true
        });

        string? filePath = result?["result"]?["download_path"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(filePath))
        {
            video.LocalFilePath = filePath;
            progress?.Report($"Downloaded to: {filePath}");
            return filePath;
        }
        return null;
    }

    // ------------------------------------------------------------------ //
    // Thumbnail upload to spee.ch
    // ------------------------------------------------------------------ //

    private async Task<string?> UploadThumbnailAsync(string thumbPath, string name)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(thumbPath);
            form.Add(new StreamContent(fileStream), "file", Path.GetFileName(thumbPath));
            form.Add(new StringContent(name), "name");
            form.Add(new StringContent("image/jpeg"), "type");

            var resp = await _http.PostAsync("https://spee.ch/api/claim/publish", form);
            var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
            return json?["data"]?["serveUrl"]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    private async Task<JsonNode?> CallAsync(string method, object parameters)
    {
        var body = new { method, @params = parameters };
        var content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        var resp = await _http.PostAsync(ApiUrl, content);
        string raw = await resp.Content.ReadAsStringAsync();
        return JsonNode.Parse(raw);
    }

    private static string MakeValidName(string title)
    {
        const string valid = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-";
        var sb = new StringBuilder();
        foreach (char c in title.Replace(' ', '-'))
            if (valid.Contains(c))
                sb.Append(c);
        return sb.Length > 0 ? sb.ToString() : "video";
    }
}
