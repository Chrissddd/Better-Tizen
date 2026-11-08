printf 'CSFT_LOG_SCHEMA\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tv.csft-service/data/log.db '.schema'
printf '\nCSFT_INTERNAL_SCHEMA\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tv.csft-service/data/internal.db '.schema'
printf '\nANALYTICS_SCHEMA\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tizen.samsung-analytics/data/.samsung_analytics-log.db '.schema'
printf '\nSLIVE_PROFILE_SCHEMA\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tv.slive-daemon/data/profile.db '.schema'
printf '\nTV_HISTORY_SAMPLE\n'
sqlite3 -readonly /opt/usr/home/owner/share/tvhistory.db 'SELECT time, data FROM history_coll ORDER BY rowid DESC LIMIT 4;'
printf '\nWEBAPP_HISTORY_SAMPLE\n'
head -4 /opt/share/webappservice/log/was_history.log
printf '\nSMARTLAUNCHER_DATA\n'
head -c 5000 /opt/usr/home/owner/share/voltapps/firstscreen_data/makeYourSmartLauncherData.txt
printf '\nCOMPOSITOR_HELP\n'
enlightenment_info -help 2>&1 | head -70
enlightenment_input_key --help 2>&1 | head -45
