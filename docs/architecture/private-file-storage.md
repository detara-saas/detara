# Armazenamento privado de arquivos

## Contrato

`IArquivoStorage` é a fronteira técnica para salvar, abrir, verificar e excluir conteúdo por chave lógica. Regras de negócio, controllers e banco não conhecem caminho físico, URL pública ou SDK de cloud. A aplicação gera a chave; o browser nunca a escolhe.

As fotos permanentes de veículos usam o formato:

```text
empresas/{empresaId}/veiculos/{veiculoId}/fotos/{guid}.{extensao-detectada}
```

O banco guarda somente essa chave e metadados. O conteúdo é entregue por endpoint autenticado, com autorização e isolamento por tenant antes da abertura do stream.

## Provider local

`LocalArquivoStorage` resolve `Storage.Local.RootPath` a partir do diretório da API, exige uma raiz fora do `wwwroot`, rejeita caminhos absolutos, barras invertidas, segmentos vazios e `.`/`..`, e confirma que o caminho resolvido continua dentro da raiz. A gravação usa arquivo temporário no diretório final, flush e rename atômico. Leituras e escritas são feitas por stream.

Exemplo de configuração:

```json
{
  "Storage": {
    "Provider": "Local",
    "Local": {
      "RootPath": "data/storage"
    }
  }
}
```

`LocalArquivoStorage` serve ao desenvolvimento e QA local e não deve ser considerado estratégia definitiva de produção. A raiz local é ignorada pelo Git.

## Segurança de imagens

Uploads de fotos de veículo aceitam JPEG, PNG e WebP até 10 MiB. O backend detecta o formato pelos bytes iniciais, ignora a extensão e o content type declarados pelo cliente, rejeita arquivos vazios ou divergentes e escolhe a extensão final a partir do conteúdo detectado. O nome original é saneado e usado somente como metadado de apresentação.

Não há URL pública, base64 persistido, binário no banco ou exposição da chave lógica na API. O frontend baixa cada imagem com `HttpClient` autenticado e cria uma Blob URL temporária no browser, revogada quando a página é descartada.

## Consistência

No upload, o veículo e o tenant são validados antes da escrita. Se a persistência dos metadados falhar, o arquivo recém-criado é removido em compensação. Na exclusão, os metadados são removidos primeiro e a exclusão física é tentada explicitamente; uma falha física é reportada e pode produzir órfão, opção preferida a manter uma referência quebrada no banco.

Não existe job de limpeza preventiva nesta etapa. Reconciliação de órfãos, thumbnails, transformação de imagem, HEIC, EXIF e um adapter futuro de Object Storage permanecem no backlog.
