namespace VidMerger;

public class VideoItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public string ThumbnailUrl { get; set; } = "";
    public string LocalFilePath { get; set; } = "";
    public string LocalThumbnailPath { get; set; } = "";
    public VideoPlatform Platform { get; set; }

    // LBRY-specific
    public string LbryClaimId { get; set; } = "";
    public string LbryName { get; set; } = "";
    public string LbryChannelId { get; set; } = "";
    public double LbryBid { get; set; } = 0.0001;

    public bool IsDownloaded => !string.IsNullOrEmpty(LocalFilePath) && File.Exists(LocalFilePath);

    public override string ToString() => Title;
}

public enum VideoPlatform
{
    YouTube,
    LBRY,
    Rumble
}
