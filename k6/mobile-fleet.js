import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate } from 'k6/metrics';

const DEVICE_COUNT = parseInt(__ENV.DEVICE_COUNT || '50', 10);
const DEVICE_PREFIX = __ENV.DEVICE_PREFIX || 'TAB-';
const DEVICE_SECRET_PREFIX = __ENV.DEVICE_SECRET_PREFIX || 'dev-secret-';
const DEVICE_PAD_WIDTH = parseInt(__ENV.DEVICE_PAD_WIDTH || '2', 10);
const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const PLAYBACK_INTERVAL_SECONDS = parseFloat(__ENV.PLAYBACK_INTERVAL_SECONDS || '15');
const HEARTBEAT_INTERVAL_SECONDS = parseFloat(__ENV.HEARTBEAT_INTERVAL_SECONDS || '30');
const INCLUDE_HEALTH_CHECKS = (__ENV.INCLUDE_HEALTH_CHECKS || 'true').toLowerCase() !== 'false';

const playbackErrors = new Rate('playback_errors');
const heartbeatErrors = new Rate('heartbeat_errors');
const devicesSeen = new Counter('devices_seen');

export const options = {
  scenarios: {
    fleet_polling: {
      executor: 'constant-vus',
      vus: DEVICE_COUNT,
      duration: __ENV.DURATION || '10m',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<2000', 'p(99)<5000'],
    'http_req_duration{endpoint:playback}': ['p(95)<1000'],
    'http_req_duration{endpoint:heartbeat}': ['p(95)<500'],
    playback_errors: ['rate<0.05'],
    heartbeat_errors: ['rate<0.05'],
  },
};

export default function () {
  const deviceCode = deviceCodeForVu(__VU);
  const deviceSecret = deviceSecretForDevice(deviceCode);
  devicesSeen.add(1, { device: deviceCode });

  if (INCLUDE_HEALTH_CHECKS && __ITER % 20 === 0 && __VU === 1) {
    const health = http.get(`${BASE_URL}/health/ready`, { tags: { endpoint: 'health' } });
    check(health, { 'health ready': (r) => r.status === 200 });
  }

  const playback = http.get(`${BASE_URL}/api/mobile/playback-state/${deviceCode}`, {
    headers: { 'X-Device-Secret': deviceSecret },
    tags: { endpoint: 'playback' },
  });
  check(playback, {
    'playback accepted': (r) => r.status === 200 || r.status === 404,
    'playback not unauthorized': (r) => r.status !== 401,
  });
  playbackErrors.add(playback.status >= 500 || playback.status === 401);

  sleep(Math.max(0.1, PLAYBACK_INTERVAL_SECONDS));

  const heartbeat = http.post(
    `${BASE_URL}/api/mobile/heartbeat`,
    JSON.stringify({ deviceCode }),
    {
      headers: {
        'Content-Type': 'application/json',
        'X-Device-Secret': deviceSecret,
      },
      tags: { endpoint: 'heartbeat' },
    }
  );
  check(heartbeat, {
    'heartbeat accepted': (r) => r.status === 200 || r.status === 404,
    'heartbeat not unauthorized': (r) => r.status !== 401,
  });
  heartbeatErrors.add(heartbeat.status >= 500 || heartbeat.status === 401);

  sleep(Math.max(0.1, HEARTBEAT_INTERVAL_SECONDS));
}

function deviceCodeForVu(vu) {
  const index = ((vu - 1) % DEVICE_COUNT) + 1;
  return `${DEVICE_PREFIX}${String(index).padStart(DEVICE_PAD_WIDTH, '0')}`;
}

function deviceSecretForDevice(deviceCode) {
  return `${DEVICE_SECRET_PREFIX}${deviceCode}`;
}
