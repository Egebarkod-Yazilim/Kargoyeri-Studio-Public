// k6 smoke test — sanity check.
// Usage: k6 run loadtest/smoke.js
// Tek VU, 30 saniye, 5 RPS hedef.

import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    vus: 1,
    duration: '30s',
    thresholds: {
        http_req_failed: ['rate<0.01'],            // <%1 hata
        http_req_duration: ['p(95)<500'],          // p95 < 500ms
    },
};

const BASE = __ENV.BASE_URL || 'http://localhost:5000';
const API_KEY = __ENV.API_KEY || 'studio-demo-key';

export default function () {
    const headers = { 'X-Api-Key': API_KEY };

    const r1 = http.get(`${BASE}/healthz`);
    check(r1, { 'healthz=200': (r) => r.status === 200 });

    const r2 = http.get(`${BASE}/api/v1/providers`, { headers });
    check(r2, { 'providers=200': (r) => r.status === 200 });

    sleep(1);
}
