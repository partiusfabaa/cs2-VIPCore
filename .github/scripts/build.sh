#!/usr/bin/env bash
set -euo pipefail

props=(-c Release)
if [ -n "${VERSION:-}" ]; then
  props+=("-p:Version=$VERSION")
fi

projects=()
if [ -n "${MODULES:-}" ]; then
  for m in $MODULES; do projects+=(modules/"$m"/*.csproj); done
else
  projects=(modules/*/*.csproj)
fi

out="$PWD/out"
publish="$out/publish"
rm -rf "$out"
mkdir -p "$publish"

rm -rf modules/*/build modules/*/bin modules/*/obj

dotnet publish VipCoreApi/VipCoreApi.csproj "${props[@]}" -o "$publish/VipCoreApi"
if [ -z "${MODULES:-}" ]; then
  dotnet publish VIPCore/VIPCore.csproj "${props[@]}" -o "$publish/VIPCore"
fi

cp "$publish/VipCoreApi/VipCoreApi.dll" VipCoreApi.dll

failed=()
for proj in "${projects[@]}"; do
  name=$(basename "$proj" .csproj)
  echo "::group::$name"
  if ! dotnet publish "$proj" "${props[@]}" -o "$publish/modules/$name"; then
    failed+=("$name")
    echo "::error::$name failed to build"
  fi
  echo "::endgroup::"
done
if [ ${#failed[@]} -ne 0 ]; then
  echo "Failed modules: ${failed[*]}"
  exit 1
fi

mapfile -t provided < <(cd "$publish/VipCoreApi" && ls | grep -v '^VipCoreApi\.')
strip_provided() {
  for f in "${provided[@]}"; do rm -rf "${1:?}/$f"; done
}

if [ -z "${MODULES:-}" ]; then
  pkg="$out/pkg/core/addons/counterstrikesharp"
  mkdir -p "$pkg/plugins" "$pkg/shared/VipCoreApi" "$pkg/configs/plugins/VIPCore/Modules"
  strip_provided "$publish/VIPCore"
  rm -f "$publish/VIPCore"/VipCoreApi.*
  cp -r "$publish/VIPCore" "$pkg/plugins/VIPCore"
  cp -r VIPCore/lang "$pkg/plugins/VIPCore/lang"
  cp "$publish/VipCoreApi"/VipCoreApi.{dll,pdb,deps.json} "$pkg/shared/VipCoreApi/"
  (cd "$out/pkg/core" && zip -qr "$out/VIPCore.zip" addons)
fi

for dir in "$publish/modules"/*/; do
  strip_provided "$dir"
  rm -f "$dir"/VipCoreApi.*
done
(cd "$publish/modules" && zip -qr "$out/VIP_Modules.zip" .)

echo "Packed:"
ls -la "$out"/*.zip
