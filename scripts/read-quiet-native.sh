set -eu
id
find /usr/share/dotnet.tizen -name Tizen.NUI.dll -o -name Tizen.Applications.Common.dll -o -name Tizen.System.dll -o -name Tizen.dll
A=/opt/usr/apps/com.samsung.tv.bixby-promotion
ls -ld "$A" "$A/bin" "$A/res" "$A/bin/.native_image" 2>/dev/null || true
ls -l "$A/bin" 2>/dev/null || true
chsmack "$A/bin/BixbyPromotion.dll" 2>/dev/null || true
cat "$A/bin/BixbyPromotion.runtimeconfig.json" "$A/bin/BixbyPromotion.deps.json" 2>/dev/null || true
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeAssign.dll read
/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/HomeBoot.dll read
for f in /usr/lib/libsource-api.so /usr/lib/libcapi-media-sound-manager-tv.so.0 /usr/lib/libsettings-api.so; do ls -l "$f"; done
