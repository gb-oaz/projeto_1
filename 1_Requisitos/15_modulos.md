---
projeto: "boaz"
documento: "15_modulos"
tipo: "Funcionalidade"
titulo: "Módulos"
versao: "v31"
data: "2026-11-12 16:58"
status: "Em revisão"
objetivo: "definir como um módulo do deboaz é construído, para que todo módulo siga o mesmo molde antes de entrar no catálogo."
---

> **boaz · Funcionalidade** · `15_modulos` · **v31** · 2026-11-12 16:58 · **Em revisão**

# Módulos

**Objetivo:** definir como um módulo do deboaz é construído, para que todo módulo siga o mesmo molde antes de entrar no catálogo.

---

<a id="cenarios-tela"></a>

## Cenários de tela

<a id="porta-de-entrada"></a>

### Porta de entrada de todo módulo

```gherkin
Cenário: registrar antes de especificar
  Dado o time deboaz com a ideia do módulo Agenda
  Quando registra a Agenda aqui, com nome, tipo, cobrança e quem usa
  Então a Agenda ganha o ID "MOD-12"
  E só depois ganha o documento "NN_modulo-agenda.md", as vistas e o lugar no Catálogo

Cenário: documento grande se divide
  Dado o documento do módulo Usuários com mais de 12 requisitos funcionais
  Então ele se divide em "17_modulo-usuario-a-autenticacao.md"
  E "17_modulo-usuario-b-autorizacao.md"
```

<a id="RF-126"></a>

### `RF-126` Módulo só para os perfis registrados

```gherkin
Cenário: perfil registrado
  Dado a Presença contratada pela Escola Bento Freitas
  E o professor Caio, staff com Utilização na Presença
  Quando ele abre Módulos no hub
  Então vê o card da Presença

Cenário: perfil fora do registro
  Dado o Catálogo registrado só para o administrador e os membros do time deboaz
  Quando a conta master da Escola Bento Freitas abre Módulos
  Então não vê o Catálogo

Cenário: frente fora do registro
  Dado a Presença registrada para o participante só no quest
  Quando o Lucas Andrade entra no quest com o código EBFGA-LUCAS-0412
  Então vê a presença dele no quest
  E nenhuma tela da Presença pede entrada no hub para ele
```

<a id="RF-127"></a>

### `RF-127` Registro no Catálogo

```gherkin
Cenário: dados do registro
  Dado o módulo Usuários no Catálogo
  Quando o administrador do deboaz abre a aba Dados
  Então vê "MOD-03", "Da conta" e "Com custo · por usuário"
  E os perfis Administrador do deboaz, Conta master da instituição e Educador autônomo
  E o perfil Staff, todos no hub

Cenário: registro diferente do Catálogo
  Dado a Presença registrada também para o responsável no quest
  Quando o Catálogo ainda não tem o responsável na Presença
  Então a aba Dados da Presença mostra o aviso "Diferente do registro MOD-06"
```

<a id="RF-132"></a>

### `RF-132` Ficha do módulo

```gherkin
Cenário: ficha incompleta
  Dado a Agenda catalogada sem o dado macro do participante
  Quando o administrador do deboaz abre a aba Dados da Agenda
  Então vê "Dado macro · Participante" marcado como "Falta"
  E não consegue passar a Agenda a Disponível

Cenário: ficha completa
  Dado a Presença com todos os dados da ficha, para cada perfil que vê o card
  Quando o administrador do deboaz passa a Presença a Disponível
  Então a Presença aparece na vitrine do hub

Cenário: ícone repetido
  Dado o ícone de fact_check já usado pela Presença
  Quando a ficha de outro módulo traz o mesmo ícone
  Então a aba Dados mostra "Ícone já usado pela Presença (MOD-06)"
```

<a id="RF-128"></a>

### `RF-128` Quatro tamanhos do card

```gherkin
Cenário: tamanho padrão
  Dado a Escola Bento Freitas com a Presença recém-contratada
  Quando a conta master abre Contratados
  Então o card da Presença aparece no tamanho normal, quadrado, na largura toda no compact

Cenário: abrir pela reduzida
  Dado o card da Carteirinha na reduzida, na Carteira do Lucas Andrade
  Quando o Lucas toca no card
  Então a Carteirinha abre, como abriria na normal ou na expandida

Cenário: proporções
  Dado a Escola Bento Freitas com o Quiz na mínima e o SAC na reduzida
  E a Presença na normal e a Carteirinha na expandida
  Quando a conta master abre Contratados no compact, com 4 colunas
  Então o Quiz ocupa 1 coluna e 1 linha, o SAC 2 × 2, a Presença a largura toda e a mesma altura
  E a Carteirinha a largura toda e o dobro da altura da Presença

Cenário: janela larga
  Dado os mesmos cards
  Quando a conta master abre Contratados numa janela expanded
  Então cada card tem o mesmo tamanho do compact
  E mais cards ficam lado a lado

Cenário: expandida inteira
  Quando a conta master rola Contratados até a Carteirinha
  Então a barra do topo e as abas se escondem
  E a Carteirinha aparece inteira acima da barra de navegação
```

