---
projeto: "boaz"
documento: "14_estrutura-projetos"
tipo: "Transversal"
titulo: "Estrutura dos projetos"
versao: "v19"
data: "2026-11-13 09:11"
status: "Em revisão"
objetivo: "definir uma organização de pastas e de dependências única para backend, web e mobile, que separa o domínio (core) dos adaptadores."
---

> **boaz · Transversal** · `14_estrutura-projetos` · **v19** · 2026-11-13 09:11 · **Em revisão**

# Estrutura dos projetos

**Objetivo:** definir uma organização de pastas e de dependências única para backend, web e mobile, que separa o domínio (core) dos adaptadores.

---

<a id="cenarios"></a>

## Cenários

<a id="arquitetura-hexagonal"></a>

### Arquitetura hexagonal

```gherkin
Cenário: uma frente por pasta
  Então o código fica em 5_DEV/: o backend em C#, a web em Vue e os apps em .NET MAUI
  E cada frente tem um core/ por contexto e feature, com domínio, casos de uso e portas
  E na web e no mobile os adaptadores primários ficam em infra/in e os secundários em infra/out
  E no backend os primários ficam em ms_mono/primario/{in,out}
  E os secundários do backend ficam em ms_mono/secundario/{in,out}

Cenário: um runtime por frente (decisão de 2026-10-24)
  Então o backend é um único serviço, o ms_mono, com todos os contextos
  E a web é um projeto com três builds, um por app, sem cópia nem module federation
  E o mobile tem um projeto .NET MAUI por app, que gera o Android e o iOS

Cenário: testes fora dos projetos
  Então os testes de qa ficam em 5_DEV/qa/ e testam o que está rodando
  E dentro de cada projeto ficam só os unitários e os de integração entre poucos componentes
```

<a id="RN-143"></a>

### `RN-143` Mesma estrutura nas três frentes

```gherkin
Esquema do Cenário: feature hub/salas
  Quando a feature hub/salas existe na frente <frente>
  Então o core fica em core/hub/salas/
  E os adaptadores ficam em <adaptadores>

  Exemplos:
    | frente  | adaptadores                                                           |
    | backend | ms_mono/primario/{in,out}/hub/salas/ e secundario/{in,out}/hub/salas/ |
    | web     | apps/hub/infra/in/hub/salas/ e infra/out/hub/salas/                   |
    | mobile  | apps/hub/infra/in/hub/salas/ e infra/out/hub/salas/                   |
```

<a id="RN-144"></a>

### `RN-144` Contextos

```gherkin
Cenário: contextos de cada frente
  Então os contextos são landing_page, hub, quest e commons
  E o mobile não tem landing_page

Cenário: contexto novo
  Quando alguém cria o contexto "financeiro"
  Então este documento precisa de versão nova antes
```

<a id="RN-145"></a>

### `RN-145` Nomes em snake_case

```gherkin
Cenário: mesmo nome nas três frentes
  Quando a feature "salas ao vivo" nasce no hub
  Então a pasta é core/hub/salas_ao_vivo/ no backend, na web e no mobile
```

<a id="RN-146"></a>

### `RN-146` Contexto importa só commons

```gherkin
Esquema do Cenário: import entre contextos
  Quando core/hub/ importa <alvo>
  Então o import é <resultado>

  Exemplos:
    | alvo          | resultado                          |
    | core/commons/ | aceito                             |
    | core/quest/   | recusado: o comum vai para commons |

Esquema do Cenário: import entre features
  Quando um caso de uso de core/hub/sac/chamados/ importa <alvo>
  Então o import é <resultado>

  Exemplos:
    | alvo                                   | resultado                               |
    | core/commons/auditoria/                | aceito                                  |
    | core/commons/usuario/usuarios/usecases | recusado: usecases/ de outra feature    |
```

<a id="RN-147"></a>

### `RN-147` Core sem entrada e saída

```gherkin
Esquema do Cenário: dependência do core
  Quando core/hub/salas/ usa <dependência>
  Então <resultado>

  Exemplos:
    | dependência           | resultado  |
    | o cliente do MongoDB  | é recusado |
    | o HttpClient          | é recusado |
    | o Vue, em components/ | é aceito   |
    | o Vue, em usecases/   | é recusado |
```

<a id="RN-148"></a>

### `RN-148` Portas e adaptadores

```gherkin
Cenário: fluxo de uma chamada
  Dado o endpoint de criar sala em infra/in/hub/salas/
  E no backend, em ms_mono/primario/in/hub/salas/
  Então ele chama a feature só pela interface de ports/in/
  E usecases/ implementa ports/in/ e usa ports/out/
  E o repositório em infra/out/hub/salas/ implementa ports/out/
  E no backend, em ms_mono/secundario/out/hub/salas/
```

