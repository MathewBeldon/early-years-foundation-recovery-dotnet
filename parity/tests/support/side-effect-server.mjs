import fs from 'node:fs/promises';
import http from 'node:http';
import path from 'node:path';

const port = Number(process.env.SIDE_EFFECT_SINK_PORT ?? '9090');
const captureDirectory = process.env.SIDE_EFFECT_CAPTURE_DIR ?? '/tmp/side-effects';
let sequence = 0;

await fs.mkdir(captureDirectory, { recursive: true });

async function clearCaptures() {
  const entries = await fs.readdir(captureDirectory, { withFileTypes: true });
  await Promise.all(entries.filter(entry => entry.isFile()).map(entry =>
    fs.unlink(path.join(captureDirectory, entry.name)),
  ));
  sequence = 0;
}

async function listCaptures() {
  const names = (await fs.readdir(captureDirectory))
    .filter(name => name.endsWith('.json'))
    .sort();
  return Promise.all(names.map(async name =>
    JSON.parse(await fs.readFile(path.join(captureDirectory, name), 'utf8')),
  ));
}

async function readBody(request) {
  const chunks = [];
  for await (const chunk of request) chunks.push(chunk);
  return Buffer.concat(chunks).toString('utf8');
}

const server = http.createServer(async (request, response) => {
  const url = new URL(request.url ?? '/', `http://${request.headers.host ?? 'localhost'}`);

  if (url.pathname === '/health') {
    response.writeHead(200, { 'Content-Type': 'text/plain' });
    response.end('OK');
    return;
  }

  if (url.pathname === '/contract-fixture') {
    response.writeHead(302, {
      Location: '/contract-target',
      'Set-Cookie': 'parity_cookie=reference; Path=/; HttpOnly; SameSite=Lax',
      'X-Parity-Contract': 'redirect',
    });
    response.end();
    return;
  }

  if (url.pathname === '/contract-target') {
    response.writeHead(200, {
      'Content-Type': 'text/html; charset=utf-8',
      'Content-Security-Policy': "default-src 'self'",
      'Permissions-Policy': 'camera=(), microphone=()',
      'Referrer-Policy': 'no-referrer',
      'X-Content-Type-Options': 'nosniff',
    });
    response.end('<!doctype html><html lang="en"><head><title>Parity fixture</title></head><body><main><h1>Parity fixture</h1><p>Deterministic HTML</p></main></body></html>');
    return;
  }

  if (url.pathname === '/contract-echo') {
    const body = await readBody(request);
    response.writeHead(201, {
      'Content-Type': 'application/json; charset=utf-8',
      'Content-Security-Policy': "default-src 'none'",
      'Referrer-Policy': 'no-referrer',
    });
    response.end(JSON.stringify({ method: request.method, body }));
    return;
  }

  if (url.pathname === '/contract-inaccessible') {
    response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
    response.end('<!doctype html><html lang="en"><head><title>Accessibility negative control</title></head><body><main><h1>Negative control</h1><img src="data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw=="></main></body></html>');
    return;
  }

  if (url.pathname === '/captures' && request.method === 'DELETE') {
    await clearCaptures();
    response.writeHead(204);
    response.end();
    return;
  }

  if (url.pathname === '/captures' && request.method === 'GET') {
    response.writeHead(200, { 'Content-Type': 'application/json' });
    response.end(JSON.stringify(await listCaptures()));
    return;
  }

  if (url.pathname.startsWith('/capture/')) {
    const body = await readBody(request);
    sequence += 1;
    const selectedHeaders = Object.fromEntries(
      Object.entries(request.headers)
        .filter(([name]) => ['content-type', 'x-correlation-id'].includes(name))
        .map(([name, value]) => [name, Array.isArray(value) ? value.join(',') : value ?? '']),
    );
    const capture = {
      sequence,
      method: request.method ?? 'GET',
      path: `${url.pathname}${url.search}`,
      headers: selectedHeaders,
      body,
    };
    const filename = `${String(sequence).padStart(6, '0')}.json`;
    await fs.writeFile(path.join(captureDirectory, filename), `${JSON.stringify(capture, null, 2)}\n`);
    response.writeHead(202, { 'Content-Type': 'application/json' });
    response.end(JSON.stringify({ accepted: true, sequence }));
    return;
  }

  response.writeHead(404);
  response.end();
});

server.listen(port, '0.0.0.0', () => {
  process.stdout.write(`side-effect-sink listening on ${port}\n`);
});

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => server.close(() => process.exit(0)));
}
