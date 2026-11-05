---
projeto: "boaz"
documento: "06_design-system-telas"
tipo: "Transversal"
titulo: "Design system nas telas"
versao: "v19"
data: "2026-11-05 12:34"
status: "Aprovado"
objetivo: "garantir que toda tela, do desenho ao código, no app e nas três aplicações web, seja construída só com a identidade visual e os componentes do design system do projeto."
---

> **boaz · Transversal** · `06_design-system-telas` · **v19** · 2026-11-05 12:34 · **Aprovado**

# Design system nas telas

**Objetivo:** garantir que toda tela, do desenho ao código, no app e nas três aplicações web, seja construída só com a identidade visual e os componentes do design system do projeto.

---

<a id="cenarios"></a>

## Cenários

<a id="camadas-da-tela"></a>

### Camadas de uma tela

```gherkin
Esquema do Cenário: de que a tela é feita
  Então a camada <camada> traz <conteúdo>

  Exemplos:
    | camada                | conteúdo                                                  |
    | tokens                | cores, tipografia, formas, espaços e tempos por papel     |
    | componentes           | botões, campos, cards e barras, feitos só com tokens      |
    | componentes compostos | blocos de componentes e identidade, como a barra com logo |
    | vista                 | o desenho da tela inteira, antes do código                |

Cenário: sem biblioteca externa (decisão de 2026-10-16)
  Então nenhuma frente usa biblioteca externa de estilização ou de componentes visuais
  E o tema de cor padrão hoje é Branco e Preto, com a identidade ainda provisória
```

<a id="RN-27"></a>

### `RN-27` Vista antes do código

```gherkin
Cenário: tela nova
  Quando o time vai criar a tela Permissões
  Então desenha primeiro a vista, antes de qualquer código da tela
```

<a id="RN-28"></a>

### `RN-28` Só o design system na vista

```gherkin
Cenário: falta um componente
  Dado a vista precisando de um seletor de data que não existe
  Então o seletor entra antes no design system
```

<a id="RN-29"></a>

### `RN-29` Componente com vista

```gherkin
Cenário: componente sem uso
  Quando alguém propõe um componente que nenhuma vista usa
  Então o componente não é criado
```

<a id="RN-30"></a>

### `RN-30` Componente comum

```gherkin
Cenário: segundo módulo usa
  Dado o cartão de turma usado só pela Presença
  Quando a Agenda passa a usar o mesmo cartão
  Então ele vira componente comum, com uma implementação compartilhada
```

<a id="RN-31"></a>

### `RN-31` Componente composto

```gherkin
Cenário: barra com logo
  Então a barra de navegação com o logo, botões padrão e imagens da marca é um componente composto
```

<a id="RN-32"></a>

### `RN-32` App e web iguais

```gherkin
Cenário: Módulos no app e na web
  Então Módulos usa os mesmos componentes e tokens no app e na web
  E só a disposição muda conforme a classe de janela
```

<a id="RN-33"></a>

### `RN-33` Aprovação da vista

```gherkin
Cenário: vista aprovada
  Quando a vista é aprovada na revisão do pull request para a master
  Então o código da tela pode começar
```

<a id="RN-34"></a>

### `RN-34` Mascote para retorno

```gherkin
Cenário: personagem de fora
  Quando alguém propõe uma ilustração de rosto que não é do design system
  Então ela é recusada
```

<a id="RN-35"></a>

### `RN-35` Mascote existente

```gherkin
Cenário: forma nova
  Quando uma vista precisa de um mascote em forma de hexágono, que não existe
  Então a forma entra antes no design system
```

<a id="RN-36"></a>

### `RN-36` Cor do mascote

```gherkin
Esquema do Cenário: cor por tema
  Dado o tema <tema> e "Mascotes nas cores do tema" <opção>
  Então o mascote "acertou" fica <cor>

  Exemplos:
    | tema           | opção     | cor                         |
    | Oceano         | ligado    | na cor do Oceano            |
    | Oceano         | desligado | na cor da própria expressão |
    | Branco e Preto | ligado    | na cor da própria expressão |
```

