printf 'TVHISTORY_SCHEMA\n'
sqlite3 -readonly /opt/usr/home/owner/share/tvhistory.db '.schema'
printf '\nHOME_MEDIA_SCHEMA\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tv.home.media/shared/trusted/sqlite.db '.schema'
printf '\nAOC_APP_SCHEMA\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tv.slive-daemon/data/aocAppInfo.db '.schema'
printf '\nVOLT_FILES\n'
find /opt/usr/home/owner/share/voltapps -maxdepth 3 -type f 2>/dev/null | head -80
printf '\nTV_CAPTURE_TOOLS\n'
ls /usr/bin /usr/sbin | grep -Ei 'screenshot|screen.?cap|cap.?screen|enlightenment|e_comp|capture' | head -55
printf '\nPKG_SCHEMA\n'
sqlite3 -readonly /opt/dbspace/.pkgmgr_parser.db '.schema package_app_info'
sqlite3 -readonly /opt/dbspace/.pkgmgr_parser.db '.schema package_info'
