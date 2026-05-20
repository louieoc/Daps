#!/usr/bin/env bash
# Shared secrets generation logic for Grist DAPS projects.
# Do not run directly — source this from prerequisites.dev.sh or prerequisites.prod.sh
# after setting SECRETS_DIR and FORCE.

ENV_FILE="$SECRETS_DIR/.env"

generate_secret() {
    openssl rand -base64 48 | tr -d '\r\n'
}

mkdir -p "$SECRETS_DIR"
chmod 700 "$SECRETS_DIR"

if [ "$FORCE" -eq 1 ] || [ ! -f "$ENV_FILE" ]; then
    echo "GRIST_SESSION_SECRET=$(generate_secret)" > "$ENV_FILE"
    chmod 644 "$ENV_FILE"
    echo "  generated: .env (GRIST_SESSION_SECRET)"
else
    echo "  secrets already present; skipping (use --force to regenerate)"
fi
