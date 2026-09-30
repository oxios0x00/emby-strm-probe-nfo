using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Logging;
using System;
using System.Threading;

namespace StrmProbeNfo
{
    /// <summary>
    /// Shared logic for applying a .strm item's sibling .strm.nfo sidecar to
    /// its MediaStreams, used by both the event-driven path
    /// (StrmNfoEntryPoint) and the scheduled catch-up task
    /// (StrmNfoScheduledTask) so they can never drift apart.
    /// </summary>
    public static class StrmNfoApplier
    {
        /// <summary>
        /// Returns true if streams were parsed and saved, false if this item
        /// wasn't a candidate (not a .strm, or no sidecar .nfo yet) or the
        /// parsed result was empty. Never throws - failures are logged and
        /// swallowed, since this runs per-item across a whole library and one
        /// bad .nfo shouldn't stop the rest.
        /// </summary>
        public static bool TryApply(BaseItem item, IItemRepository itemRepository, ILogger logger)
        {
            if (item == null || string.IsNullOrEmpty(item.Path))
            {
                return false;
            }

            if (!item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var nfoPath = item.Path + ".nfo";
            if (!System.IO.File.Exists(nfoPath))
            {
                // vod_manager only writes .nfo for measured relations, and in
                // practice writes .strm and .strm.nfo together from
                // already-probed data - so this should be rare, but the
                // caller (scheduled task or a fresh ItemAdded) should just
                // pick it up on a later pass once vod-probe/vod_manager
                // catch up, not treat it as an error.
                return false;
            }

            try
            {
                var streams = NfoParser.ParseStreamDetails(nfoPath);
                if (streams.Count == 0)
                {
                    logger.Warn("StrmProbeNfo - {0} parsed to zero streams, skipping", nfoPath);
                    return false;
                }

                // Safe to act on item.InternalId directly, no title/folder
                // lookup: each .strm version is its own distinct Emby item
                // (confirmed empirically against a real scanned library,
                // 2026-09-29), so SaveMediaStreams's delete-all-by-itemId
                // behavior only ever touches this exact item's own rows.
                itemRepository.SaveMediaStreams(item.InternalId, streams, CancellationToken.None);

                logger.Info("StrmProbeNfo - saved {0} stream(s) for {1} (item id {2}) from {3}",
                    streams.Count, item.Path, item.InternalId, nfoPath);
                return true;
            }
            catch (Exception ex)
            {
                logger.ErrorException("StrmProbeNfo - failed to apply {0} to item id {1}", ex, nfoPath, item.InternalId);
                return false;
            }
        }
    }
}
