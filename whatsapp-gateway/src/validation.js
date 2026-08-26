import { ValidationError } from './errors.js';

const tenantPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const idempotencyPattern = /^[a-zA-Z0-9/_-]{8,200}$/;

export function normalizeTenantId(value) {
  if (typeof value !== 'string' || !tenantPattern.test(value.trim())) {
    throw new ValidationError('EmpresaId inválido.');
  }
  return value.trim().toLowerCase();
}

export function normalizePhone(value) {
  if (typeof value !== 'string') {
    throw new ValidationError('Telefone inválido.');
  }
  let digits = value.replace(/\D/g, '');
  if (digits.length === 10 || digits.length === 11) {
    digits = `55${digits}`;
  }
  if (digits.length < 10 || digits.length > 15 || digits.startsWith('0')) {
    throw new ValidationError('Telefone inválido. Informe país e DDD.');
  }
  return digits;
}

export function validateMessage(value) {
  if (typeof value !== 'string') {
    throw new ValidationError('Mensagem inválida.');
  }
  const normalized = value.trim();
  if (normalized.length < 1 || normalized.length > 4_096) {
    throw new ValidationError('A mensagem deve possuir entre 1 e 4096 caracteres.');
  }
  return normalized;
}

export function validateIdempotencyKey(value) {
  if (typeof value !== 'string' || !idempotencyPattern.test(value)) {
    throw new ValidationError('Chave de idempotência inválida.');
  }
  return value;
}
