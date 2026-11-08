printf 'APP_CONTROL_FILES\n'
ls -la /etc/app-control /opt/dbspace | head -75
printf '\nHOME_PACKAGES\n'
pkgcmd -l | grep -Ei 'name \[(.*home.*|.*hub.*|.*browser.*|.*netflix.*|.*spotify.*|.*plex.*)\]'
printf '\nAUTOSTART_VCONF\n'
vconftool list db 2>&1 | grep -Ei 'home|autostart|autorun|boot|last.*app' | head -80
printf '\nDOTNET_HOST\n'
ls -la /usr/share/dotnet /usr/share/dotnet.tizen
printf '\nAPP_CONTROL_READ\n'
for f in /etc/app-control/*; do echo "$f"; head -35 "$f" 2>/dev/null; done
printf '\nSYSTEM_HASHES\n'
sha256sum /etc/passwd /usr/share/sdbd/public.pem
