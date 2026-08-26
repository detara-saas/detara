import http from 'node:http';
import path from 'node:path';
import QRCode from 'qrcode';
import { createApp } from './app.js';
import { loadConfig } from './config.js';
import { DeliveryStore } from './delivery-store.js';
import { WhatsAppGatewayService } from './gateway-service.js';
import { createLogger } from './logger.js';
import { SessionRegistry } from './session-registry.js';
import { WhatsAppClientFactory } from './whatsapp-client-factory.js';

const config = loadConfig();
const logger = createLogger();
const registry = new SessionRegistry(config.sessionsPath);
const deliveryStore = new DeliveryStore(config.sessionsPath);
const clientFactory = new WhatsAppClientFactory(config);
const service = new WhatsAppGatewayService({
  clientFactory,
  qrEncoder: QRCode.toDataURL,
  registry,
  deliveryStore,
  logger,
  connectWaitMs: config.connectWaitMs,
});

await service.start();
const app = createApp({ service, apiKey: config.apiKey, logger });
const server = http.createServer(app);
server.requestTimeout = 30_000;
server.headersTimeout = 15_000;
server.maxRequestsPerSocket = 100;
server.listen(config.port, config.host, () => {
  logger.info('Gateway WhatsApp iniciado.', {
    path: path.basename(config.sessionsPath),
  });
});

async function shutdown(signal) {
  logger.info('Encerrando gateway WhatsApp.', { path: signal });
  server.close();
  await service.shutdown();
  process.exit(0);
}

process.once('SIGTERM', () => void shutdown('SIGTERM'));
process.once('SIGINT', () => void shutdown('SIGINT'));