<a id="RN-37"></a>

### `RN-37` Mascote com texto

```gherkin
Cenário: acerto
  Quando o Lucas acerta a pergunta
  Então o mascote aparece junto do texto "Acertou!"
```

<a id="RN-38"></a>

### `RN-38` Expressão por situação

```gherkin
Esquema do Cenário: expressão de cada situação
  Quando a situação é <situação>
  Então o mascote usa <expressões>

  Exemplos:
    | situação                | expressões                                         |
    | boas-vindas e despedida | feliz, tchau ou piscadinha                         |
    | acerto e progresso      | acertou, arrasou, orgulhoso, rindo muito, aliviado |
    | erro do usuário         | errou, ops, preocupado ou envergonhado             |
    | dúvida e atenção        | dúvida, pensando, curioso, desconfiado, uau        |
    | espera                  | esperando, com pressa ou sonolento                 |
    | social e cuidado        | beijo, silêncio, doente ou brincalhão              |
    | sistema e manutenção    | consertando, doente, sonolento ou aliviado         |

Cenário: nunca deboche no erro
  Quando o Lucas erra
  Então o mascote nunca usa "rindo muito" nem "brincalhão"
```

<a id="RN-248"></a>

### `RN-248` Aviso de ação

```gherkin
Esquema do Cenário: resultado de cada ação
  Quando a ação termina em <resultado>
  Então aparece o aviso de ação com o selo <selo> na cor <cor>

  Exemplos:
    | resultado    | selo        | cor     |
    | concluído    | de volta    | sucesso |
    | informação   | operacional | padrão  |
    | recusado     | instável    | erro    |
    | indisponível | fora do ar  | erro    |
    | espera       | manutenção  | aviso   |

Cenário: a tela muda depois da ação
  Quando a conta master salva e a tela volta à lista
  Então o aviso aparece flutuante sobre a lista
  E nenhuma ação termina em silêncio
```

<a id="RN-249"></a>

### `RN-249` Selos sempre animados

```gherkin
Cenário: selo de 16dp
  Dado um selo de status de 16dp
  Então ele é animado
  E fica parado só com movimento reduzido
```

<a id="RNF-93"></a>

### `RNF-93` Fonte única

```gherkin
Cenário: cor nova
  Quando uma tela precisa de uma cor
  Então a cor vem de 0_Assets/design/
```

<a id="RNF-94"></a>

### `RNF-94` Arquivos de cada plataforma

```gherkin
Esquema do Cenário: arquivos gerados
  Então <plataforma> usa <arquivos>

  Exemplos:
    | plataforma | arquivos                               |
    | a web      | tokens.css, tipografia.css e tokens.ts |
    | o app      | Tokens.xaml e paletas/                 |
```

<a id="RNF-95"></a>

### `RNF-95` Não editar gerados

```gherkin
Cenário: mudar uma cor
  Quando o time muda a semente do tema Oceano
  Então muda identidade/marca.json e roda "pnpm run gerar"
  E ninguém edita tokens.css à mão
```

<a id="RNF-108"></a>

### `RNF-108` Cópias nas aplicações

```gherkin
Esquema do Cenário: onde as cópias ficam
  Quando roda "pnpm run gerar"
  Então a cópia de <frente> vai para <pasta>

  Exemplos:
    | frente | pasta                                                       |
    | web    | 5_DEV/frontend/web/core/commons/design/                     |
    | mobile | 5_DEV/frontend/mobile/core/commons/Resources/Styles/Design/ |
```

<a id="RNF-96"></a>

### `RNF-96` Sem valor solto

```gherkin
Esquema do Cenário: valor no código da tela
  Quando o código da tela usa <valor>
  Então <resultado>

  Exemplos:
    | valor                              | resultado  |
    | color: #1A73E8                     | é recusado |
    | padding: 12px                      | é recusado |
    | transition: 200ms                  | é recusado |
    | color: var(--md-sys-color-primary) | é aceito   |
```

