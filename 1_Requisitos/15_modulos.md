---
projeto: "boaz"
documento: "15_modulos"
tipo: "Funcionalidade"
titulo: "Módulos"
versao: "v25"
data: "2026-11-05 12:34"
status: "Aprovado"
objetivo: "definir como um módulo do deboaz é construído, para que todo módulo siga o mesmo molde antes de entrar no catálogo."
---

> **boaz · Funcionalidade** · `15_modulos` · **v25** · 2026-11-05 12:34 · **Aprovado**

# Módulos

**Objetivo:** definir como um módulo do deboaz é construído, para que todo módulo siga o mesmo molde antes de entrar no catálogo.

---

<a id="cenarios"></a>

## Cenários

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

### `RF-128` Três tamanhos do card

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
  E, no menu, "Expandida" aparece marcada

Cenário: outro aparelho
  Dado o Lucas Andrade com a Carteirinha na reduzida no celular
  Quando ele entra no quest pelo computador da escola
  Então a Carteirinha aparece no tamanho normal
```

<a id="RF-133"></a>

### `RF-133` Atualização dos números

```gherkin
Cenário: na abertura, o padrão
  Dado a conta master da Escola Bento Freitas com Módulos aberto e "Na abertura" marcado na Conta
  E o card de Usuários com "248" usuários ativos
  Quando um staff cria um participante em outro aparelho
  Então o card continua com "248"
  E, ao sair de Módulos e voltar, mostra "249"

Cenário: ao vivo
  Dado a mesma conta master com "Ao vivo" marcado na Conta
  Quando um staff cria um participante em outro aparelho
  Então o card de Usuários passa a "249" sem ela sair de Módulos
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
    | a série das semanas | 7 pontos      | cabe                            |
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
    | ação                       |
    | troca de aba em Módulos    |
    | volta de um módulo aberto  |
    | puxa a tela para atualizar |

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
  Dado o módulo "MOD-02" SAC, da conta, sem custo, sem documento, Registrado
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
    | o nome, até 16 caracteres     | todo módulo            | reduzida, normal, expandida e topo  |
    | a frase, até duas linhas      | contratável            | Catálogo e vitrine da landing page  |
    | o mascote padrinho            | contratável            | Catálogo e destaque da landing page |
    | as notificações do selo       | todo módulo            | o selo, nos quatro tamanhos         |
    | o dado macro, por perfil      | todo módulo            | reduzida                            |
    | o número principal e rótulo   | todo módulo            | normal e expandida                  |
    | a tendência e o sentido       | número que muda        | normal e expandida                  |
    | a série curta ou progresso    | todo módulo            | normal e expandida                  |
    | até 3 números de apoio        | opcional               | normal e expandida                  |
    | a série das semanas, 7 pontos | todo módulo            | expandida                           |
    | a lista, até 3 linhas         | todo módulo            | expandida                           |
    | o estado sem dados            | todo módulo            | normal, expandida e reduzida        |
    | o rótulo falado               | todo módulo            | os quatro tamanhos                  |
    | as publicações no Feed        | módulo que publica     | Feed do hub e do quest              |
    | as abas e quem vê cada uma    | módulo com tela no hub | topo do módulo aberto               |
    | o aviso para completar        | módulo que pede algo   | módulo aberto e card                |
    | a vitrine de telas            | contratável            | Catálogo e página do módulo         |
    | como funciona e quem usa      | contratável            | Catálogo e página do módulo         |
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

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v25** | 2026-11-05 12:34 | Formato `.md`: migrado de `.html` sem mudar cenários, telas nem diagramas; links entre documentos apontam para `.md`; a [porta de entrada](#porta-de-entrada) cita os documentos em `.md` |
| v24 | 2026-11-03 22:11 | Aprovado, sem pendências em aberto |
| v23 | 2026-11-03 22:01 | Imagem enviada em SVG: toda imagem é convertida em SVG no hub ou no quest e enviada como texto (`RN-260`) |
| v22 | 2026-11-03 10:48 | Registro do Catálogo (`MOD-14`) aponta para o documento renomeado 16_modulo-catalogo |
| v21 | 2026-11-01 18:20 | Só Gherkin: contexto, RF, RN, registro de módulos (um cenário por MOD-NN, mesmo ID como âncora), quem usa cada módulo, ficha do módulo e fora de escopo viram cenários; status → Em revisão |
| v20 | 2026-11-01 03:57 | Aprovado |
| v19 | 2026-11-01 03:38 | `MOD-15` Painel de administração retirado: juntado ao `MOD-03` Usuários, que no time deboaz cuida também das contas de instituição e de educador autônomo |
| v18 | 2026-11-01 02:08 | Registro do Usuários (`MOD-03`) aponta também para 17-a (autenticação) e 17-b (autorização) |
| v17 | 2026-11-01 01:20 | Aprovação |
| v16 | 2026-11-01 01:14 | Tarefas (`MOD-09`) passa a ser da conta, base da aplicação, cobrado por participante (`RN-204`); ficha do módulo ganha Publicações no Feed |

---

<sub>boaz · v25 · 2026-11-05 12:34 · `1_Requisitos/15_modulos.md`</sub>
