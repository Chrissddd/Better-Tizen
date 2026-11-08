printf 'VCONF_KEYS\n'
for key in db/appfw/home_appid db/appfw/startup_appid db/menu/smart_hub/ontvsetting/auto_start db/menu/app_booting db/menu/system/smartfeatures/autorun-defaultapp; do printf '%s\n' "$key"; vconftool get "$key" 2>&1; done
printf '\nAPP_CONTROL_SCHEMA\n'
sqlite3 -readonly /opt/dbspace/.appsvc.db '.schema' 2>&1
printf '\nDEFAULTS\n'
sqlite3 -readonly /opt/dbspace/.appsvc.db 'SELECT * FROM sqlite_master WHERE type="table";' 2>&1
printf '\nCONTEXT_SCHEMA\n'
sqlite3 -readonly /opt/dbspace/.app-context.db '.schema' 2>&1
