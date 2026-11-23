#!/usr/bin/env bash
# Installs the rgb3210 display apps for the current user and the udev rule (needs sudo for the rule).
# Apps are published self-contained: a user service does not see DOTNET_ROOT of a mise/user dotnet.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
app_dir="$here/.."
lib_dir="$HOME/.local/lib/rgb3210"
unit_dir="$HOME/.config/systemd/user"

case "$(uname -m)" in
	x86_64) rid=linux-x64 ;;
	aarch64) rid=linux-arm64 ;;
	*) echo "Unsupported architecture: $(uname -m)" >&2; exit 1 ;;
esac

for app in CpuGraph MemGraph BtcGraph; do
	dotnet publish "$app_dir/RW.LedDisplay.$app" -c Release -r "$rid" --self-contained -o "$lib_dir"
done

install -Dm644 "$here/rgb3210@.service" "$unit_dir/rgb3210@.service"
mkdir -p "$HOME/.config/rgb3210"
systemctl --user daemon-reload

sudo install -Dm644 "$here/70-rgb3210.rules" /etc/udev/rules.d/70-rgb3210.rules
sudo udevadm control --reload
# Re-run the rule for displays that are already plugged in
sudo udevadm trigger --action=add --subsystem-match=tty

echo "Installed. Displays: ls /dev/rgb3210/; services: systemctl --user list-units 'rgb3210@*'"
echo "Per-display settings: $here/rgb3210.conf.example -> ~/.config/rgb3210/<serial>.conf"
