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
    /// Catch-up sweep for .strm items the event-driven path
    /// (StrmNfoEntryPoint) never saw or couldn't complete:
    /// - items scanned before this plugin was installed (confirmed
    ///   necessary, 2026-09-29: a routine rescan does not refire
    ///   ItemAdded/ItemUpdated for already-known, unchanged items),
    /// - a .strm whose .nfo wasn't written yet at scan time,
    /// - an item missed because the plugin was disabled during a scan.
    ///
    /// Query pattern (HasPath/HasContainer/ExcludeItemTypes) taken from the
    /// real, working faush01/StrmExtract task. Unlike StrmExtract this task
    /// never probes, so it has none of that plugin's multi-version
    /// same-title collision risk (see the project's Obsidian notes on the
    /// Dispatcharr VOD-proxy idle-session-reuse bug) and can process every
    /// candidate in a run without special-casing versions.
    /// </summary>
    public class StrmNfoScheduledTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IItemRepository _itemRepository;
        private readonly ILogger _logger;

        public StrmNfoScheduledTask(ILibraryManager libraryManager, IItemRepository itemRepository, ILogManager logManager)
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
                _logger.Info("StrmProbeNfo scheduled task - no library configured in the plugin settings, nothing to do");
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
            _logger.Info("StrmProbeNfo scheduled task - {0} item(s) to consider", results.Length);

            var candidates = results
                .Where(item => !string.IsNullOrEmpty(item.Path)
                    && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase)
                    && item.GetMediaStreams().Count == 0)
                .ToList();

            _logger.Info("StrmProbeNfo scheduled task - {0} candidate(s) with no MediaStreams yet", candidates.Count);

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

            _logger.Info("StrmProbeNfo scheduled task - applied MediaInfo to {0}/{1} candidate(s)", applied, candidates.Count);

            return Task.CompletedTask;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerDaily,
                    TimeOfDayTicks = TimeSpan.FromHours(3).Ticks,
                },
            };
        }

        public string Category => "STRM Probe NFO";

        public string Key => "StrmProbeNfoCatchUpTask";

        public string Description => "Applies MediaInfo from .strm.nfo sidecars to .strm items missed by the event-driven path (already scanned before install, .nfo not ready yet, plugin disabled during a scan).";

        public string Name => "Apply pending .strm.nfo sidecars";
    }
}
