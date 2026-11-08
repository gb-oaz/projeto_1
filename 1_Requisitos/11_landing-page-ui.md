---
projeto: "boaz"
documento: "11_landing-page-ui"
tipo: "Funcionalidade"
titulo: "Landing page · UI"
versao: "v14"
data: "2026-11-05 12:34"
status: "Aprovado"
objetivo: "definir como a landing page e a página do módulo ficam na tela, com os textos, os componentes compostos, o fundo e o mascote de cada bloco, no celular e na web."
---

> **boaz · Funcionalidade** · `11_landing-page-ui` · **v14** · 2026-11-05 12:34 · **Aprovado**

# Landing page · UI

**Objetivo:** definir como a landing page e a página do módulo ficam na tela, com os textos, os componentes compostos, o fundo e o mascote de cada bloco, no celular e na web.

---

<a id="cenarios"></a>

## Cenários

<a id="RF-63"></a>

### `RF-63` Título e frase

```gherkin
Cenário: primeira tela
  Dado a landing page aberta
  Então a apresentação mostra "Aprender fica deboaz" e a frase de valor
```

<a id="RF-64"></a>

### `RF-64` Fundo da apresentação

```gherkin
Cenário: texto legível sobre o fundo
  Dado a apresentação sobre o fundo Halo
  Então o título, a frase e as ações ficam num painel de superfície

Cenário: logo na abertura
  Dado a landing page aberta
  Então o logo se monta uma vez (anel, antenas e o nome) e fica parado
```

<a id="RF-103"></a>

### `RF-103` Brilhos passando pelos temas

```gherkin
Cenário: troca suave de tema no fundo
  Dado a landing page aberta no tema Roxo
  Quando passam 6 segundos
  Então os brilhos do fundo trocam em 2 segundos para as cores do próximo tema colorido
  E o painel e o logo continuam no tema Roxo

Cenário: movimento reduzido
  Dado um aparelho com a preferência de movimento reduzido ativa
  Quando a landing page abre
  Então os brilhos ficam parados nas cores do primeiro tema e o logo aparece pronto
```

<a id="RF-66"></a>

### `RF-66` Títulos dos blocos

```gherkin
Cenário: bloco Módulos
  Quando o visitante chega ao bloco Módulos
  Então vê o título "Módulos" e o apoio "Do mais contratado ao menos contratado"
```

<a id="RF-67"></a>

### `RF-67` Cards de perfil

```gherkin
Cenário: escola
  Então o card Escola mostra "A família acompanha frequência, notas e agenda do aluno."
```

<a id="RF-68"></a>

### `RF-68` Passos

```gherkin
Cenário: passos do destaque
  Dado o Quiz ao vivo em destaque
  Então Como funciona mostra "Quiz ao vivo · os passos do módulo em destaque"
  E, nessa ordem, "Crie o quiz no hub", "Abra a sala e passe o código" e "Jogue pelo quest"
  E, quando o destaque muda, mostra os passos do novo módulo
```

<a id="RF-69"></a>

### `RF-69` Compostos nos blocos

```gherkin
Cenário: revisão da tela
  Dado a landing page implementada
  Quando é comparada com a vista da página
  Então cada bloco usa o mesmo componente composto indicado na vista
```

<a id="RF-70"></a>

### `RF-70` Página do módulo

```gherkin
Cenário: abrir um módulo
  Quando o visitante toca em "Saiba mais" no card de um módulo
  Então vê o nome, a frase, a vitrine com as telas do módulo no hub e no quest e os indicadores
  E os blocos Benefícios, Onde aparece, Depoimentos e Outros módulos
```

<a id="RF-73"></a>

### `RF-73` Menu aberto

```gherkin
Cenário: menu no celular
  Dado a landing page numa janela compact, no tema Roxo
  Quando o visitante toca no botão de menu
  Então vê o item "Tema" com "Roxo" e as ações "Começar grátis", "Entrar numa sala" e "Entrar"
```

<a id="RF-74"></a>

### `RF-74` Destaque

```gherkin
Cenário: módulo em destaque
  Dado o módulo Quiz em destaque, com o padrinho estrela
  Quando o visitante chega ao bloco Destaque
  Então vê o cartão em primary-container com "Quiz ao vivo" e a frase
  E "Saiba mais" e a estrela pela metade
  E o fundo Anel gigante atrás da estrela tem a forma de estrela
```

<a id="RF-71"></a>

### `RF-71` Rodapé

```gherkin
Cenário: lema
  Então o rodapé mostra o logo e "Na força do conhecimento, a sabedoria floresce."
```

<a id="RN-115"></a>

### `RN-115` Dados de exemplo

```gherkin
Cenário: vistas com exemplo
  Quando alguém abre as vistas da landing page
  Então módulos, números e depoimentos são exemplos
  E a página mostra os dados reais, nas regras da UX base
```

<a id="RN-116"></a>

### `RN-116` Mudança exige versão

```gherkin
Cenário: mascote novo no destaque
  Quando o time troca o mascote do bloco Destaque
  Então este documento e a vista ganham versão nova antes do código
```

<a id="RN-118"></a>

### `RN-118` Tema de cor

```gherkin
Esquema do Cenário: cores por tema
  Dado o tema <tema> ativo
  Então o logo usa <logo>
  E os mascotes usam <mascotes>

  Exemplos:
    | tema           | logo                        | mascotes                   |
    | Oceano         | as cores da marca do Oceano | as cores do Oceano         |
    | Branco e Preto | as cores da marca do tema   | a cor da própria expressão |
```

