export const defaultJourneyTargets = ['tests/journeys', 'tests/concurrency'];

export function decodeArguments(encoded) {
  if (!encoded) return [...defaultJourneyTargets];
  if (!/^[A-Za-z0-9+/]*={0,2}$/.test(encoded) || encoded.length % 4 !== 0) {
    throw new Error('PLAYWRIGHT_ARGS_BASE64 is not valid base64.');
  }

  const decoded = Buffer.from(encoded, 'base64').toString('utf8');
  const args = decoded.split('\0');
  if (args.at(-1) === '') args.pop();
  if (args.length === 0 || args.some((argument) => argument.includes('\0'))) {
    throw new Error('PLAYWRIGHT_ARGS_BASE64 did not contain valid arguments.');
  }

  // Option-only invocations such as `--workers=6` or `-g title` modify the
  // default journey targets. A leading positional argument explicitly replaces them.
  return args[0]?.startsWith('-') ? [...defaultJourneyTargets, ...args] : args;
}
