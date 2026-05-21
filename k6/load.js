import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

export const options = {
  stages: [
    { duration: '2m', target: 10 },  // ramp up
    { duration: '5m', target: 50 },  // sustained load
    { duration: '2m', target: 50 },  // hold
    { duration: '1m', target: 0 },   // ramp down
  ],
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<2000', 'p(99)<5000'],
    'http_req_duration{endpoint:playback}': ['p(95)<1000'],
    'http_req_duration{endpoint:heartbeat}': ['p(95)<500'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const DEVICE_CODE = __ENV.DEVICE_CODE || 'TAB-01';
const DEVICE_SECRET = __ENV.DEVICE_SECRET || 'dev-secret-TAB-01';

const errorRate = new Rate('errors');

export default function () {
  // Health check (low frequency)
  if (__ITER % 10 === 0) {
    const health = http.get(`${BASE_URL}/health/ready`);
    check(health, { 'health ready': (r) => r.status === 200 });
    errorRate.add(health.status !== 200);
  }

  // Playback state — primary read path
  const playback = http.get(
    `${BASE_URL}/api/mobile/playback-state/${DEVICE_CODE}`,
    {
      headers: { 'X-Device-Secret': DEVICE_SECRET },
      tags: { endpoint: 'playback' },
    }
  );
  check(playback, { 'playback ok': (r) => r.status === 200 || r.status === 404 });
  errorRate.add(playback.status >= 500);

  sleep(0.5);

  // Heartbeat
  const heartbeat = http.post(
    `${BASE_URL}/api/mobile/heartbeat`,
    JSON.stringify({ deviceCode: DEVICE_CODE }),
    {
      headers: { 'Content-Type': 'application/json', 'X-Device-Secret': DEVICE_SECRET },
      tags: { endpoint: 'heartbeat' },
    }
  );
  check(heartbeat, { 'heartbeat ok': (r) => r.status === 200 || r.status === 404 });
  errorRate.add(heartbeat.status >= 500);

  sleep(1);
}
