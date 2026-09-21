#!/bin/sh

set -eu
. /srv/app/scripts/docker-dotnet-common.sh

poll_seconds="${DOTNET_WATCH_POLL_SECONDS:-5}"
previous_manifest="$artifacts_root/.watch-manifest"
next_manifest="$artifacts_root/.watch-manifest.next"
source_manifest > "$previous_manifest"

select_build_target() {
  changed_paths="$1"
  selected_project=''
  selected_assembly=''

  while IFS= read -r changed_path; do
    [ -n "$changed_path" ] || continue
    case "$changed_path" in
      *.csproj|*.props|*.targets)
        selected_project="$dev_host_project"
        selected_assembly='all'
        break
        ;;
    esac
    case "$changed_path" in
      "$dotnet_root/Roblox.ApiProxy/"*) candidate_project='Roblox.ApiProxy/Roblox.ApiProxy.csproj'; candidate_assembly='Roblox.ApiProxy' ;;
      "$dotnet_root/Roblox.Website/"*) candidate_project='Roblox.Website/Roblox.Website.csproj'; candidate_assembly='Roblox.Website' ;;
      "$dotnet_root/Services/Roblox.Services.DataStore/"*) candidate_project='Services/Roblox.Services.DataStore/Roblox.Services.DataStore.csproj'; candidate_assembly='Roblox.Services.DataStore' ;;
      "$dotnet_root/Services/Roblox.Services.Api/"*) candidate_project='Services/Roblox.Services.Api/Roblox.Services.Api.csproj'; candidate_assembly='Roblox.Services.Api' ;;
      "$dotnet_root/Services/Roblox.Services.Donation/"*) candidate_project='Services/Roblox.Services.Donation/Roblox.Services.Donation.csproj'; candidate_assembly='Roblox.Services.Donation' ;;
      "$dotnet_root/Services/Roblox.Services.Data/"*) candidate_project='Services/Roblox.Services.Data/Roblox.Services.Data.csproj'; candidate_assembly='Roblox.Services.Data' ;;
      "$dotnet_root/Services/Roblox.Services.Avatar/"*) candidate_project='Services/Roblox.Services.Avatar/Roblox.Services.Avatar.csproj'; candidate_assembly='Roblox.Services.Avatar' ;;
      "$dotnet_root/Services/Roblox.Services.Thumbnails/"*) candidate_project='Services/Roblox.Services.Thumbnails/Roblox.Services.Thumbnails.csproj'; candidate_assembly='Roblox.Services.Thumbnails' ;;
      "$dotnet_root/Services/Roblox.Services.Users/"*) candidate_project='Services/Roblox.Services.Users/Roblox.Services.Users.csproj'; candidate_assembly='Roblox.Services.Users' ;;
      "$dotnet_root/Services/Roblox.Services.Games/"*) candidate_project='Services/Roblox.Services.Games/Roblox.Services.Games.csproj'; candidate_assembly='Roblox.Services.Games' ;;
      "$dotnet_root/Services/Roblox.Services.Admin/"*) candidate_project='Services/Roblox.Services.Admin/Roblox.Services.Admin.csproj'; candidate_assembly='Roblox.Services.Admin' ;;
      *) candidate_project="$dev_host_project"; candidate_assembly='all' ;;
    esac

    if [ -z "$selected_project" ]; then
      selected_project="$candidate_project"
      selected_assembly="$candidate_assembly"
    elif [ "$selected_project" != "$candidate_project" ]; then
      selected_project="$dev_host_project"
      selected_assembly='all'
      break
    fi
  done <<EOF
$changed_paths
EOF

  if [ -z "$selected_project" ]; then
    selected_project="$dev_host_project"
    selected_assembly='all'
  fi
}

echo "[dotnet-watch] Watching the complete dev graph every ${poll_seconds}s."

while sleep "$poll_seconds"; do
  source_manifest > "$next_manifest"
  if cmp -s "$previous_manifest" "$next_manifest"; then
    continue
  fi

  # Let editors finish atomic-save and multi-file operations before compiling.
  sleep 1
  source_manifest > "$next_manifest"
  changed_paths="$(comm -3 "$previous_manifest" "$next_manifest" | sed 's/^[[:space:]]*//' | awk '{$1=""; $2=""; sub(/^[[:space:]]+/, ""); print}' | LC_ALL=C sort -u)"
  select_build_target "$changed_paths"
  echo "[dotnet-watch] Source change detected; building $selected_project."

  if flock --close "$build_lock" sh -eu -c '
    . /srv/app/scripts/docker-dotnet-common.sh
    restore_if_needed
    build_dev_graph "$1"
  ' sh "$selected_project"; then
    cp "$next_manifest" "$previous_manifest"
    source_signature > "$source_signature_file"
    if [ "$selected_assembly" = 'all' ]; then
      touch_service_stamps
    else
      touch_service_stamp "$selected_assembly"
    fi
    echo "[dotnet-watch] Build succeeded; service processes will restart."
  else
    # Keep the last good processes alive. A subsequent edit retries the build.
    cp "$next_manifest" "$previous_manifest"
    echo "[dotnet-watch] Build failed; retaining the last good service outputs." >&2
  fi
done
