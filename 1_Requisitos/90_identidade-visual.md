---
projeto: "boaz"
documento: "90_identidade-visual"
tipo: "Transversal"
titulo: "Identidade visual"
versao: "v26"
data: "2026-11-05 12:34"
status: "Aprovado"
objetivo: "definir como o nome, o símbolo, o ícone, o favicon, as fontes, as ilustrações, os fundos e as animações do deboaz aparecem em todas as frentes."
---

> **boaz · Transversal** · `90_identidade-visual` · **v26** · 2026-11-05 12:34 · **Aprovado**

# Identidade visual

**Objetivo:** definir como o nome, o símbolo, o ícone, o favicon, as fontes, as ilustrações, os fundos e as animações do deboaz aparecem em todas as frentes.

---

<a id="cenarios"></a>

## Cenários

<a id="RN-39"></a>

### `RN-39` Nome

```gherkin
Esquema do Cenário: como escrever
  Quando o nome aparece em <lugar>
  Então é escrito "<forma>"

  Exemplos:
    | lugar                        | forma        |
    | qualquer frente ou documento | deboaz       |
    | dentro do produto            | hub e quest  |
    | fora do produto              | deboaz hub   |
    | fora do produto              | deboaz quest |
```

<a id="RN-40"></a>

### `RN-40` Logo principal

```gherkin
Cenário: composição do logo
  Então o logo é o símbolo seguido de "deboaz", centralizado na altura do símbolo
  E o nome é contorno da Fredoka Medium
```

<a id="RN-41"></a>

### `RN-41` Símbolo

```gherkin
Cenário: forma do símbolo
  Então o símbolo é um b e um d: barriga redonda com uma antena de cada lado
  E um vão que acompanha a curva separa as antenas da barriga

Esquema do Cenário: uso proibido
  Quando alguém <ação> o símbolo
  Então o uso é recusado

  Exemplos:
    | ação                            |
    | distorce                        |
    | gira                            |
    | recorta                         |
    | fecha o vão do                  |
    | muda a proporção das antenas do |
```

<a id="RN-42"></a>

### `RN-42` Identidade monocromática

```gherkin
Esquema do Cenário: cor oficial
  Quando o logo fica sobre fundo <fundo>
  Então aparece em <cor>

  Exemplos:
    | fundo  | cor    |
    | claro  | preto  |
    | escuro | branco |
```

<a id="RN-43"></a>

### `RN-43` Tamanho mínimo

```gherkin
Cenário: 16 px
  Quando o símbolo aparece numa tela
  Então tem pelo menos 16 px de altura
```

<a id="RN-44"></a>

### `RN-44` Ícone das lojas

```gherkin
Cenário: ícone oficial
  Então o ícone das lojas é o símbolo branco sobre quadrado preto
  E fica na área segura de 66 %, sem transparência
```

<a id="RN-45"></a>

### `RN-45` Favicon

```gherkin
Esquema do Cenário: favicon por tema
  Dado o tema <tema> ativo
  Então o favicon é <favicon>

  Exemplos:
    | tema           | favicon                                      |
    | Branco e Preto | o oficial                                    |
    | Oceano         | quadrado na primária e símbolo em on-primary |

Cenário: 16 × 16
  Então o favicon continua legível em 16 × 16 px
```

<a id="RN-46"></a>

### `RN-46` Fontes

```gherkin
Esquema do Cenário: fonte por uso
  Quando a tela mostra <texto>
  Então usa <fonte>

  Exemplos:
    | texto            | fonte   |
    | um título        | Fredoka |
    | o nome do logo   | Fredoka |
    | o corpo do texto | Roboto  |
```

<a id="RN-47"></a>

### `RN-47` Ilustrações

```gherkin
Cenário: estado vazio
  Quando uma lista está vazia
  Então a ilustração é um mascote do design system
```

<a id="RN-48"></a>

### `RN-48` Animação de personagem

