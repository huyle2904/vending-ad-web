#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"
COOKIE_JAR="$(mktemp)"
TMP_UPLOAD="$(mktemp --suffix=.mp4)"

cleanup() {
  rm -f "${COOKIE_JAR}" "${TMP_UPLOAD}"
}
trap cleanup EXIT

step() {
  printf "\n[%s] %s\n" "$(date -u +%H:%M:%S)" "$1"
}

check_http_ok() {
  local url="$1"
  local label="$2"
  local code

  code="$(curl -sS -o /tmp/smoke-response.txt -w "%{http_code}" "${url}")"
  if [[ "${code}" != "200" && "${code}" != "302" ]]; then
    echo "❌ ${label} failed (HTTP ${code})"
    cat /tmp/smoke-response.txt
    exit 1
  fi

  echo "✅ ${label} (HTTP ${code})"
}

step "1) Health checks"
check_http_ok "${BASE_URL}/health/live" "Live health"
check_http_ok "${BASE_URL}/health/ready" "Ready health"
check_http_ok "${BASE_URL}/metrics" "Metrics endpoint"

step "2) Login page availability"
check_http_ok "${BASE_URL}/account/login" "Login page"

step "3) Home page availability"
check_http_ok "${BASE_URL}/" "Home page"

step "4) Device endpoints availability"
# Use simple availability checks to validate endpoint routing after deploy.
DEVICE_HEARTBEAT_CODE="$(curl -sS -o /tmp/smoke-device-heartbeat.txt -w "%{http_code}" -X POST "${BASE_URL}/api/portal/heartbeat")"
if [[ "${DEVICE_HEARTBEAT_CODE}" != "200" && "${DEVICE_HEARTBEAT_CODE}" != "400" && "${DEVICE_HEARTBEAT_CODE}" != "401" && "${DEVICE_HEARTBEAT_CODE}" != "415" ]]; then
  echo "❌ Device heartbeat returned unexpected HTTP ${DEVICE_HEARTBEAT_CODE}"
  cat /tmp/smoke-device-heartbeat.txt
  exit 1
fi

echo "✅ Device heartbeat reachable (HTTP ${DEVICE_HEARTBEAT_CODE})"

PLAYBACK_STATE_CODE="$(curl -sS -o /tmp/smoke-device-playback.txt -w "%{http_code}" "${BASE_URL}/api/mobile/playback-state/DEVICE-001")"
if [[ "${PLAYBACK_STATE_CODE}" != "200" && "${PLAYBACK_STATE_CODE}" != "400" && "${PLAYBACK_STATE_CODE}" != "401" && "${PLAYBACK_STATE_CODE}" != "404" ]]; then
  echo "❌ Device playback-state returned unexpected HTTP ${PLAYBACK_STATE_CODE}"
  cat /tmp/smoke-device-playback.txt
  exit 1
fi

echo "✅ Device playback-state reachable (HTTP ${PLAYBACK_STATE_CODE})"

step "5) Upload endpoint availability"
printf '\x00\x00\x00\x18ftypmp42\x00\x00\x00\x00mp42isom' > "${TMP_UPLOAD}"
UPLOAD_CODE="$(curl -sS -o /tmp/smoke-upload.txt -w "%{http_code}" -X POST "${BASE_URL}/api/portal/upload" -F "file=@${TMP_UPLOAD};type=video/mp4")"
if [[ "${UPLOAD_CODE}" != "200" && "${UPLOAD_CODE}" != "302" && "${UPLOAD_CODE}" != "400" && "${UPLOAD_CODE}" != "401" && "${UPLOAD_CODE}" != "415" ]]; then
  echo "❌ Upload endpoint returned unexpected HTTP ${UPLOAD_CODE}"
  cat /tmp/smoke-upload.txt
  exit 1
fi

echo "✅ Upload endpoint reachable (HTTP ${UPLOAD_CODE})"

step "6) Schedule page availability"
check_http_ok "${BASE_URL}/portal/schedules" "Schedule page"

echo "\n🎉 Smoke tests completed successfully for ${BASE_URL}"
