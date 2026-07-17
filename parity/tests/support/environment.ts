export interface TargetUrls {
  /** Single application under test for standalone journeys (Rails, gateway, or .NET). */
  target: string;
  rails: string;
  dotnet: string;
  gateway: string;
  sideEffectSink: string;
  govOneSimulator: string;
}

function requiredUrl(name: string, fallback?: string): string {
  const value = process.env[name] ?? fallback;
  if (!value) {
    throw new Error(`${name} must be configured.`);
  }

  return new URL(value).toString().replace(/\/$/, '');
}

function optionalUrl(name: string, fallback: string): string {
  return requiredUrl(name, process.env[name] ? undefined : fallback);
}

/**
 * Dual-stack URLs remain available for differential parity tests.
 * Standalone journeys only require TARGET_BASE_URL (plus the One Login simulator).
 */
export function targetUrls(): TargetUrls {
  const target = requiredUrl(
    'TARGET_BASE_URL',
    process.env.RAILS_BASE_URL ?? 'http://localhost:3000',
  );

  return {
    target,
    rails: optionalUrl('RAILS_BASE_URL', target),
    dotnet: optionalUrl('DOTNET_BASE_URL', 'http://localhost:5000'),
    gateway: optionalUrl('GATEWAY_BASE_URL', 'http://localhost:8080'),
    sideEffectSink: optionalUrl('SIDE_EFFECT_SINK_URL', 'http://localhost:9090'),
    govOneSimulator: optionalUrl('GOV_ONE_SIMULATOR_URL', 'http://localhost:4000'),
  };
}
