set -eu
key() {
 /usr/bin/dbus-send --system --type=method_call --print-reply --dest=org.enlightenment.wm /org/enlightenment/wm org.enlightenment.wm.Test.EventKey uint32:1 string:"$1" >/dev/null
 sleep .1
 /usr/bin/dbus-send --system --type=method_call --print-reply --dest=org.enlightenment.wm /org/enlightenment/wm org.enlightenment.wm.Test.EventKey uint32:0 string:"$1" >/dev/null
}
echo AFTER_STANDBY_WITHOUT_MANUAL_LAUNCH
cat /proc/sys/kernel/random/boot_id
vconftool get memory/appfw/current_fullscreen_appid
cat /tmp/quiet-home-state.txt
cat /tmp/quiet-power-check.txt
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeAssign.dll read
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/QuietBoot.dll read
app_launcher -u 5001 -s NuvioTV001.NuvioTV
sleep 1
key XF86Home
sleep 1
vconftool get memory/appfw/current_fullscreen_appid | grep 'com.samsung.tv.bixby-promotion'
grep '^page=0$' /tmp/quiet-home-state.txt
grep '^data_error=$' /tmp/quiet-home-state.txt
sha256sum /opt/usr/apps/com.samsung.tv.bixby-promotion/bin/BixbyPromotion.dll
systemctl stop quiet-standby-check-20261108.timer quiet-standby-check-20261108.service
rm -f /tmp/quiet-power-check.txt /home/owner/share/tmp/sdk_tools/QuietPowerCheck.dll
S=/home/owner/share/tmp/sdk_tools/quiet-native-20261108
rm -rf "$S/unpacked" "$S/update" "$S/final-update"
rm -f "$S/package.tar.gz" "$S/update.tar.gz" "$S/final-update.tar.gz"
echo PRODUCTION_HOME_AFTER_POWER_AND_CLEANUP_VERIFIED
