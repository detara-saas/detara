import path from 'node:path';

function parsePositiveInteger(value, fallback, minimum, maximum) {
  const parsed = Number.parseInt(value ?? '', 10);
  return Number.isInteger(parsed) && parsed >= minimum && parsed <= maximum
    ? parsed
    : fallback;
}

export function loadConfig(environment = process.env) {
  const apiKey = environment.DETARA_WHATSAPP_GATEWAY_API_KEY?.trim() ?? '';
  if (apiKey.length < 32 || apiKey.toUpperCase().includes('CHANGE_ME')) {
    throw new Error('DETARA_WHATSAPP_GATEWAY_API_KEY deve possuir pelo menos 32 caracteres.');
  }

  const sessionsPath = path.resolve(
    environment.DETARA_WHATSAPP_SESSIONS_PATH?.trim() || './sessions',
  );
  return Object.freeze({
    host: environment.HOST?.trim() || '127.0.0.1',
    port: parsePositiveInteger(environment.PORT, 3000, 1, 65535),
    apiKey,
    sessionsPath,
    connectWaitMs: parsePositiveInteger(
      environment.DETARA_WHATSAPP_CONNECT_WAIT_MS,
      20_000,
      1_000,
      60_000,
    ),
    chromiumExecutablePath:
      environment.PUPPETEER_EXECUTABLE_PATH?.trim() || undefined,
  });
}
