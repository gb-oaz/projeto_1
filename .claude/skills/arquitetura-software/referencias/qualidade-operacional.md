# Segurança, observabilidade e custo

## Segurança de aplicação (OWASP como checklist)

- **Autenticação/sessão**: token de acesso curto + renovação HttpOnly (requisito 17_a); revogar no logout e na troca de senha.
- **Autorização**: checada no servidor antes do caso de uso, por módulo/permissão (requisito 17_b); nunca confie no front esconder botão.
- **Isolamento por escola** (multilocatário): todo acesso a dado filtra pelo locatário de quem pede.
- **Entrada**: valide forma no DTO, regra no domínio; recuse content-type inesperado (RN-271).
- **Segredos**: nunca no código, no repositório ou em log; localmente por variável de ambiente, na Azure no Key Vault por RBAC (RN-24, RNF-68).
- **Dados sensíveis**: senha só com hash forte; nada de senha, token ou documento pessoal em log.
- **Dependências**: Dependabot (RNF-21), versões fixadas (RNF-19).
- **Superfície**: sem acesso público aos dados (RNF-62), private endpoints (RNF-61), WAF na produção (RNF-79).

## Observabilidade

- **Log estruturado** com nível correto, traceId propagado e contexto (usuário, escola, endpoint) — sem dados sensíveis.
- **Métricas**: latência por endpoint (p95 contra o `RNF`), taxa de erro por código, uso de cache.
- **Rastros** distribuídos: o traceId liga requisição, log e auditoria.
- Na Azure: Log Analytics (RNF-81) e Application Insights (RNF-82), com limite de ingestão (RNF-83).
- Toda recusa relevante tem código estável para virar alerta ou consulta.

## Custo (arquitetura orientada a custos)

- Homologação mínima e desligável (RN-22, RN-26, RNF-92); produção com réplicas definidas por variável (RNF-58, RNF-66).
- Prefira serviço gerenciado com escala a zero ou consumo quando o `RNF` permitir.
- Orçamentos e tags por ambiente (RNF-84, RNF-85); log com limite de ingestão.
- Toda decisão que adiciona custo fixo (gateway, réplica, SKU maior) vai para ADR com o custo estimado.
