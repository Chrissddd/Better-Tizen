printf 'USAGE_DB_PATHS\n'
find /opt/dbspace /opt/usr/dbspace /opt/usr/home/owner /opt/usr/share /opt/share -maxdepth 4 -type f \( -iname '*usage*' -o -iname '*history*' -o -iname '*recent*' -o -iname '*launcher*.db' -o -iname '*app*log*.db' -o -iname '*running*apps*.db' -o -iname '*hub*.db' \) 2>/dev/null | head -100
printf '\nPARSER_TABLES\n'
sqlite3 -readonly /opt/dbspace/.pkgmgr_parser.db '.tables'
printf '\nOWNER_DB_NAMES\n'
find /opt/usr/home/owner -maxdepth 5 -type f -name '*.db' 2>/dev/null | head -90
printf '\nRESOURCED_PATHS\n'
ls -la /opt/usr/dbspace/.resourced* /opt/dbspace/.resourced* /opt/usr/share/resourced* /opt/share/resourced* 2>/dev/null
