#!/bin/sh

set -eu

if [ "$PWD" != "/app" ]; then
  echo "[frontend] Expected /app as the working directory, got $PWD." >&2
  exit 1
fi

lock_hash="$(sha256sum package.json package-lock.json 2>/dev/null | sha256sum | cut -d' ' -f1)"
if [ ! -f node_modules/.deps-hash ] || [ "$(cat node_modules/.deps-hash)" != "$lock_hash" ]; then
  npm install
  printf '%s' "$lock_hash" > node_modules/.deps-hash
fi

cache_dir=/app/.next
bundler_marker="$cache_dir/.docker-dev-bundler"
bundler_mode=webpack-watchpack-v1
cached_mode="$(cat "$bundler_marker" 2>/dev/null || true)"

if [ "$cached_mode" != "$bundler_mode" ]; then
  echo "[frontend] Clearing an incompatible Next.js dev cache ($cached_mode -> $bundler_mode)."
  rm -rf "$cache_dir"
  mkdir -p "$cache_dir"
  printf '%s' "$bundler_mode" > "$bundler_marker"
fi

exec npm run dev -- --webpack
