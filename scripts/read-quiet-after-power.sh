set -eu
cat /proc/sys/kernel/random/boot_id
vconftool get memory/appfw/current_fullscreen_appid
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeAssign.dll read
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/QuietBoot.dll read
cat /tmp/quiet-home-state.txt