```gherkin
Cenário: movimento reduzido
  Dado o aparelho com movimento reduzido
  Então os mascotes aparecem parados
```

<a id="RN-49"></a>

### `RN-49` Hastes por frente

```gherkin
Esquema do Cenário: símbolo de cada frente
  Quando a frente é <frente>
  Então usa o símbolo de hastes <hastes>

  Exemplos:
    | frente       | hastes |
    | landing page | longas |
    | hub          | longas |
    | quest        | curtas |
```

<a id="RN-50"></a>

### `RN-50` Logo no tema de cor

```gherkin
Cenário: tema Oceano
  Dado o tema Oceano ativo
  Então o anel fica na terciária
  E a antena do b e "boaz" na primária
  E a antena do d e "de" na secundária

Cenário: tema monocromático
  Dado o tema Branco e Preto ativo
  Então o logo é o oficial
```

<a id="RN-51"></a>

### `RN-51` Abertura

```gherkin
Cenário: abertura animada
  Quando o hub abre
  Então o anel se desenha, as antenas brotam do vão e sobem e o nome entra
  E tudo acontece em até 2,5 s e termina parado no logo
```

<a id="RN-52"></a>

### `RN-52` Carregamento

```gherkin
Cenário: espera
  Quando o hub espera uma resposta
  Então um arco percorre o anel e as antenas descem ao vão e voltam
  E o texto "Carregando…" aparece junto, nas cores do tema

Cenário: movimento reduzido
  Dado o aparelho com movimento reduzido
  Então a abertura e o carregamento aparecem parados
```

<a id="RN-59"></a>

### `RN-59` Fechamento e ciclo

```gherkin
Cenário: fechamento
  Quando a sessão termina
  Então o nome sai, as antenas recolhem e o anel se apaga, em até 2,5 s

Cenário: ciclo
  Quando uma tela de apresentação usa o ciclo
  Então abertura e fechamento se repetem em laço, com o logo formado entre os dois
```

<a id="RN-53"></a>

### `RN-53` Identidade da instituição

```gherkin
Cenário: escola com identidade própria
  Dado a EBFGA com o módulo de identidade da instituição
  Então os usuários dela veem a identidade da escola no lugar da padrão
  E o símbolo do deboaz aparece menor no carregamento e na assinatura "by deboaz"
```

<a id="RN-54"></a>

### `RN-54` Marca no app

```gherkin
Cenário: tema no celular
  Dado o tema Oceano escolhido no app
  Então o logo e as animações seguem o Oceano
  E o ícone na tela inicial do aparelho continua o oficial
```

<a id="RN-55"></a>

### `RN-55` Fundos da marca

```gherkin
Esquema do Cenário: os seis fundos
  Então o fundo <fundo> existe, feito só de peças do símbolo e de mascotes

  Exemplos:
    | fundo             |
    | Estampa           |
    | Anel gigante      |
    | Antenas em ritmo  |
    | Ondas do vão      |
    | Símbolo recortado |
    | Halo              |

Cenário: Estampa
  Então a Estampa é feita de rabiscos pequenos, girados e num tom discreto
  E serve em formato horizontal, vertical e quadrado
```

<a id="RN-56"></a>

### `RN-56` Uso de cada fundo

```gherkin
Esquema do Cenário: onde cada fundo aparece
  Quando a tela é <tela>
  Então usa o fundo <fundo>

  Exemplos:
    | tela                                         | fundo             |
    | qualquer tela do hub, do quest ou da landing | Estampa           |
    | o login e o verso do cartão do aluno         | Estampa           |
    | o cabeçalho do hub e a capa de sala          | Anel gigante      |
    | o destaque da landing, só com o anel         | Anel gigante      |
    | um rodapé ou uma tela de espera              | Antenas em ritmo  |
    | um formulário do hub                         | Ondas do vão      |
    | o card do catálogo, só com as linhas         | Ondas do vão      |
    | a abertura do app                            | Símbolo recortado |

Cenário: Estampa de fundo de tela
  Então fica abaixo da barra do topo, fora das barras e do menu
  E 30% mais leve, com rabiscos de ~48dp em qualquer janela
```

