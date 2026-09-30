using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using System.Linq;

namespace StrmProbeNfo
{
    /// <summary>
    /// Subscribes to library events and applies MediaInfo from each .strm
    /// item's sibling .strm.nfo sidecar, never by probing the stream.
    ///
    /// Each .strm version of a title gets its own distinct Emby item/ItemId
    /// (confirmed empirically against a real scanned library, 2026-09-29 -
    /// see the project's Obsidian notes) - so it's safe to act on whatever
    /// specific item each event carries directly, without any title/folder
    /// level lookup or grouping.
    ///
    /// This only ever reacts to items a scan actually touches. Emby does not
    /// refire ItemAdded/ItemUpdated for already-known, unchanged items on a
    /// routine rescan (confirmed empirically, 2026-09-29) - so anything
    /// scanned before this plugin existed, or before its .nfo was ready,
    /// needs StrmNfoScheduledTask to catch up.
    ///
    /// Only acts within libraries the user explicitly enabled in the plugin
    /// config (empty by default - see StrmProbeNfoOptions), so it never
    /// touches Emby-managed synthetic libraries (Collections, Live TV, etc.)
    /// or libraries a multi-library setup didn't intend for this plugin.
    /// </summary>
    public class StrmNfoEntryPoint : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IItemRepository _itemRepository;
        private readonly ILogger _logger;

        public StrmNfoEntryPoint(ILibraryManager libraryManager, IItemRepository itemRepository, ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _itemRepository = itemRepository;
            _logger = logManager.GetLogger(Plugin.Instance?.Name ?? "StrmProbeNfo");
        }

        public void Run()
        {
            _libraryManager.ItemAdded += OnItemAddedOrUpdated;
            _libraryManager.ItemUpdated += OnItemAddedOrUpdated;
        }

        private void OnItemAddedOrUpdated(object? sender, ItemChangeEventArgs e)
        {
            var enabledLibraryIds = Plugin.Instance?.GetEnabledLibraryInternalIds();
            if (enabledLibraryIds == null || enabledLibraryIds.Count == 0)
            {
                return;
            }

            if (!e.CollectionFolders.Any(folder => enabledLibraryIds.Contains(folder.InternalId)))
            {
                return;
            }

            StrmNfoApplier.TryApply(e.Item, _itemRepository, _logger);
        }

        public void Dispose()
        {
            _libraryManager.ItemAdded -= OnItemAddedOrUpdated;
            _libraryManager.ItemUpdated -= OnItemAddedOrUpdated;
        }
    }
}
