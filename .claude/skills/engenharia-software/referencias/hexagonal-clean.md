# Arquitetura hexagonal × Clean Architecture

## Hexagonal (Ports and Adapters) — o modelo do projeto

- **Portas de entrada** (`ports/in/`): o que a aplicação oferece (interface de cada caso de uso).
- **Portas de saída** (`ports/out/`): o que a aplicação precisa do mundo (repositório, relógio, envio de e-mail, fila, cache).
- **Adaptadores primários** (`infra/in/`): quem dirige — endpoints Minimal API no back, telas/rotas no front, filas consumidas.
- **Adaptadores secundários** (`infra/out/`): quem é dirigido — Mongo, Redis, HTTP para outro serviço, armazenamento.
- **Composição**: `Program.cs` / `main.ts` / `MauiProgram.cs` ligam porta ↔ adaptador (RN-149).

## Clean Architecture — correspondência

| Clean | Projeto |
|---|---|
| Entities | `domain/` |
| Use Cases (interactors) | `usecases/` + `ports/in/` |
| Interface Adapters (controllers, presenters, gateways) | `infra/in`, `infra/out`, `dtos/` |
| Frameworks & Drivers | ASP.NET Core, Vue, MAUI, drivers Mongo/Redis |

A regra é a mesma: **dependências só apontam para dentro**. O projeto usa o vocabulário hexagonal; não crie camadas extras ("Application", "Infrastructure" como projetos novos) sem decisão do arquiteto.

## Fluxo de uma chamada (back)

```
POST /hub/salas → endpoint (infra/in/hub/salas) → valida forma → ICriarSala (ports/in)
  → CriarSala (usecases) → Sala.Criar(...) (domain) → IRepositorioSalas (ports/out)
  → RepositorioSalasMongo (infra/out/hub/salas) → resposta mapeada pelo endpoint
```

## Checagens rápidas

```bash
# I/O dentro do core do backend (deve sair vazio)
grep -rnE 'using (MongoDB|StackExchange|Microsoft\.AspNetCore|System\.Net\.Http)' 5_DEV/backend/core --include=*.cs | grep -v '/obj/'

# fetch/axios/Vue em usecases ou domain da web (deve sair vazio)
grep -rnE "from 'vue'|fetch\(|axios" 5_DEV/frontend/web/core/*/*/usecases 5_DEV/frontend/web/core/*/*/domain 2>/dev/null

# contexto da web importando outro contexto que não commons (deve sair vazio)
for c in hub quest landing_page; do
  grep -rnE "core/(hub|quest|landing_page)/" "5_DEV/frontend/web/core/$c" --include=*.ts --include=*.vue | grep -v "core/$c/"
done
```

Os comandos são heurísticos: confirme o achado lendo o arquivo antes de reportar.
