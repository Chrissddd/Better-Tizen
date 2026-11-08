set -eu
test "$(id -u)" = 0
A=/opt/usr/apps/com.samsung.tv.bixby-promotion
D=/opt/usr/share/quiet-tizen-backups/qn85f-home-20261108
S=/home/owner/share/tmp/sdk_tools
test "$(sha256sum "$A/bin/BixbyPromotion.dll" | cut -d' ' -f1)" = 4d9e68c6377ea6ebd7b1335312c63f30479941ddd292c02c901095a2b2e81b83
test "$(sha256sum "$S/QuietHomeFinal.dll" | cut -d' ' -f1)" = 32363e6738a82d67f6e62be3c36ab177c9349e45e6fc1e7724d61f260a5f8479
cp "$S/restore-quiet-native.sh" "$D/restore-quiet-native.sh"
app_launcher -u 5001 -k com.samsung.tv.bixby-promotion
cp "$S/QuietHomeFinal.dll" "$A/bin/BixbyPromotion.dll"
chown 201:0 "$A/bin/BixbyPromotion.dll"
chmod 644 "$A/bin/BixbyPromotion.dll"
chsmack -a User::Pkg::com.samsung.tv.bixby-promotion::RO "$A/bin/BixbyPromotion.dll"
app_launcher -u 5001 -s com.samsung.tv.bixby-promotion
sleep 2
grep -q '^library_count=19$' /tmp/quiet-home-state.txt
/usr/bin/dotnet "$S/HomeAssign.dll" read | grep '^MAIN_ASSIGNED=DuskLaunch.Dusk$'
/usr/bin/dotnet "$S/HomeAssign.dll" restore
/usr/bin/dotnet "$S/HomeAssign.dll" set com.samsung.tv.bixby-promotion
/usr/bin/dotnet "$S/QuietBoot.dll" enable
/usr/bin/dotnet "$S/HomeAssign.dll" read
sha256sum "$A/bin/BixbyPromotion.dll"
echo NATIVE_QUIET_HOME_STARTUP_AND_FOCUS_REPAIR_VERIFIED
