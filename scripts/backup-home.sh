printf 'APP_CONTROL_DUMP\n'
sqlite3 -readonly /opt/dbspace/.appsvc.db '.dump'
printf '\nAPP_CONTEXT_DUMP\n'
sqlite3 -readonly /opt/dbspace/.app-context.db '.dump'
printf '\nHOME_AND_STARTUP_VCONF\n'
for key in db/appfw/home_appid db/appfw/startup_appid db/appfw/endup_appid db/menu/smart_hub/ontvsetting/auto_start db/menu/app_booting db/menu/system/smartfeatures/autorun-defaultapp db/first_screen/type db/appfw/is_root_cold_boot; do vconftool get "$key" 2>&1; done
