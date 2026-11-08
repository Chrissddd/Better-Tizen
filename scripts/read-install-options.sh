printf 'INSTALL_TOOLS\n'
command -v wascmd pkgcmd wrt-installer wgt-installer
printf '\nWASCMD_OPTIONS\n'
wascmd --help 2>&1 | head -90
printf '\nPKGCMD_OPTIONS\n'
pkgcmd --help 2>&1 | head -100
printf '\nFIRSTSCREEN_VCONF\n'
vconftool get db/menu/first_screen/first_screen_type 2>&1
vconftool get db/menu/first_screen/last_app 2>&1
printf '\nNETWORK_REMOTE_CLIENTS\n'
ls -la /opt/share/webremote /opt/usr/share/webremote 2>/dev/null
