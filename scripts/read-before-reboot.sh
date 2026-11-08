printf 'BOOT_ID\n'
cat /proc/sys/kernel/random/boot_id
printf '\nUPTIME\n'
cat /proc/uptime
printf '\nSAVED_HOME_BOOT\n'
for key in db/appfw/home_appid db/appfw/startup_appid db/menu/smart_hub/ontvsetting/auto_start memory/appfw/current_fullscreen_appid; do vconftool get "$key"; done
