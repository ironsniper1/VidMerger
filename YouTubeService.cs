using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Google.Apis.Util.Store;
using System.Text.Json.Nodes;

namespace VidMerger;

/// <summary>
/// Handles all YouTube interaction.
/// - Listing and downloading always use yt-dlp (no API key needed).
/// - Uploading uses the YouTube Data API v3 (requires OAuth credentials in Settings).
/// </summary>
public class YouTubeService
{
    private Google.Apis.YouTube.v3.YouTubeService? _service;
    private readonly AppSettings _settings;

    /// <summary>True if the user has authenticated and can upload.</summary>
    public bool CanUpload => _service != null;

    public YouTubeService(AppSettings settings)
    {
        _settings = settings;
    }

    // ================================================================== //
    // Authentication (optional — only needed for uploading to YouTube)
    // ================================================================== //

    public async Task AuthenticateAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.YouTubeClientId) ||
            string.IsNullOrWhiteSpace(_settings.YouTubeClientSecret))
            throw new InvalidOperationException(
                "YouTube Client ID and Client Secret must be set in Settings to enable uploading.");

        var secrets = new ClientSecrets
        {
            ClientId = _settings.YouTubeClientId,
            ClientSecret = _settings.YouTubeClientSecret
        };

        string tokenFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tokens");
        Directory.CreateDirectory(tokenFolder);

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            new[]
            {
                Google.Apis.YouTube.v3.YouTubeService.Scope.Youtube,
                Google.Apis.YouTube.v3.YouTubeService.Scope.YoutubeUpload,
                Google.Apis.YouTube.v3.YouTubeService.Scope.YoutubeForceSsl
            },
            "user",
            CancellationToken.None,
            new FileDataStore(tokenFolder, true)
        );

        _service = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ContentCreatorManager"
        });
    }

    // ================================================================== //
    // List channel videos via yt-dlp — no API key needed
    // ================================================================== //

    public async Task<List<VideoItem>> GetChannelVideosAsync(
        string channelUrl,
        IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(channelUrl))
            throw new ArgumentException("Channel URL is required.");

        progress?.Report("Fetching video list from YouTube (this may take a moment)...");

        var psi = new System.Diagnostics.ProcessStartInfo("yt-dlp")
        {
            Arguments = $"--flat-playlist --dump-single-json \"{channelUrl}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = System.Diagnostics.Process.Start(psi)
            ?? throw new Exception("Failed to start yt-dlp. Make sure it is installed and on your PATH.");

        string output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
        {
            string err = await proc.StandardError.ReadToEndAsync();
            throw new Exception($"yt-dlp error: {err}");
        }

        var videos = new List<VideoItem>();
        var json = JsonNode.Parse(output);
        var entries = json?["entries"]?.AsArray();
        if (entries == null) return videos;

        foreach (var entry in entries)
        {
            if (entry == null) continue;
            string id = entry["id"]?.GetValue<string>() ?? "";
            string title = entry["title"]?.GetValue<string>() ?? id;
            string desc = entry["description"]?.GetValue<string>() ?? "";
            string thumbUrl = entry["thumbnail"]?.GetValue<string>() ?? "";

            var tags = new List<string>();
            var tagArr = entry["tags"]?.AsArray();
            if (tagArr != null)
                foreach (var t in tagArr)
                    tags.Add(t?.GetValue<string>() ?? "");

            var item = new VideoItem
            {
                Id = id,
                Title = title,
                Description = desc,
                Tags = tags,
                ThumbnailUrl = thumbUrl,
                Platform = VideoPlatform.YouTube
            };
            videos.Add(item);
            progress?.Report($"Found: {title}");
        }

        return videos;
    }

    // ================================================================== //
    // Download via yt-dlp — no API key needed
    // ================================================================== //

    public async Task<string?> DownloadVideoAsync(
        VideoItem video,
        string outputFolder,
        IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(outputFolder);
        string url = $"https://www.youtube.com/watch?v={video.Id}";
        string outTemplate = Path.Combine(outputFolder, $"yt_{video.Id}.%(ext)s");

        progress?.Report($"Downloading: {video.Title}");

        var psi = new System.Diagnostics.ProcessStartInfo("yt-dlp")
        {
            Arguments = $"--format \"bestvideo+bestaudio/best\" --merge-output-format mp4 --write-thumbnail --convert-thumbnails jpg -o \"{outTemplate}\" \"{url}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc == null) return null;

        proc.OutputDataReceived += (_, e) => { if (e.Data != null) progress?.Report(e.Data); };
        proc.BeginOutputReadLine();
        await proc.WaitForExitAsync();

        string expectedFile = Path.Combine(outputFolder, $"yt_{video.Id}.mp4");
        if (File.Exists(expectedFile))
        {
            video.LocalFilePath = expectedFile;
            video.LocalThumbnailPath = FindThumbnail(outputFolder, $"yt_{video.Id}");
            return expectedFile;
        }

        var found = Directory.GetFiles(outputFolder, $"yt_{video.Id}.*")
            .FirstOrDefault(f => !f.EndsWith(".jpg") && !f.EndsWith(".png") && !f.EndsWith(".webp"));
        if (found != null)
        {
            video.LocalFilePath = found;
            video.LocalThumbnailPath = FindThumbnail(outputFolder, $"yt_{video.Id}");
        }
        return found;
    }

    // ================================================================== //
    // Upload via YouTube API — requires OAuth credentials
    // ================================================================== //

    public async Task<bool> UploadVideoAsync(
        VideoItem video,
        string videoFilePath,
        string thumbnailPath,
        IProgress<string>? progress = null)
    {
        if (_service == null)
            throw new InvalidOperationException(
                "Not authenticated. Go to File → Settings, enter your API credentials, then click 'Enable YouTube Upload'.");

        var ytVideo = new Video
        {
            Snippet = new VideoSnippet
            {
                Title = video.Title,
                Description = video.Description,
                Tags = video.Tags,
                CategoryId = "22"
            },
            Status = new VideoStatus
            {
                PrivacyStatus = "public",
                SelfDeclaredMadeForKids = false
            }
        };

        using var fileStream = new FileStream(videoFilePath, FileMode.Open);
        var uploadReq = _service.Videos.Insert(ytVideo, "snippet,status", fileStream, "video/*");

        uploadReq.ProgressChanged += (p) =>
            progress?.Report(p.Status == UploadStatus.Uploading
                ? $"Uploading: {p.BytesSent / 1024 / 1024} MB sent"
                : $"Status: {p.Status}");

        uploadReq.ResponseReceived += (v) =>
        {
            video.Id = v.Id;
            progress?.Report($"Upload complete: {v.Id}");
        };

        var result = await uploadReq.UploadAsync();
        if (result.Status != UploadStatus.Completed) return false;

        // Thumbnail (best-effort)
        if (File.Exists(thumbnailPath) && !string.IsNullOrEmpty(video.Id))
        {
            try
            {
                using var thumbStream = new FileStream(thumbnailPath, FileMode.Open);
                var thumbReq = _service.Thumbnails.Set(video.Id, thumbStream, "image/jpeg");
                await thumbReq.UploadAsync();
            }
            catch { }
        }

        return true;
    }

    private static string FindThumbnail(string folder, string baseName)
    {
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".webp" })
        {
            string path = Path.Combine(folder, baseName + ext);
            if (File.Exists(path)) return path;
        }
        return "";
    }
}
