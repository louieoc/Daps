#!/usr/bin/env bash
#
# Reports a remote host's resource usage as tab-separated records on stdout.
#
# Run by `dapsman prod system`. The toolkit container pipes this file to `bash -s` over SSH,
# so nothing is written to the remote host and the workflow stays read-only. Because the
# script itself arrives on stdin, no command in here may read stdin -- doing so would consume
# the rest of the script.
#
# Usage: remote-system-status.sh <docker-command> <verbose:1|0> <projects-root>
#
# Output is one record per line: <key><TAB><value>[<TAB><value>...]. Repeated keys
# (container, project, docker_disk) form a list, which is why this is not a key/value map.
#
# This script deliberately does no arithmetic, no unit conversion, and no percentages. Bash
# has no floats without bc, which is not guaranteed to be installed. Values are reported in
# whatever unit the source gives them and Dapsman converts in C#: collect here, interpret
# there. It uses only coreutils and /proc, so it needs nothing installed on the host.
#
# Every optional section is individually fault-tolerant. A host with no Docker, or with no
# projects deployed yet, loses that section rather than failing the whole command.

set -euo pipefail

docker_cmd="${1:-docker}"
verbose="${2:-0}"
projects_root="${3:-/srv/projects}"

# -n so sudo fails rather than prompting. A prompt here would try to read the password from
# stdin, which is the remainder of this script.
sudo_prefix=""
if [ "$(id -u)" -ne 0 ]; then
	sudo_prefix="sudo -n"
fi

printf 'hostname\t%s\n' "$(hostname)"
printf 'arch\t%s\n' "$(uname -m)"

# The platform Docker runs images as -- "linux/amd64" -- which is what an image built for this
# host must target. Not derived from uname: the kernel and the Docker daemon can disagree (a
# 64-bit kernel running a 32-bit userland), and the daemon is the one that will execute the image.
#
# This is the one summary reading that needs Docker, but `2>/dev/null || true` means a host
# without it reports an empty value rather than failing, so the section still cannot break on a
# missing tool. Keep it here: `dapsman prod deploy` collects a non-verbose reading to find out
# what to build for, so moving this into the verbose block below would silently break that.
printf 'docker_platform\t%s\n' "$($docker_cmd version --format '{{.Server.Os}}/{{.Server.Arch}}' 2>/dev/null || true)"

printf 'cores\t%s\n' "$(nproc)"

read -r load1 load5 load15 _ < /proc/loadavg
printf 'loadavg\t%s\t%s\t%s\n' "$load1" "$load5" "$load15"

while read -r key value _; do
	case "$key" in
		MemTotal:)     printf 'mem_total_kb\t%s\n' "$value" ;;
		MemAvailable:) printf 'mem_available_kb\t%s\n' "$value" ;;
		SwapTotal:)    printf 'swap_total_kb\t%s\n' "$value" ;;
		SwapFree:)     printf 'swap_free_kb\t%s\n' "$value" ;;
	esac
done < /proc/meminfo

df -P -B1 / | awk 'NR==2 {
	printf "disk_total_bytes\t%s\ndisk_used_bytes\t%s\ndisk_free_bytes\t%s\n", $2, $3, $4
}'

read -r uptime_seconds _ < /proc/uptime
printf 'uptime_seconds\t%s\n' "$uptime_seconds"

# Written by unattended-upgrades when an installed update (usually a kernel) needs a restart
# to take effect.
if [ -e /var/run/reboot-required ]; then
	printf 'reboot_required\t1\n'
else
	printf 'reboot_required\t0\n'
fi

if [ "$verbose" != "1" ]; then
	exit 0
fi

# Pipe-delimited rather than tab-delimited from Docker, then converted: docker's --format
# handling of \t varies by version, whereas '|' is passed through literally. Neither container
# names nor image sizes can contain '|'.
$docker_cmd stats --no-stream --format '{{.Name}}|{{.CPUPerc}}|{{.MemUsage}}' 2>/dev/null \
	| awk -F'|' '{ printf "container\t%s\t%s\t%s\n", $1, $2, $3 }' || true

$docker_cmd system df --format '{{.Type}}|{{.Size}}|{{.Reclaimable}}' 2>/dev/null \
	| awk -F'|' '{ printf "docker_disk\t%s\t%s\t%s\n", $1, $2, $3 }' || true

if [ -d "$projects_root" ]; then
	for dir in "$projects_root"/*/; do
		[ -d "$dir" ] || continue
		name="$(basename "$dir")"
		size="$($sudo_prefix du -sb "$dir" 2>/dev/null | cut -f1)" || true
		if [ -n "${size:-}" ]; then
			printf 'project\t%s\t%s\n' "$name" "$size"
		fi
	done
fi
