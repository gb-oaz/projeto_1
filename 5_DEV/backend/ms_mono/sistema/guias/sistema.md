Endpoints do próprio runtime do **ms_mono**, sem módulo.

## Como usar

`GET /health` responde `200`, sem corpo, quando o backend está no ar e pronto para atender. Não pede login.

- **CI local:** espera o `200` antes de rodar o qa.
- **Azure Container Apps:** uma revisão nova só recebe tráfego depois dele.
- **Monitoramento:** qualquer resposta diferente de `200`, ou a falta de resposta, é backend fora do ar.

O `/health` não confere o banco nem o cache: o backend sobe e responde mesmo com o MongoDB ou o Redis fora do ar, e conecta no primeiro pedido que precisar deles.

## Configuração

O backend lê os parâmetros do `appsettings.json` e das variáveis de ambiente, que valem mais. Na variável, `:` vira `__`: `Mongo:Banco` é `Mongo__Banco`; as entradas e senhas das contas iniciais vêm do Key Vault com o nome exato (`USUARIO_ADMIN_SENHA`). A lista completa, com o que é segredo, está no `README.md` de `5_DEV/backend/`.
