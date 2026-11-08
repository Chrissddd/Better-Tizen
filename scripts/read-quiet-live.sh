set -eu
vconftool get memory/appfw/current_fullscreen_appid
cat /tmp/quiet-home-state.txt 2>/dev/null || true
cat /opt/usr/home/owner/apps_rw/com.samsung.tv.bixby-promotion/data/quiet-local.json 2>/dev/null || true
ps -ef | grep -E 'BixbyPromotion|QuietProbe' | grep -v grep || true
enlightenment_info -dump_screen -p /home/owner/share/tmp/sdk_tools/ -n quiet-native-20261108.png 2>&1 || true
