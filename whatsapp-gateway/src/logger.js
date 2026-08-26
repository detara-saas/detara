const allowedMetadata = new Set([
  'empresaId',
  'errorType',
  'requestId',
  'method',
  'path',
  'statusCode',
]);

function sanitize(metadata = {}) {
  return Object.fromEntries(
    Object.entries(metadata).filter(([key]) => allowedMetadata.has(key)),
  );
}

export function createLogger(output = console) {
  const write = (level, message, metadata) => {
    const entry = JSON.stringify({
      timestamp: new Date().toISOString(),
      level,
      message,
      ...sanitize(metadata),
    });
    const method = level === 'error' ? 'error' : level === 'warn' ? 'warn' : 'log';
    output[method](entry);
  };
  return Object.freeze({
    info: (message, metadata) => write('info', message, metadata),
    warn: (message, metadata) => write('warn', message, metadata),
    error: (message, metadata) => write('error', message, metadata),
  });
}
