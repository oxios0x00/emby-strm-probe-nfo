using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Common;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;

namespace StrmProbeNfo
{
    public class Plugin : BasePluginSimpleUI<StrmProbeNfoOptions>
    {
        public static Plugin? Instance { get; private set; }

        private static readonly Guid _id = new Guid("674c14f7-064c-4b2e-937f-86f56e456490");

        private readonly IApplicationHost _applicationHost;

        public Plugin(IApplicationHost applicationHost) : base(applicationHost)
        {
            _applicationHost = applicationHost;
            Instance = this;
        }

        public override string Name => "STRM Probe NFO";

        public override Guid Id => _id;

        public override string Description =>
            "Populates MediaInfo for .strm items from a sibling .strm.nfo sidecar, without probing the stream.";

        protected override StrmProbeNfoOptions OnBeforeShowUI(StrmProbeNfoOptions options)
        {
            var libraryManager = _applicationHost.Resolve<ILibraryManager>();
            options.AvailableLibraries = libraryManager.GetVirtualFolders()
                .Select(f => new EditorSelectOption(f.ItemId, f.Name))
                .ToList();
            return base.OnBeforeShowUI(options);
        }

        /// <summary>
        /// Resolves the configured library selection to internal long ids,
        /// the form InternalItemsQuery.AncestorIds and BaseItem.InternalId
        /// actually use. VirtualFolderInfo.ItemId is already that same
        /// internal long id as a string, not a Guid (confirmed live,
        /// 2026-09-30: GetVirtualFolders() returned Value "3"/"1640"/"5437"
        /// for real libraries, not Guid-looking strings) - no BaseItem
        /// lookup needed, just a direct long.Parse.
        ///
        /// Use AncestorIds here, not TopParentIds: decompiling the real
        /// SqliteItemRepository query builder (2026-09-30) showed
        /// TopParentIds is only ever consulted inside a branch gated on
        /// `query.User != null` (per-user library-access scoping), so it
        /// silently has zero effect on a query built without a User - as
        /// confirmed live (same item count with an enabled library, a
        /// different one, or a nonexistent id). AncestorIds, backed by the
        /// AncestorIds2 closure table, has a real, User-independent WHERE
        /// clause (HasAncestorIdsCondition only checks AncestorIds.Length).
        ///
        /// Empty when nothing is configured yet - callers must treat that as
        /// "act on nothing", never as "unfiltered", since an empty
        /// AncestorIds array means unfiltered to Emby's own query engine.
        /// </summary>
        public HashSet<long> GetEnabledLibraryInternalIds()
        {
            var result = new HashSet<long>();
            foreach (var idString in GetOptions().GetEnabledLibraryIdSet())
            {
                if (long.TryParse(idString, out var internalId))
                {
                    result.Add(internalId);
                }
            }
            return result;
        }
    }
}
