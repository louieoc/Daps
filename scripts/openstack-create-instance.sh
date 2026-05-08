#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Required inputs (export before running)
#   OS_IMAGE_ID            - openstack image ID or name
#   OS_FLAVOR_ID           - openstack flavor ID or name
#   OS_NETWORK_ID          - openstack network ID or name
#   OS_KEY_NAME            - keypair name in openstack
# Optional inputs
#   OS_SERVER_NAME         - instance name (default: daps-prod)
#   OS_SERVER_ID           - existing server ID to reuse
#   OS_SECURITY_GROUP_NAME - security group name (default: daps-web-sg)
#   OS_FLOATING_IP_NETWORK - external network name
#   OS_INSTANCE_VARS_FILE  - vars file to update with OS_SERVER_ID / OS_SERVER_IP

OS_SERVER_NAME="${OS_SERVER_NAME:-daps-prod}"
OS_SERVER_ID="${OS_SERVER_ID:-}"
SECURITY_GROUP_NAME="${OS_SECURITY_GROUP_NAME:-daps-web-sg}"
FLOATING_IP_NETWORK="${OS_FLOATING_IP_NETWORK:-}"
OS_INSTANCE_VARS_FILE="${OS_INSTANCE_VARS_FILE:-}"

required_vars=(OS_IMAGE_ID OS_FLAVOR_ID OS_NETWORK_ID OS_KEY_NAME)
for var in "${required_vars[@]}"; do
  if [[ -z "${!var:-}" ]]; then
    echo "Missing required env var: $var" >&2
    exit 1
  fi
done

ensure_security_group() {
  if ! openstack security group show "$SECURITY_GROUP_NAME" >/dev/null 2>&1; then
    openstack security group create "$SECURITY_GROUP_NAME" >/dev/null
    openstack security group rule create --proto tcp --dst-port 22 "$SECURITY_GROUP_NAME" >/dev/null
    openstack security group rule create --proto tcp --dst-port 80 "$SECURITY_GROUP_NAME" >/dev/null
    openstack security group rule create --proto tcp --dst-port 443 "$SECURITY_GROUP_NAME" >/dev/null
  fi
}

resolve_existing_server() {
  local matches newest
  matches=$(openstack server list --name "$OS_SERVER_NAME" -f value -c ID | wc -l | tr -d ' ')
  if [[ "$matches" == "0" ]]; then
    return 1
  fi

  if [[ "$matches" == "1" ]]; then
    OS_SERVER_ID=$(openstack server list --name "$OS_SERVER_NAME" -f value -c ID)
    echo "Reusing existing server: $OS_SERVER_ID"
    return 0
  fi

  echo "Multiple servers named '$OS_SERVER_NAME' exist. Picking the newest by creation time." >&2
  newest=$(for id in $(openstack server list --name "$OS_SERVER_NAME" -f value -c ID); do
    created=$(openstack server show "$id" -f value -c created)
    echo "$created $id"
  done | sort | tail -n 1 | awk '{print $2}')

  if [[ -z "$newest" ]]; then
    echo "Unable to determine newest server. Set OS_SERVER_ID manually." >&2
    openstack server list --name "$OS_SERVER_NAME"
    exit 1
  fi

  OS_SERVER_ID="$newest"
  echo "Selected server: $OS_SERVER_ID"
  return 0
}

create_server_cli() {
  local server_create_args=(
    --image "$OS_IMAGE_ID"
    --flavor "$OS_FLAVOR_ID"
    --key-name "$OS_KEY_NAME"
    --security-group "$SECURITY_GROUP_NAME"
    --user-data "${script_dir}/openstack-cloud-init.yaml"
    -f value -c id
  )

  server_create_args+=(--network "$OS_NETWORK_ID")

  OS_SERVER_ID=$(openstack server create \
    "${server_create_args[@]}" \
    "$OS_SERVER_NAME")
}

wait_for_server_ip() {
  local attempts addresses ip

  for attempts in $(seq 1 30); do
    addresses=$(openstack server show "$OS_SERVER_ID" -f value -c addresses 2>/dev/null || true)
    ip=$(python3 - <<'PY' "$addresses"
import re
import sys
text = sys.argv[1]
match = re.search(r'(\d+\.\d+\.\d+\.\d+)', text)
print(match.group(1) if match else "")
PY
)

    if [[ -n "$ip" ]]; then
      echo "$ip"
      return 0
    fi

    sleep 2
  done

  return 1
}

upsert_export_var() {
  local file_path="$1"
  local var_name="$2"
  local var_value="$3"

  python3 - <<'PY' "$file_path" "$var_name" "$var_value"
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
name = sys.argv[2]
value = sys.argv[3]
line = f'export {name}="{value}"'

if path.exists():
    lines = path.read_text().splitlines()
else:
    lines = []

updated = False
for idx, existing in enumerate(lines):
    if existing.startswith(f"export {name}="):
        lines[idx] = line
        updated = True
        break

if not updated:
    if lines and lines[-1] != "":
        lines.append("")
    lines.append(line)

path.write_text("\n".join(lines) + "\n")
PY
}

persist_instance_details() {
  local ip_address="$1"

  if [[ -z "$OS_INSTANCE_VARS_FILE" ]]; then
    return 0
  fi

  upsert_export_var "$OS_INSTANCE_VARS_FILE" "OS_SERVER_ID" "$OS_SERVER_ID"
  upsert_export_var "$OS_INSTANCE_VARS_FILE" "OS_SERVER_IP" "$ip_address"
}

ensure_security_group

if [[ -z "$OS_SERVER_ID" ]]; then
  if ! resolve_existing_server; then
    create_server_cli
  fi
fi

if [[ -n "$FLOATING_IP_NETWORK" ]]; then
  FLOATING_IP=$(openstack floating ip create "$FLOATING_IP_NETWORK" -f value -c floating_ip_address)
  openstack server add floating ip "$OS_SERVER_ID" "$FLOATING_IP"
  echo "Floating IP: $FLOATING_IP"
  persist_instance_details "$FLOATING_IP"
else
  echo "Skipping floating IP allocation (no OS_FLOATING_IP_NETWORK set)."
  SERVER_IP=$(wait_for_server_ip || true)
  if [[ -n "${SERVER_IP:-}" ]]; then
    echo "Server IP: $SERVER_IP"
    persist_instance_details "$SERVER_IP"
  else
    echo "Unable to determine server IP yet."
  fi
fi

echo "Server: $OS_SERVER_NAME ($OS_SERVER_ID)"