<a id="RN-137"></a>

### `RN-137` Fundo Estampa

```gherkin
Cenário: fundo da página
  Quando o visitante abre a landing page
  Então o fundo Estampa cobre a área abaixo da barra do topo, atrás dos blocos
  E o Halo, o Anel gigante e as Ondas do vão ficam por cima

Cenário: menu aberto
  Quando o visitante abre o menu
  Então o menu fica sem Estampa
```

<a id="RN-117"></a>

### `RN-117` Código depois da vista

```gherkin
Cenário: vista em revisão
  Dado a vista da página do módulo sem aprovação no pull request
  Então o código da página do módulo não começa
```

<a id="RNF-148"></a>

### `RNF-148` Títulos

```gherkin
Esquema do Cenário: papel do título da apresentação
  Quando a apresentação abre na janela <janela>
  Então o título usa <papel>

  Exemplos:
    | janela   | papel           |
    | compact  | headline-medium |
    | medium   | headline-medium |
    | expanded | display-small   |

Cenário: títulos dos blocos
  Então os títulos dos blocos usam headline-small
```

<a id="RNF-149"></a>

### `RNF-149` Contraste da apresentação

```gherkin
Cenário: texto no painel
  Quando a apresentação abre sobre o Halo
  Então o texto fica no painel de superfície, com contraste de 4,5:1 ou mais
  E nunca direto sobre o fundo
```

<a id="RNF-150"></a>

### `RNF-150` Colunas da apresentação

```gherkin
Esquema do Cenário: colunas por janela
  Quando a apresentação abre na janela <janela>
  Então mostra <layout>

  Exemplos:
    | janela   | layout                                           |
    | compact  | uma coluna, logo acima do painel                 |
    | medium   | uma coluna, logo acima do painel                 |
    | expanded | duas colunas, painel à esquerda e logo à direita |
```

<a id="fora-de-escopo"></a>

### Fora de escopo

```gherkin
Cenário: o que este documento não trata
  Então o conteúdo de cada módulo fica no documento do módulo
  E termos de uso e privacidade são texto do Jurídico, com o mesmo topo e rodapé
  E a UI do hub e do quest fica em 12_hub-ui e 13_quest-ui
```

---

<a id="telas"></a>

## Telas

**Landing page · vista** — `2_Diagramas/vista-landing-pagina_v08_20261024-0357.svg`

[![Vista da landing page inteira em compact, medium e expanded, com o tema de cor e o modo pelo botão Paleta, com o composto e a identidade de cada bloco e a apresentação sobre o fundo Halo com o logo](../2_Diagramas/vista-landing-pagina_v08_20261024-0357.svg)](../2_Diagramas/vista-landing-pagina_v08_20261024-0357.svg)

**Página do módulo · vista** — `2_Diagramas/vista-landing-modulo_v05_20261024-0357.svg`

[![Vista da página do módulo, no exemplo do módulo Presença, em compact, medium e expanded; tema de cor e modo pelo botão Paleta](../2_Diagramas/vista-landing-modulo_v05_20261024-0357.svg)](../2_Diagramas/vista-landing-modulo_v05_20261024-0357.svg)

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v14** | 2026-11-05 12:34 | Formato `.md`: migrado de `.html` sem mudar cenários, telas nem diagramas; links entre documentos apontam para `.md` |
| v13 | 2026-11-03 22:11 | Aprovado, sem pendências em aberto |
| v12 | 2026-11-01 18:21 | Só Gherkin, telas e diagramas: contexto, tabelas de RF, RN e RNF e fora de escopo viram cenários (mesmos IDs como âncora); vistas em Telas; status → Em revisão |
| v11 | 2026-10-24 03:59 | Vistas com paletas (casca v06): uma linha de molduras e o botão **Paleta** para ver as telas em todos os temas do design system, claro ou escuro (padrão Branco e Preto, claro); vistas da página v08 e da página do módulo v05; vista no tema Roxo removida; status → Aprovado |
| v10 | 2026-10-21 22:12 | `RF-64` alterado: apresentação sobre o fundo Halo com o logo inteiro na abertura; `RF-65` removido (turma de mascotes); `RF-103` criado: brilhos do Halo passando pelos temas coloridos; `RNF-150` e `RN-137` alterados; vistas v07 e no tema Roxo v06; status → Aprovado |
| v09 | 2026-10-20 01:57 | Status → Aprovado |
| v08 | 2026-10-20 01:27 | `RN-137` incluído: fundo Estampa em toda a página, abaixo da barra do topo e fora do menu; vistas da página v06, no tema Roxo v05 e da página do módulo v04; status → Em revisão; Destaque, Como funciona (do destaque) e Módulos, nessa ordem (`RF-66`, `RF-68`); vitrine de telas na página do módulo (`RF-70`) |
| v07 | 2026-10-19 15:06 | Status → Aprovado |
| v06 | 2026-10-19 14:24 | `RF-74` alterado: destaque com o fundo Anel gigante na forma do padrinho; cards do catálogo com as Ondas do vão (comum `RF-44`); vistas da página v05, no tema Roxo v04 e da página do módulo v03; status → Em revisão |
| v05 | 2026-10-19 04:35 | Status → Aprovado |

---

<sub>boaz · v14 · 2026-11-05 12:34 · `1_Requisitos/11_landing-page-ui.md`</sub>
