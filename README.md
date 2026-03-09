# VidMerger v1.0

> A Windows desktop app for content creators who publish on both YouTube and LBRY/Odysee. Load your video libraries from both platforms, compare them side by side, and sync what's missing — automatically or one video at a time.

![Platform](https://img.shields.io/badge/platform-Windows-blue)
![Framework](https://img.shields.io/badge/.NET-8.0-purple)
![Version](https://img.shields.io/badge/version-1.0.0-green)

---

## Acknowledgements

This project was inspired by [**tiff1002**](https://github.com/tiff1002) and her original [ContentCreatorManager](https://github.com/tiff1002/ContentCreatorManager) — a Python-based tool for managing and syncing content across platforms. VidMerger takes that same core idea and reimplements it as a standalone C# / WinForms desktop application with an expanded feature set. Big thanks to tiff1002 for the inspiration and the original concept.

---

## What It Does

VidMerger solves a simple but tedious problem: keeping your YouTube and LBRY/Odysee channels in sync. It loads your full video list from both platforms, compares them by title, and shows you exactly what's missing from each side. You can then download and re-upload the missing videos individually, or use **Sync All** to process the entire list automatically in the background.

---

## Features

- **Load & Compare** — fetch your full video library from YouTube and LBRY and see what's missing from each platform
- **Download** — download any video from YouTube or LBRY via yt-dlp with one click
- **Upload to LBRY** — publish downloaded videos directly to your LBRY channel via the local daemon
- **Upload to YouTube** — upload LBRY videos back to YouTube using the Data API v3
- **Sync All** — automatically download and upload every missing video in one operation with a progress bar and cancel button
- **Multi-channel support** — manage multiple YouTube channels and switch active channels on the fly
- **Channel Settings** — a single unified settings window with tabs for YouTube credentials and LBRY bid settings
- **Single executable** — ships as a self-contained `.exe` with no install or runtime required

---

## Requirements

- **Windows 10 or 11**
- [yt-dlp](https://github.com/yt-dlp/yt-dlp/releases) — place `yt-dlp.exe` in the same folder as `VidMerger.exe`
- [LBRY Desktop](https://lbry.com/get) — must be running locally for LBRY features (the app talks to the daemon at `http://localhost:5279`)
- YouTube Data API v3 credentials — optional, only needed if you want to upload videos TO YouTube

---

## Installation

VidMerger ships as a single self-contained `.exe`. No installer, no .NET runtime needed.

1. Download `VidMerger.exe` from the [Releases](../../releases) page
2. Place it in a folder of your choice (e.g. `C:\Tools\VidMerger\`)
3. Download [yt-dlp.exe](https://github.com/yt-dlp/yt-dlp/releases) and place it in the **same folder**
4. Place `app.ico` in the same folder (included in the release)
5. Run `VidMerger.exe`

---

## First-Time Setup

### YouTube Channel

1. Click **⚙ Channel Settings** in the toolbar
2. On the **YouTube** tab, enter a name and your channel URL (e.g. `https://www.youtube.com/@YourChannel/videos`)
3. Click **Add** — it will automatically be set as your active channel
4. Click **Close**
5. Back on the main window, click **Load Videos** under YouTube

### LBRY

1. Make sure **LBRY Desktop** is open and running
2. On the main window, click **Connect** under LBRY
3. Select your channel from the list if prompted
4. Click **Load Videos**

### YouTube Upload API (optional)

Only needed if you want to upload videos back TO YouTube.

1. Go to [Google Cloud Console](https://console.cloud.google.com)
2. Create a new project (or use an existing one)
3. Enable the **YouTube Data API v3**
4. Go to **Credentials** → **Create Credentials** → **OAuth 2.0 Client ID**
5. Set application type to **Desktop app**
6. Copy your **Client ID** and **Client Secret**
7. In VidMerger: **⚙ Channel Settings** → **YouTube** tab → paste them into the Upload API fields → **Save API**
8. Back on the main window, click **Enable Upload API** and sign in with your Google account

---

## How to Use

### Comparing Your Libraries

1. Load your YouTube videos (click **Load Videos** under YouTube)
2. Connect to LBRY and load your LBRY videos (click **Connect** then **Load Videos** under LBRY)
3. Click **Compare** in either panel:
   - **Missing from LBRY** — videos on YouTube that haven't been uploaded to LBRY
   - **Missing from YouTube** — videos on LBRY that haven't been uploaded to YouTube

### Syncing Individual Videos

1. Select a video from the missing list
2. Click **Download** to download it locally
3. Click **Upload to LBRY** or **Upload to YT** to publish it

### Sync All

1. Click **Compare** to populate the missing list
2. Click **⟳ Sync All** — VidMerger will automatically download and upload every video in the list one by one
3. A progress bar shows current status; click **Cancel** at any time to stop
4. A summary popup shows how many succeeded and failed when complete

---

## Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or later (Community edition works fine)

### Steps

```bash
git clone https://github.com/yourusername/VidMerger.git
cd VidMerger
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
VidMerger.csproj          — project file (.NET 8, WinForms)
Program.cs                — entry point
AppSettings.cs            — settings model, saved to settings.json next to the exe
VideoItem.cs              — video data model (title, ID, platform, local file path)
YouTubeService.cs         — yt-dlp for listing/downloading, YouTube API v3 for uploading
LbryService.cs            — LBRY daemon HTTP API wrapper (localhost:5279)
MainForm.cs               — main application window
ChannelSettingsForm.cs    — unified channel and API settings dialog
app.ico                   — application icon
```

---

## Notes & Limitations

- **Title matching** — VidMerger compares videos by title (case-insensitive). If a video was uploaded with a slightly different title on one platform it may show as missing even if it exists.
- **LBRY requires Desktop app** — VidMerger talks to the LBRY daemon directly. LBRY Desktop must be open and running.
- **YouTube upload quota** — the YouTube Data API has a daily quota limit. Uploading large numbers of videos may hit this limit.
- **yt-dlp must be kept updated** — YouTube frequently changes in ways that break older versions of yt-dlp. If downloading stops working, update yt-dlp.

---

## License

MIT License — see [LICENSE](LICENSE) for details.
