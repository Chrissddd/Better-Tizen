printf 'DUSK_INSTALL\n'
pkgcmd -l -t wgt | grep -F Dusk
printf '\nDUSK_PROCESSES\n'
ps -ef | grep -E 'DuskLaunch|wrt-loader|webprocess' | grep -v grep
