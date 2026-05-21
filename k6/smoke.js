import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 1,
  duration: '1m',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<2000'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const DEVICE_CODE = __ENV.DEVICE_CODE || 'TEST-DEVICE-001';
const DEVICE_SECRET = __ENV.DEVICE_SECRET || 'test-secret';

export default function () {
  // Health check
  const health = http.get(`${BASE_URL}/health/ready`);
  check(health, { 'health ready 200': (r) => r.status === 200 });

  // Playback state
  const playback = http.get(`${BASE_URL}/api/mobile/playback-state/${DEVICE_CODE}`, {
    headers: { 'X-Device-Secret': DEVICE_SECRET },
  });
  check(playback, { 'playback state 200 or 404': (r) => r.status === 200 || r.status === 404 });

  // Heartbeat
  const heartbeat = http.post(
    `${BASE_URL}/api/mobile/heartbeat`,
    JSON.stringify({ deviceCode: DEVICE_CODE }),
    { headers: { 'Content-Type': 'application/json', 'X-Device-Secret': DEVICE_SECRET } }
  );
  check(heartbeat, { 'heartbeat 200 or 404': (r) => r.status === 200 || r.status === 404 });

  sleep(1);
}
