#!/usr/bin/env bash
set -euo pipefail

before="${1:-}"
if [ -n "$before" ]; then
  range="$before..HEAD"
  changed=$(git diff --name-only "$before" HEAD)
  messages=$(git log --format=%B "$range")
else
  range=""
  changed=$(git diff-tree --root --no-commit-id --name-only -r HEAD)
  messages=$(git log -1 --format=%B)
fi

latest=$(git tag -l 'v*' | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | sed 's/^v//' | sort -V | tail -n1 || true)

full=false
if [ -z "$latest" ] || [ "${FORCE_FULL:-}" = true ]; then
  full=true
elif grep -qE '^VIPCore/(VIPCore|VipCoreApi)/' <<<"$changed"; then
  full=true
elif grep -qiE '\[(major|minor)\]|\[v?[0-9]+\.[0-9]+\.[0-9]+\]' <<<"$messages"; then
  full=true
fi

if [ "$full" = true ]; then
  version=$(bash "$(dirname "$0")/next-version.sh" $range)
  echo "mode=full"
  echo "version=$version"
  echo "tag=v$version"
  echo "modules="
  exit 0
fi

modules=$( (grep -oE '^VIPCore/modules/[^/]+/' <<<"$changed" || true) | cut -d/ -f3 | sort -u | while read -r m; do
  if compgen -G "VIPCore/modules/$m/*.csproj" >/dev/null; then echo "$m"; fi
done | paste -sd' ' -)

if [ -z "$modules" ]; then
  echo "mode=none"
  echo "version="
  echo "tag="
  echo "modules="
  exit 0
fi

n=$(git tag -l "v$latest-modules-*" | sed "s/^v$latest-modules-//" | grep -E '^[0-9]+$' | sort -n | tail -n1 || true)
echo "mode=modules"
echo "version=$latest"
echo "tag=v$latest-modules-$(( ${n:-0} + 1 ))"
echo "modules=$modules"
