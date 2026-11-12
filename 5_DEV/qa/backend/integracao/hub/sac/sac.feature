# language: pt
# Cenários de API de 1_Requisitos/18_modulo-sac.md (v04), copiados do requisito:
# mudou lá, muda aqui. Cada cenário leva a tag do ID que cobre.
Funcionalidade: SAC
  1_Requisitos/18_modulo-sac.md · Cenários de API

  @formato-comum
  Cenário: resposta de erro
    Quando um pedido ao SAC é recusado
    Então o corpo traz o código, a mensagem e, quando houver, o campo:
      """
      {
        "codigo": "DADOS_INVALIDOS",
        "mensagem": "O campo \"assunto\" deve ter de 5 a 120 caracteres.",
        "campo": "assunto"
      }
      """

  @formato-comum
  Cenário: identificadores, datas e valores
    Então o chamado vai no endereço pelo número, como "SAC-000042"
    E toda data e hora segue a ISO 8601 em UTC, como "2026-11-12T13:00:00Z"
    E status e categoria vão em maiúsculas, como "EM_ATENDIMENTO" e "MODULO_COM_PROBLEMA"
    E só a conta master, o autônomo, o administrador e o membro com "MOD-02" usam o SAC

  @RF-171
  Cenário: instituição abre um chamado
    Dado o token da conta master da EBFGA, com "MOD-02" com "U"
    E o anexo "anx-7f3a21" enviado antes por POST /sac/anexos
    Quando envia POST /sac/chamados em 2026-11-12T13:00:00Z com:
      """
      {
        "categoria": "MODULO_COM_PROBLEMA",
        "assunto": "Presença não salva a chamada",
        "descricao": "Desde ontem, ao tocar em Salvar na chamada da turma, aparece erro.",
        "relacionadoA": null,
        "anexos": [
          "anx-7f3a21"
        ]
      }
      """
    Então a resposta é 201 com:
      """
      {
        "id": "SAC-000042",
        "status": "ABERTO",
        "categoria": "MODULO_COM_PROBLEMA",
        "abertoEm": "2026-11-12T13:00:00Z",
        "prazoResposta": "2026-11-12T21:00:00Z",
        "responsavel": null
      }
      """

  @RF-171
  Esquema do Cenário: pedido inválido
    Quando a conta master envia POST /sac/chamados com <falha>
    Então a resposta é 422 DADOS_INVALIDOS com o campo "<campo>"

    Exemplos:
      | falha                                 | campo      |
      | sem a categoria                       | categoria  |
      | a categoria "SENHA_CONTA_MASTER"      | categoria  |
      | o assunto com 4 caracteres            | assunto    |
      | a descrição com 5.001 caracteres      | descricao  |
      | o relacionadoA "SAC-000045", do Pedro | relacionadoA |

  @RF-171
  Cenário: membro do time não abre chamado
    Quando o Caio, com "MOD-02" com "U", envia POST /sac/chamados
    Então a resposta é 403 ACESSO_NEGADO

  @RF-172
  Cenário: lista da instituição
    Dado o token da conta master da EBFGA
    Quando envia GET /sac/chamados?situacao=EM_ANDAMENTO&pagina=1
    Então a resposta é 200 com itens e total, até 50 por página
    E itens traz o SAC-000042 e o SAC-000039, o atualizado por último primeiro
    E cada item traz id, assunto, categoria, status, atualizadoEm e novaResposta

  @RF-172
  Cenário: item com resposta nova
    Quando a conta master envia GET /sac/chamados
    Então o item do SAC-000042 traz:
      """
      {
        "id": "SAC-000042",
        "assunto": "Presença não salva a chamada",
        "categoria": "MODULO_COM_PROBLEMA",
        "status": "AGUARDANDO_CLIENTE",
        "atualizadoEm": "2026-11-12T14:20:00Z",
        "novaResposta": true
      }
      """

  @RF-173
  Cenário: abrir o chamado marca como lido
    Quando a conta master envia GET /sac/chamados/SAC-000042
    Então a resposta é 200 com o chamado, as mensagens e a linhaDoTempo, sem as notas internas
    E o próximo GET /sac/chamados traz o SAC-000042 com novaResposta false

  @RF-173
  Cenário: responder ao time
    Quando a conta master envia POST /sac/chamados/SAC-000042/mensagens com:
      """
      {
        "texto": "Turma 7A",
        "anexos": [
          "anx-81bc09"
        ],
        "interna": false
      }
      """
    Então a resposta é 201 com:
      """
      {
        "id": "msg-000318",
        "autor": "Escola Bento Freitas",
        "enviadaEm": "2026-11-12T14:30:00Z",
        "statusChamado": "EM_ATENDIMENTO"
      }
      """

  @RF-173
  Esquema do Cenário: mensagem recusada
    Quando a conta master envia POST /sac/chamados/<chamado>/mensagens com <corpo>
    Então a resposta é <resposta>

    Exemplos:
      | chamado    | corpo                        | resposta                              |
      | SAC-000031 | o texto "Oi"                 | 409 CHAMADO_FECHADO                   |
      | SAC-000042 | o texto vazio                | 422 DADOS_INVALIDOS com o campo texto |
      | SAC-000042 | "interna": true              | 403 ACESSO_NEGADO                     |

  @RF-174
  Cenário: fila de chamados em andamento
    Dado o token do Caio, com o papel "MEMBRO_TIME" e "MOD-02" com "U"
    Quando envia GET /sac/chamados?situacao=EM_ANDAMENTO&pagina=1
    Então a resposta é 200 com os 14 chamados de todas as contas, até 50 por página
    E o mais antigo esperando o time vem primeiro
    E cada item traz também conta, responsavel, prazoResposta e atrasado

  @RF-174
  Esquema do Cenário: filtros da fila
    Quando o Caio envia GET /sac/chamados?<filtro>
    Então itens traz <resultado>

    Exemplos:
      | filtro                        | resultado                                     |
      | responsavel=nenhum            | os "ABERTO" sem responsável                   |
      | responsavel=eu                | os chamados do Caio                           |
      | atrasado=true                 | os 2 com o prazo vencido                      |
      | status=AGUARDANDO_CLIENTE     | os 4 à espera da conta                        |
      | notaMaxima=2                  | os fechados com nota 1 ou 2                   |
      | busca=EBFGA                   | os da Escola Bento Freitas, de todo status    |

  @RF-174
  Cenário: filtro do time pedido pela conta
    Quando a conta master envia GET /sac/chamados?responsavel=eu
    Então a resposta é 422 DADOS_INVALIDOS com o campo "responsavel"

  @RF-175
  Cenário: assumir
    Dado o SAC-000042 "ABERTO", sem responsável
    Quando o Caio envia POST /sac/chamados/SAC-000042/assumir
    Então a resposta é 200 com:
      """
      {
        "id": "SAC-000042",
        "status": "EM_ATENDIMENTO",
        "responsavel": "Caio Lima"
      }
      """

  @RF-175
  Cenário: já assumido por outro
    Dado o SAC-000042 com a Ana Lima como responsável
    Quando o Caio envia POST /sac/chamados/SAC-000042/assumir sem corpo
    Então a resposta é 409 JA_ASSUMIDO com o responsavel "Ana Lima"
    E com "substituir" true no corpo, a resposta é 200 com o Caio como responsável

  @RF-175
  Cenário: nota interna
    Quando o Caio envia POST /sac/chamados/SAC-000042/mensagens com "interna": true
    Então a resposta é 201
    E GET /sac/chamados/SAC-000042 da conta master não traz a nota

  @RF-175
  Cenário: conta não assume
    Quando a conta master envia POST /sac/chamados/SAC-000042/assumir
    Então a resposta é 403 ACESSO_NEGADO

  @RF-176
  Cenário: pedir informação
    Quando o Caio envia POST /sac/chamados/SAC-000042/status com:
      """
      {
        "status": "AGUARDANDO_CLIENTE",
        "mensagem": "Qual a turma?"
      }
      """
    Então a resposta é 200 com o status "AGUARDANDO_CLIENTE"
    E a mensagem entra na conversa, visível para a conta

  @RF-176
  Cenário: resolver
    Quando o Caio envia POST /sac/chamados/SAC-000042/status com:
      """
      {
        "status": "RESOLVIDO",
        "mensagem": "Corrigido. Pode salvar de novo."
      }
      """
    Então a resposta é 200 com o status "RESOLVIDO" e resolvidoEm

  @RF-176
  Cenário: sem mensagem
    Quando o Caio envia o status "RESOLVIDO" sem a mensagem
    Então a resposta é 422 DADOS_INVALIDOS com o campo "mensagem"

  @RF-177
  Cenário: reabrir
    Dado o SAC-000042 "RESOLVIDO" há 2 dias, com a EBFGA no limite de 10 em andamento
    Quando a conta master envia POST /sac/chamados/SAC-000042/reabrir com:
      """
      {
        "mensagem": "Ainda aparece o erro na 8B"
      }
      """
    Então a resposta é 200 com o status "EM_ATENDIMENTO" e o mesmo responsavel

  @RF-177
  Cenário: encerrar
    Quando a conta master envia POST /sac/chamados/SAC-000039/encerrar
    Então a resposta é 200 com o status "FECHADO"

  @RF-177
  Cenário: time não encerra pela conta
    Quando o Caio envia POST /sac/chamados/SAC-000039/encerrar
    Então a resposta é 403 ACESSO_NEGADO

  @RF-178
  Cenário: confirmar com avaliação
    Dado o SAC-000042 "RESOLVIDO"
    Quando a conta master envia POST /sac/chamados/SAC-000042/avaliacao com:
      """
      {
        "nota": 5,
        "comentario": "Resolvido em 4 h, obrigado"
      }
      """
    Então a resposta é 201 e o chamado passa a "FECHADO"

  @RF-178
  Esquema do Cenário: avaliação recusada
    Quando <quem> envia POST /sac/chamados/SAC-000042/avaliacao com <corpo>
    Então a resposta é <resposta>

    Exemplos:
      | quem           | corpo                         | resposta                                |
      | a conta master | sem a nota                    | 422 DADOS_INVALIDOS com o campo nota    |
      | a conta master | a nota 6                      | 422 DADOS_INVALIDOS com o campo nota    |
      | a conta master | um comentário de 501 letras   | 422 DADOS_INVALIDOS com o campo comentario |
      | a conta master | a nota 4, já avaliado         | 409 JA_AVALIADO                         |
      | o Caio         | a nota 5                      | 403 ACESSO_NEGADO                       |

  @RF-179
  Cenário: pedido na fila do time
    Dado o pedido "ajuda-000087" de POST /contas/esqueci-senha-master, com 202
    Quando o Caio envia GET /sac/chamados?categoria=SENHA_CONTA_MASTER
    Então itens traz:
      """
      {
        "id": "SAC-000087",
        "categoria": "SENHA_CONTA_MASTER",
        "origem": "ENTRAR_SEM_LOGIN",
        "pedido": "ajuda-000087",
        "conta": null,
        "instituicao": "Escola Bento Freitas",
        "contato": "(51) 99999-0000",
        "status": "ABERTO"
      }
      """

  @RF-179
  Cenário: resolver o pedido
    Quando o Caio envia POST /sac/chamados/SAC-000087/status com o status "RESOLVIDO"
    E a mensagem "Código enviado ao contato"
    Então a resposta é 200 e a mensagem é gravada como nota interna
    E o prazo de primeira resposta fica cumprido

  @RF-179
  Cenário: sem conversa com quem pediu
    Quando o Caio envia POST /sac/chamados/SAC-000087/mensagens com "interna": false
    Então a resposta é 409 SEM_CONVERSA
    E com "interna": true a resposta é 201

  @RN-288
  Esquema do Cenário: acesso ao chamado
    Quando <quem> envia GET /sac/chamados/SAC-000045, do Pedro Reis
    Então a resposta é <resposta>

    Exemplos:
      | quem                                 | resposta                    |
      | o Pedro Reis                         | 200                         |
      | o Caio, com "MOD-02" com "U"         | 200                         |
      | a conta master da EBFGA              | 404 CHAMADO_INEXISTENTE     |
      | o Rui, membro do time sem "MOD-02"   | 403 ACESSO_NEGADO           |
      | a staff Maria                        | 403 ACESSO_NEGADO           |

  @RN-288
  Cenário: anexo de outra conta
    Quando a conta master da EBFGA envia GET /sac/anexos/anx-55d0e2, do chamado do Pedro
    Então a resposta é 404 ANEXO_INEXISTENTE

  @RN-290
  Esquema do Cenário: mudança recusada
    Dado o chamado SAC-000042 "<de>"
    Quando <quem> envia POST /sac/chamados/SAC-000042/<acao>
    Então a resposta é <resposta>

    Exemplos:
      | de        | quem           | acao                       | resposta                      |
      | ABERTO    | o Caio         | status com "RESOLVIDO"     | 409 TRANSICAO_INVALIDA        |
      | FECHADO   | a conta master | reabrir                    | 409 CHAMADO_FECHADO           |
      | RESOLVIDO | a conta master | reabrir, depois de 7 dias  | 409 PRAZO_REABERTURA_VENCIDO  |
      | RESOLVIDO | o Caio         | status com "FECHADO"       | 409 TRANSICAO_INVALIDA        |
      | RESOLVIDO | a conta master | mensagens                  | 409 TRANSICAO_INVALIDA        |

  @RN-290
  Cenário: fechamento por prazo
    Dado o SAC-000039 "AGUARDANDO_CLIENTE" desde 2026-11-05T13:00:00Z
    Quando chega 2026-11-12T13:00:00Z sem mensagem da conta
    Então GET /sac/chamados/SAC-000039 traz o status "RESOLVIDO"
    E a linhaDoTempo traz o evento com o autor "sistema"

  @RN-292
  Cenário: enviar um anexo
    Quando a conta master envia POST /sac/anexos com o arquivo "erro.png" de 2 MB
    Então a resposta é 201 com:
      """
      {
        "id": "anx-7f3a21",
        "nome": "erro.png",
        "tipo": "image/png",
        "tamanho": 2097152
      }
      """
    E o anexo não usado num chamado ou mensagem em 24 h é descartado

  @RN-292
  Esquema do Cenário: anexo recusado
    Quando a conta master envia <pedido>
    Então a resposta é <resposta>

    Exemplos:
      | pedido                                         | resposta                   |
      | POST /sac/anexos com "foto.jpg" de 11 MB       | 413 ANEXO_GRANDE           |
      | POST /sac/anexos com "tela.mp4"                | 415 TIPO_NAO_ACEITO        |
      | POST /sac/anexos com "nota.pdf" com ameaça     | 422 ANEXO_RECUSADO         |
      | uma mensagem com 6 anexos                      | 422 LIMITE_ANEXOS          |
      | POST /sac/anexos com a verificação fora do ar  | 503 VERIFICACAO_INDISPONIVEL |

  @RN-292
  Esquema do Cenário: anexo que não pode ser usado
    Quando a conta master envia POST /sac/chamados ou /mensagens com o anexo <anexo>
    Então a resposta é 422 DADOS_INVALIDOS com o campo "anexos"

    Exemplos:
      | anexo                                      |
      | "anx-000000", que não existe               |
      | "anx-55d0e2", enviado pelo Pedro Reis      |
      | "anx-7f3a21", já usado no SAC-000042       |
      | "anx-1c2d3e", descartado depois de 24 h    |

  @RN-293
  Esquema do Cenário: mensagem não muda
    Quando o Caio envia <pedido> /sac/chamados/SAC-000042/mensagens/msg-000318
    Então a resposta é 405

    Exemplos:
      | pedido |
      | PATCH  |
      | PUT    |
      | DELETE |

  @RN-294
  Cenário: décimo primeiro chamado
    Dado a EBFGA com 10 chamados em ABERTO, EM_ATENDIMENTO ou AGUARDANDO_CLIENTE
    Quando a conta master envia POST /sac/chamados
    Então a resposta é 409 LIMITE_CHAMADOS e nenhum chamado é aberto

  @RN-294
  Cenário: reabrir não confere o limite
    Quando a conta master envia POST /sac/chamados/SAC-000042/reabrir com os 10 em andamento
    Então a resposta é 200 com o status "EM_ATENDIMENTO"

  @RN-295
  Cenário: card da instituição
    Dado o token da conta master da EBFGA
    Quando envia GET /sac/card
    Então a resposta é 200 com:
      """
      {
        "modulo": "MOD-02",
        "perfil": "INSTITUICAO",
        "versao": 57,
        "atualizadoEm": "2026-11-12T14:20:00Z",
        "vazio": false,
        "selo": 1,
        "reduzida": {
          "valor": 2,
          "rotulo": "chamados em andamento"
        },
        "normal": {
          "valor": 2,
          "rotulo": "chamados em andamento",
          "apoio": [
            {
              "nome": "Aguardando você",
              "valor": 1
            },
            {
              "nome": "Em atendimento",
              "valor": 1
            },
            {
              "nome": "Resolvidos no mês",
              "valor": 3
            }
          ]
        }
      }
      """
    E traz também a expandida e as series de dia, semana e mes, 12 números cada

  @RN-295
  Esquema do Cenário: quem pede o card
    Quando <quem> envia GET /sac/card
    Então a resposta é <resposta>

    Exemplos:
      | quem                          | resposta                              |
      | o Caio, com "MOD-02" com "U"  | 200 com o perfil "DEBOAZ" e o selo 5  |
      | o Pedro Reis                  | 200 com o perfil "AUTONOMO"           |
      | a staff Maria                 | 403 ACESSO_NEGADO                     |

  @RN-295
  Cenário: ao vivo
    Quando a conta master envia GET /sac/card/ao-vivo?intervalo=30
    Então a resposta é 200, como em RN-279 de 15_modulos

  @RN-296
  Esquema do Cenário: avaliação fora do prazo ou do caminho
    Dado o chamado <chamado>
    Quando a conta dona envia POST /sac/chamados/<id>/avaliacao com a nota 4
    Então a resposta é <resposta>

    Exemplos:
      | chamado                                 | id         | resposta                     |
      | fechado há 6 dias, sem avaliação        | SAC-000040 | 201                          |
      | fechado há 8 dias, sem avaliação        | SAC-000036 | 409 PRAZO_AVALIACAO_VENCIDO  |
      | encerrado pela conta, sem resposta      | SAC-000039 | 409 TRANSICAO_INVALIDA       |

  @RN-297
  Esquema do Cenário: ações auditadas
    Quando <quem> envia <pedido> com sucesso
    Então "auditoria" ganha "INICIADO" e "CONCLUIDO" da ação "<acao>", com o mesmo traceId

    Exemplos:
      | quem           | pedido                                      | acao               |
      | a conta master | POST /sac/chamados                          | ABRIR_CHAMADO      |
      | a conta master | POST /sac/chamados/SAC-000042/mensagens     | ENVIAR_MENSAGEM    |
      | o Caio         | POST /sac/chamados/SAC-000042/assumir       | ASSUMIR_CHAMADO    |
      | o Caio         | POST /sac/chamados/SAC-000042/status        | MUDAR_STATUS       |
      | a conta master | POST /sac/chamados/SAC-000042/reabrir       | REABRIR_CHAMADO    |
      | a conta master | POST /sac/chamados/SAC-000039/encerrar      | ENCERRAR_CHAMADO   |
      | a conta master | POST /sac/chamados/SAC-000042/avaliacao     | AVALIAR_CHAMADO    |
      | o administrador| PUT /sac/configuracao                       | SALVAR_PRAZOS_SAC  |

  @RN-297
  Cenário: mudança feita pelo prazo
    Quando o SAC-000039 passa a "RESOLVIDO" por 7 dias sem resposta
    Então "auditoria" ganha a ação "FECHAR_POR_PRAZO", sem quem, com o alvo "SAC-000039"

  @RF-181
  Cenário: salvar os prazos
    Dado o token do administrador do deboaz, com "MOD-02" com "CU"
    Quando envia PUT /sac/configuracao com:
      """
      {
        "prazos": [
          {
            "categoria": "ACESSO_E_SENHA",
            "horasUteis": 4
          },
          {
            "categoria": "DUVIDA_DE_USO",
            "horasUteis": 12
          }
        ]
      }
      """
    Então a resposta é 200 e GET /sac/configuracao traz os prazos novos

  @RF-181
  Cenário: time lê os prazos
    Quando o Caio, com "MOD-02" só com "U", envia GET /sac/configuracao
    Então a resposta é 200 com os prazos de cada categoria
    E a conta master da EBFGA recebe 403 ACESSO_NEGADO no mesmo pedido

  @RF-181
  Esquema do Cenário: recusas
    Quando <quem> envia PUT /sac/configuracao com <corpo>
    Então a resposta é <resposta>

    Exemplos:
      | quem                          | corpo                    | resposta                 |
      | o administrador do deboaz     | horasUteis 81            | 422 FORA_DOS_LIMITES     |
      | o administrador do deboaz     | horasUteis 0             | 422 FORA_DOS_LIMITES     |
      | o Caio, só com "U" no MOD-02  | horasUteis 12            | 403 ACESSO_NEGADO        |
      | a conta master da EBFGA       | horasUteis 12            | 403 ACESSO_NEGADO        |

  @RNF-230
  Cenário: conversa ao vivo
    Dado o token da conta master da EBFGA
    Quando envia GET /sac/chamados/SAC-000042/ao-vivo com Accept "text/event-stream"
    Então a resposta é 200 e a conexão fica aberta
    E quando o Caio envia uma mensagem, o evento "mensagem" chega em até 5 s
    E o evento tem o id da mensagem, como "msg-000319", e o mesmo corpo de GET do chamado
    E a nota interna do Caio não chega à conta

  @RNF-230
  Cenário: reconexão sem perder mensagem
    Dado a conexão caída depois do evento "msg-000318"
    Quando a conta master reconecta com o cabeçalho Last-Event-ID "msg-000318"
    Então recebe primeiro as mensagens enviadas depois da "msg-000318", na ordem

  @RNF-230
  Cenário: chamado de outra conta
    Quando a conta master envia GET /sac/chamados/SAC-000045/ao-vivo
    Então a resposta é 404 CHAMADO_INEXISTENTE

  @fora-de-escopo
  Cenário: o que o SAC não faz
    Então o SAC não tem chat ao vivo, telefone nem atendimento por robô
    E o staff, o participante e o responsável não abrem chamado
    E a conversa com pais e alunos é do módulo Mensagens
    E o time deboaz não abre chamado em nome da conta
