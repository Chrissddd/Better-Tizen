printf 'ANALYTICS_ACCOUNT_APPS\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tizen.samsung-analytics/data/.samsung_analytics-log.db 'SELECT _id,pkg_id FROM ac_tbl;'
printf '\nAGGREGATE_EVENT_NAMES\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tizen.samsung-analytics/data/.samsung_analytics-log.db 'SELECT a.pkg_id,s.event_name,s.hit_type,COUNT(*),SUM(s.hit_cnt),MAX(s.max_ts) FROM evt_summ_bat_table s LEFT JOIN ac_tbl a ON a._id=s.ac_id GROUP BY a.pkg_id,s.event_name,s.hit_type LIMIT 80;'
printf '\nSTREAM_EVENT_NAMES\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tizen.samsung-analytics/data/.samsung_analytics-log.db 'SELECT a.pkg_id,s.event_name,s.hit_type,COUNT(*),MIN(s.ts),MAX(s.ts) FROM summ_stm_tbl s LEFT JOIN ac_tbl a ON a._id=s.ac_id GROUP BY a.pkg_id,s.event_name,s.hit_type LIMIT 80;'
printf '\nRECENT_BATCH_EVENT_NAMES\n'
sqlite3 -readonly /opt/usr/home/owner/apps_rw/com.samsung.tizen.samsung-analytics/data/.samsung_analytics-log.db 'SELECT a.pkg_id,s.event_name,s.hit_type,COUNT(*),MIN(s.ts),MAX(s.ts) FROM summ_bat_tbl s LEFT JOIN ac_tbl a ON a._id=s.ac_id GROUP BY a.pkg_id,s.event_name,s.hit_type LIMIT 80;'
printf '\nFULLSCREEN_STATE\n'
vconftool get memory/appfw/current_fullscreen_appid
printf '\nCAPTURE_RESULT\n'
enlightenment_info -dump_screen -p /home/owner/share/tmp/sdk_tools/ -n dusk-ui-20261019.png 2>&1
ls -l /home/owner/share/tmp/sdk_tools/dusk-ui-20261019.png