<a id="RN-149"></a>

### `RN-149` Ponto de composição

```gherkin
Esquema do Cenário: onde as portas são ligadas
  Então na frente <frente> as portas se ligam aos adaptadores só em <arquivo>

  Exemplos:
    | frente  | arquivo                    |
    | backend | Program.cs                 |
    | web     | main.ts de cada app        |
    | mobile  | MauiProgram.cs de cada app |
```

<a id="RN-151"></a>

### `RN-151` QA caixa-preta

```gherkin
Cenário: suíte importa o backend
  Quando uma suíte de 5_DEV/qa/ importa um tipo de 5_DEV/backend/
  Então a revisão recusa, porque o qa testa só o que está rodando
```

<a id="RN-152"></a>

### `RN-152` Tipos de teste do qa

```gherkin
Esquema do Cenário: o que cada tipo testa
  Quando o teste é de <tipo>
  Então ele cobre <o quê>

  Exemplos:
    | tipo       | o quê                                                 |
    | integração | fluxos e regras pela API, com banco e cache reais     |
    | funcional  | fluxos do usuário pela interface, com o backend no ar |
    | carga      | tempo de resposta e erros do backend sob volume       |
```

<a id="RN-153"></a>

### `RN-153` Ferramenta livre por suíte

```gherkin
Cenário: trocar a ferramenta
  Dado a suíte backend/integracao/ seguindo o contrato
  Quando o time troca a ferramenta da suíte
  Então o CI não muda
```

<a id="RN-154"></a>

### `RN-154` Testes por feature

```gherkin
Cenário: teste de salas
  Quando alguém escreve o teste de criar sala na integração
  Então ele fica em backend/integracao/hub/salas/
  E cita o ID do requisito que cobre, como "RF-68"
```

<a id="RN-155"></a>

### `RN-155` Limites de carga

```gherkin
Cenário: limite vem do requisito
  Dado o requisito de salas com "200 pedidos por segundo em até 300 ms"
  Então o teste de carga usa esse limite
  E o qa não define limite próprio
```

<a id="RN-156"></a>

### `RN-156` Quando a carga roda

```gherkin
Esquema do Cenário: execução da carga
  Quando a carga <momento>
  Então ela <resultado>

  Exemplos:
    | momento                          | resultado |
    | é pedida no CI local             | roda      |
    | é pedida contra homologação      | roda      |
    | seria disparada por pull request | não roda  |
    | seria apontada para produção     | não roda  |
```

<a id="RNF-175"></a>

### `RNF-175` Estrutura do backend

```gherkin
Cenário: pastas do backend
  Então 5_DEV/backend/ tem Boaz.slnx, core/<contexto>/<feature>/, ms_mono/ e tests/
  E cada feature tem domain/, dtos/, ports/in/, ports/out/ e usecases/
  E usecases/ tem uma classe por caso de uso, com a interface dela em ports/in/
  E dtos/ tem os dados que entram e saem dos casos de uso, com a validação dos campos

Cenário: features de um módulo
  Quando um módulo reúne mais de uma feature, como Usuários
  Então elas ficam juntas em core/<contexto>/<módulo>/<feature>/
  E Usuários fica em core/commons/usuario/, com usuario/, autenticacao/ e autorizacao/

Cenário: bibliotecas do core
  Então o core usa só as bibliotecas do próprio .NET, sem pacote de terceiros

Cenário: auditoria comum
  Então core/commons/auditoria/ tem domain/, ports/out/ e usecases/, sem ports/in/ nem dtos/
  E ports/out/ tem IAuditoria e IAuditoriaDeErros
  E usecases/ tem AuditoriaDaExecucao, usada pelos casos de uso de qualquer contexto
  E o adaptador AuditoriaNoBanco fica em ms_mono/secundario/out/commons/auditoria/
```

<a id="RNF-176"></a>

### `RNF-176` Projetos do core

```gherkin
Cenário: projeto do hub
  Então core/hub/ é o projeto Boaz.Core.Hub, que referencia só Boaz.Core.Commons
  E não tem pacote de ASP.NET Core, MongoDB nem Redis
```

<a id="RNF-177"></a>

### `RNF-177` ms_mono

```gherkin
Cenário: runtime do backend
  Então ms_mono/ é o ASP.NET Core que gera a imagem backend-mono e referencia todos os contextos
  E tem primario/ e secundario/, cada um com in/ e out/ por <contexto>/<feature>/
  E primario/in/ recebe quem chama: os endpoints, a partida e o agendamento
  E primario/out/ responde a quem chamou: o fluxo SSE e a sessão na resposta
  E secundario/in/ recebe o que chega da infraestrutura: as assinaturas de canal do Redis
  E secundario/out/ chama a infraestrutura: MongoDB, Redis, índices e configurações
  E sistema/ guarda prontidão e documentação da API
  E wwwroot/doc/api/design/ guarda a cópia gerada do design system
```

