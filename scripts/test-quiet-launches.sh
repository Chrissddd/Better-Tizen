set -eu
key() {
 /usr/bin/dbus-send --system --type=method_call --print-reply --dest=org.enlightenment.wm /org/enlightenment/wm org.enlightenment.wm.Test.EventKey uint32:1 string:"$1" >/dev/null
 sleep .1
 /usr/bin/dbus-send --system --type=method_call --print-reply --dest=org.enlightenment.wm /org/enlightenment/wm org.enlightenment.wm.Test.EventKey uint32:0 string:"$1" >/dev/null
 sleep .25
}
app_launcher -u 5001 -s com.samsung.tv.bixby-promotion
sleep 1
key Down
key Right
echo INITIAL_FOCUS
cat /tmp/quiet-home-state.txt
grep -q '^selected_name=PlayStation 5$' /tmp/quiet-home-state.txt
key Return
sleep 2
echo HDMI4_ACTUAL
vconftool get memory/system/source_type
vconftool get memory/system/source_type | grep 'value = 16 '
vconftool get memory/appfw/current_fullscreen_appid
app_launcher -u 5001 -s com.samsung.tv.bixby-promotion
sleep 1
key Down
key Right
key Right
key Right
key Right
key Right
echo NUVIO_FOCUS
cat /tmp/quiet-home-state.txt
grep -q '^selected_name=Nuvio$' /tmp/quiet-home-state.txt
key Return
sleep 2
echo NUVIO_ACTUAL
vconftool get memory/appfw/current_fullscreen_appid
app_launcher -u 5001 -s com.samsung.tv.bixby-promotion
sleep 1
echo AFTER_RETURN
cat /tmp/quiet-home-state.txt
cat /opt/usr/home/owner/apps_rw/com.samsung.tv.bixby-promotion/data/quiet-local.json
