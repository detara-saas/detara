import { createHash, randomUUID, timingSafeEqual } from 'node:crypto';
import express from 'express';
import { GatewayError, ValidationError } from './errors.js';
import {
  normalizePhone,
  normalizeTenantId,
  validateIdempotencyKey,
  validateMessage,
} from './validation.js';

function secureEquals(left, right) {
  const leftHash = createHash('sha256').update(left).digest();
  const rightHash = createHash('sha256').update(right).digest();
  return timingSafeEqual(leftHash, rightHash);
}

export function createApp({ service, apiKey, logger }) {
  const app = express();
  app.disable('x-powered-by');
  app.use((request, response, next) => {
    request.requestId = randomUUID();
    response.setHeader('X-Trace-Id', request.requestId);
    response.setHeader('X-Content-Type-Options', 'nosniff');
    response.setHeader('X-Frame-Options', 'DENY');
    response.setHeader('Referrer-Policy', 'no-referrer');
    response.setHeader('Cache-Control', 'no-store');
    next();
  });
  app.use(express.json({ limit: '16kb', strict: true }));
  app.use((request, response, next) => {
    const authorization = request.get('authorization') ?? '';
    const token = authorization.startsWith('Bearer ')
      ? authorization.slice('Bearer '.length)
      : '';
    if (!token || !secureEquals(token, apiKey)) {
      response.status(401).json({
        success: false,
        code: 'nao_autorizado',
        message: 'Autenticação do gateway inválida.',
        traceId: request.requestId,
      });
      return;
    }
    next();
  });

  app.get('/healthz', (_request, response) => {
    response.json({ status: 'Healthy' });
  });

  app.post('/sessions/:empresaId/connect', async (request, response) => {
    const empresaId = validateTenantBinding(request, request.params.empresaId);
    const result = await service.connect(empresaId);
    response.json(result);
  });

  app.get('/sessions/:empresaId/status', async (request, response) => {
    const empresaId = validateTenantBinding(request, request.params.empresaId);
    const result = await service.getStatus(empresaId);
    response.json(result);
  });

  app.delete('/sessions/:empresaId', async (request, response) => {
    const empresaId = validateTenantBinding(request, request.params.empresaId);
    const result = await service.disconnect(empresaId);
    response.json(result);
  });

  app.post('/messages/send', async (request, response) => {
    if (!request.body || typeof request.body !== 'object' || Array.isArray(request.body)) {
      throw new ValidationError('Payload inválido.');
    }
    const empresaId = validateTenantBinding(request, request.body.empresaId);
    const result = await service.sendMessage({
      empresaId,
      phone: normalizePhone(request.body.telefone),
      message: validateMessage(request.body.mensagem),
      idempotencyKey: validateIdempotencyKey(
        request.body.chaveIdempotencia,
      ),
    });
    response.json({ status: 'Sent', ...result });
  });

  app.use((request, response) => {
    response.status(404).json({
      success: false,
      code: 'rota_nao_encontrada',
      message: 'Rota não encontrada.',
      traceId: request.requestId,
    });
  });

  app.use((error, request, response, _next) => {
    if (error instanceof GatewayError) {
      response.status(error.statusCode).json({
        success: false,
        code: error.code,
        message: error.safeMessage,
        traceId: request.requestId,
      });
      return;
    }
    if (error instanceof SyntaxError || error?.type === 'entity.too.large') {
      response.status(400).json({
        success: false,
        code: 'requisicao_invalida',
        message: 'Payload JSON inválido.',
        traceId: request.requestId,
      });
      return;
    }
    logger.error('Falha não tratada no gateway WhatsApp.', {
      requestId: request.requestId,
      method: request.method,
      path: request.path,
      errorType: error?.name ?? 'Error',
    });
    response.status(500).json({
      success: false,
      code: 'erro_interno',
      message: 'Não foi possível concluir a operação.',
      traceId: request.requestId,
    });
  });

  return app;
}

function validateTenantBinding(request, rawEmpresaId) {
  const empresaId = normalizeTenantId(rawEmpresaId);
  const header = normalizeTenantId(request.get('x-detara-tenant-id'));
  if (empresaId !== header) {
    throw new GatewayError(
      403,
      'tenant_invalido',
      'O tenant da requisição não corresponde ao tenant informado.',
    );
  }
  return empresaId;
}
