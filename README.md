# STRM Probe NFO

Emby plugin that populates media technical info (`MediaStream`: video, audio, and subtitle details) for `.strm` library items from a sibling `.strm.nfo` sidecar file, instead of probing the stream itself.

## Why

For a `.strm` item, Emby normally has no technical media info unless it probes the underlying stream (`ffprobe` against a remote/proxied URL). If that information is already known and available as a `.strm.nfo` sidecar next to the `.strm` file, this plugin reads it directly instead — no network probe required.

## Requirements

- Emby Server 4.10.1.0 or compatible (.NET 8)
- A `.strm.nfo` sidecar next to each `.strm` file, using the Kodi/XBMC `streamdetails` XML format, e.g. `Movie (Year).strm` → `Movie (Year).strm.nfo`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<movie>
  <fileinfo>
    <streamdetails>
      <video>
        <codec>hevc</codec>
        <width>3840</width>
        <height>2160</height>
        <bitrate>15000000</bitrate>
        <framerate>23.976</framerate>
        <hdrtype>hdr10</hdrtype>
      </video>
      <audio>
        <codec>eac3</codec>
        <channels>6</channels>
        <language>eng</language>
      </audio>
      <subtitle>
        <codec>subrip</codec>
        <language>eng</language>
      </subtitle>
    </streamdetails>
  </fileinfo>
</movie>
```

## Features

- Populates video codec/resolution/bitrate/framerate/HDR type, audio tracks (codec/channels/language), and subtitle tracks (codec/language/forced/hearing-impaired) from the `.nfo`
- Applies info as soon as a `.strm` item is scanned into the library
- A daily scheduled task catches up on items the event path missed (e.g. items added before the plugin was installed, or before their `.nfo` existed)
- An on-demand task can force a full reprocess of every item, useful after a `.nfo` schema change that should be reflected on already-processed items
- Per-library configuration: choose which libraries the plugin should act on (none by default)

## Installation

1. Download `StrmProbeNfo.dll` from the [latest release](../../releases/latest)
2. Copy it into Emby's `plugins/` folder
3. Restart Emby Server
4. In the dashboard, go to Plugins → STRM Probe NFO → Settings, and select which libraries the plugin should process

## Building from source

Requires the .NET SDK and the real Emby reference assemblies, which aren't redistributed here since they're Emby's own binaries. `fetch-emby-libs.sh` pulls them from a running Emby server over SSH/Docker — edit the `HOST`/`KEY` variables at the top of the script to point at your own server first:

```bash
./fetch-emby-libs.sh
cd StrmProbeNfo
dotnet build
```

Targets `netstandard2.0`.

## Roadmap

- Handle item removal (`ItemRemoved`)
- Avoid a redundant duplicate write when an item fires both `ItemAdded` and `ItemUpdated`
