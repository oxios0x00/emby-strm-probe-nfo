#!/bin/bash
# Pulls the real Emby reference assemblies out of a running Emby server
# instead of relying on the mediabrowser.server.core NuGet package, which
# tends to lag behind real Emby releases.
#
# Edit HOST, KEY, and CONTAINER below to match your own server.
set -e
HOST=user@your-emby-host
KEY=~/.ssh/id_ed25519
CONTAINER=EmbyServer
DEST="$(dirname "$0")/StrmProbeNfo/lib"

for dll in MediaBrowser.Common.dll MediaBrowser.Controller.dll MediaBrowser.Model.dll Emby.Web.GenericEdit.dll; do
  ssh -i "$KEY" -o ConnectTimeout=10 "$HOST" "docker cp $CONTAINER:/system/$dll /tmp/$dll"
  scp -i "$KEY" "$HOST:/tmp/$dll" "$DEST/$dll"
done

echo "Done. DLLs in $DEST"
