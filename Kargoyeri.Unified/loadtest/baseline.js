// k6 baseline yuk testi.
// Hedef: production trafigine yakin yuk altinda istikrar.
// Profile: 5 dk ramp-up -> 10 dk steady -> 2 dk ramp-down.
// VU = 50, hedef ~50 RPS.
//
// Usage:
//   k6 run -e BASE_URL=https://studio-staging.kargoyeri.com -e API_KEY=... loadtest/baseline.js
//
// Sonuc: stdout + InfluxDB (eger -o influxdb=... ile calistirilirsa)

import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Trend, Counter } from 'k6/metrics';

export const options = {
    stages: [
        { duration: '5m',  target: 50 },
        { duration: '10m', target: 50 },
        { duration: '2m',  target: 0 },
    ],
    thresholds: {
        http_req_failed:   ['rate<0.02'],          // <%2 hata
        http_req_duration: ['p(95)<800', 'p(99)<2000'],
        'http_req_duration{endpoint:list}':   ['p(95)<400'],
        'http_req_duration{endpoint:create}': ['p(95)<1500'],
    },
};

const BASE = __ENV.BASE_URL || 'http://localhost:5000';
const API_KEY = __ENV.API_KEY || 'studio-demo-key';

const createTrend = new Trend('shipment_create_duration');
const createCount = new Counter('shipment_create_total');

function uuid() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = (Math.random() * 16) | 0;
        const v = c === 'x' ? r : (r & 0x3) | 0x8;
        return v.toString(16);
    });
}

const headers = () => ({
    'X-Api-Key': API_KEY,
    'Content-Type': 'application/json',
    'Idempotency-Key': uuid(),
});

const samplePayload = () => JSON.stringify({
    orderReference: `LOAD-${Date.now()}-${Math.floor(Math.random() * 10000)}`,
    provider: 'Aras',
    source: 'Manual',
    currencyCode: 'TRY',
    sender: {
        name: 'k6 Loadtest', phone: '05550000001', city: 'Istanbul',
        district: 'Kadikoy', addressLine1: 'Loadtest Sokak No:1', countryCode: 'TR'
    },
    recipient: {
        name: 'k6 Recipient', phone: '05550000002', city: 'Ankara',
        district: 'Cankaya', addressLine1: 'k6 Caddesi No:5', countryCode: 'TR'
    },
    packages: [{ packageSequence: 1, weight: 1.0, desi: 1.0, description: 'k6 test' }]
});

export default function () {
    group('list-shipments', () => {
        const r = http.get(`${BASE}/api/v1/shipments?page=1&pageSize=20`, {
            headers: headers(),
            tags: { endpoint: 'list' },
        });
        check(r, { 'list 2xx': (r) => r.status >= 200 && r.status < 300 });
    });

    group('create-shipment', () => {
        const r = http.post(`${BASE}/api/v1/shipments`, samplePayload(), {
            headers: headers(),
            tags: { endpoint: 'create' },
        });
        createTrend.add(r.timings.duration);
        createCount.add(1);
        check(r, { 'create 2xx': (r) => r.status >= 200 && r.status < 300 });
    });

    sleep(Math.random() * 2 + 1);
}
