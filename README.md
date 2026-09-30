# STRM Probe NFO

Emby plugin that populates `MediaStream` info (video, audio, subtitles) for `.strm` library items from a sibling `.strm.nfo` sidecar (written by [vod_manager](https://github.com/oxios0x00/vod-manager), a Dispatcharr plugin, from [vod-probe](https://github.com/oxios0x00) measurements) — never by probing the stream itself. The technical info already exists upstream; re-deriving it via `ffprobe` against the stream would be pure redundant work.

Design notes, decisions, and the dev journal live in the Obsidian vault: `Dev/Emby STRM Probe NFO/`.

## How it works

- Reads the Kodi/XBMC-style `streamdetails` block from `<name>.strm.nfo` (video codec/resolution/bitrate/framerate/HDR type, audio tracks with codec/channels/language, subtitle tracks with codec/language/forced/hearing-impaired).
- Applies it via `IItemRepository.SaveMediaStreams`, scoped to the exact item from the event/query — never a title/folder-level lookup, so multi-version titles (several `.strm` files per movie/episode) never collide.
- Two ways items get processed:
  - **Event-driven**: subscribes to `ItemAdded`/`ItemUpdated`, applies immediately during any scan.
  - **Scheduled catch-up task** ("Apply pending .strm.nfo sidecars"): sweeps for `.strm` items with no `MediaStreams` yet — needed because a routine Emby rescan doesn't refire events for already-known, unchanged items, so anything scanned before this plugin existed (or before its `.nfo` was ready) would otherwise never get picked up. Runs daily by default.
  - **Manual full reprocess task** ("Force full .strm.nfo reprocess"): reprocesses every `.strm` item unconditionally, even ones that already have `MediaStreams`. No automatic trigger — run it from the dashboard after a `.nfo` schema change (e.g. a new field) that should be reflected on already-processed items. Deliberately not automatic: at real library scale, unconditionally re-reading every `.nfo` on every scheduled run is wasted disk I/O for a change that essentially never happens in practice.
- **Configurable per library** (Dashboard → Plugins → STRM Probe NFO → Settings): pick which libraries the plugin should touch. Nothing is enabled by default, so it never acts on Emby's own internal libraries (Collections, Live TV, etc.) or on libraries you didn't intend it for.

## Build

Requires the real Emby reference assemblies in `StrmProbeNfo/lib/` (gitignored — not redistributed, they're Emby's own binaries). Fetch them from your Emby server's container:

```bash
./fetch-emby-libs.sh
```

Then:

```bash
cd StrmProbeNfo
dotnet build
```

Targets `netstandard2.0`, verified against real Emby **4.10.1.0**.

## Install

Copy `StrmProbeNfo/bin/Debug/netstandard2.0/StrmProbeNfo.dll` (or the release asset) into Emby's `plugins/` folder and restart the server.

## Status

Working end-to-end, verified against a real Emby 4.10.1.0 server: video/audio/subtitle parsing, `SaveMediaStreams` writes, the event handler, both scheduled tasks, and the per-library configuration UI have all been exercised against real scanned libraries, not just compiled.

Not yet implemented: `ItemRemoved` handling, dedup of the `ItemAdded`+`ItemUpdated` double-fire on new items (harmless but wasteful).
