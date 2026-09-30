using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StrmProbeNfo
{
    /// <summary>
    /// Manual, opt-in full resync: reprocesses every .strm item in the
    /// enabled libraries regardless of whether it already has MediaStreams,
    /// unlike StrmNfoScheduledTask which only ever touches items with none.
    ///
    /// Exists because .strm.nfo content can change after an item was already
    /// processed (e.g. vod_manager gaining subtitle support, a real case hit
    /// 2026-09-30), and the normal catch-up task has no way to detect that -
    /// it only looks at whether MediaStreams exist at all, not whether the
    /// underlying .nfo changed since. Re-deriving that automatically (comparing
    /// .nfo mtime, or unconditionally reprocessing everything on every
    /// scheduled run) was considered and explicitly rejected: at real
    /// production scale (~45000 items) either approach means heavy disk I/O
    /// or DB writes on every single scheduled run, for a change that in
    /// practice essentially never happens once vod_manager has written a
    /// .nfo. So this stays entirely manual - no default trigger, run only
    /// from the dashboard when actually needed.
    /// </summary>
    public class StrmNfoForceReprocessTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IItemRepository _itemRepository;
        private readonly ILogger _logger;

        public StrmNfoForceReprocessTask(ILibraryManager libraryManager, IItemRepository itemRepository, ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _itemRepository = itemRepository;
            _logger = logManager.GetLogger(Plugin.Instance?.Name ?? "StrmProbeNfo");
        }

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var enabledLibraryIds = Plugin.Instance?.GetEnabledLibraryInternalIds();
            if (enabledLibraryIds == null || enabledLibraryIds.Count == 0)
            {
                _logger.Info("StrmProbeNfo force-reprocess task - no library configured in the plugin settings, nothing to do");
                return Task.CompletedTask;
            }

            var query = new InternalItemsQuery
            {
                HasPath = true,
                Recursive = true,
                AncestorIds = enabledLibraryIds.ToArray(),
                ExcludeItemTypes = new[] { "Folder", "CollectionFolder", "UserView", "Series", "Season", "Trailer", "Playlist" },
            };

            var results = _libraryManager.GetItemList(query);

            var candidates = results
                .Where(item => !string.IsNullOrEmpty(item.Path)
                    && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                .ToList();

            _logger.Info("StrmProbeNfo force-reprocess task - {0} .strm item(s) to reprocess unconditionally", candidates.Count);

            int applied = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (StrmNfoApplier.TryApply(candidates[i], _itemRepository, _logger))
                {
                    applied++;
                }

                progress.Report(100.0 * (i + 1) / candidates.Count);
            }

            _logger.Info("StrmProbeNfo force-reprocess task - applied MediaInfo to {0}/{1} item(s)", applied, candidates.Count);

            return Task.CompletedTask;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }

        public string Category => "STRM Probe NFO";

        public string Key => "StrmProbeNfoForceReprocessTask";

        public string Description => "Reprocesses every .strm.nfo sidecar unconditionally, even for items that already have MediaInfo. Run manually after a change to vod_manager's .nfo output (e.g. new fields) that should be reflected on already-processed items. Not scheduled automatically.";

        public string Name => "Force full .strm.nfo reprocess";
    }
}