<a id="RNF-97"></a>

### `RNF-97` Sem biblioteca visual

```gherkin
Esquema do Cenário: biblioteca recusada
  Quando a web adiciona <biblioteca>
  Então a biblioteca é recusada

  Exemplos:
    | biblioteca |
    | Tailwind   |
    | Bootstrap  |
    | Vuetify    |
    | PrimeVue   |
```

<a id="RNF-98"></a>

### `RNF-98` Recursos permitidos

```gherkin
Cenário: o que pode
  Então as fontes da identidade e os Material Symbols de marca.json são permitidos
  E bibliotecas sem visual, como roteamento, estado e testes, não são afetadas
```

<a id="RNF-99"></a>

### `RNF-99` Estados do componente

```gherkin
Cenário: botão
  Então o botão tem normal, hover, foco visível, pressionado e desabilitado
  E alvo de toque de pelo menos 48dp
```

<a id="RNF-100"></a>

### `RNF-100` Componente comum no design system

```gherkin
Cenário: mesmo pull request
  Quando o cartão de turma vira componente comum
  Então o mesmo pull request traz tokens, catálogo e desenho no tema.svg
```

<a id="RNF-101"></a>

### `RNF-101` Uma implementação por frente

```gherkin
Cenário: três apps web
  Então hub, quest e landing page usam a mesma implementação do componente comum
```

<a id="RNF-109"></a>

### `RNF-109` Componentes comuns da web

```gherkin
Cenário: import sem cópia
  Então os componentes comuns da web ficam em core/commons/components/
  E todos os apps os importam sem cópia
```

<a id="RNF-102"></a>

### `RNF-102` Arquivo da vista

```gherkin
Cenário: nome da vista
  Então a vista da entrada do hub é "2_Diagramas/vista-hub-entrar_v01_20261101-1306.svg"
  E é desenhada pela skill diagrama-dev, tipo Vista
```

<a id="RNF-103"></a>

### `RNF-103` Molduras e paletas

```gherkin
Esquema do Cenário: uma vista por plataforma
  Quando a tela é desenhada para <plataforma>
  Então a vista se chama "vista-<aplicação>-<tela>-<sufixo>"
  E mostra <molduras>

  Exemplos:
    | plataforma                      | sufixo | molduras                           |
    | a web no navegador              | web    | medium e expanded, em escala 0,5   |
    | a web instalada no celular, PWA | pwa    | compact, com as anotações          |
    | o app mobile                    | mobile | compact, com as anotações          |

Cenário: telas no documento
  Então a seção Telas do requisito tem os grupos Web, PWA e Mobile, nesta ordem

Cenário: paletas
  Então a vista traz todos os temas de cor, claro e escuro, pelo botão Paleta
  E sem escolha abre no tema padrão, claro
```

<a id="RNF-104"></a>

### `RNF-104` Anotações da vista

```gherkin
Cenário: bloco anotado
  Então cada bloco diz o componente (comum, do módulo ou composto) e a identidade com o caminho
```

<a id="RNF-105"></a>

### `RNF-105` Implementação igual à vista

```gherkin
Cenário: divergência
  Quando a tela implementada precisa divergir da vista
  Então a vista ganha versão nova primeiro
```

<a id="RNF-106"></a>

### `RNF-106` Arquivos de identidade

```gherkin
Cenário: imagem nova
  Quando uma tela precisa de uma imagem nova
  Então a imagem entra antes em 0_Assets/design/identidade/
```

<a id="RNF-110"></a>

### `RNF-110` Origem dos mascotes

```gherkin
Esquema do Cenário: mascotes de cada frente
  Então <frente> usa os mascotes de <origem>

  Exemplos:
    | frente | origem                                         |
    | a web  | plataformas/web/mascotes.mjs, cópia em commons |
    | o app  | 0_Assets/design/mascotes/, parados e animados  |
```

<a id="RNF-111"></a>

### `RNF-111` Mascote novo no catálogo

