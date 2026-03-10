# VidMerger v1.2

> A Windows desktop app for content creators who publish on both YouTube and LBRY/Odysee. Load your video libraries from both platforms, compare them side by side, and sync what's missing — automatically or one at a time. Fully handles YouTube Shorts detection and duplicate LBRY claim names.

![Platform](https://img.shields.io/badge/platform-Windows-blue)
![Framework](https://img.shields.io/badge/.NET-8.0-purple)
![Version](https://img.shields.io/badge/version-1.2.0-green)

---

## Acknowledgements

This project was inspired by [**tiff1002**](https://github.com/tiff1002) and her original [ContentCreatorManager](https://github.com/tiff1002/ContentCreatorManager) — a Python-based tool for managing and syncing content across platforms. VidMerger takes that same core idea and reimplements it as a standalone C# / WinForms desktop application with an expanded feature set. Big thanks to tiff1002 for the inspiration and the original concept.

---

## What It Does

VidMerger solves a simple but tedious problem: keeping your YouTube and LBRY/Odysee channels in sync. It loads your full video list from both platforms, compares them by title, and shows you exactly what's missing from each side. You can then download and re-upload the missing videos individually, or use **Sync All** to process the entire list automatically in the background.

Shorts are detected by fetching your YouTube channel's `/shorts` tab separately, so they are always correctly identified regardless of duration or URL format.

---

## Features

- **Load & Compare** — fetch your full video library from YouTube and LBRY and see exactly what's missing from each platform, deduplicated by video ID
- **YouTube Shorts detection** — Shorts are identified by fetching your channel's `/shorts` tab directly; no guessing by duration or URL
- **Filter by type** — filter any list to show All, Videos Only, or Shorts Only
- **Include Shorts in Sync** — optional checkbox to include Shorts when running Sync All
- **Download** — download any video from YouTube or LBRY via yt-dlp with one click
- **Upload to LBRY** — publish downloaded videos directly to your LBRY channel via the local daemon
- **Upload to YouTube** — upload LBRY videos back to YouTube using the Data API v3
- **Sync All** — automatically download and upload every missing video in one operation with live status, a progress bar, and a cancel button
- **Cancel with cleanup** — when cancelling a sync, choose to delete the partial download or keep it
- **Failed video retry** — after a sync, any failed videos are shown in a checklist dialog where you can select and retry them individually
- **Duplicate LBRY claim names** — if a claim name already exists on LBRY, the app finds the next available name (e.g. `-2`, `-3`) and prompts you to confirm or enter a custom name before uploading
- **yt-dlp auto-download** — if `yt-dlp.exe` is not found, the app downloads the latest release automatically on first run
- **Multi-channel support** — manage multiple YouTube channels and switch active channels on the fly
- **Channel Settings** — a unified settings window with tabs for YouTube credentials and LBRY bid settings
- **Single executable** — ships as a self-contained `.exe` with no installer or .NET runtime required

---

## Requirements

- **Windows 10 or 11**
- [LBRY Desktop](https://lbry.com/get) — must be running locally for LBRY features (the app communicates with the daemon at `http://localhost:5279`)
- YouTube Data API v3 credentials — optional, only needed if you want to upload videos **to** YouTube
- `yt-dlp.exe` — auto-downloaded on first run if not present; or place it manually in the same folder as `VidMerger.exe`

---

## Installation

VidMerger ships as a single self-contained `.exe`. No installer, no .NET runtime needed.

1. Download `VidMerger.exe` from the [Releases](../../releases) page
2. Place it in a folder of your choice (e.g. `C:\Tools\VidMerger\`)
3. Run `VidMerger.exe` — yt-dlp will be downloaded automatically if it isn't present

---

## First-Time Setup

### YouTube Channel

1. Click **⚙ Channel Settings** in the toolbar
2. On the **YouTube** tab, enter a name and your channel URL (e.g. `https://www.youtube.com/@YourChannel`)
3. Click **Add** — it will automatically become your active channel
4. Click **Close**
5. Back on the main window, click **Load Videos** under YouTube

### LBRY

1. Make sure **LBRY Desktop** is open and running
2. Click **Connect** under LBRY on the main window
3. Select your channel from the list if prompted — the button turns green when connected
4. Click **Load Videos**

### YouTube Upload API (optional)

Only needed if you want to upload videos back **to** YouTube.

1. Go to [Google Cloud Console](https://console.cloud.google.com)
2. Create a new project
3. Enable the **YouTube Data API v3**
4. Go to **Credentials** → **Create Credentials** → **OAuth 2.0 Client ID**
5. Set application type to **Desktop app**
6. Copy your **Client ID** and **Client Secret**
7. In VidMerger: **⚙ Channel Settings** → **YouTube** tab → paste them into the Upload API fields → **Save API**
8. Back on the main window, click **Enable Upload API** and sign in with your Google account

---

## How to Use

### Comparing Your Libraries

1. Load your YouTube videos (**Load Videos** under YouTube)
2. Connect to LBRY and load your LBRY videos (**Connect** then **Load Videos** under LBRY)
3. Click **Compare** in either panel:
   - **Left panel** — videos on YouTube missing from LBRY
   - **Right panel** — videos on LBRY missing from YouTube
4. Use the **filter dropdown** to view All, Videos Only, or Shorts Only

### Syncing Individual Videos

1. Select a video from the missing list
2. Click **Download** to save it locally
3. Click **Upload to LBRY** or **Upload to YT** to publish it

### Sync All

1. Click **Compare** to populate the missing list
2. Optionally check **Include Shorts in Sync All** to include Shorts
3. Click **⟳ Sync All** — VidMerger downloads and uploads every video one by one
4. Watch the progress bar and status strip for live updates
5. Click **Cancel** at any time — you'll be asked whether to delete the partial download or keep it
6. When complete, a summary shows how many succeeded and failed
7. If there were failures, click **Yes** to open the retry dialog and re-run just the failed videos

### Duplicate LBRY Claim Names

If a video's claim name already exists on LBRY (e.g. you uploaded it before under a different title), VidMerger will:

1. Automatically find the next available name (e.g. `my-video-2`)
2. Show a dialog asking you to confirm the suggested name or type a custom one
3. Upload with the chosen name — the video title on LBRY stays unchanged

---

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or later (Community edition works fine)

### Steps

```bash
git clone https://github.com/ironsniper1/ContentCreatorManager.git
cd ContentCreatorManager
dotnet build
dotnet run
```

### Publishing a Single Executable

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Output: `bin\Release\net8.0-windows\win-x64\publish\VidMerger.exe`

---

## Project Structure

```
VidMerger.csproj          — project file (.NET 8, WinForms, explicit compile list)
Program.cs                — entry point
AppSettings.cs            — settings model, saved to settings.json next to the exe
VideoItem.cs              — video data model (title, ID, platform, IsShort, local paths)
YouTubeService.cs         — yt-dlp for listing/downloading (fetches /videos + /shorts tabs separately), YouTube API v3 for uploading
LbryService.cs            — LBRY daemon HTTP API wrapper (localhost:5279), duplicate claim name detection
MainForm.cs               — main application window (two-panel layout, filter dropdowns, Sync All, retry dialog)
ChannelSettingsForm.cs    — unified channel and API settings dialog
app.ico                   — application icon
```

---

## Changelog

### v1.2.0 (current)
- Added YouTube Shorts support — Shorts detected by fetching `/videos` and `/shorts` tabs separately for 100% accuracy
- Added filter dropdowns (Show All / Videos Only / Shorts Only) on both panels
- Added **Include Shorts in Sync All** checkbox
- Added **Failed Videos** retry dialog — failed videos shown in a checklist after sync and can be retried individually
- Added cancel-with-delete — cancelling a sync prompts whether to delete the partial download
- Added duplicate LBRY claim name detection — auto-suggests next available name and prompts user to confirm or enter a custom one
- Fixed LBRY upload timeout — HttpClient timeout increased to 2 hours, removed unreliable confirmation polling
- Fixed Load Videos crash on channels with float-format durations
- Fixed filter dropdown not correctly hiding Shorts
- Fixed Cancel button not stopping yt-dlp immediately
- Fixed failed videos dialog buttons being cut off
- Fixed compiler ambiguity errors — csproj now uses an explicit compile list so old leftover files are ignored

### v1.1.0
- yt-dlp auto-downloaded on first run if not present in app folder
- LBRY Connect button turns green on successful connection

### v1.0.0
- Initial release
- YouTube ↔ LBRY two-panel compare and sync
- Download and upload individual videos
- Sync All with progress bar
- Multi-channel support
- Unified Channel Settings dialog

---

## Notes & Limitations

- **Title matching** — VidMerger compares videos by title (case-insensitive). If a video was uploaded with a slightly different title on one platform it may show as missing even if it exists.
- **LBRY requires Desktop app** — VidMerger communicates with the LBRY daemon directly at `localhost:5279`. LBRY Desktop must be open and running.
- **YouTube upload quota** — the YouTube Data API has a daily quota. Uploading large numbers of videos in one day may hit this limit.
- **yt-dlp must be kept updated** — YouTube frequently changes in ways that break older versions of yt-dlp. If downloading stops working, update yt-dlp (just replace `yt-dlp.exe` in the app folder).
- **Private/deleted videos** — videos that are private, unlisted, or deleted on YouTube cannot be downloaded by yt-dlp and will show as Download Failed in the retry dialog.

---

## License

MIT License — see [LICENSE](LICENSE) for details.
