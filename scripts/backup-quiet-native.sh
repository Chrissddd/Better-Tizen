set -eu
test "$(id -u)" = 0
A=/opt/usr/apps/com.samsung.tv.bixby-promotion
D=/opt/usr/share/quiet-tizen-backups/qn85f-home-20261108
test -s "$A/bin/BixbyPromotion.dll"
test "$(stat -c %u:%g "$A/bin/BixbyPromotion.dll")" = 201:0
test ! -e "$D"
test ! -e "$A/res/quiet-home"
umask 077
mkdir -p "$D"
for name in .pkgmgr_parser .app-context; do
 sqlite3 -readonly "/opt/dbspace/$name.db" ".backup '$D/$name.db'"
 test "$(sqlite3 -readonly "$D/$name.db" 'PRAGMA quick_check;')" = ok
done
sqlite3 -readonly -header -csv /opt/dbspace/.pkgmgr_parser.db "SELECT app_id,app_disable,app_onboot,app_autorestart FROM package_app_info WHERE app_id='com.samsung.tv.bixby-promotion';" > "$D/package-flags-before.csv"
test "$(wc -l < "$D/package-flags-before.csv")" = 2
cp -a "$A" "$D/bixby-promotion-original"
sha256sum "$A/bin/BixbyPromotion.dll" > "$D/original-home.sha256"
chsmack "$A/bin/BixbyPromotion.dll" > "$D/smack-before.txt"
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeAssign.dll read > "$D/home-before.txt"
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeBoot.dll read > "$D/preferences-before.txt"
cp /home/owner/share/tmp/sdk_tools/restore-quiet-native.sh "$D/restore-quiet-native.sh"
tar -czf /home/owner/share/tmp/sdk_tools/quiet-native-original-20261108.tar.gz -C "$D" .
chmod 644 /home/owner/share/tmp/sdk_tools/quiet-native-original-20261108.tar.gz
cat "$D/package-flags-before.csv" "$D/original-home.sha256"
echo ORIGINAL_PACKAGE_AND_DATABASE_BACKUP_SAVED
