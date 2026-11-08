set -eu
cat /proc/sys/kernel/random/boot_id
cat /tmp/quiet-home-state.txt
ps -ef | grep BixbyPromotion | grep -v grep
enlightenment_info -dump_screen -p /home/owner/share/tmp/sdk_tools/ -n quiet-cold-boot.png
