#!/usr/bin/env bash
# Shared secrets generation logic for WordPress DAPS projects.
# Do not run directly — source this from prerequisites.sh or prerequisites.prod.sh
# after setting SECRETS_DIR and FORCE.

SECRET_FILES=(
    "db_password.txt"
    "mysql_root_password.txt"
    "auth_key.txt"
    "secure_auth_key.txt"
    "logged_in_key.txt"
    "nonce_key.txt"
    "auth_salt.txt"
    "secure_auth_salt.txt"
    "logged_in_salt.txt"
    "nonce_salt.txt"
)

generate_secret() {
    openssl rand -base64 48 | tr -d '\r\n'
}

mkdir -p "$SECRETS_DIR"
chmod 700 "$SECRETS_DIR"

generated=0
for name in "${SECRET_FILES[@]}"; do
    path="$SECRETS_DIR/$name"
    if [ "$FORCE" -eq 1 ] || [ ! -f "$path" ]; then
        generate_secret > "$path"
        chmod 644 "$path"
        echo "  generated: $name"
        ((generated++)) || true
    fi
done

if [ "$generated" -eq 0 ]; then
    echo "  secrets already present; skipping (use --force to regenerate)"
fi