<a id="RN-57"></a>

### `RN-57` Peças de cada fundo

```gherkin
Cenário: sorteio a cada abertura
  Quando a pessoa abre de novo uma tela com Estampa ou Antenas em ritmo
  Então os mascotes são sorteados de novo
  E as Antenas têm de 0 a 8 mascotes

Cenário: forma do anel
  Dado o Anel gigante com o mascote em forma de estrela
  Então o anel toma a forma de estrela
  E com a forma círculo, é o anel do símbolo
```

<a id="RN-157"></a>

### `RN-157` Ilustrações de status

```gherkin
Esquema do Cenário: os sete estados
  Então existe a ilustração "<estado>"

  Exemplos:
    | estado                |
    | Operacional           |
    | Lento                 |
    | Instabilidade parcial |
    | Fora do ar            |
    | Manutenção programada |
    | Reparo em andamento   |
    | Voltou!               |

Cenário: peças e selo
  Então cada ilustração tem o mascote, o anel como medidor, a linha de pulso e as antenas
  E serve como selo, só com o medidor e o rosto
```

<a id="RN-158"></a>

### `RN-158` Mascote e cor de cada estado

```gherkin
Esquema do Cenário: estado fixo
  Quando o estado é <estado>
  Então o mascote fica <mascote> e a cor é <cor>

  Exemplos:
    | estado                | mascote                  | cor       |
    | Operacional           | feliz                    | sucesso   |
    | Lento                 | esperando                | aviso     |
    | Instabilidade parcial | tonto entre dois felizes | aviso     |
    | Fora do ar            | doente, nunca chorando   | erro      |
    | Manutenção programada | sonolento                | terciária |
    | Reparo em andamento   | consertando              | aviso     |
    | Voltou!               | aliviado comemorando     | sucesso   |
```

<a id="RN-159"></a>

### `RN-159` Uso do status

```gherkin
Cenário: pior estado
  Dado o Quiz lento e o SAC fora do ar
  Então a tela de status mostra Fora do ar

Cenário: manutenção
  Quando há uma manutenção programada até 06:00
  Então a ilustração informa a volta às 06:00

Cenário: depois do reparo
  Quando a pessoa abre o produto pela primeira vez depois do reparo
  Então vê Voltou!

Cenário: lista de serviços
  Então cada serviço tem o selo de 24dp a 64dp com o nome ao lado
```

<a id="RN-160"></a>

### `RN-160` Duas etapas da abertura

```gherkin
Cenário: splash e abertura animada
  Quando o app abre no modo escuro
  Então a splash nativa mostra o símbolo oficial parado no centro do preto, sem texto
  E a abertura animada começa do mesmo preto e do mesmo símbolo, sobre o Halo, sem salto
```

<a id="RN-161"></a>

### `RN-161` Abertura de cada frente

```gherkin
Esquema do Cenário: mascote da abertura do quest
  Quando o Lucas abre o quest <momento>
  Então o mascote entra <expressão> depois do nome

  Exemplos:
    | momento                         | expressão |
    | pela primeira vez               | curioso   |
    | depois de um tempo sem entrar   | tchau     |
    | à noite                         | sonolento |
    | pela primeira vez após o reparo | aliviado  |

Cenário: abertura do hub
  Quando a conta master abre o hub
  Então vê só o logo de hastes longas
  E na primeira abertura depois de um reparo também vê o mascote aliviado
```

<a id="RN-162"></a>

### `RN-162` Carregamento depois da abertura

```gherkin
Cenário: produto ainda carregando
  Dado a abertura animada terminada e o hub ainda carregando
  Então a animação de carregamento aparece embaixo com "Carregando…"
```

<a id="RN-58"></a>

### `RN-58` Logo no fundo

