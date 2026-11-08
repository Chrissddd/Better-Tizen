set -eu
test "$(id -u)" = 0
A=/opt/usr/apps/com.samsung.tv.bixby-promotion
D=/opt/usr/share/quiet-tizen-backups/qn85f-home-20261108
S=/home/owner/share/tmp/sdk_tools/quiet-native-20261108
test -s "$D/bixby-promotion-original/bin/BixbyPromotion.dll"
test -s "$S/package.tar.gz"
test ! -e "$S/unpacked"
test ! -e "$A/res/quiet-home"
test "$(sha256sum "$A/bin/BixbyPromotion.dll" | cut -d' ' -f1)" = 4ac11223f42bc520c0101a0ab369e3ac4899b48f9af437afce59d713793974b7
mkdir "$S/unpacked"
tar -xzf "$S/package.tar.gz" -C "$S/unpacked"
test -s "$S/unpacked/BixbyPromotion.dll"
test -s "$S/unpacked/quiet-home/apps.json"
test -s "$S/unpacked/quiet-home/ambient/snoopy-space.mp4"
app_launcher -u 5001 -k com.samsung.tv.bixby-promotion || true
if test -d "$A/bin/.native_image"; then mv "$A/bin/.native_image" "$D/home-native-image"; fi
cp "$S/unpacked/BixbyPromotion.dll" "$A/bin/BixbyPromotion.dll"
cp -R "$S/unpacked/quiet-home" "$A/res/"
chown 201:0 "$A/bin/BixbyPromotion.dll"
chmod 644 "$A/bin/BixbyPromotion.dll"
chsmack -a User::Pkg::com.samsung.tv.bixby-promotion::RO "$A/bin/BixbyPromotion.dll"
find "$A/res/quiet-home" -type d -exec chmod 755 {} \;
find "$A/res/quiet-home" -type f -exec chmod 644 {} \;
find "$A/res/quiet-home" -exec chown 201:0 {} \;
find "$A/res/quiet-home" -exec chsmack -a User::Pkg::com.samsung.tv.bixby-promotion::RO {} \;
sha256sum "$A/bin/BixbyPromotion.dll"
app_launcher -u 5001 -s com.samsung.tv.bixby-promotion
echo MANUAL_NATIVE_LAUNCH_REQUESTED_HOME_UNCHANGED
