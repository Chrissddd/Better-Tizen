set -eu
test "$(id -u)" = 0
A=/opt/usr/apps/com.samsung.tv.bixby-promotion
S=/home/owner/share/tmp/sdk_tools/quiet-native-20261108
test "$(sha256sum "$A/bin/BixbyPromotion.dll" | cut -d' ' -f1)" = 100c808956122692ca9b8cd769d0a2cde88052413992b096ada5d673c0f77ce8
test ! -e "$S/final-update"
mkdir "$S/final-update"
tar -xzf "$S/final-update.tar.gz" -C "$S/final-update"
test "$(sha256sum "$S/final-update/BixbyPromotion.dll" | cut -d' ' -f1)" = 4d9e68c6377ea6ebd7b1335312c63f30479941ddd292c02c901095a2b2e81b83
app_launcher -u 5001 -k com.samsung.tv.bixby-promotion
cp "$S/final-update/BixbyPromotion.dll" "$A/bin/BixbyPromotion.dll"
cp -R "$S/final-update/quiet-home/." "$A/res/quiet-home/"
chown 201:0 "$A/bin/BixbyPromotion.dll"
chmod 644 "$A/bin/BixbyPromotion.dll"
chsmack -a User::Pkg::com.samsung.tv.bixby-promotion::RO "$A/bin/BixbyPromotion.dll"
find "$A/res/quiet-home" -type d -exec chmod 755 {} \;
find "$A/res/quiet-home" -type f -exec chmod 644 {} \;
find "$A/res/quiet-home" -exec chown 201:0 {} \;
find "$A/res/quiet-home" -exec chsmack -a User::Pkg::com.samsung.tv.bixby-promotion::RO {} \;
app_launcher -u 5001 -s com.samsung.tv.bixby-promotion
sleep 2
grep -q '^font_loaded=True$' /tmp/quiet-home-state.txt
grep -q '^library_count=19$' /tmp/quiet-home-state.txt
grep -q '^data_error=$' /tmp/quiet-home-state.txt
echo FINAL_NATIVE_BUILD_STARTED
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeAssign.dll set com.samsung.tv.bixby-promotion
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/QuietBoot.dll enable
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeAssign.dll read
echo HOME_AND_STARTUP_ASSIGNED_TO_NATIVE_QUIET