<a id="RF-129"></a>

### `RF-129` Ordem e tamanho no aparelho

```gherkin
Cenário: mudar o tamanho
  Dado a conta master em Ordenar
  Quando toca no menu do card da Presença e escolhe "Expandida"
  Então o card da Presença ocupa a linha toda
  E no menu "Expandida" aparece marcada

Cenário: outro aparelho
  Dado o Lucas Andrade com a Carteirinha na reduzida no celular
  Quando ele entra no quest pelo computador da escola
  Então a Carteirinha aparece no tamanho normal
```

<a id="RF-133"></a>

### `RF-133` Atualização dos números

> Escolha salva para a pessoa: [17_modulo-usuario `RN-283`](17_modulo-usuario.md#RN-283)

```gherkin
Cenário: na abertura, o padrão
  Dado a conta master da Escola Bento Freitas com Módulos aberto e "Na abertura" marcado na Conta
  E o card de Usuários com "248" usuários ativos
  Quando um staff cria um participante em outro aparelho
  Então o card continua com "248"
  E ao sair de Módulos e voltar, mostra "249"

Cenário: ao vivo
  Dado a mesma conta master com "Ao vivo" e "A cada 15 s" marcados na Conta
  Quando um staff cria um participante em outro aparelho
  Então em até 15 s o card de Usuários passa a "249" sem ela sair de Módulos
  E a rolagem da tela não muda

Cenário: outro aparelho
  Dado a conta master com "Ao vivo" marcado no computador
  Quando ela entra no hub pelo celular
  Então a Conta mostra "Ao vivo" marcado

Cenário: escolha no quest
  Dado o Lucas Andrade no Perfil do quest
  Quando ele marca "Ao vivo" em "Atualização dos cards"
  Então os cards da Carteira passam a se atualizar com a tela aberta
```

<a id="RN-202"></a>

### `RN-202` Registro antes do documento

```gherkin
Cenário: ID nunca reaproveitado
  Dado o "MOD-10" Flashcards retirado
  Quando o time registra um módulo novo
  Então ele recebe o próximo ID livre, e não "MOD-10"

Cenário: módulo sem registro
  Dado um módulo "Biblioteca" sem linha neste registro
  Então ele não tem documento, vista nem lugar no Catálogo
```

<a id="RN-203"></a>

### `RN-203` Tipos de módulo

```gherkin
Esquema do Cenário: comportamento de cada tipo
  Quando o módulo é <tipo>
  Então <como entra>
  E <vitrine>

  Exemplos:
    | tipo        | como entra                           | vitrine                   |
    | da conta    | entra sozinho com a conta e não sai  | não aparece na vitrine    |
    | contratável | entra quando a conta master contrata | aparece na vitrine do hub |
    | do time     | só na conta do time, sem custo       | nunca vai à vitrine       |
```

<a id="RN-204"></a>

### `RN-204` Cobrança

```gherkin
Esquema do Cenário: modelo de cobrança
  Quando o módulo <módulo> é <cobrança>
  Então contam <quem conta>

  Exemplos:
    | módulo   | cobrança                     | quem conta                                      |
    | Usuários | com custo · por usuário      | staff e participantes ativos                    |
    | Quiz     | com custo · por participante | participantes ativos                            |
    | Tarefas  | com custo · por participante | participantes ativos, staff já paga no Usuários |
    | Perfil   | sem custo                    | ninguém                                         |

Cenário: quem nunca conta
  Então o responsável e o acesso anônimo nunca contam na cobrança
  E preço, desconto e plano anual são do Catálogo
```

<a id="RN-205"></a>

### `RN-205` Perfis

```gherkin
Esquema do Cenário: perfil válido em Quem usa
  Quando o registro cita o perfil "<perfil>"
  Então o perfil é <resultado>

  Exemplos:
    | perfil                      | resultado                  |
    | Administrador do deboaz     | aceito                     |
    | Membro do time deboaz       | aceito                     |
    | Conta master da instituição | aceito                     |
    | Educador autônomo           | aceito                     |
    | Staff                       | aceito                     |
    | Participante                | aceito                     |
    | Responsável                 | aceito                     |
    | Acesso anônimo              | aceito                     |
    | Professor                   | recusado: entra antes aqui |
```

<a id="RN-206"></a>

### `RN-206` Frentes

```gherkin
Cenário: frentes de um contratável
  Dado a Presença, contratável
  Então o staff a usa no hub, o participante no quest
  E a Presença tem a página do módulo na landing page
```

<a id="RN-207"></a>

### `RN-207` Ciclo de vida

```gherkin
Cenário: ordem dos estados
  Então o módulo passa por Registrado, Especificado, Vistas aprovadas, Catalogado e Disponível
  E pode ir a Retirado de qualquer estado

Cenário: módulo juntado a outro
  Quando o Flashcards é juntado às Tarefas
  Então o "MOD-10" fica no registro com o estado "Retirado: juntado ao MOD-09"
```

<a id="RN-208"></a>

### `RN-208` Documento do módulo

```gherkin
Cenário: o que cada linha de Quem usa gera
  Dado a linha "Presença · Staff · Hub · fazer a chamada"
  Então ela vira ao menos um requisito funcional
  E um ator no diagrama de casos de uso
  E a vista "vista-hub-presenca-staff"
```

<a id="RN-209"></a>

### `RN-209` Mudança começa aqui

```gherkin
Cenário: renomear um módulo
  Quando o time decide renomear "Mensagens" para "Conversas"
  Então muda primeiro este registro, com versão nova
  E depois o documento do módulo e o Catálogo
```

<a id="RN-213"></a>

### `RN-213` Tamanho inicial do card

```gherkin
Cenário: começa no normal
  Quando a escola contrata a Presença
  Então o card da Presença começa no tamanho normal

Cenário: módulo sem documento
  Dado a Agenda só registrada
  Então o card mostra só o ícone, o nome, o selo e o espaço do conteúdo
```

<a id="RN-214"></a>

### `RN-214` Grade dos cards

```gherkin
Esquema do Cenário: espaço de cada tamanho
  Quando o card está na <tamanho>
  Então ocupa <colunas> colunas e <linhas> linhas

  Exemplos:
    | tamanho   | colunas | linhas |
    | mínima    | 1       | 1      |
    | reduzida  | 2       | 2      |
    | normal    | 4       | 4      |
    | expandida | 4       | 8      |

Cenário: janela larga
  Dado células quadradas de lado mínimo 76dp e pelo menos 4 colunas
  Quando a janela fica mais larga
  Então a grade ganha colunas e o card mantém o tamanho
  E cada card entra no primeiro lugar livre, na ordem da pessoa
```

<a id="RN-217"></a>

### `RN-217` Ficha do módulo

```gherkin
Cenário: ficha por perfil
  Dado a Presença vista no card pela conta master e pelo staff
  Então a ficha traz os dados do card uma vez para cada um

Cenário: ficha libera o ciclo
  Dado a Agenda com a ficha incompleta
  Então a Agenda não passa de Registrado a Especificado
  E só passa a Vistas aprovadas com os quatro tamanhos desenhados
```

<a id="RN-218"></a>

### `RN-218` Limites da ficha

```gherkin
Esquema do Cenário: limite de cada dado
  Quando o documento traz <dado> com <tamanho>
  Então <resultado>

  Exemplos:
    | dado                | tamanho       | resultado                       |
    | o nome              | 17 caracteres | é encurtado no documento        |
    | o valor do macro    | 7 caracteres  | é encurtado no documento        |
    | o rótulo do macro   | 20 caracteres | cabe                            |
    | a série curta       | 5 pontos      | cabe                            |
    | os números de apoio | 4             | é encurtado para 3 no documento |
    | a série longa       | 12 pontos     | cabe                            |
    | a lista             | 4 linhas      | é encurtada para 3 no documento |
```

<a id="RN-219"></a>

### `RN-219` Ícone do módulo

```gherkin
Cenário: ícone repetido
  Dado o ícone "fact_check" da Presença
  Quando a Agenda traz o mesmo ícone
  Então o ícone é recusado

Cenário: ícone novo
  Quando um módulo precisa de um ícone que não existe
  Então o ícone entra antes no design system
```

<a id="RN-216"></a>

### `RN-216` Rolagem da grade

```gherkin
Cenário: rolar para baixo
  Dado Módulos aberto no compact
  Quando a pessoa rola para baixo
  Então a barra do topo e as abas se escondem e a barra de navegação fica
  E quando rola para cima, elas voltam
```

<a id="RN-215"></a>

### `RN-215` Preferência do aparelho

```gherkin
Cenário: aparelho novo
  Dado o Lucas com a Carteirinha na reduzida no celular
  Quando ele limpa os dados do app
  Então os cards voltam à ordem padrão e ao tamanho normal
```

<a id="RN-220"></a>

### `RN-220` Atualização dos cards

```gherkin
Cenário: padrão e alcance
  Dado a conta master sem escolha feita
  Então a atualização é "Na abertura", para todos os cards, em todos os aparelhos

Esquema do Cenário: o que conta como abertura
  Dado "Na abertura" marcado
  Quando a conta master <ação>
  Então os números dos cards se atualizam

  Exemplos:
    | ação                                  |
    | toca em Módulos na barra de navegação |
    | troca de aba em Módulos               |
    | volta de um módulo aberto             |
    | puxa a tela para atualizar            |

Cenário: ao vivo sem piscar
  Dado "Ao vivo" marcado
  Quando o número de um card muda
  Então muda no lugar, sem piscar o card nem mudar a rolagem
  E o selo de notificações segue a mesma escolha
```

<a id="RN-260"></a>

### `RN-260` Imagem enviada em SVG

```gherkin
Cenário: enviar uma imagem
  Dado uma imagem PNG, JPEG ou WebP escolhida numa tela do hub ou do quest
  Quando a pessoa confirma o envio
  Então o hub ou o quest converte a imagem em SVG no navegador ou no aparelho
  E envia o SVG como texto, no corpo do pedido
  E o deboaz recebe a imagem só como texto SVG

Esquema do Cenário: toda imagem segue a regra
  Quando a pessoa envia <imagem>
  Então ela segue para o deboaz convertida em SVG

  Exemplos:
    | imagem                                   |
    | a foto da instituição no hub             |
    | a foto do estudante no quest             |
    | uma imagem da vitrine de um módulo       |
    | qualquer imagem pedida por um módulo     |
```

<a id="RN-276"></a>

### `RN-276` Card comum de todo módulo

> Ícone do card: [`RN-219`](#RN-219), guardado na aba Dados do [16_modulo-catalogo `RF-123`](16_modulo-catalogo.md#RF-123)

```gherkin
Cenário: participante como perfil do card
  Dado a ficha da Carteirinha com o card para a conta master e para o participante
  Quando o Lucas Andrade abre a Carteira no quest
  Então vê o card da Carteirinha

Cenário: perfil fora da ficha
  Dado a ficha do Usuários sem o participante entre os perfis do card
  Quando o Lucas Andrade abre a Carteira no quest
  Então não vê o card do Usuários

Cenário: mesmo ícone em todo perfil e tamanho
  Dado o ícone do Usuários guardado na aba Dados do Catálogo
  Quando a conta master e a staff Maria veem o card do Usuários nos quatro tamanhos
  Então as duas veem o mesmo ícone em todos eles
  E os números mudam de um perfil para o outro
```

<a id="RN-277"></a>

### `RN-277` Recortes dia, semana e mês

> Toque no card fora do seletor abre o módulo: [`RF-128`](#RF-128)

```gherkin
Esquema do Cenário: série de cada tamanho
  Dado o card do Usuários da EBFGA em 2026-11-08 com o recorte "<recorte>"
  Quando o card está na <tamanho>
  Então a série mostra <pontos>, o atual por último, com os rótulos <rotulos>

  Exemplos:
    | recorte | tamanho   | pontos                | rotulos                |
    | Mês     | normal    | os 5 últimos meses    | "jul" a "nov"          |
    | Mês     | expandida | os 12 últimos meses   | "dez" a "nov"          |
    | Semana  | normal    | as 5 últimas semanas  | "S8" a "S12"           |
    | Semana  | expandida | as 12 últimas semanas | "S1" a "S12"           |
    | Dia     | normal    | os 5 últimos dias     | "4" a "8"              |
    | Dia     | expandida | os 12 últimos dias    | "28" a "8"             |

Esquema do Cenário: tendência do recorte
  Dado o card do Usuários da EBFGA com 335 agora e <antes> no fim do <periodo> anterior
  Quando a conta master escolhe "<recorte>" no card
  Então a tendência mostra "<tendencia>"

  Exemplos:
    | recorte | periodo | antes | tendencia    |
    | Mês     | mês     | 323   | +12 no mês   |
    | Semana  | semana  | 333   | +2 na semana |
    | Dia     | dia     | 334   | +1 no dia    |

Cenário: trocar o recorte sem abrir o módulo
  Dado a conta master com o card do Usuários na normal, em "Mês", no celular
  Quando toca em "Semana" no seletor Dia, Semana e Mês do card
  Então a série e a tendência passam à semana e o módulo não abre
  E no computador o card continua em "Mês", o padrão, porque a escolha fica no aparelho
```

<a id="RN-279"></a>

### `RN-279` Atualização na abertura e ao vivo

> Escolha da pessoa: [`RF-133`](#RF-133) e [`RN-220`](#RN-220)

```gherkin
Cenário: padrão, sem conexão aberta
  Dado a conta master da EBFGA sem escolha feita, com "Na abertura" marcado
  Quando toca em Módulos na barra de navegação
  Então os números dos cards se atualizam nesse momento
  E nada mais chega até a próxima abertura, para economizar dados

Esquema do Cenário: ligar o ao vivo
  Dado a conta master com "Na abertura" marcado na Conta
  Quando marca "Ao vivo" e escolhe "<intervalo>"
  Então os cards se atualizam a cada <segundos> s com Módulos aberto
  E a linha "Intervalo" aparece só com "Ao vivo" marcado
  E a escolha vale também nos outros aparelhos dela

  Exemplos:
    | intervalo                 | segundos |
    | A cada 5 s                | 5        |
    | A cada 15 s               | 15       |
    | A cada 30 s               | 30       |
    | A cada 1 min              | 60       |
    | A cada 5 min, já marcado  | 300      |

Cenário: desligar o ao vivo para economizar dados
  Dado a conta master com "Ao vivo" e "A cada 5 s" marcados
  Quando marca "Na abertura"
  Então a conexão ao vivo fecha e a linha "Intervalo" se esconde
  E os números só se atualizam ao tocar em Módulos, como no padrão
```

<a id="RN-282"></a>

### `RN-282` Ao vivo encerrado e queda de rede

```gherkin
Esquema do Cenário: encerrado pelo deboaz
  Dado a conta master com "Ao vivo" salvo e Módulos aberto no tablet
  Quando o tablet recebe o evento "encerrado" com o código "<codigo>"
  Então o tablet mostra o snackbar "<aviso>", sem ação, que some sozinho
  E o seletor do tablet segue mostrando "Na abertura" marcado
  E só o tablet fica pausado em "Na abertura"; o "Ao vivo" salvo para ela não muda
  E o tablet não reconecta sozinho

  Exemplos:
    | codigo          | aviso                                         |
    | LIMITE_CONEXOES | Ao vivo pausado: aberto em outros 3 aparelhos |
    | ACESSO_NEGADO   | Ao vivo pausado: sem acesso a este módulo     |

Cenário: queda de rede
  Dado a conta master com "Ao vivo" e Módulos aberto no celular
  Quando a rede cai
  Então o celular tenta reconectar depois de 1, 2, 4, 8, 16, 32 e 60 s
  E depois a cada 60 s, até a rede voltar ou Módulos fechar
  E enquanto tenta, cada card mantém os últimos números com a marca "reconectando…"
  E a tela segue livre para rolar e tocar
  E o card mostra os números atuais ao reconectar

Cenário: reabrir o app não religa
  Dado o tablet pausado em "Na abertura" depois do "encerrado", com "Ao vivo" salvo
  Quando a conta master fecha e abre o app no tablet
  Então o tablet continua pausado e os números só se atualizam ao tocar em Módulos
  E quando ela marca "Ao vivo" de novo no tablet, a pausa sai e o tablet reconecta
  E nada novo é salvo, porque a escolha dela já era "Ao vivo"

Cenário: sessão terminada com o ao vivo aberto
  Dado a conta master com "Ao vivo" e Módulos aberto no tablet
  Quando o tablet recebe o evento "encerrado" com o código "SESSAO_TERMINADA"
  Então não aparece snackbar de ao vivo pausado
  E ela vai para a tela de entrada, como em qualquer sessão terminada
```

<a id="MOD-01"></a>

### `MOD-01` Contas

```gherkin
Esquema do Cenário: registro do Contas
  Dado o módulo "MOD-01" Contas, da conta, sem custo, sem documento, Registrado
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil                      | frente | objetivo                                  |
    | a conta master e o autônomo | hub    | ver e pagar a cobrança mensal dos módulos |
```

<a id="MOD-02"></a>

### `MOD-02` SAC

```gherkin
Esquema do Cenário: registro do SAC
  Dado o módulo "MOD-02" SAC, da conta, sem custo, Especificado
  E o documento 18_modulo-sac
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil                      | frente | objetivo                                        |
    | a conta master e o autônomo | hub    | falar com o time deboaz                         |
    | o membro do time            | hub    | atender as contas e o pedido de senha da master |
```

<a id="MOD-03"></a>

### `MOD-03` Usuários

```gherkin
Esquema do Cenário: registro do Usuários
  Dado o módulo "MOD-03" Usuários, da conta, com custo por usuário, Especificado
  E os documentos 17_modulo-usuario, 17_modulo-usuario-a e 17_modulo-usuario-b
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil           | frente | objetivo                                         |
    | o administrador  | hub    | criar membros do time e cuidar das contas        |
    | o membro do time | hub    | cuidar das contas na lista Contas                |
    | a conta master   | hub    | criar staff e participantes e decidir permissões |
    | o autônomo       | hub    | criar os participantes                           |
    | o staff com Adm. | hub    | criar e cuidar dos usuários abaixo dele          |
```

<a id="MOD-04"></a>

### `MOD-04` Sugestão

```gherkin
Esquema do Cenário: registro da Sugestão
  Dado o módulo "MOD-04" Sugestão, da conta, sem custo, sem documento, Registrado
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil         | frente | objetivo                       |
    | o staff        | hub    | sugerir módulos à conta master |
    | a conta master | hub    | receber as sugestões do staff  |
```

<a id="MOD-05"></a>

### `MOD-05` Quiz

```gherkin
Esquema do Cenário: registro do Quiz
  Dado o módulo "MOD-05" Quiz, contratável, com custo por participante, Registrado
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil                     | frente | objetivo                                     |
    | o staff e o autônomo       | hub    | criar o quiz, abrir a sala e passar o código |
    | o participante e o anônimo | quest  | entrar na sala e responder ao vivo           |
```

<a id="MOD-06"></a>

### `MOD-06` Presença

```gherkin
Esquema do Cenário: registro da Presença
  Dado o módulo "MOD-06" Presença, contratável, com custo por participante, Registrado
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil                         | frente | objetivo                           |
    | a conta master                 | hub    | contratar e delegar os professores |
    | o staff                        | hub    | fazer a chamada na aba Utilização  |
    | o participante e o responsável | quest  | acompanhar a presença              |
```

<a id="MOD-07"></a>

### `MOD-07` Carteirinha

```gherkin
Esquema do Cenário: registro da Carteirinha
  Dado o módulo "MOD-07" Carteirinha, contratável, com custo por participante, Registrado
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil         | frente | objetivo                                     |
    | a conta master | hub    | contratar e configurar a carteirinha         |
    | o participante | quest  | completar com a foto e mostrar, na Carteira  |
    | o responsável  | quest  | autorizar a foto do estudante menor de idade |
```

<a id="MOD-08"></a>

### `MOD-08` Turmas

```gherkin
Esquema do Cenário: registro do Turmas
  Dado o módulo "MOD-08" Turmas, da conta, sem custo, sem documento, Registrado
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil                   | frente | objetivo                                      |
    | a conta master e o staff | hub    | cadastrar as turmas, o staff com Configuração |
```

<a id="MOD-09"></a>

### `MOD-09` Tarefas

```gherkin
Esquema do Cenário: registro do Tarefas
  Dado o módulo "MOD-09" Tarefas, com flashcards, da conta, com custo por participante
  E o estado Registrado, sem documento
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil               | frente | objetivo                                           |
    | o staff e o autônomo | hub    | criar tarefas com prazo e envios                   |
    | o participante       | quest  | fazer as tarefas e ver os prazos no espaço Tarefas |
    | o staff e o autônomo | hub    | criar tarefas de estudo com flashcards             |
    | o participante       | quest  | estudar com os flashcards na Carteira              |

Cenário: regras que voltam no documento do módulo
  Então a agenda, o card Agora e os desafios saíram da casca e voltam no documento do Tarefas
```

<a id="MOD-10"></a>

### `MOD-10` Flashcards

```gherkin
Cenário: retirado
  Então o "MOD-10" Flashcards está "Retirado: juntado ao MOD-09"
```

<a id="MOD-11"></a>

### `MOD-11` Chat da turma

```gherkin
Cenário: retirado
  Então o "MOD-11" Chat da turma está "Retirado: juntado ao MOD-16"
```

<a id="MOD-12"></a>

### `MOD-12` Agenda

```gherkin
Esquema do Cenário: registro da Agenda
  Dado o módulo "MOD-12" Agenda, contratável, com custo por participante, Registrado
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil                         | frente | objetivo                                     |
    | a conta master                 | hub    | publicar o cronograma escolar                |
    | o participante e o responsável | quest  | ver os eventos e receber o aviso antes deles |
```

<a id="MOD-13"></a>

### `MOD-13` Perfil

```gherkin
Esquema do Cenário: registro do Perfil
  Dado o módulo "MOD-13" Perfil, da conta, sem custo, sem documento, Registrado
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil                         | frente | objetivo                                          |
    | o participante e o responsável | quest  | acompanhar agenda, notas, pontuação e engajamento |
```

<a id="MOD-14"></a>

### `MOD-14` Catálogo

```gherkin
Esquema do Cenário: registro do Catálogo
  Dado o módulo "MOD-14" Catálogo, do time, sem custo, documento 16_modulo-catalogo, Especificado
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil           | frente | objetivo                                        |
    | o administrador  | hub    | catalogar e definir preço, faixas e plano anual |
    | o membro do time | hub    | ver o catálogo e simular, sem salvar            |
```

<a id="MOD-15"></a>

### `MOD-15` Painel de administração

```gherkin
Cenário: retirado
  Então o "MOD-15" Painel de administração está "Retirado: juntado ao MOD-03"
```

<a id="MOD-16"></a>

### `MOD-16` Mensagens

```gherkin
Esquema do Cenário: registro do Mensagens
  Dado o módulo "MOD-16" Mensagens, contratável, com custo por participante, Registrado
  Então <perfil> o usa no <frente> para <objetivo>

  Exemplos:
    | perfil                         | frente | objetivo                                         |
    | o participante e o responsável | quest  | conversar entre si, na turma e com a escola      |
    | a conta master e o staff       | hub    | conversar com os participantes e os responsáveis |
```

<a id="ficha-do-modulo"></a>

### Ficha do módulo

```gherkin
Esquema do Cenário: o que todo módulo entrega
  Quando o módulo é especificado
  Então a ficha traz <dado> de <quem precisa>, usado em <onde>

  Exemplos:
    | dado                          | quem precisa           | onde                                |
    | o ícone, só dele              | todo módulo            | os quatro tamanhos e o Catálogo     |
    | os perfis que veem o card     | todo módulo            | o card; o participante pode ser um  |
    | o nome, até 16 caracteres     | todo módulo            | reduzida, normal, expandida e topo  |
    | a frase, até duas linhas      | contratável            | Catálogo e vitrine da landing page  |
    | o mascote padrinho            | contratável            | Catálogo e destaque da landing page |
    | as notificações do selo       | todo módulo            | o selo, nos quatro tamanhos         |
    | o dado macro, por perfil      | todo módulo            | reduzida                            |
    | o número principal e rótulo   | todo módulo            | normal e expandida                  |
    | a tendência e o sentido       | número que muda        | normal e expandida                  |
    | a série curta ou progresso    | todo módulo            | normal, por dia, semana e mês       |
    | até 3 números de apoio        | opcional               | normal e expandida                  |
    | a série longa, 12 pontos      | todo módulo            | expandida, por dia, semana e mês    |
    | a lista, até 3 linhas         | todo módulo            | expandida                           |
    | o estado sem dados            | todo módulo            | normal, expandida e reduzida        |
    | o rótulo falado               | todo módulo            | os quatro tamanhos                  |
    | as publicações no Feed        | módulo que publica     | Feed do hub e do quest              |
    | as abas e quem vê cada uma    | módulo com tela no hub | topo do módulo aberto               |
    | o aviso para completar        | módulo que pede algo   | módulo aberto e card                |
    | a vitrine de telas            | contratável            | Catálogo e página do módulo         |
    | como funciona e quem usa      | contratável            | Catálogo e página do módulo         |

Cenário: cada número de uma consulta
  Quando o módulo é especificado
  Então cada número do card diz de que dados do próprio módulo sai
  E o documento do módulo guarda esses números numa coleção só dele, como na RN-278
```

<a id="fora-de-escopo"></a>

### Fora de escopo

```gherkin
Cenário: o que este documento não trata
  Então preço, desconto, plano anual e quem contratou ficam no Catálogo
  E as permissões dentro de um módulo ficam no Módulo Usuários
  E funcionalidades, telas e valores da ficha ficam no documento de cada módulo
  E o desenho da grade e do menu do card fica nos componentes comuns e nas vistas
```

---

<a id="cenarios-api"></a>

## Cenários de API

<a id="RN-276-api"></a>

### `RN-276` Card comum de todo módulo

> Corpo inteiro do card do Usuários: [17_modulo-usuario `RN-189`](17_modulo-usuario.md#RN-189-api)

```gherkin
Esquema do Cenário: campos do card
  Dado a conta master da EBFGA com o token de acesso e o "MOD-03" contratado
  Quando envia GET /usuarios/card
  Então a resposta é 200 e traz <campo> com <conteudo>

  Exemplos:
    | campo        | conteudo                                                     |
    | modulo       | o ID do módulo, "MOD-03"                                     |
    | perfil       | o perfil de quem pede, "INSTITUICAO"                         |
    | versao       | o número que sobe a cada mudança dos números                 |
    | atualizadoEm | a data e a hora da última mudança, em UTC                    |
    | vazio        | true quando o módulo ainda não tem dados                     |
    | selo         | o número do selo, 0 sem nada a avisar                        |
    | reduzida     | valor e rotulo do dado macro                                 |
    | normal       | valor, rotulo e até 3 apoio, cada um com nome e valor        |
    | expandida    | até 3 linhas em lista, cada uma com nome e valor             |
    | series       | dia, semana e mes, 12 números cada, do mais antigo ao atual  |

Cenário: todos os tamanhos e nenhum ícone
  Quando a conta master envia GET /usuarios/card
  Então a resposta traz a reduzida, a normal e a expandida, sem pedir o tamanho
  E não traz o ícone, que vem do Catálogo

Cenário: perfil fora da ficha
  Dado a ficha do Usuários sem o participante entre os perfis do card
  Quando o Lucas Andrade envia GET /usuarios/card
  Então a resposta é 403 ACESSO_NEGADO
```

<a id="RN-278"></a>

### `RN-278` Números do card guardados prontos

> Instância: coleção "cardUsuarios" do [17_modulo-usuario `RN-280`](17_modulo-usuario.md#RN-280)

```gherkin
Cenário: atualizado na mesma execução da mudança
  Dado o card do Usuários da EBFGA na versao 418, com 298 participantes ativos
  Quando o staff João envia POST /usuarios e cria o participante Lucas Andrade com 201
  Então o card guardado passa à versao 419 com 299 participantes na mesma execução
  E o próximo GET /usuarios/card traz 299, lido do card guardado

Cenário: fechamento do dia, da semana e do mês
  Dado o card do Usuários da EBFGA com 400 dias, 106 semanas e 24 meses guardados
  Quando chega 00:00 de 2027-03-01, segunda-feira, no horário de Brasília
  Então o card guardado abre o dia 2027-03-01, a semana dele e o mês 2027-03
  E sai o dia, a semana e o mês mais antigos, para manter 400, 106 e 24

Cenário: números que mudam só com o tempo
  Dado o Lucas Andrade com o último acesso em 2026-10-01, sem mudança desde então
  Quando a conta master envia GET /usuarios/card em 2026-11-08
  Então o Lucas conta em "Sem acesso há 30 dias"
  E essa contagem, o selo e "Códigos vencidos" são refeitos na leitura e no fechamento do dia
  E a versao não sobe por eles
```

<a id="RN-279-api"></a>

### `RN-279` Atualização na abertura e ao vivo

```gherkin
Cenário: conectar com o intervalo
  Dado a conta master da EBFGA com o token de acesso
  Quando envia GET /usuarios/card/ao-vivo?intervalo=30 com Accept "text/event-stream"
  Então a resposta é 200 e a conexão fica aberta
  E o primeiro evento "card" traz o mesmo corpo de GET /usuarios/card, com id igual à versao
  E a cada 30 s chega um evento "card", se a versao mudou
  E um ": ping" chega a cada 30 s, qualquer que seja o intervalo, para manter a conexão

Esquema do Cenário: intervalo pedido
  Quando a conta master envia GET /usuarios/card/ao-vivo <pedido>
  Então <resposta>

  Exemplos:
    | pedido          | resposta                                                     |
    | ?intervalo=5    | é 200, com um "card" a cada 5 s, se a versao mudou           |
    | ?intervalo=300  | é 200, com um "card" a cada 300 s e um ": ping" a cada 30 s  |
    | sem o parâmetro | é 200, como ?intervalo=300, o padrão                         |
    | ?intervalo=10   | é 422 DADOS_INVALIDOS com o campo "intervalo"                |
    | ?intervalo=0    | é 422 DADOS_INVALIDOS com o campo "intervalo"                |

Cenário: quarta conexão do mesmo usuário
  Dado a conta master com 3 conexões ao vivo abertas: celular, tablet e computador
  Quando ela envia GET /usuarios/card/ao-vivo de um segundo computador
  Então a 4ª conexão recebe 200 e fica aberta
  E a mais antiga recebe o evento "encerrado" com o código "LIMITE_CONEXOES" e fecha
```

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v31** | 2026-11-12 16:58 | `MOD-02` SAC Especificado, com o documento [18_modulo-sac](18_modulo-sac.md) |
| v30 | 2026-11-09 09:05 | Gherkin sem vírgula logo depois de Dado, Então ou E (`RF-129`, `RF-133`, `RN-278`, `RN-279`, `RN-282`), sem mudar o comportamento |
| v29 | 2026-11-09 03:21 | `RN-279`: a linha "Intervalo" só aparece com "Ao vivo" marcado; `RN-282`: aviso em snackbar sem ação que some sozinho, com "Na abertura" visível no seletor; "reconectando…" nos cards sem travar a tela; SESSAO_TERMINADA leva à tela de entrada, sem aviso de pausa |
| v28 | 2026-11-09 00:53 | `RN-282`: a pausa depois do "encerrado" é só daquele aparelho e não muda o "Ao vivo" salvo; reabrir o app não religa, só marcar "Ao vivo" de novo no aparelho, sem salvar nada novo; Em aberto respondido e seção removida |
| v27 | 2026-11-09 00:44 | `RN-277` com os rótulos dos pontos (nov, S1 a S12, dia do mês), a tendência por recorte ("+12 no mês", "+2 na semana", "+1 no dia") e o seletor que não abre o módulo; `RN-278` selo e Códigos vencidos refeitos na leitura, sem subir a versao; `RN-279-api` ping a cada 30 s; `RN-282` novo (aviso do encerrado, aparelho volta a Na abertura, reconexão até 60 s); `RF-133` cita a preferência salva (17 `RN-283`); Em aberto: volta ao ao vivo |
| v26 | 2026-11-08 23:16 | Card comum de todo módulo: `RN-276` (perfis da ficha, o participante pode ser um; ícone do Catálogo; GET /<modulo>/card com todos os tamanhos), `RN-277` recortes dia, semana e mês, `RN-278` números guardados prontos, `RN-279` na abertura e ao vivo (GET /<modulo>/card/ao-vivo, 5 a 300 s, até 3 conexões); `RF-128` quatro tamanhos; `RF-133`, `RN-218`, `RN-220` e a ficha do módulo ajustados; seção Cenários vira Cenários de tela e ganha Cenários de API; status → Em revisão |
| v25 | 2026-11-05 12:34 | Formato `.md`: migrado de `.html` sem mudar cenários, telas nem diagramas; links entre documentos apontam para `.md`; a [porta de entrada](#porta-de-entrada) cita os documentos em `.md` |
| v24 | 2026-11-03 22:11 | Aprovado, sem pendências em aberto |
| v23 | 2026-11-03 22:01 | Imagem enviada em SVG: toda imagem é convertida em SVG no hub ou no quest e enviada como texto (`RN-260`) |
| v22 | 2026-11-03 10:48 | Registro do Catálogo (`MOD-14`) aponta para o documento renomeado 16_modulo-catalogo |

---

<sub>boaz · v31 · 2026-11-12 16:58 · `1_Requisitos/15_modulos.md`</sub>
