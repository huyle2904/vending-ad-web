#!/usr/bin/env bash
set -euo pipefail

DB_NAME="${DB_NAME:-VendingAdDb}"
BACKUP_DIR="${BACKUP_DIR:-/var/opt/mssql/backup}"
TIMESTAMP="$(date -u +%Y%m%d_%H%M%S)"
BACKUP_FILE="${BACKUP_DIR}/${DB_NAME}_Full_${TIMESTAMP}.bak"
RETENTION_DAYS="${RETENTION_DAYS:-30}"

mkdir -p "${BACKUP_DIR}"

/opt/mssql-tools18/bin/sqlcmd \
  -S "${SQLSERVER_HOST:-localhost},${SQLSERVER_PORT:-1433}" \
  -U "${SQLSERVER_USER:-sa}" \
  -P "${SQLSERVER_PASSWORD:?SQLSERVER_PASSWORD is required}" \
  -C \
  -Q "BACKUP DATABASE [${DB_NAME}] TO DISK='${BACKUP_FILE}' WITH FORMAT, INIT, COMPRESSION, NAME='${DB_NAME} Full Backup';"

find "${BACKUP_DIR}" -name "${DB_NAME}_Full_*.bak" -type f -mtime +"${RETENTION_DAYS}" -delete

echo "Backup completed: ${BACKUP_FILE}"
