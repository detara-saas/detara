export class GatewayError extends Error {
  constructor(statusCode, code, safeMessage) {
    super(safeMessage);
    this.name = 'GatewayError';
    this.statusCode = statusCode;
    this.code = code;
    this.safeMessage = safeMessage;
  }
}

export class ValidationError extends GatewayError {
  constructor(message) {
    super(400, 'requisicao_invalida', message);
  }
}

export class ConflictError extends GatewayError {
  constructor(code, message) {
    super(409, code, message);
  }
}
