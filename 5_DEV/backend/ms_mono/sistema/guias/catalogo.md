![deboaz](/doc/api/design/banner.svg)

Catálogo das APIs do **deboaz**. Escolha a API no seletor do topo, à direita: cada uma abre com um guia de uso, os diagramas do módulo e, abaixo, os endpoints para testar daqui mesmo.

### APIs do catálogo

Todas rodam no **ms_mono**, o backend único do deboaz (contextos `landing_page`, `hub`, `quest` e `commons` num só serviço). Cada módulo é uma API separada no seletor.

| API | Endereços | Para quê |
|---|---|---|
| **Sistema** | `/health` | Prontidão do backend |
| **Autenticação** | `/auth/*` | Entrar no hub, no quest e na sala, confirmar a senha, renovar o token, sair e os aparelhos conectados |
| **Usuários** | `/contas`, `/usuarios`, `/time` | Contas assinantes, staff, participantes e membros do time, o card e o tempo de sessão |
| **Autorização** | `/usuarios/{id}/permissoes` | Permissões por módulo e aba, com If-Match, e o registro de mudanças |
| **SAC** | `/sac/chamados`, `/sac/anexos`, `/sac/configuracao`, `/sac/card` | Chamados da conta com o time deboaz: conversa (e ao vivo), anexos, atendimento, avaliação, prazos por categoria e o card |

### Por onde começar

1. Abra **Autenticação** e entre no hub em `POST /auth/hub/entrar` com `"aparelho": "APP"`: a resposta traz o `tokenAcesso`.
2. Toque **Authorize**, no alto da API, e cole o token. Os endpoints com cadeado passam a mandar `Authorization: Bearer <token>`.
3. O token vale 15 min. Quando um pedido responder `401`, renove em `POST /auth/renovar` com o `tokenRenovacao`.

### O que vem por aí

Cada módulo novo entra no seletor como uma API própria quando for construído, com o guia e os diagramas dele. O catálogo cresce sem mudar de endereço.

### Onde este catálogo existe

Em homologação, para acompanhar o sistema, e na máquina de quem desenvolve. Em produção, `/doc/api` não existe.
