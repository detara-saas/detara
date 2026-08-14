namespace Detara.Application.Abstracoes;

public sealed class RecursoNaoEncontradoException(string mensagem) : Exception(mensagem);

public sealed class ConflitoRegraNegocioException(string mensagem) : Exception(mensagem);
