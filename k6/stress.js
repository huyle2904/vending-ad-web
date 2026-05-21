import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

export const options = {
  stages: [
    { duration: '2m', target: 50 },   // ramp to normal load
    { duration: '3m', target: 50 },   // hold normal
    { duration: '2m', target: 100 },  // ramp to high load
    { duration: '3m', target: 100 },  // hold high
    { duration: '2m', target: 200 },  // ramp to breaking point
    { duration: '3m', target: 200 },  // hold at breaking point
    { duration: '2m', target: 0 },    // ramp down
  ],
  thresholds: {
    // stress test — looser thresholds, goal is to find limits
    http_req_failed: ['rate<0.10'],
    http_req_duration: ['p(95)<5000'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const DEVICE_CODE = __ENV.DEVICE_CODE || 'TAB-01';
const DEVICE_SECRET = __ENV.DEVICE_SECRET || 'dev-secret-TAB-01';

const errorRate = new Rate('errors');

export default function () {
  const playback = http.get(
    `${BASE_URL}/api/mobile/playback-state/${DEVICE_CODE}`,
    { headers: { 'X-Device-Secret': DEVICE_SECRET } }
  );
  check(playback, { 'playback ok': (r) => r.status === 200 || r.status === 404 });
  errorRate.add(playback.status >= 500);

  sleep(0.5);

  const heartbeat = http.post(
    `${BASE_URL}/api/mobile/heartbeat`,
    JSON.stringify({ deviceCode: DEVICE_CODE }),
    { headers: { 'Content-Type': 'application/json', 'X-Device-Secret': DEVICE_SECRET } }
  );
  check(heartbeat, { 'heartbeat ok': (r) => r.status === 200 || r.status === 404 });
  errorRate.add(heartbeat.status >= 500);

  sleep(0.5);
}
