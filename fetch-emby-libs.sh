#!/bin/bash
# Pulls the real Emby reference assemblies out of the live EmbyServer-test
# container instead of relying on the mediabrowser.server.core NuGet package,
# which lags behind real Emby releases (latest published there as of
# 2026-09-29 is 4.10.0.24-beta2, our real target is 4.10.1.0).
set -e
HOST=root@192.168.1.94
KEY=~/.ssh/id_ed25519_dispatcharr_unraid
DEST="$(dirname "$0")/StrmProbeNfo/lib"

for dll in MediaBrowser.Common.dll MediaBrowser.Controller.dll MediaBrowser.Model.dll Emby.Web.GenericEdit.dll; do
  ssh -i "$KEY" -o ConnectTimeout=10 "$HOST" "docker cp EmbyServer-test:/system/$dll /tmp/$dll"
  scp -i "$KEY" "$HOST:/tmp/$dll" "$DEST/$dll"
done

echo "Done. DLLs in $DEST"
