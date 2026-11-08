printf 'DOTNET_LOCATIONS\n'
ls -ld /usr/share/dotnet* /usr/lib/dotnet* /opt/usr/share/dotnet* 2>/dev/null
readlink -f /usr/bin/dotnet
find /usr/share /usr/lib -maxdepth 5 \( -name libcoreclr.so -o -name System.Private.CoreLib.dll -o -name Microsoft.NETCore.App.deps.json -o -name '*.runtimeconfig.json' \) 2>/dev/null | head -70
printf '\nAVAILABLE_TOOLS\n'
command -v sqlite3 strings readelf pkgcmd app_launcher aul_test app_config vconftool
printf '\nAPP_LIST\n'
pkgcmd -l 2>&1 | head -90
printf '\nHOME_CONFIG_PATHS\n'
find /opt/dbspace /opt/usr/dbspace /etc -maxdepth 2 -iname '*app*control*' -o -iname '*home*' -o -iname '*autostart*' 2>/dev/null | head -60
