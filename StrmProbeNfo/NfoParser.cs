using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace StrmProbeNfo
{
    /// <summary>
    /// Result of parsing a .strm.nfo sidecar: the stream list for
    /// SaveMediaStreams, plus the item-level fields (BaseItem.Width/Height/
    /// RunTimeTicks/TotalBitrate/Size) that live outside &lt;streamdetails&gt;
    /// and need a separate IItemRepository.SaveItem call - SaveMediaStreams
    /// only ever touches the MediaStreams2 table, never MediaItems' own
    /// Width/Height etc. (confirmed by decompiling the real native probe
    /// path, BaseMediaInfoProber&lt;T&gt;, 2026-09-30). Container is
    /// deliberately not included yet - vod-probe's raw ffprobe format_name
    /// (e.g. "matroska,webm") isn't confirmed to be the normalized form Emby
    /// itself writes there, and BaseItem.Container may factor into direct-play
    /// vs transcode decisions, so it's not worth setting from a guess.
    /// </summary>
    public sealed class NfoParseResult
    {
        public List<MediaStream> Streams { get; } = new List<MediaStream>();
        public int? Width { get; set; }
        public int? Height { get; set; }
        public long? RunTimeTicks { get; set; }
        public int? TotalBitrate { get; set; }
        public long? Size { get; set; }
    }

    /// <summary>
    /// Parses the Kodi/XBMC-style "streamdetails" section vod_manager writes
    /// into each .strm.nfo sidecar, restricted to the fields SaveMediaStreams
    /// actually persists (see the project's Obsidian notes for the full
    /// column list and schema history).
    /// </summary>
    public static class NfoParser
    {
        public static NfoParseResult Parse(string nfoPath)
        {
            var result = new NfoParseResult();

            var doc = XDocument.Load(nfoPath);
            var fileinfo = doc.Root?.Element("fileinfo");
            var streamDetails = fileinfo?.Element("streamdetails");
            if (fileinfo == null || streamDetails == null)
            {
                return result;
            }

            int index = 0;

            var video = streamDetails.Element("video");
            if (video != null)
            {
                result.Width = ElemInt(video, "width");
                result.Height = ElemInt(video, "height");

                var durationSeconds = ElemFloat(video, "durationinseconds");
                result.RunTimeTicks = durationSeconds.HasValue ? (long)(durationSeconds.Value * 10_000_000L) : (long?)null;

                result.Streams.Add(new MediaStream
                {
                    Index = index++,
                    Type = MediaStreamType.Video,
                    Codec = ElemString(video, "codec"),
                    Width = result.Width,
                    Height = result.Height,
                    BitRate = ElemInt(video, "bitrate"),
                    AverageFrameRate = ElemFloat(video, "framerate"),
                    RealFrameRate = ElemFloat(video, "framerate"),
                    IsDefault = true,
                    ExtendedVideoType = ParseHdrType(ElemString(video, "hdrtype")),
                    ExtendedVideoSubType = ExtendedVideoSubTypes.None,
                });
            }

            bool firstAudio = true;
            foreach (var audio in streamDetails.Elements("audio"))
            {
                result.Streams.Add(new MediaStream
                {
                    Index = index++,
                    Type = MediaStreamType.Audio,
                    Codec = ElemString(audio, "codec"),
                    Channels = ElemInt(audio, "channels"),
                    Language = ElemString(audio, "language"),
                    BitRate = ElemInt(audio, "bitrate"),
                    IsDefault = firstAudio,
                });
                firstAudio = false;
            }

            foreach (var subtitle in streamDetails.Elements("subtitle"))
            {
                result.Streams.Add(new MediaStream
                {
                    Index = index++,
                    Type = MediaStreamType.Subtitle,
                    Codec = ElemString(subtitle, "codec"),
                    Language = ElemString(subtitle, "language"),
                    IsForced = ElemBool(subtitle, "forced"),
                    IsHearingImpaired = ElemBool(subtitle, "hearingimpaired"),
                });
            }

            result.TotalBitrate = ElemInt(fileinfo, "totalbitrate");
            result.Size = ElemLong(fileinfo, "size");

            return result;
        }

        /// <summary>
        /// Maps the .nfo's &lt;hdrtype&gt; value to Emby's ExtendedVideoType,
        /// the real persisted field that drives the computed VideoRange
        /// property (confirmed by decompiling MediaStream.GenerateVideoRange,
        /// 2026-09-29). Confirmed against the real catalogue (2026-09-30):
        /// "sdr", "hdr10", "dolby_vision" (underscore, not "dolbyvision") -
        /// hdr10plus/hlg not yet observed in a real file.
        /// </summary>
        internal static ExtendedVideoTypes ParseHdrType(string? hdrType)
        {
            if (string.IsNullOrWhiteSpace(hdrType))
            {
                return ExtendedVideoTypes.None;
            }

            var normalized = hdrType!.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "").Replace("+", "plus");
            return normalized switch
            {
                "sdr" => ExtendedVideoTypes.None,
                "hdr10" => ExtendedVideoTypes.Hdr10,
                "hdr10plus" => ExtendedVideoTypes.Hdr10Plus,
                "hlg" => ExtendedVideoTypes.HyperLogGamma,
                "dolbyvision" or "dv" => ExtendedVideoTypes.DolbyVision,
                _ => ExtendedVideoTypes.None,
            };
        }

        private static string? ElemString(XElement parent, string name) => parent.Element(name)?.Value;

        private static int? ElemInt(XElement parent, string name)
        {
            var s = parent.Element(name)?.Value;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (int?)null;
        }

        private static float? ElemFloat(XElement parent, string name)
        {
            var s = parent.Element(name)?.Value;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (float?)null;
        }

        private static bool ElemBool(XElement parent, string name)
        {
            var s = parent.Element(name)?.Value;
            return bool.TryParse(s, out var v) && v;
        }

        private static long? ElemLong(XElement parent, string name)
        {
            var s = parent.Element(name)?.Value;
            return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (long?)null;
        }
    }
}
