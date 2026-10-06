// k6 spike testi — ani trafik sicrasinda davranis.
// Black Friday, kampanya cikisi senaryosu.

import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    stages: [
        { duration: '10s', target: 10 },
        { duration: '1m',  target: 10 },
        { duration: '10s', target: 500 }, // SPIKE!
        { duration: '3m',  target: 500 },
        { duration: '10s', target: 10 },
        { duration: '3m',  target: 10 },
        { duration: '10s', target: 0 },
    ],
    thresholds: {
        http_req_failed: ['rate<0.15'],  // spike sirasinda %15 hata acceptable
    },
};

const BASE = __ENV.BASE_URL || 'http://localhost:5000';
const API_KEY = __ENV.API_KEY || 'studio-demo-key';

export default function () {
    const r = http.get(`${BASE}/api/v1/providers`, {
        headers: { 'X-Api-Key': API_KEY },
    });
    check(r, { 'response received': (r) => r.status > 0 });
    sleep(0.5);
}