<a id="RNF-178"></a>

### `RNF-178` Testes do backend

```gherkin
Cenário: projeto de teste
  Então tests/ espelha core/ e ms_mono/
  E Boaz.Core.Hub tem o projeto xUnit Boaz.Core.Hub.Tests

Cenário: testes de uma feature do core
  Dado a feature core/commons/usuario/autenticacao/
  Então os unitários testam domain/ e a validação de dtos/
  E os de integração usam um mock que implementa os ports/out/ e chama os usecases/
  E ficam em tests/core/commons/usuario/autenticacao/, com domain/, dtos/, mocks/ e usecases/

Cenário: cobertura de cada projeto de teste
  Então cada projeto de teste mede só os projetos que testa
  E core/commons é medido por Boaz.Core.Commons.Tests, e não pelos testes do ms_mono
```

<a id="RNF-179"></a>

### `RNF-179` Estrutura da web

```gherkin
Cenário: pastas da web
  Então 5_DEV/frontend/web/ é um projeto Vite, com um package.json e um pnpm-lock.yaml
  E tem core/<contexto>/<feature>/ com components/, domain/, usecases/ e ports/
  E apps/landing_page/, apps/hub/, apps/quest/ e tests/
```

<a id="RNF-193"></a>

### `RNF-193` Testes da web

```gherkin
Cenário: teste sem navegador
  Então tests/ da web roda sem navegador e sem backend
  E os testes de ponta a ponta ficam no qa
```

<a id="RNF-180"></a>

### `RNF-180` Apps da web

```gherkin
Cenário: app do hub
  Então apps/hub/ tem index.html, main.ts e a configuração do Vite
  E infra/in/ com páginas e rotas e infra/out/ com clientes HTTP e armazenamento
  E importa o core pelo alias @core
```

<a id="RNF-181"></a>

### `RNF-181` Imagens da web

```gherkin
Esquema do Cenário: um Dockerfile, três imagens
  Quando o build roda com APP=<app>
  Então gera a imagem <imagem>

  Exemplos:
    | app          | imagem                |
    | landing_page | frontend-landing-page |
    | hub          | frontend-hub          |
    | quest        | frontend-quest        |
```

<a id="RNF-182"></a>

### `RNF-182` Commons da web

```gherkin
Cenário: lint dos imports
  Então core/commons/ guarda os componentes em components/ e o design system em design/
  E o lint do CI reprova import que viole RN-146 ou RN-147
```

<a id="RNF-183"></a>

### `RNF-183` Estrutura do mobile

```gherkin
Cenário: pastas do mobile
  Então 5_DEV/frontend/mobile/ tem Boaz.Mobile.slnx, core/, apps/hub/, apps/quest/ e tests/
  E components/ tem ContentView e ViewModel
```

<a id="RNF-184"></a>

### `RNF-184` Bibliotecas do mobile

```gherkin
Cenário: biblioteca do hub
  Então core/hub/ é Boaz.Mobile.Core.Hub, que referencia só Boaz.Mobile.Core.Commons
  E o design system gerado fica em core/commons/Resources/Styles/Design/
```

<a id="RNF-185"></a>

### `RNF-185` Apps do mobile

```gherkin
Cenário: app do hub
  Então apps/hub/ gera net10.0-android e net10.0-ios num só projeto
  E tem infra/in/ com Shell e páginas, infra/out/ e MauiProgram.cs
```

<a id="RNF-186"></a>

### `RNF-186` Testes do mobile

```gherkin
Cenário: espelho
  Então tests/ espelha core/ e apps/, com um projeto xUnit por projeto testado
```

<a id="RNF-187"></a>

### `RNF-187` Alvos do mobile

```gherkin
Esquema do Cenário: alvos por sistema
  Quando o build roda no <sistema>
  Então os apps geram <alvos>

  Exemplos:
    | sistema | alvos                         |
    | macOS   | net10.0-android e net10.0-ios |
    | Linux   | net10.0-android               |

Cenário: bibliotecas do core
  Então as bibliotecas também geram net10.0, alvo dos testes
```

<a id="RNF-198"></a>

### `RNF-198` Endereço do backend no app

```gherkin
Esquema do Cenário: ApiUrl no build
  Quando o app é compilado para <destino>
  Então ApiUrl recebe <endereço>

  Exemplos:
    | destino        | endereço                  |
    | o qa funcional | API_URL_DISPOSITIVO       |
    | homologação    | o endereço de homologação |
    | produção       | o endereço de produção    |
```

<a id="RNF-190"></a>

### `RNF-190` Suítes do qa

```gherkin
Cenário: pastas das suítes
  Então as suítes são backend/integracao/ e backend/carga/
  E frontend/web/funcional/ e frontend/mobile/funcional/
```

