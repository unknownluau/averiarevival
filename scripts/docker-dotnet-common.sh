#!/bin/sh

set -eu

repository_root=/srv/app
dotnet_root=/srv/app/Roblox
dev_host_project=Roblox.DevHost/Roblox.DevHost.csproj
artifacts_root=/tmp/averia-artifacts
build_lock="$artifacts_root/.build.lock"
restore_signature_file="$artifacts_root/.restore-signature"
source_signature_file="$artifacts_root/.source-signature"

service_assemblies="
Roblox.ApiProxy
Roblox.Website
Roblox.Services.DataStore
Roblox.Services.Api
Roblox.Services.Donation
Roblox.Services.Data
Roblox.Services.Avatar
Roblox.Services.Thumbnails
Roblox.Services.Users
Roblox.Services.Games
Roblox.Services.Admin
"

restore_signature() {
  find "$dotnet_root" \
    -path "$dotnet_root/Roblox.Libraries/Json" -prune -o \
    -type d \( \
      -name bin -o \
      -name obj -o \
      -name '*Tests' -o \
      -name '*Test' -o \
      -name Roblox.UnitTest -o \
      -name Roblox.IntegrationTest \
    \) -prune -o \
    -type f \( \
      -name '*.csproj' -o \
      -name '*.props' -o \
      -name '*.targets' -o \
      -name 'Directory.Packages.*' -o \
      -name 'NuGet.config' -o \
      -name 'global.json' -o \
      -name 'packages.lock.json' \
    \) -printf '%T@ %s %p\n' \
    | LC_ALL=C sort \
    | sha256sum \
    | cut -d' ' -f1
}

source_manifest() {
  find "$dotnet_root" \
    -path "$dotnet_root/Roblox.Libraries/Json" -prune -o \
    -type d \( \
      -name bin -o \
      -name obj -o \
      -name '*Tests' -o \
      -name '*Test' -o \
      -name Roblox.UnitTest -o \
      -name Roblox.IntegrationTest \
    \) -prune -o \
    -type f \( \
      -name '*.cs' -o \
      -name '*.cshtml' -o \
      -name '*.razor' -o \
      -name '*.resx' -o \
      -name 'appsettings*.json' -o \
      -name '*.csproj' -o \
      -name '*.props' -o \
      -name '*.targets' \
    \) -printf '%T@ %s %p\n' \
    | LC_ALL=C sort
}

source_signature() {
  source_manifest \
    | sha256sum \
    | cut -d' ' -f1
}

restore_if_needed() {
  current_restore_signature="$(restore_signature)"
  previous_restore_signature="$(cat "$restore_signature_file" 2>/dev/null || true)"

  if [ "$current_restore_signature" = "$previous_restore_signature" ]; then
    echo "[dotnet] Dependency inputs unchanged; skipping restore."
    return
  fi

  echo "[dotnet] Restoring the dev host graph once, without parallel downloads."
  cd "$dotnet_root"
  dotnet restore "$dev_host_project" \
    --disable-parallel \
    --property:UseArtifactsOutput=true \
    --property:ArtifactsPath="$artifacts_root"
  printf '%s' "$current_restore_signature" > "$restore_signature_file"
}

build_dev_graph() {
  project="${1:-$dev_host_project}"
  dependency_argument=''
  build_server_argument=''
  if [ "$project" != "$dev_host_project" ]; then
    dependency_argument='--no-dependencies'
  fi
  if [ "${DOTNET_DISABLE_BUILD_SERVERS:-false}" = 'true' ]; then
    build_server_argument='--disable-build-servers'
  fi
  echo "[dotnet] Building $project serially into $artifacts_root."
  cd "$dotnet_root"
  dotnet build "$project" \
    --no-restore \
    $dependency_argument \
    $build_server_argument \
    --maxcpucount:1 \
    --property:BuildInParallel=false \
    --property:ExcludeAppSettingsFromOutput=true \
    --property:UseArtifactsOutput=true \
    --property:ArtifactsPath="$artifacts_root" \
    --verbosity:minimal
}

touch_service_stamps() {
  mkdir -p "$artifacts_root/run-stamps"
  for assembly in $service_assemblies; do
    touch "$artifacts_root/run-stamps/$assembly"
  done
}

ensure_service_stamps() {
  mkdir -p "$artifacts_root/run-stamps"
  for assembly in $service_assemblies; do
    stamp="$artifacts_root/run-stamps/$assembly"
    if [ ! -f "$stamp" ]; then
      touch "$stamp"
    fi
  done
}

touch_service_stamp() {
  assembly="${1:?service assembly is required}"
  mkdir -p "$artifacts_root/run-stamps"
  touch "$artifacts_root/run-stamps/$assembly"
}

all_service_outputs_exist() {
  for assembly in $service_assemblies; do
    if [ ! -f "$artifacts_root/bin/$assembly/debug/$assembly.dll" ]; then
      return 1
    fi
  done
  return 0
}
