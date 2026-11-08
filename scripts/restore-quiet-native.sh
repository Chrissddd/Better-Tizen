set -eu
test "$(id -u)" = 0
A=/opt/usr/apps/com.samsung.tv.bixby-promotion
D=/opt/usr/share/quiet-tizen-backups/qn85f-home-20261108
test -s "$D/bixby-promotion-original/bin/BixbyPromotion.dll"
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/QuietBoot.dll dusk
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeAssign.dll restore
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeAssign.dll set DuskLaunch.Dusk
app_launcher -u 5001 -k com.samsung.tv.bixby-promotion || true
cp -a "$D/bixby-promotion-original/bin/BixbyPromotion.dll" "$A/bin/BixbyPromotion.dll"
if test -d "$D/home-native-image"; then
 test ! -e "$A/bin/.native_image"
 mv "$D/home-native-image" "$A/bin/.native_image"
fi
sqlite3 -bail /opt/dbspace/.pkgmgr_parser.db <<SQL
ATTACH '$D/.pkgmgr_parser.db' AS original;
BEGIN IMMEDIATE;
CREATE TEMP TABLE guard(ok INTEGER CHECK(ok=1));
INSERT INTO guard SELECT count(*)=1 FROM original.package_app_info WHERE app_id='com.samsung.tv.bixby-promotion';
INSERT INTO guard SELECT count(*)=1 FROM package_app_info WHERE app_id='com.samsung.tv.bixby-promotion';
UPDATE package_app_info SET
 app_disable=(SELECT app_disable FROM original.package_app_info WHERE app_id='com.samsung.tv.bixby-promotion'),
 app_onboot=(SELECT app_onboot FROM original.package_app_info WHERE app_id='com.samsung.tv.bixby-promotion'),
 app_autorestart=(SELECT app_autorestart FROM original.package_app_info WHERE app_id='com.samsung.tv.bixby-promotion')
WHERE app_id='com.samsung.tv.bixby-promotion';
DROP TABLE guard;
COMMIT;
SQL
app_launcher -u 5001 -s DuskLaunch.Dusk
sha256sum "$A/bin/BixbyPromotion.dll"
echo RESTORED_ORIGINAL_CONTAINER_AND_DUSK_HOME
