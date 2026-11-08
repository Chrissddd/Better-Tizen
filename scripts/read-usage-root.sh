printf 'USAGE_EXACT_PATHS\n'
ls -l /opt/dbspace/.rua.db /opt/usr/dbspace/.sap.db /opt/usr/home/owner/.applications/dbspace/.context-app-history.db 2>/dev/null
printf '\nCSFT_KEYS\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tv.csft-service/data/log.db 'SELECT DISTINCT KEY, VALUE_ENCRYPTED FROM CSFT_KV_TABLE_V4;' | head -60
printf '\nANALYTICS_SCHEMA\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tizen.samsung-analytics/data/.samsung_analytics-log.db '.schema'
printf '\nPROFILE_KEYS\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tv.slive-daemon/data/profile.db 'SELECT profile_name FROM ProfileDataTable;' | head -40
printf '\nCOMPOSITOR_HELP\n'
enlightenment_info -help 2>&1 | head -110
enlightenment_input_key --help 2>&1 | head -45
printf '\nCACHE_FILE_TYPE\n'
ls -l /opt/usr/home/owner/apps_rw/com.samsung.tv.home.media/data/.RecentSectionCache
head -c 600 /opt/usr/home/owner/apps_rw/com.samsung.tv.home.media/data/.RecentSectionCache
