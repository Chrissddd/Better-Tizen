printf 'DUSK_PROCESS\n'
pgrep -af 'DuskLaunch'
printf '\nDEVTOOLS_PORT\n'
netstat -ntlp 2>/dev/null | grep -E '44995|Dusk|webprocess' | head -15
printf '\nCRASH_FILES\n'
find /opt/usr/share/crash /opt/usr/share/core /opt/share/crash -maxdepth 2 -iname '*Dusk*' 2>/dev/null | head -15
printf '\nHOME_CURRENT\n'
vconftool get memory/appfw/current_fullscreen_appid
