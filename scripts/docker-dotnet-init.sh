#!/bin/sh

set -eu
. /srv/app/scripts/docker-dotnet-common.sh

mkdir -p "$artifacts_root"

flock --close "$build_lock" sh -eu -c '
  . /srv/app/scripts/docker-dotnet-common.sh
  restore_if_needed

  current_source_signature="$(source_signature)"
  previous_source_signature="$(cat "$source_signature_file" 2>/dev/null || true)"
  if [ "$current_source_signature" != "$previous_source_signature" ] || ! all_service_outputs_exist; then
    build_dev_graph
    printf "%s" "$current_source_signature" > "$source_signature_file"
    touch_service_stamps
  else
    echo "[dotnet] Shared dev outputs are current; skipping build."
    ensure_service_stamps
  fi
'
