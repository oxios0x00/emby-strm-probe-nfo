using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace StrmProbeNfo
{
    /// <summary>
    /// Parses the Kodi/XBMC-style "streamdetails" section vod_manager writes
    /// into each .strm.nfo sidecar into a list of Emby MediaStream objects,
    /// restricted to the fields SaveMediaStreams actually persists (see the
    /// project's Obsidian notes for the full column list and schema history).
    ///
    /// durationinseconds is intentionally not used here - it belongs on the
    /// item's own RunTimeTicks, not on a MediaStream, and is out of scope for
    /// this call.
    /// </summary>
    public static class NfoParser
    {
        public static List<MediaStream> ParseStreamDetails(string nfoPath)
        {
            var doc = XDocument.Load(nfoPath);
            var streamDetails = doc.Root?.Element("fileinfo")?.Element("streamdetails");
            if (streamDetails == null)
            {
                return new List<MediaStream>();
            }

            var streams = new List<MediaStream>();
            int index = 0;

            var video = streamDetails.Element("video");
            if (video != null)
            {
                streams.Add(new MediaStream
                {
                    Index = index++,
                    Type = MediaStreamType.Video,
                    Codec = ElemString(video, "codec"),
                    Width = ElemInt(video, "width"),
                    Height = ElemInt(video, "height"),
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
                streams.Add(new MediaStream
                {
                    Index = index++,
                    Type = MediaStreamType.Audio,
                    Codec = ElemString(audio, "codec"),
                    Channels = ElemInt(audio, "channels"),
                    Language = ElemString(audio, "language"),
                    IsDefault = firstAudio,
                });
                firstAudio = false;
            }

            foreach (var subtitle in streamDetails.Elements("subtitle"))
            {
                streams.Add(new MediaStream
                {
                    Index = index++,
                    Type = MediaStreamType.Subtitle,
                    Codec = ElemString(subtitle, "codec"),
                    Language = ElemString(subtitle, "language"),
                    IsForced = ElemBool(subtitle, "forced"),
                    IsHearingImpaired = ElemBool(subtitle, "hearingimpaired"),
                });
            }

            return streams;
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
    }
}
