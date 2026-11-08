set -eu
test "$(id -u)" = 0
A=/opt/usr/apps/com.samsung.tv.bixby-promotion
D=/opt/usr/share/quiet-tizen-backups/qn85f-home-20261108
S=/home/owner/share/tmp/sdk_tools/quiet-native-20261108
test -s "$D/bixby-promotion-original/bin/BixbyPromotion.dll"
test "$(sha256sum "$A/bin/BixbyPromotion.dll" | cut -d' ' -f1)" = 67c882478f00987bc872f614d12198f15c776a8c9756be192476392aa29c5267
test ! -e "$S/update"
mkdir "$S/update"
tar -xzf "$S/update.tar.gz" -C "$S/update"
test "$(sha256sum "$S/update/BixbyPromotion.dll" | cut -d' ' -f1)" = 100c808956122692ca9b8cd769d0a2cde88052413992b096ada5d673c0f77ce8
cp /home/owner/share/tmp/sdk_tools/restore-quiet-native.sh "$D/restore-quiet-native.sh"
app_launcher -u 5001 -k com.samsung.tv.bixby-promotion
cp "$S/update/BixbyPromotion.dll" "$A/bin/BixbyPromotion.dll"
cp -R "$S/update/quiet-home/." "$A/res/quiet-home/"
chown 201:0 "$A/bin/BixbyPromotion.dll"
chmod 644 "$A/bin/BixbyPromotion.dll"
chsmack -a User::Pkg::com.samsung.tv.bixby-promotion::RO "$A/bin/BixbyPromotion.dll"
find "$A/res/quiet-home" -type d -exec chmod 755 {} \;
find "$A/res/quiet-home" -type f -exec chmod 644 {} \;
find "$A/res/quiet-home" -exec chown 201:0 {} \;
find "$A/res/quiet-home" -exec chsmack -a User::Pkg::com.samsung.tv.bixby-promotion::RO {} \;
sha256sum "$A/bin/BixbyPromotion.dll"
app_launcher -u 5001 -s com.samsung.tv.bixby-promotion
echo ART_UPDATE_LAUNCHED_HOME_UNCHANGED