<a id="RNF-191"></a>

### `RNF-191` Contrato das suítes em contêiner

```gherkin
Cenário: rodar a integração
  Dado backend/integracao/ com um Dockerfile na raiz
  Quando o CI roda a imagem com API_URL
  Então os resultados saem em JUnit XML em /resultados
  E a imagem termina com 0 se tudo passou e com outro código se algo falhou
```

<a id="RNF-192"></a>

### `RNF-192` Funcional da web

```gherkin
Cenário: uma rodada por app
  Então frontend/web/funcional/ usa Cypress
  E roda uma vez por app, com as pastas do app e de commons/
```

<a id="RNF-196"></a>

### `RNF-196` Funcional do mobile

```gherkin
Cenário: rodar no emulador Android
  Quando executar.sh roda com PLATAFORMA, APP, APP_PACOTE, APPIUM_URL, API_URL e API_URL_DISPOSITIVO
  Então os testes em C# com Appium rodam no emulador
  E o JUnit XML vai para RESULTADOS e o código é 0 só se tudo passou
  E o Appium, o UiAutomator2 e o XCUITest têm versão exata no package.json
```

---

<a id="diagramas"></a>

## Diagramas

**Estrutura dos projetos** — `2_Diagramas/estrutura-projetos_v09_20261113-0921.svg`

[![Estrutura dos projetos: padrão de cada feature (infra/in, components, ports/in, usecases, domain, dtos, ports/out, infra/out), agrupada por módulo quando preciso e as pastas do backend (core e ms_mono com primario/in, primario/out, secundario/in e secundario/out), da web (core e apps landing_page, hub, quest) e do mobile (core e apps hub, quest), as suítes do qa por alvo e tipo (backend/integracao, backend/carga, frontend/web/funcional com Cypress, frontend/mobile/funcional com Appium) com o contrato de cada uma, e as regras de dependência](../2_Diagramas/estrutura-projetos_v09_20261113-0921.svg)](../2_Diagramas/estrutura-projetos_v09_20261113-0921.svg)

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v19** | 2026-11-13 09:11 | Auditoria comum em core/commons/auditoria/ (domain/, ports/out/ com IAuditoria e IAuditoriaDeErros, usecases/ com AuditoriaDaExecucao, sem ports/in/ nem dtos/) e o adaptador AuditoriaNoBanco em ms_mono/secundario/out/commons/auditoria/ (`RNF-175`); import entre features: commons/auditoria aceito, usecases/ de outra feature recusado (`RN-146`) |
| v18 | 2026-11-12 02:03 | Diagrama: estrutura-projetos v07 → v08 (ms_mono com primario/in, primario/out, secundario/in e secundario/out e o papel de cada um, `RNF-177`; regras de dependência com primario/secundario no backend e infra/in e infra/out na web e no mobile, `RN-143`, `RN-148`); cenários sem mudança |
| v17 | 2026-11-12 01:58 | Adaptadores do backend em ms_mono/primario/{in,out} e ms_mono/secundario/{in,out}, sem infra/out no ms_mono (arquitetura hexagonal, `RN-143`, `RN-148`, `RNF-177`); web e mobile seguem com infra/in e infra/out |
| v16 | 2026-11-11 23:18 | Adaptadores primários do backend em ms_mono/primario e infra/out só com os secundários (arquitetura hexagonal, `RN-143`, `RN-148`, `RNF-177`); web e mobile seguem com apps/<app>/infra/in; diagrama estrutura-projetos v06 → v07; status → Em revisão |
| v15 | 2026-11-05 12:34 | Formato `.md`: migrado de `.html` sem mudar cenários, telas nem diagramas; links entre documentos apontam para `.md` |
| v14 | 2026-11-03 22:11 | Aprovado, sem pendências em aberto |
| v13 | 2026-11-02 12:26 | `RNF-175` alterado: `dtos/`, um caso de uso por classe, features agrupadas por módulo (`core/commons/usuario/`) e só bibliotecas do .NET no core; `RNF-178` alterado: unitários no domínio, integração com mock das portas e cobertura medida por projeto; diagrama `estrutura-projetos_v06` |
| v12 | 2026-11-01 18:25 | Só Gherkin e diagramas: contexto, RN e RNF viram cenários (mesmos IDs como âncora); status → Em revisão |
| v11 | 2026-10-27 00:49 | Status → Aprovado |
| v10 | 2026-10-26 23:53 | `RNF-177` alterado: `ms_mono/sistema/` (endpoints de sistema e documentação da API) e `wwwroot/doc/api/design/` (cópia gerada do design system) |

---

<sub>boaz · v19 · 2026-11-13 09:11 · `1_Requisitos/14_estrutura-projetos.md`</sub>