```gherkin
Cenário: logo animado no fundo
  Então o logo no fundo fica parado ou usa abertura, fechamento, ciclo ou carregamento
  E com movimento reduzido, logo e mascotes ficam parados
```

<a id="RNF-116"></a>

### `RNF-116` Texto alternativo

```gherkin
Esquema do Cenário: imagem da marca
  Quando o logo aparece <situação>
  Então ele é <resultado>

  Exemplos:
    | situação                     | resultado          |
    | sozinho                      | lido como "deboaz" |
    | com "deboaz" escrito ao lado | decorativo         |
```

<a id="RNF-117"></a>

### `RNF-117` Ícone do app

```gherkin
Cenário: tamanhos das lojas
  Então todos os tamanhos de ícone do Android e do iOS saem do desenho de 1024 × 1024 px
```

<a id="RNF-119"></a>

### `RNF-119` Texto sobre fundo

```gherkin
Cenário: texto sobre o Halo
  Quando um texto fica sobre o Halo
  Então fica num painel de superfície com contraste de 4,5:1 ou mais

Cenário: texto sobre a Estampa
  Quando um campo outlined fica sobre a Estampa de fundo de tela
  Então o campo ganha o fundo da tela, para o texto ficar legível
  E a Estampa é decorativa para leitores de tela
```

<a id="RNF-199"></a>

### `RNF-199` Status acessível

```gherkin
Cenário: status para o leitor de tela
  Dado a ilustração Fora do ar
  Então o título e a mensagem vêm escritos ao lado, num painel de superfície
  E a ilustração é decorativa e fica parada com movimento reduzido
```

<a id="RNF-200"></a>

### `RNF-200` Ícone da splash

```gherkin
Cenário: círculo central
  Então o ícone da splash cabe no círculo central de 2/3 do quadro
  E o mesmo desenho serve ao iOS e ao app instalado pelo navegador
```

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v26** | 2026-11-05 12:34 | Formato `.md`: migrado de `.html` sem mudar cenários, telas nem diagramas; links entre documentos apontam para `.md` |
| v25 | 2026-11-03 22:11 | Aprovado, sem pendências em aberto |
| v24 | 2026-11-01 18:25 | Só Gherkin: contexto, RN e RNF viram cenários (mesmos IDs como âncora); status → Em revisão |
| v23 | 2026-10-27 23:25 | Status → Aprovado |
| v22 | 2026-10-27 23:17 | Splash screens: `RN-160` a `RN-162` e `RNF-200` incluídos; status → Em revisão |
| v21 | 2026-10-27 23:12 | Ilustrações de status do sistema: `RN-157` a `RN-159` e `RNF-199` incluídos; status → Aprovado |
| v20 | 2026-10-21 22:12 | `RN-56` alterado: Halo nas boas-vindas com o logo (apresentação da landing page, entrada do hub e acesso do quest), com o logo inteiro na abertura e os brilhos passando pelos temas coloridos; Ondas do vão sai da entrada do hub; status → Aprovado |
| v19 | 2026-10-20 01:57 | Status → Aprovado |
| v18 | 2026-10-20 00:43 | `RN-55` e `RN-56` alterados: Estampa como rabiscos espalhados (mascotes em contorno com rosto, números, sinais, o símbolo e miudezas, pequenos, girados e num tom discreto), também na tela Fixos do quest; status → Em revisão; Estampa também no fundo de todas as telas do hub, do quest e da landing page, abaixo da barra do topo e fora das barras de navegação (`RN-56`); texto direto sobre a Estampa (`RNF-119`); componentes sem fundo próprio (campo outlined, botões outlined e de texto soltos na tela, botão segmentado, chip e controles do carrossel) ganham o fundo da tela, legíveis sobre a Estampa (`RNF-119`) |
| v17 | 2026-10-19 15:06 | Status → Aprovado |

---

<sub>boaz · v26 · 2026-11-05 12:34 · `1_Requisitos/90_identidade-visual.md`</sub>
