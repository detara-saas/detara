# Checklist de lançamento — beta privada

Marque somente com evidência verificável e data.

## Código e release

- [ ] CI verde: restore, build Release, testes, format e pending model changes.
- [ ] Nenhuma vulnerabilidade Critical/High conhecida.
- [ ] Imagens API/Web imutáveis e associadas ao commit aprovado.
- [ ] Compose Production validado; somente portas 80/443 publicadas.
- [ ] Migrations revisadas e bundle com hash registrado.

## Infra e segurança

- [ ] DNS/TLS válidos; HTTP redireciona para HTTPS.
- [ ] Firewall, SSH por chave, usuário sem root e atualizações automáticas/revisadas.
- [ ] `.env.production` e PFX com permissão `0600`, fora do Git e com backup seguro.
- [ ] JWT tenant e Platform distintos; MFA Platform validado.
- [ ] SQL runtime não é `sa`; SQL não está exposto externamente.
- [ ] Bucket privado e credencial mínima; nenhuma URL pública de objeto.
- [ ] Forwarded headers só do proxy confiável.

## Operação

- [ ] `/health/live` e `/health/ready` monitorados externamente.
- [ ] Alertas de disco, memória, 5xx/429, backup e expiração TLS testados.
- [ ] Logs JSON acessíveis e busca por correlation ID testada.
- [ ] Backup de 6h agendado, cópia offsite e retenção configuradas.
- [ ] Restore drill recente aprovado com `DBCC CHECKDB` e duração dentro do RTO.
- [ ] Deploy e rollback ensaiados em ambiente de homologação.
- [ ] Contatos e severidades do runbook de incidente confirmados.

## Produto e privacidade

- [ ] Fluxos de login tenant, Platform MFA, onboarding, orçamento/OS, financeiro e convite passaram no smoke test.
- [ ] Upload/download de mídia autorizado testado com dois tenants adversariais.
- [ ] Remetente Resend verificado e envio real de convite confirmado.
- [ ] Termos, política de privacidade, canal de suporte e responsável definidos.
- [ ] Empresas participantes aceitaram escopo de beta e limites de suporte.
- [ ] Processo de exportação/correção/exclusão de dados pessoais foi revisado.

## Decisão

- [ ] Go aprovado por responsável técnico e responsável de produto.
- [ ] Riscos aceitos possuem dono e prazo.
