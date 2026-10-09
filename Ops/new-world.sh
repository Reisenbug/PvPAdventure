#!/usr/bin/env bash
set -euo pipefail

base=/home/ubuntu/tmodloader
release="$base/releases/independent-20261005"
config="$release/serverconfig.txt"
exec 9>"$base/.new-world.lock"
flock -n 9 || { echo 'Another world replacement is running.' >&2; exit 1; }
sudo -n true
cd "$release"
sudo -n docker compose -p pvpa-independent config -q

stamp=$(date +%Y%m%d-%H%M%S)
world_name="Independent-$stamp"
old_world=$(basename "$(awk -F= '$1 == "world" {print substr($0, 7)}' "$config")")
[[ "$old_world" == *.wld && -f "$base/world/$old_world" ]] ||
  { echo 'Current world file was not found; nothing was changed.' >&2; exit 1; }
old_name="${old_world%.wld}"
backup="$base/backups/previous-world"
pending=$(mktemp -d "$base/backups/.previous-world.XXXXXX")
cp -a "$config" "$pending/serverconfig.txt"
echo "Saving $old_name. Only the previous world will be kept."
sudo -n docker update --restart=no tmodloader >/dev/null
trap 'sudo -n docker update --restart=unless-stopped tmodloader >/dev/null 2>&1 || true' EXIT

if [[ $(sudo -n docker inspect tmodloader --format '{{.State.Status}}') == running ]]; then
  { sleep 1; printf 'save\nexit\n'; sleep 3; } |
    timeout 120 script -q -e -c 'sudo -n docker attach --sig-proxy=false tmodloader' /dev/null
fi
[[ $(sudo -n docker inspect tmodloader --format '{{.State.Status}}') == exited ]] ||
  { echo "Server did not exit normally; world configuration was not changed. Recovery: $pending" >&2; exit 1; }
sudo -n cp -a "$base/world/$old_world" "$pending/"
if [[ -f "$base/world/$old_name.twld" ]]; then
  sudo -n cp -a "$base/world/$old_name.twld" "$pending/"
fi

awk '!/^(world|worldname|autocreate|difficulty|seed|evil)=/' "$config" > "$config.new"
printf 'world=/home/tml/.local/share/Terraria/tModLoader/Worlds/%s.wld\nworldname=%s\nautocreate=1\ndifficulty=0\n' \
  "$world_name" "$world_name" >> "$config.new"
mv "$config.new" "$config"
started_at=$(date -u +%Y-%m-%dT%H:%M:%SZ)
sudo -n docker compose -p pvpa-independent up -d --force-recreate

echo "Generating $world_name: small, classic, random seed and evil."
deadline=$((SECONDS + 300))
while (( SECONDS < deadline )); do
  output=$(sudo -n docker logs --since "$started_at" tmodloader 2>&1)
  if [[ "$output" == *'Server started'* && -f "$base/world/$world_name.wld" ]]; then
    sudo -n rm -rf -- "$backup"
    sudo -n mv -- "$pending" "$backup"
    sudo -n find "$base/world" -maxdepth 1 -type f \
      \( -name '*.wld*' -o -name '*.twld*' \) \
      ! -name "$world_name.wld" ! -name "$world_name.twld" -delete
    if [[ -d "$base/world/Backups" ]]; then
      sudo -n find "$base/world/Backups" -type f -name '*.zip' -delete
    fi
    sudo -n find "$base/backups" -type d \
      \( -name world -o -name 'world-before-*' -o -name '*-new-world' \) \
      -prune -exec rm -rf -- {} +
    echo 'Ready: 111.229.120.30:6767'
    echo "World: $world_name"
    echo "Previous world: $backup/$old_world"
    echo 'SSC character data was kept.'
    exit 0
  fi
  if [[ $(sudo -n docker inspect tmodloader --format '{{.State.Status}}') != running ]]; then
    sudo -n docker logs --tail 30 tmodloader
    echo "Server stopped during world generation. Recovery: $pending" >&2
    exit 1
  fi
  sleep 2
done
sudo -n docker logs --tail 30 tmodloader
echo "Startup not confirmed within 5 minutes. Recovery: $pending" >&2
exit 1
