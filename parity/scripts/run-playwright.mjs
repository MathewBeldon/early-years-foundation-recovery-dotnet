import { spawn } from 'node:child_process';
import { decodeArguments } from './playwright-arguments.mjs';

const args = decodeArguments(process.env.PLAYWRIGHT_ARGS_BASE64);
const executable = process.platform === 'win32' ? 'npx.cmd' : 'npx';
const child = spawn(executable, ['playwright', 'test', ...args], {
  shell: false,
  stdio: 'inherit',
});

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => child.kill(signal));
}

child.on('error', (error) => {
  process.stderr.write(`Could not start Playwright: ${error.message}\n`);
  process.exitCode = 1;
});

child.on('exit', (code, signal) => {
  if (signal) {
    process.stderr.write(`Playwright exited after ${signal}.\n`);
    process.exitCode = 1;
    return;
  }
  process.exitCode = code ?? 1;
});
