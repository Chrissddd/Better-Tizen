set -eu
key() {
 /usr/bin/dbus-send --system --type=method_call --print-reply --dest=org.enlightenment.wm /org/enlightenment/wm org.enlightenment.wm.Test.EventKey uint32:1 string:"$1" >/dev/null
 sleep .1
 /usr/bin/dbus-send --system --type=method_call --print-reply --dest=org.enlightenment.wm /org/enlightenment/wm org.enlightenment.wm.Test.EventKey uint32:0 string:"$1" >/dev/null
 sleep .25
}
app_launcher -u 5001 -s NuvioTV001.NuvioTV
sleep 1
echo FROM_NUVIO
vconftool get memory/appfw/current_fullscreen_appid
key XF86Home
sleep 1
vconftool get memory/appfw/current_fullscreen_appid | grep 'com.samsung.tv.bixby-promotion'
grep '^page=0$' /tmp/quiet-home-state.txt
app_launcher -u 5001 -s org.tizen.tv-viewer
sleep 1
echo FROM_HDMI4
vconftool get memory/system/source_type
key XF86Home
sleep 1
vconftool get memory/appfw/current_fullscreen_appid | grep 'com.samsung.tv.bixby-promotion'
grep '^page=0$' /tmp/quiet-home-state.txt
enlightenment_info -dump_screen -p /home/owner/share/tmp/sdk_tools/ -n quiet-focused-fixed.png
key Up
enlightenment_info -dump_screen -p /home/owner/share/tmp/sdk_tools/ -n quiet-unfocused-fixed.png
key Right
key Down
key Down
key Down
key Down
sleep .5
enlightenment_info -dump_screen -p /home/owner/share/tmp/sdk_tools/ -n quiet-apps-second-page.png
echo SECOND_APPS_PAGE
grep -E '^(page|library_page|artwork_pending)=' /tmp/quiet-home-state.txt
key XF86Home
sleep .5
echo FINAL_HOME
cat /tmp/quiet-home-state.txt
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/QuietBoot.dll read
cat /proc/sys/kernel/random/boot_id
command -v systemd-run