```gherkin
Cenário: expressão nova
  Quando a expressão "sonhador" entra no núcleo
  Então o mesmo pull request traz o catálogo e o tema.svg em versão nova
```

<a id="RNF-112"></a>

### `RNF-112` Um animado por vez

```gherkin
Cenário: dois mascotes na tela
  Dado dois mascotes na área de foco
  Então no máximo um fica animado

Cenário: movimento reduzido
  Dado o aparelho com movimento reduzido
  Então todos os mascotes ficam parados
```

<a id="RNF-113"></a>

### `RNF-113` Mascote decorativo

```gherkin
Cenário: leitor de tela
  Então o leitor de tela ignora o mascote e lê o texto ao lado
  E o mascote não recebe foco
```

<a id="RNF-114"></a>

### `RNF-114` Tamanho do mascote

```gherkin
Esquema do Cenário: tamanho mínimo
  Quando o mascote é <tipo>
  Então tem pelo menos <tamanho>

  Exemplos:
    | tipo       | tamanho |
    | só o rosto | 24dp    |
    | com corpo  | 40dp    |

Cenário: contorno de adesivo
  Então só o rosto mantém o contorno de adesivo no fundo claro e no escuro
```

<a id="RNF-115"></a>

### `RNF-115` Mascote anotado

```gherkin
Cenário: mascote na vista
  Então a vista indica expressão, forma, proporção, movimento e o texto que o acompanha
```

<a id="RNF-118"></a>

### `RNF-118` Mascote no app

```gherkin
Cenário: mesmos movimentos
  Então o mascote do app tem os mesmos movimentos da web
  E fica parado com movimento reduzido no aparelho
```

<a id="RNF-107"></a>

### `RNF-107` Verificação no CI

```gherkin
Esquema do Cenário: CI falha
  Quando <situação>
  Então o CI falha

  Exemplos:
    | situação                                            |
    | os gerados do design system estão desatualizados    |
    | a cópia em core/commons/design/ está desatualizada  |
    | o código de uma tela tem cor, fonte ou medida solta |
```

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v19** | 2026-11-05 12:34 | Formato `.md`: migrado de `.html` sem mudar cenários, telas nem diagramas; links entre documentos apontam para `.md` |
| v18 | 2026-11-03 22:11 | Aprovado, sem pendências em aberto |
| v17 | 2026-11-03 11:37 | `RNF-103` alterado: uma vista por plataforma, web (medium e expanded), pwa (compact) e mobile (compact), e a seção Telas agrupada em Web, PWA e Mobile |
| v16 | 2026-11-01 18:29 | Só Gherkin: contexto, RN, situações dos mascotes e RNF viram cenários (mesmos IDs como âncora); status → Em revisão |
| v15 | 2026-11-01 13:15 | Aprovado |
| v14 | 2026-11-01 13:08 | Toda ação confirmada termina num Aviso de ação com o resultado, bom ou ruim, e o tom pelo selo (`RN-248`); selos e ilustrações de status sempre animados, parados só com movimento reduzido (`RN-249`) |
| v13 | 2026-10-27 23:12 | Tabela de situações e expressões: linha Sistema e manutenção (consertando, doente, sonolento e aliviado), com a expressão nova consertando (`RN-38`); status → Aprovado |
| v12 | 2026-10-24 21:32 | Status → Aprovado |
| v11 | 2026-10-24 21:28 | `RNF-108`, `RNF-109` e `RNF-110` alterados para a Estrutura dos projetos: uma cópia do design system por frente em `core/commons/` e componentes comuns da web em `core/commons/components/`, sem cópia; status → Em revisão |
| v10 | 2026-10-24 03:59 | `RNF-103` alterado: a vista de tela traz todos os temas de cor do design system, claro e escuro, numa linha de molduras; o documento escolhe tema e modo pelo botão Paleta (padrão Branco e Preto, claro); status → Aprovado |

---

<sub>boaz · v19 · 2026-11-05 12:34 · `1_Requisitos/06_design-system-telas.md`</sub>
