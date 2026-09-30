using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Model.Attributes;

namespace StrmProbeNfo
{
    public class StrmProbeNfoOptions : EditableOptionsBase
    {
        public override string EditorTitle => "STRM Probe NFO";

        public override string EditorDescription =>
            "Bibliothèques dans lesquelles peupler le MediaInfo depuis les .strm.nfo. Aucune bibliothèque n'est ciblée par défaut.";

        [Browsable(false)]
        public IEnumerable<EditorSelectOption> AvailableLibraries { get; set; } = new List<EditorSelectOption>();

        [DisplayName("Bibliothèques ciblées")]
        [Description("Bibliothèques à traiter (événements + tâches planifiées). Aucune sélectionnée par défaut, y compris pour les bibliothèques internes (Collections, Live TV...).")]
        [EditMultilSelect]
        [SelectItemsSource(nameof(AvailableLibraries))]
        public string EnabledLibraryIds { get; set; } = string.Empty;

        public HashSet<string> GetEnabledLibraryIdSet()
        {
            if (string.IsNullOrWhiteSpace(EnabledLibraryIds))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(
                EnabledLibraryIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()),
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
