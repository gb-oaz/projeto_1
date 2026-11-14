# language: pt
# Cenários de API de 1_Requisitos/18_modulo-sac.md (v06), copiados do requisito:
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
    E cada item traz também conta, responsavel, prazoResposta, prazoPerto e atrasado

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

  @RN-289
  Esquema do Cenário: prazo perto na fila
    Dado o SAC-000087 sem 1ª resposta, com prazoResposta "2026-11-12T17:00:00Z"
    Quando o Caio envia GET /sac/chamados?situacao=EM_ANDAMENTO em <quando>
    Então o item do SAC-000087 traz prazoPerto <perto> e atrasado <atrasado>

    Exemplos:
      | quando               | perto | atrasado |
      | 2026-11-12T15:59:00Z | false | false    |
      | 2026-11-12T16:00:00Z | true  | false    |
      | 2026-11-12T16:40:00Z | true  | false    |
      | 2026-11-12T17:01:00Z | false | true     |

  @RN-289
  Cenário: 1 hora útil atravessando a noite
    Dado o SAC-000088 sem 1ª resposta, com prazoResposta "2026-11-13T11:30:00Z"
    Quando o Caio envia GET /sac/chamados?situacao=EM_ANDAMENTO em "2026-11-12T20:30:00Z"
    Então o item do SAC-000088 traz prazoPerto true, com 1 hora útil até o prazo

  @RN-289
  Cenário: chamado já respondido
    Dado o SAC-000042 com a 1ª resposta do Caio e o prazoResposta daqui a 30 min
    Quando o Caio envia GET /sac/chamados?situacao=EM_ANDAMENTO
    Então o item do SAC-000042 traz prazoPerto false e atrasado false

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
      | POST /sac/anexos com "nota.jpg" com ameaça     | 422 ANEXO_RECUSADO         |
      | uma mensagem com 6 anexos                      | 422 LIMITE_ANEXOS          |
      | POST /sac/anexos com a verificação fora do ar  | 503 VERIFICACAO_INDISPONIVEL |

  @RN-292
  Esquema do Cenário: tipo não aceito
    Quando a conta master envia POST /sac/anexos <arquivo>
    Então a resposta é 415 com:
      """
      {
        "codigo": "TIPO_NAO_ACEITO",
        "mensagem": "Envie PNG ou JPG."
      }
      """

    Exemplos:
      | arquivo                       |
      | com "boleto.pdf"              |
      | com um PDF chamado "foto.png" |
      | com "tela.mp4"                |

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

  @RN-292
  Cenário: baixar o anexo
    Dado o "erro.png" do SAC-000042, enviado como "anx-7f3a21"
    Quando a conta master envia GET /sac/anexos/anx-7f3a21
    Então a resposta é 200 com o arquivo, igual ao enviado
    E o cabeçalho Content-Type "image/png"
    E o cabeçalho Content-Disposition 'attachment; filename="erro.png"'

  @RN-292
  Esquema do Cenário: ganchos de teste do SAC fora do teste
    Dado a aplicação iniciada em <ambiente>, com Teste__Relogio <flag>
    Quando envia <metodo> <endereco>
    Então a resposta é 404

    Exemplos:
      | ambiente    | flag    | metodo | endereco                |
      | produção    | "sim"   | POST   | /_teste/sac/agendamento |
      | produção    | "sim"   | PUT    | /_teste/sac/verificacao |
      | homologação | ausente | POST   | /_teste/sac/agendamento |
      | homologação | ausente | DELETE | /_teste/sac/verificacao |

  @RN-292
  Cenário: gancho da verificação sem o campo disponivel
    Dado a aplicação de homologação, com Teste__Relogio "sim"
    Quando envia PUT /_teste/sac/verificacao sem "disponivel"
    Então a resposta é 422 DADOS_INVALIDOS com o campo "disponivel"

  @RN-293
  Esquema do Cenário: mensagem não muda
    Quando o Caio envia <pedido> /sac/chamados/SAC-000042/mensagens/msg-000318
    Então a resposta é 405

    Exemplos:
      | pedido |
      | PATCH  |
      | PUT    |
      | DELETE |

  @RN-293
  Cenário: sem login
    Quando alguém sem token envia PATCH /sac/chamados/SAC-000042/mensagens/msg-000318
    Então a resposta é 405, sem corpo, com o cabeçalho Allow vazio
    E o token não é conferido

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
    E traz Content-Type "text/event-stream" e Cache-Control "no-store"
    E o 1º evento "card" chega logo ao abrir, com o corpo de GET /sac/card
    E sem versão nova, a cada 30 s chega o comentário ": ping"

  @RN-295
  Esquema do Cenário: card sem sessão válida
    Quando alguém envia GET <endereco> <token>
    Então a resposta é 401 SESSAO_TERMINADA, seja qual for o problema do token
    E nenhum fluxo fica aberto

    Exemplos:
      | endereco                       | token                                  |
      | /sac/card                      | sem o cabeçalho Authorization          |
      | /sac/card                      | com o token vencido                    |
      | /sac/card/ao-vivo?intervalo=30 | com o token adulterado                 |
      | /sac/card/ao-vivo?intervalo=30 | da sessão encerrada em outro aparelho  |

  @RN-295
  Esquema do Cenário: intervalo inválido no card ao vivo
    Quando a conta master envia GET /sac/card/ao-vivo?intervalo=<intervalo>
    Então a resposta é 422 com:
      """
      {
        "codigo": "DADOS_INVALIDOS",
        "mensagem": "O campo \"intervalo\" está faltando ou é inválido.",
        "campo": "intervalo"
      }
      """
    E nenhum fluxo fica aberto

    Exemplos:
      | intervalo |
      | 10        |
      | 0         |

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
    E a resposta traz o cabeçalho X-Trace-Id com esse traceId

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

  @RN-297
  Esquema do Cenário: X-Trace-Id da ação auditada
    Quando <quem> envia POST /sac/chamados <caso>
    Então a resposta é <resposta> com o cabeçalho X-Trace-Id, como "trc-7f3a9c21"
    E esse é o traceId de "INICIADO" em "auditoria" e de <registro>

    Exemplos:
      | quem           | caso            | resposta            | registro                    |
      | a conta master | válido          | 201                 | "CONCLUIDO"                 |
      | a conta master | sem a categoria | 422 DADOS_INVALIDOS | "auditoriaErros" com ALERTA |
      | a staff Maria  | válido          | 403 ACESSO_NEGADO   | "auditoriaErros" com ALERTA |
      | a conta master | com falha geral | 500 ERRO_INESPERADO | "auditoriaErros" com ERRO   |

  @RN-297
  Esquema do Cenário: pedidos sem auditoria
    Quando a conta master envia <pedido> com sucesso
    Então a resposta não traz X-Trace-Id e nada é gravado em "auditoria"

    Exemplos:
      | pedido                                    |
      | GET /sac/chamados/SAC-000042              |
      | GET /sac/chamados/SAC-000042/ao-vivo      |
      | GET /sac/card                             |
      | POST /sac/anexos com "erro.png"           |

  @RN-297
  Cenário: 401 antes de tudo
    Dado o token da conta master com as permissões já mudadas
    Quando envia POST /sac/chamados com o corpo "abc", que não é JSON
    Então a resposta é 401 PERMISSOES_DESATUALIZADAS, não 422
    E sem X-Trace-Id, nada é gravado em "auditoria" nem em "auditoriaErros"

  @RN-297
  Cenário: corpo que não é JSON
    Quando a conta master envia POST /sac/chamados com o corpo "abc"
    Então a resposta é 422 com:
      """
      {
        "codigo": "DADOS_INVALIDOS",
        "mensagem": "O campo \"corpo\" está faltando ou é inválido.",
        "campo": "corpo"
      }
      """
    E sem X-Trace-Id, nada é gravado em "auditoria"

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
  Cenário: prazos nunca salvos
    Dado nenhum prazo salvo na aba Configuração
    Quando o Caio envia GET /sac/configuracao
    Então a resposta é 200 com as 6 categorias, nesta ordem, nos prazos padrão:
      """
      {
        "prazos": [
          {
            "categoria": "ACESSO_E_SENHA",
            "horasUteis": 4
          },
          {
            "categoria": "COBRANCA",
            "horasUteis": 8
          },
          {
            "categoria": "MODULO_COM_PROBLEMA",
            "horasUteis": 8
          },
          {
            "categoria": "DUVIDA_DE_USO",
            "horasUteis": 16
          },
          {
            "categoria": "SUGESTAO_AO_DEBOAZ",
            "horasUteis": 40
          },
          {
            "categoria": "SENHA_CONTA_MASTER",
            "horasUteis": 4
          }
        ],
        "salvaEm": null,
        "salvaPor": null
      }
      """

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
    E traz Content-Type "text/event-stream" e Cache-Control "no-store"
    E quando o Caio envia uma mensagem, o evento "mensagem" chega em até 5 s
    E o evento tem o id da mensagem, como "msg-000319", e o mesmo corpo de GET do chamado
    E a nota interna do Caio não chega à conta

  @RNF-230
  Cenário: reconexão sem perder mensagem
    Dado a conexão caída depois do evento "msg-000318"
    Quando a conta master reconecta com o cabeçalho Last-Event-ID "msg-000318"
    Então recebe primeiro as mensagens enviadas depois da "msg-000318", na ordem

  @RNF-230
  Cenário: conversa sem mensagem nova
    Dado a conexão ao vivo do SAC-000042 aberta, sem mensagem nova
    Quando passam 30 s
    Então chega o comentário ": ping" e a conexão continua aberta

  @RNF-230
  Cenário: chamado de outra conta
    Quando a conta master envia GET /sac/chamados/SAC-000045/ao-vivo
    Então a resposta é 404 CHAMADO_INEXISTENTE

  @RNF-232
  Cenário: pronta depois da partida
    Dado a partida de homologação concluída, com as coleções do SAC e seus índices prontos
    Quando envia GET /health, sem login
    Então a resposta é 200, sem corpo

  @RNF-232
  Cenário: índice único que não pode ser criado
    Dado dois documentos de "cardSac" com o escopo "EBFGA" e o dia 2026-11-12
    Quando a aplicação de homologação inicia
    Então GET /health, sem login, não tem resposta, a conexão é recusada
    E o processo termina com código 1, como na RNF-227

  @RNF-232
  Cenário: índice não único que não pode ser criado
    Dado o banco recusa o índice chamadoId_numero em "mensagensChamado" na partida
    Quando envia GET /health, sem login
    Então a resposta é 200, sem corpo
    E o log traz um aviso com o nome do índice e da coleção, nunca o valor

  @RNF-233
  Esquema do Cenário: o mesmo arquivo nos dois armazenamentos
    Dado o armazenamento de objetos <estado> no ambiente
    E o "erro.png" de 2 MB enviado por POST /sac/anexos como "anx-7f3a21"
    Quando a conta master envia GET /sac/anexos/anx-7f3a21
    Então a resposta é 200 com os mesmos bytes enviados, sem conversão
    E o cabeçalho Content-Type "image/png"

    Exemplos:
      | estado    |
      | ligado    |
      | desligado |

  @RNF-233
  Cenário: anexo lido de onde foi guardado, sem migração
    Dado o "erro.png" enviado como "anx-7f3a21" com o armazenamento de objetos desligado
    E a aplicação reinicia com o armazenamento de objetos ligado
    Quando a conta master envia GET /sac/anexos/anx-7f3a21
    Então a resposta é 200 com os mesmos bytes enviados, sem conversão
    E o arquivo continua só no GridFS, sem migração

  @RNF-233
  Cenário: verificação fora do ar
    Dado o armazenamento de objetos ligado e a verificação de segurança fora do ar
    Quando a conta master envia POST /sac/anexos com "erro.png"
    Então a resposta é 503 VERIFICACAO_INDISPONIVEL e nada é guardado

  @RNF-234
  Esquema do Cenário: quando o arquivo do anexo é removido
    Dado o "erro.png" enviado como "anx-7f3a21" em 2026-11-12 10:00, em <ambiente>
    E <uso>
    Quando a conta master envia GET /sac/anexos/anx-7f3a21 em <quando>
    Então a resposta é 404 ANEXO_INEXISTENTE e o arquivo já foi removido

    Exemplos:
      | ambiente    | uso                                    | quando           |
      | produção    | sem chamado nem mensagem em 24 h       | 2026-11-13 10:01 |
      | homologação | sem chamado nem mensagem em 24 h       | 2026-11-13 10:01 |
      | homologação | no SAC-000042                          | 2026-12-12 10:01 |
      | produção    | no SAC-000042, fechado em 2026-11-20   | 2031-11-20 10:01 |

  @RNF-234
  Cenário: anexo vencido responde 404 antes de o arquivo sair
    Dado o "erro.png" enviado como "anx-7f3a21" em 2026-11-12 10:00, em homologação
    E sem chamado nem mensagem que o use
    E o relógio em 2026-11-13 10:01, antes de o agendamento rodar
    Quando a conta master envia GET /sac/anexos/anx-7f3a21
    Então a resposta é 404 ANEXO_INEXISTENTE

  @RNF-234
  Esquema do Cenário: arquivo ainda guardado
    Dado o "erro.png" do SAC-000042 enviado em 2026-11-12 10:00, em <ambiente>
    Quando a conta master envia GET /sac/anexos/anx-7f3a21 em <quando>
    Então a resposta é 200 com o arquivo

    Exemplos:
      | ambiente    | quando           |
      | homologação | 2026-12-12 09:59 |
      | produção    | 2031-11-19 10:00 |

  @RNF-234
  Cenário: conversa sem o arquivo em homologação
    Dado o arquivo do "anx-7f3a21" removido 30 dias depois do envio, em homologação
    Quando a conta master envia GET /sac/chamados/SAC-000042
    Então a resposta é 200 com o chamado e as mensagens, sem nada removido

  @fora-de-escopo
  Cenário: o que o SAC não faz
    Então o SAC não tem chat ao vivo, telefone nem atendimento por robô
    E o staff, o participante e o responsável não abrem chamado
    E a conversa com pais e alunos é do módulo Mensagens
    E o time deboaz não abre chamado em nome da conta
