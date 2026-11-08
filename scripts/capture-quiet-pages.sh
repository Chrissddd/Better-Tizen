set -eu
key() {
 /usr/bin/dbus-send --system --type=method_call --print-reply --dest=org.enlightenment.wm /org/enlightenment/wm org.enlightenment.wm.Test.EventKey uint32:1 string:"$1" >/dev/null
 sleep .1
 /usr/bin/dbus-send --system --type=method_call --print-reply --dest=org.enlightenment.wm /org/enlightenment/wm org.enlightenment.wm.Test.EventKey uint32:0 string:"$1" >/dev/null
 sleep .25
}
app_launcher -u 5001 -s com.samsung.tv.bixby-promotion
sleep 1
enlightenment_info -dump_screen -p /home/owner/share/tmp/sdk_tools/ -n quiet-home-final.png
echo HOME
cat /tmp/quiet-home-state.txt
key Up
key Right
sleep 1
enlightenment_info -dump_screen -p /home/owner/share/tmp/sdk_tools/ -n quiet-apps-final.png
echo APPS
cat /tmp/quiet-home-state.txt
key Right
sleep 1
enlightenment_info -dump_screen -p /home/owner/share/tmp/sdk_tools/ -n quiet-settings-final.png
echo SETTINGS
cat /tmp/quiet-home-state.txt
app_launcher -u 5001 -s com.samsung.tv.bixby-promotion
