#!/bin/sh

set -u

assembly="${1:?service assembly name is required}"
shift

artifacts_root=/tmp/averia-artifacts
dll="$artifacts_root/bin/$assembly/debug/$assembly.dll"
stamp="$artifacts_root/run-stamps/$assembly"
child_pid=''
stopping=0

stop_child() {
  stopping=1
  if [ -n "$child_pid" ] && kill -0 "$child_pid" 2>/dev/null; then
    kill -TERM "$child_pid" 2>/dev/null || true
    wait "$child_pid" 2>/dev/null || true
  fi
}

trap stop_child INT TERM

while [ "$stopping" -eq 0 ]; do
  while [ ! -f "$dll" ] || [ ! -f "$stamp" ]; do
    echo "[$assembly] Waiting for shared build output."
    sleep 1
  done

  observed_stamp="$(stat -c '%y' "$stamp")"
  echo "[$assembly] Starting $dll."
  dotnet "$dll" "$@" &
  child_pid=$!

  while kill -0 "$child_pid" 2>/dev/null; do
    sleep 1
    current_stamp="$(stat -c '%y' "$stamp" 2>/dev/null || true)"
    if [ "$current_stamp" != "$observed_stamp" ]; then
      echo "[$assembly] New build detected; restarting."
      kill -TERM "$child_pid" 2>/dev/null || true
      break
    fi
  done

  wait "$child_pid" 2>/dev/null || true
  child_pid=''
  if [ "$stopping" -eq 0 ]; then
    sleep 1
  fi
done
