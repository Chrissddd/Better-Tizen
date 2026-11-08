set -eu
key() {
 /usr/bin/dbus-send --system --type=method_call --print-reply --dest=org.enlightenment.wm /org/enlightenment/wm org.enlightenment.wm.Test.EventKey uint32:1 string:"$1" >/dev/null
 sleep .1
 /usr/bin/dbus-send --system --type=method_call --print-reply --dest=org.enlightenment.wm /org/enlightenment/wm org.enlightenment.wm.Test.EventKey uint32:0 string:"$1" >/dev/null
 sleep .25
}
app_launcher -u 5001 -s com.samsung.tv.bixby-promotion
sleep 1
echo RESET
cat /tmp/quiet-home-state.txt
key Down
key Right
echo PS5_FOCUSED
cat /tmp/quiet-home-state.txt
