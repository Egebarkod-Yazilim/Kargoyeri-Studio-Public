// k6 stress testi — sistemin kirildigi noktayi bul.
// 0 -> 500 VU lineer artis.
//
// Usage:
//   k6 run -e BASE_URL=https://studio-staging.kargoyeri.com -e API_KEY=... loadtest/stress.js

import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    stages: [
        { duration: '2m',  target: 100 },
        { duration: '5m',  target: 100 },
        { duration: '2m',  target: 200 },
        { duration: '5m',  target: 200 },
        { duration: '2m',  target: 300 },
        { duration: '5m',  target: 300 },
        { duration: '2m',  target: 500 },
        { duration: '5m',  target: 500 },
        { duration: '5m',  target: 0 },
    ],
    thresholds: {
        http_req_failed:   ['rate<0.10'],   // 500 VU'da %10 acceptable, <%10 ise OK
        http_req_duration: ['p(95)<5000'],
    },
};

const BASE = __ENV.BASE_URL || 'http://localhost:5000';
const API_KEY = __ENV.API_KEY || 'studio-demo-key';

export default function () {
    const r = http.get(`${BASE}/api/v1/providers`, {
        headers: { 'X-Api-Key': API_KEY },
    });
    check(r, { '2xx or 429': (r) => (r.status >= 200 && r.status < 300) || r.status === 429 });
    sleep(Math.random() * 2);
}
