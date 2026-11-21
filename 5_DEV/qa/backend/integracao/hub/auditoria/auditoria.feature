# language: pt
# Cenários de API de 1_Requisitos/20_modulo-auditoria.md (v10), copiados do requisito:
# mudou lá, muda aqui. Cada cenário leva a tag do ID que cobre.
Funcionalidade: Auditoria
  1_Requisitos/20_modulo-auditoria.md · Cenários de API

  @formato-comum
  Cenário: resposta de erro
    Quando um pedido à Auditoria é recusado
    Então o corpo traz o código, a mensagem e, quando houver, o campo:
      """
      {
        "codigo": "PERIODO_LONGO",
        "mensagem": "Escolha um período de até 31 dias.",
        "campo": "ate"
      }
      """

  @formato-comum
  Cenário: datas, tempos e quem usa
    Então toda data e hora segue a ISO 8601 em UTC, como "2026-11-20T13:42:30Z"
    E a hora de um registro vai com milissegundos, como "2026-11-20T13:20:14.102Z"
    E dias, semanas e meses das séries começam às 00:00 do horário de Brasília
    E todo tempo vai em milissegundos inteiros, em campos terminados em "Ms"
    E estado, resultado, tipo, nível e ação vão em maiúsculas, como "FORA_DO_AR"
    E só o papel "ADMINISTRADOR" e o "MEMBRO_TIME" com "MOD-17" com "U" usam a Auditoria
    E todo endereço de /auditoria é GET
    E a consulta atendida traz o cabeçalho X-Trace-Id e fica na auditoria, como na RN-325

  @formato-comum
  Esquema do Cenário: mensagem de cada código de erro
    Quando a resposta da Auditoria é <resposta>
    Então a "mensagem" do corpo é "<mensagem>"

    Exemplos:
      | resposta                               | mensagem                                        |
      | 403 ACESSO_NEGADO                      | Você não tem permissão para esta ação.          |
      | 404 SERVICO_INEXISTENTE                | Serviço não encontrado.                         |
      | 404 EXECUCAO_INEXISTENTE               | Execução não encontrada.                        |
      | 422 DADOS_INVALIDOS, campo "janela"    | O campo "janela" está faltando ou é inválido.   |
      | 422 PERIODO_LONGO, em /registros       | Escolha um período de até 31 dias.              |
      | 422 PERIODO_LONGO, em /execucoes       | Escolha um período de até 7 dias.               |
      | 422 PERIODO_LONGO, em /ocorrencias     | Escolha um período de até 31 dias.              |
      | 422 PERIODO_INVERTIDO                  | O fim deve ser depois do começo.                |
      | 503 BANCO_DE_DADOS_FORA | Os registros dependem do banco de dados, fora do ar. |

  @RF-194
  Cenário: um serviço fora do ar
    Dado o token do Caio, com "MOD-17" com "U", em produção
    E o intermediador de pagamento fora do ar desde 2026-11-20T13:30:00Z
    Quando envia GET /auditoria/saude em 2026-11-20T13:42:40Z
    Então a resposta é 200 e traz, entre os 8 serviços e os módulos da RF-196:
      """
      {
        "ambiente": "PRODUCAO",
        "verificadoEm": "2026-11-20T13:42:30Z",
        "geral": "FORA_DO_AR",
        "foraDoAr": 1,
        "lentos": 0,
        "servicos": [
          {
            "id": "intermediador-pagamento",
            "nome": "Intermediador de pagamento",
            "estado": "FORA_DO_AR",
            "desde": "2026-11-20T13:30:00Z",
            "tempoMs": null,
            "falha": "SEM_RESPOSTA",
            "ultimaExecucao": null
          },
          {
            "id": "banco",
            "nome": "Banco de dados",
            "estado": "NO_AR",
            "desde": "2026-11-18T09:00:12Z",
            "tempoMs": 12,
            "falha": null,
            "ultimaExecucao": null
          },
          {
            "id": "agendamento-sac",
            "nome": "Agendamento do SAC",
            "estado": "NO_AR",
            "desde": "2026-11-18T09:01:00Z",
            "tempoMs": null,
            "falha": null,
            "ultimaExecucao": "2026-11-20T13:42:00Z"
          }
        ],
        "instancias": [
          {
            "id": "ms-mono--r42-7c9f8",
            "versao": "1.4.2",
            "inicio": "2026-11-18T09:00:05Z",
            "ultimoSinal": "2026-11-20T13:42:30Z"
          },
          {
            "id": "ms-mono--r42-b81d0",
            "versao": "1.4.2",
            "inicio": "2026-11-18T09:00:07Z",
            "ultimoSinal": "2026-11-20T13:42:31Z"
          }
        ]
      }
      """
    E "servicos" vem na ordem FORA_DO_AR, LENTO, SEM_VERIFICACAO, NO_AR e DESLIGADO
    E no mesmo estado, na ordem da tabela "serviços e identificadores" abaixo

  @RF-194
  Esquema do Cenário: serviços e identificadores
    Quando o Caio envia GET /auditoria/saude
    Então "servicos" traz o "<id>" com o nome "<nome>"

    Exemplos:
      | id                       | nome                        |
      | aplicacao                | Aplicação                   |
      | banco                    | Banco de dados              |
      | cache                    | Cache                       |
      | armazenamento-anexos     | Armazenamento de anexos     |
      | verificacao-anexos       | Verificação de anexos       |
      | intermediador-pagamento  | Intermediador de pagamento  |
      | agendamento-sac          | Agendamento do SAC          |
      | agendamento-pagamento    | Agendamento do Pagamento    |

  @RF-194
  Esquema do Cenário: geral e serviço desligado
    Dado <situacao>
    Quando o Caio envia GET /auditoria/saude
    Então "geral" é "<geral>", "foraDoAr" é <fora> e "lentos" é <lentos>

    Exemplos:
      | situacao                                              | geral       | fora | lentos |
      | todos os serviços NO_AR                               | NO_AR       | 0    | 0      |
      | o armazenamento DESLIGADO e os outros NO_AR           | NO_AR       | 0    | 0      |
      | o cache LENTO e os outros NO_AR                       | LENTO       | 0    | 1      |
      | o cache LENTO e o intermediador FORA_DO_AR            | FORA_DO_AR  | 1    | 1      |

  @RF-195
  Cenário: quedas das últimas 24 h
    Dado o intermediador fora de 13:30 a 13:52 e de 06:10 a 06:12 de 2026-11-20, em UTC
    Quando o Caio envia GET /auditoria/saude/intermediador-pagamento/historico?periodo=24h
    E o relógio marca 2026-11-20T14:00:00Z
    Então a resposta é 200 com:
      """
      {
        "servico": "intermediador-pagamento",
        "nome": "Intermediador de pagamento",
        "periodo": "24h",
        "de": "2026-11-19T14:00:00Z",
        "ate": "2026-11-20T14:00:00Z",
        "disponibilidade": 98.33,
        "quedas": [
          {
            "inicio": "2026-11-20T13:30:00Z",
            "fim": "2026-11-20T13:52:00Z",
            "duracaoMin": 22,
            "falha": "SEM_RESPOSTA"
          },
          {
            "inicio": "2026-11-20T06:10:00Z",
            "fim": "2026-11-20T06:12:00Z",
            "duracaoMin": 2,
            "falha": "CONEXAO_RECUSADA"
          }
        ],
        "serie": [
          {
            "inicio": "2026-11-19T14:00:00Z",
            "tempoMedioMs": 238,
            "falhas": 0
          }
        ]
      }
      """
    E "serie" traz 24 pontos, um por hora, do mais antigo ao atual

  @RF-195
  Esquema do Cenário: período e serviço pedidos
    Quando o Caio envia GET /auditoria/saude/<servico>/historico<consulta>
    Então a resposta é <resposta>

    Exemplos:
      | servico                  | consulta        | resposta                                     |
      | banco                    | ?periodo=7d     | 200, com 7 pontos, um por dia                |
      | banco                    | ?periodo=30d    | 200, com 30 pontos, um por dia               |
      | banco                    | sem o parâmetro | 200, como ?periodo=24h                       |
      | banco                    | ?periodo=2h     | 422 DADOS_INVALIDOS com o campo "periodo"    |
      | fila                     | ?periodo=24h    | 404 SERVICO_INEXISTENTE                      |

  @RF-195
  Cenário: queda ainda aberta
    Dado o intermediador fora do ar desde 2026-11-20T13:30:00Z
    Quando o Caio envia GET /auditoria/saude/intermediador-pagamento/historico às 13:42
    Então a primeira queda vem com "fim": null e "duracaoMin": 12

  @RF-196
  Cenário: módulos em GET /auditoria/saude
    Dado o Pagamento com 250 pedidos, 10 com erro 5xx, nos 5 min antes de "verificadoEm"
    E essa é a 1ª verificação seguida com falha do Pagamento
    E o Catálogo sem pedidos nesses 5 min
    Quando o Caio envia GET /auditoria/saude
    Então a resposta é 200 e "modulos" traz, entre os outros:
      """
      [
        {
          "id": "MOD-01",
          "nome": "Pagamento",
          "estado": "LENTO",
          "pedidos": 250,
          "erros5xx": 10,
          "taxaErro5xx": 4.0,
          "p95Ms": 500
        },
        {
          "id": "MOD-14",
          "nome": "Catálogo",
          "estado": "SEM_PEDIDOS",
          "pedidos": 0,
          "erros5xx": 0,
          "taxaErro5xx": null,
          "p95Ms": null
        }
      ]
      """
    E "modulos" vem na ordem FORA_DO_AR, LENTO, NO_AR e SEM_PEDIDOS

  @RF-197
  Cenário: última hora de todos os módulos
    Dado 1.240 pedidos medidos entre 2026-11-20T12:42:00Z e 13:42:00Z
    Quando o Caio envia GET /auditoria/metricas?janela=1h às 13:42:20
    Então a resposta é 200 com:
      """
      {
        "janela": "1h",
        "modulo": null,
        "de": "2026-11-20T12:42:00Z",
        "ate": "2026-11-20T13:42:00Z",
        "resumo": {
          "pedidos": 1240,
          "porMinuto": 20.7,
          "p50Ms": 100,
          "p95Ms": 500,
          "p99Ms": 1000,
          "erros4xx": 31,
          "erros5xx": 2,
          "taxaErro5xx": 0.16
        },
        "serie": [
          {
            "inicio": "2026-11-20T12:42:00Z",
            "pedidos": 18,
            "p95Ms": 250,
            "erros5xx": 0
          }
        ]
      }
      """
    E "serie" traz 60 pontos, do mais antigo ao atual
    E o minuto sem pedido vem com "pedidos": 0 e "p95Ms": null

  @RF-197
  Esquema do Cenário: janela e módulo pedidos
    Quando o Caio envia GET /auditoria/metricas<consulta>
    Então a resposta é <resposta>

    Exemplos:
      | consulta                 | resposta                                          |
      | ?janela=24h              | 200, com 24 pontos, um por hora                   |
      | ?janela=7d               | 200, com 7 pontos, um por dia                     |
      | ?janela=30d              | 200, com 30 pontos, um por dia                    |
      | sem o parâmetro          | 200, como ?janela=1h                              |
      | ?janela=1h&modulo=MOD-02 | 200, com "modulo": "MOD-02" e só os pedidos do SAC |
      | ?janela=2h               | 422 DADOS_INVALIDOS com o campo "janela"          |
      | ?modulo=MOD-99           | 422 DADOS_INVALIDOS com o campo "modulo"          |

  @RF-198
  Cenário: endereços mais lentos
    Quando o Caio envia GET /auditoria/metricas/enderecos?janela=1h&ordem=p95
    Então a resposta é 200 com até 20 itens, o de maior p95 primeiro:
      """
      {
        "janela": "1h",
        "modulo": null,
        "ordem": "p95",
        "itens": [
          {
            "metodo": "POST",
            "endereco": "/pagamento/faturas/{id}/pagamentos",
            "modulo": "MOD-01",
            "pedidos": 42,
            "p50Ms": 500,
            "p95Ms": 3000,
            "p99Ms": 3000,
            "erros4xx": 3,
            "erros5xx": 2,
            "lento": true
          }
        ]
      }
      """
    E "lento" é true quando "p95Ms" passa de 1000 ou é null por estar acima de 10 s

  @RF-198
  Esquema do Cenário: ordem e limite
    Quando o Caio envia GET /auditoria/metricas/enderecos?janela=1h<consulta>
    Então a resposta é <resposta>

    Exemplos:
      | consulta       | resposta                                                   |
      | &ordem=pedidos | 200, com o de mais pedidos primeiro                        |
      | &ordem=erros   | 200, com o de mais erros 5xx primeiro, depois o de mais 4xx |
      | sem a ordem    | 200, como &ordem=p95                                       |
      | &limite=100    | 200, com até 100 itens                                     |
      | &limite=101    | 422 DADOS_INVALIDOS com o campo "limite"                   |
      | &ordem=nome    | 422 DADOS_INVALIDOS com o campo "ordem"                    |

  @RF-199
  Cenário: últimas 24 h, o mais novo primeiro
    Dado o participante Lucas criado pela Maria em 2026-11-20T13:41:07Z
    Quando o Caio envia GET /auditoria/registros em 2026-11-20T13:42:00Z
    Então a resposta é 200, com "auditoria" e "auditoriaErros" juntos, 50 por página:
      """
      {
        "de": "2026-11-19T13:42:00Z",
        "ate": "2026-11-20T13:42:00Z",
        "itens": [
          {
            "id": "9c1f4e2a7b3d4c5e8f9a0b1c2d3e4f5a",
            "tipo": "AUDITORIA",
            "traceId": "trc-5e2a9d14",
            "etapa": "CONCLUIDO",
            "nivel": null,
            "codigo": null,
            "acao": "CRIAR_USUARIO",
            "acaoNome": "Criar usuário",
            "modulo": "MOD-03",
            "quem": {
              "id": "usr-ebfga-maria",
              "nome": "Maria Souza"
            },
            "conta": {
              "id": "conta-ebfga",
              "sigla": "EBFGA"
            },
            "alvo": "usr-ebfga-lucas",
            "quando": "2026-11-20T13:41:07.311Z"
          }
        ],
        "proximo": "MjAyNi0xMS0yMFQxMzo0MTowNy4zMTFa"
      }
      """

  @RF-199
  Cenário: registro de recusa
    Dado a criação do Lucas recusada com 409 MATRICULA_EM_USO às 13:39:52
    Quando o Caio envia GET /auditoria/registros
    Então o item do erro vem com "tipo": "ERRO", "etapa": null
    E "nivel": "ALERTA" e "codigo": "MATRICULA_EM_USO"

  @RF-199
  Esquema do Cenário: páginas
    Dado que "[12h a 13h]" abrevia "de=2026-11-20T12:00:00Z&ate=2026-11-20T13:00:00Z"
    E 130 registros gravados por POST /_teste/auditoria/registros nessa hora, e nenhum outro
    E as consultas gravadas às 13:42 ou depois, fora do período, então não contam
    Quando o Caio envia GET /auditoria/registros?[12h a 13h]<consulta>
    Então a resposta é <resposta>

    Exemplos:
      | consulta                 | resposta                                          |
      | sem mais parâmetros      | 200 com 50 itens e "proximo"                      |
      | &depois= o proximo da 2ª | 200 com os 30 últimos e "proximo": null           |
      | &limite=200              | 200 com os 130 e "proximo": null                  |
      | &limite=201              | 422 DADOS_INVALIDOS com o campo "limite"          |
      | &depois=abc              | 422 DADOS_INVALIDOS com o campo "depois"          |

  @RF-200
  Esquema do Cenário: filtros
    Quando o Caio envia GET /auditoria/registros<filtro>
    Então a resposta é 200 e traz só <resultado>

    Exemplos:
      | filtro                       | resultado                                            |
      | ?tipo=AUDITORIA              | os registros de "auditoria"                          |
      | ?nivel=ERRO                  | os de "auditoriaErros" com o nivel ERRO              |
      | ?nivel=ALERTA                | os de "auditoriaErros" com o nivel ALERTA            |
      | ?modulo=MOD-02               | os das ações do SAC                                  |
      | ?acao=CRIAR_USUARIO          | os da ação CRIAR_USUARIO                             |
      | ?conta=ebfga                 | os da conta de sigla EBFGA, sem diferença de caixa   |
      | ?quem=usr-ebfga-maria        | os da Maria                                          |
      | ?codigo=MATRICULA_EM_USO     | os erros com esse código                             |
      | ?conta=ZZZZZ                 | nenhum, com "itens": [] e "proximo": null            |

  @RF-200
  Cenário: busca pelo traceId
    Quando o Caio envia GET /auditoria/registros?traceId=trc-4b7e01c9
    Então a resposta é 200 com todos os registros desse traceId, sem o limite do período
    E "de" e "ate" vêm null

  @RF-200
  Esquema do Cenário: filtro inválido
    Quando o Caio envia GET /auditoria/registros<filtro>
    Então a resposta é 422 DADOS_INVALIDOS com o campo "<campo>"

    Exemplos:
      | filtro           | campo   |
      | ?nivel=GRAVE     | nivel   |
      | ?tipo=LOG        | tipo    |
      | ?modulo=MOD-99   | modulo  |
      | ?acao=VOAR       | acao    |
      | ?traceId=abc     | traceId |
      | ?de=ontem        | de      |

  @RF-201
  Cenário: registros chegando
    Dado o Caio com GET /auditoria/registros/ao-vivo?nivel=ERRO aberto
    Quando a execução "trc-4b7e01c9" grava um erro em "auditoriaErros"
    Então a resposta, 200 com Content-Type "text/event-stream" e Cache-Control "no-store"
    E recebe o evento "registro", com id igual ao do registro e o corpo de um item da RF-199
    E um ": ping" chega a cada 30 s

  @RF-201
  Esquema do Cenário: filtros do ao vivo
    Quando o Caio envia GET /auditoria/registros/ao-vivo<filtro>
    Então a resposta é <resposta>

    Exemplos:
      | filtro                            | resposta                                         |
      | ?modulo=MOD-01&tipo=AUDITORIA     | 200, só com os registros de auditoria do Pagamento |
      | ?conta=ebfga                      | 200, só com os registros da EBFGA                |
      | ?de=2026-11-20T13:00:00Z          | 422 DADOS_INVALIDOS com o campo "de"             |
      | ?traceId=trc-4b7e01c9             | 422 DADOS_INVALIDOS com o campo "traceId"        |

  @RF-201
  Cenário: quarta conexão do mesmo usuário
    Dado o Caio com 3 conexões em /auditoria/registros/ao-vivo
    Quando abre a 4ª
    Então a 4ª recebe 200 e a mais antiga recebe "encerrado" com "LIMITE_CONEXOES" e fecha

  @RF-202
  Cenário: execução concluída
    Dado os 4 passos de "trc-1d6e3f08" gravados por POST /_teste/auditoria/registros
    E cada um com a "acao", a "etapa" e o "quando" do passo esperado abaixo
    E todos com "quem": "usr-ebfga-maria", "conta": "conta-ebfga" e "alvo": "usr-ebfga-paulo"
    Quando o Caio envia GET /auditoria/execucoes/trc-1d6e3f08
    Então a resposta é 200 com:
      """
      {
        "traceId": "trc-1d6e3f08",
        "acao": "DESATIVAR_USUARIO",
        "acaoNome": "Desativar usuário",
        "modulo": "MOD-03",
        "resultado": "CONCLUIDA",
        "inicio": "2026-11-20T13:20:14.102Z",
        "fim": "2026-11-20T13:20:14.286Z",
        "duracaoMs": 184,
        "codigo": null,
        "quem": {
          "id": "usr-ebfga-maria",
          "nome": "Maria Souza"
        },
        "conta": {
          "id": "conta-ebfga",
          "sigla": "EBFGA"
        },
        "alvo": "usr-ebfga-paulo",
        "passos": [
          {
            "id": "0a4c8e2f6b1d4a3c9e7f5b2d8c6a4e1f",
            "tipo": "AUDITORIA",
            "acao": "DESATIVAR_USUARIO",
            "acaoNome": "Desativar usuário",
            "interna": false,
            "etapa": "INICIADO",
            "nivel": null,
            "codigo": null,
            "quando": "2026-11-20T13:20:14.102Z"
          },
          {
            "id": "1b5d9f3a7c2e4b6d8f0a2c4e6b8d0f2a",
            "tipo": "AUDITORIA",
            "acao": "ENCERRAR_SESSOES_DO_USUARIO",
            "acaoNome": "Encerrar sessões do usuário",
            "interna": true,
            "etapa": "INICIADO",
            "nivel": null,
            "codigo": null,
            "quando": "2026-11-20T13:20:14.190Z"
          },
          {
            "id": "2c6e0a4b8d3f5c7e9a1b3d5f7c9e1a3b",
            "tipo": "AUDITORIA",
            "acao": "ENCERRAR_SESSOES_DO_USUARIO",
            "acaoNome": "Encerrar sessões do usuário",
            "interna": true,
            "etapa": "CONCLUIDO",
            "nivel": null,
            "codigo": null,
            "quando": "2026-11-20T13:20:14.251Z"
          },
          {
            "id": "3d7f1b5c9e4a6d8f0b2c4e6a8d0f2b4c",
            "tipo": "AUDITORIA",
            "acao": "DESATIVAR_USUARIO",
            "acaoNome": "Desativar usuário",
            "interna": false,
            "etapa": "CONCLUIDO",
            "nivel": null,
            "codigo": null,
            "quando": "2026-11-20T13:20:14.286Z"
          }
        ]
      }
      """

  @RF-202
  Cenário: execução que falhou
    Dado "trc-4b7e01c9" gravado pelo gancho com a ação "PAGAMENTO_CRIADO": INICIADO às 13:41:30.000
    E ERRO às 13:41:32.107, com o nível ERRO e o código "BANCO_INDISPONIVEL"
    Quando o Caio envia GET /auditoria/execucoes/trc-4b7e01c9
    Então a resposta é 200 com "resultado": "FALHOU", "codigo": "BANCO_INDISPONIVEL"
    E "duracaoMs": 2107, do "INICIADO" ao registro de erro
    E o último passo com "tipo": "ERRO" e "nivel": "ERRO"

  @RF-202
  Esquema do Cenário: execução pedida
    Quando o Caio envia GET /auditoria/execucoes/<traceId>
    Então a resposta é <resposta>

    Exemplos:
      | traceId        | resposta                                   |
      | trc-00000000   | 404 EXECUCAO_INEXISTENTE                   |
      | abc            | 422 DADOS_INVALIDOS com o campo "traceId"  |

  @RF-203
  Cenário: execuções com problema nas últimas 24 h
    Quando o Caio envia GET /auditoria/execucoes?resultado=FALHOU,INTERROMPIDA às 13:42
    Então a resposta é 200 com a contagem do período e as execuções pedidas:
      """
      {
        "de": "2026-11-19T13:42:00Z",
        "ate": "2026-11-20T13:42:00Z",
        "contagem": {
          "concluidas": 398,
          "recusadas": 9,
          "falharam": 4,
          "interrompidas": 1,
          "emAndamento": 0
        },
        "itens": [
          {
            "traceId": "trc-90ab12cd",
            "acao": "MUDAR_ESTADO_CONTA",
            "acaoNome": "Mudar estado da conta",
            "modulo": "MOD-03",
            "resultado": "INTERROMPIDA",
            "inicio": "2026-11-20T12:58:03.410Z",
            "duracaoMs": null,
            "codigo": null,
            "quem": {
              "id": "usr-dboaz-caio",
              "nome": "Caio Lima"
            },
            "conta": {
              "id": "conta-ebfga",
              "sigla": "EBFGA"
            },
            "alvo": "conta-ebfga"
          }
        ],
        "proximo": null
      }
      """
    E "contagem" conta todos os resultados do período, com os outros filtros, sem o "resultado"

  @RF-203
  Esquema do Cenário: filtros das execuções
    Quando o Caio envia GET /auditoria/execucoes<consulta>
    Então a resposta é <resposta>

    Exemplos:
      | consulta                   | resposta                                         |
      | ?resultado=RECUSADA        | 200, só com as recusadas                         |
      | sem parâmetros             | 200, com todas, a mais nova primeiro, 50 por página |
      | ?modulo=MOD-02&conta=ebfga | 200, só com as do SAC da EBFGA                   |
      | ?resultado=PERDIDA         | 422 DADOS_INVALIDOS com o campo "resultado"      |

  @RF-204
  Cenário: ocorrências abertas e voltas recentes
    Dado o intermediador FORA_DO_AR desde 2026-11-20T13:30:00Z e o MOD-01 LENTO desde 13:31:00
    E o cache de volta a NO_AR às 13:41:00, depois de sair dele às 13:31:00
    Quando o Caio envia GET /auditoria/ocorrencias/abertas às 13:45:00
    Então a resposta é 200, sem o cabeçalho X-Trace-Id, com:
      """
      {
        "abertas": [
          {
            "id": "5e8d1c2b9a7f4e3d8c6b5a4f3e2d1c0b",
            "tipo": "SERVICO",
            "alvo": "intermediador-pagamento",
            "nome": "Intermediador de pagamento",
            "de": "NO_AR",
            "para": "FORA_DO_AR",
            "inicio": "2026-11-20T13:30:00Z",
            "quando": "2026-11-20T13:30:00Z",
            "falha": "SEM_RESPOSTA",
            "duracaoMin": null
          },
          {
            "id": "0a1b2c3d4e5f40718293a4b5c6d7e8f9",
            "tipo": "MODULO",
            "alvo": "MOD-01",
            "nome": "Pagamento",
            "de": "NO_AR",
            "para": "LENTO",
            "inicio": "2026-11-20T13:31:00Z",
            "quando": "2026-11-20T13:31:00Z",
            "falha": null,
            "duracaoMin": null
          }
        ],
        "voltaram": [
          {
            "id": "9f8e7d6c5b4a43210fedcba987654321",
            "tipo": "SERVICO",
            "alvo": "cache",
            "nome": "Cache",
            "de": "FORA_DO_AR",
            "para": "NO_AR",
            "inicio": "2026-11-20T13:31:00Z",
            "quando": "2026-11-20T13:41:00Z",
            "falha": null,
            "duracaoMin": 10
          }
        ]
      }
      """
    E "abertas" traz a última mudança de cada serviço ou módulo fora de NO_AR
    E vem com FORA_DO_AR antes de LENTO e, entre as iguais, a mais nova primeiro
    E "voltaram" traz só as voltas a NO_AR dos últimos 10 min, a mais nova primeiro
    E "inicio" é quando o alvo saiu de NO_AR, e "duracaoMin" só vem na volta

  @RF-204
  Cenário: ocorrências ao vivo
    Dado o Caio com GET /auditoria/ocorrencias/ao-vivo aberto
    Então a resposta é 200 com Content-Type "text/event-stream" e Cache-Control "no-store"
    E o 1º evento, logo ao abrir, é "abertas", com o corpo de GET /auditoria/ocorrencias/abertas
    E quando o cache passa a LENTO, o evento "ocorrencia" chega em até 5 s com um item
    E um ": ping" chega a cada 30 s

  @RF-204
  Esquema do Cenário: a mesma ocorrência nos outros ao vivo da Auditoria
    Dado o Caio com GET <conexao> aberto
    Quando o cache passa a LENTO
    Então recebe também o evento "ocorrencia" em até 5 s, com o mesmo corpo

    Exemplos:
      | conexao                                  |
      | /auditoria/saude/ao-vivo                 |
      | /auditoria/registros/ao-vivo?nivel=ERRO  |

  @RF-204
  Cenário: conexão que conta no limite do usuário
    Dado o Caio com 3 conexões ao vivo da Auditoria, entre saúde, registros e ocorrências
    Quando abre GET /auditoria/ocorrencias/ao-vivo mais uma vez
    Então a mais antiga recebe "encerrado" com "LIMITE_CONEXOES" e fecha

  @RF-205
  Cenário: ocorrências do período
    Dado o relógio em 2026-11-20T14:00:00Z
    Quando o Caio envia GET /auditoria/ocorrencias
    Então a resposta é 200, com o cabeçalho X-Trace-Id e as últimas 24 h:
      """
      {
        "de": "2026-11-19T14:00:00Z",
        "ate": "2026-11-20T14:00:00Z",
        "itens": [
          {
            "id": "3c4d5e6f7a8b49c0d1e2f3a4b5c6d7e8",
            "tipo": "SERVICO",
            "alvo": "intermediador-pagamento",
            "nome": "Intermediador de pagamento",
            "de": "FORA_DO_AR",
            "para": "NO_AR",
            "inicio": "2026-11-20T13:30:00Z",
            "quando": "2026-11-20T13:52:00Z",
            "falha": null,
            "duracaoMin": 22
          }
        ],
        "proximo": null
      }
      """
    E "itens" vem da mais nova para a mais antiga, até 50 por página

  @RF-205
  Esquema do Cenário: filtros da lista
    Quando o Caio envia GET /auditoria/ocorrencias<consulta>
    Então a resposta é <resposta>

    Exemplos:
      | consulta                          | resposta                                       |
      | ?tipo=MODULO                      | 200, só com as dos módulos                     |
      | ?alvo=intermediador-pagamento     | 200, só com as do intermediador                |
      | ?para=FORA_DO_AR                  | 200, só com as mudanças para FORA_DO_AR        |
      | ?limite=200                       | 200, com até 200 itens                         |
      | ?tipo=FILA                        | 422 DADOS_INVALIDOS com o campo "tipo"         |
      | ?alvo=fila                        | 422 DADOS_INVALIDOS com o campo "alvo"         |
      | ?para=COM_FALHAS                  | 422 DADOS_INVALIDOS com o campo "para"         |
      | ?limite=201                       | 422 DADOS_INVALIDOS com o campo "limite"       |

  @RF-205
  Esquema do Cenário: limite de 31 dias, sem depender do relógio
    Quando o Caio envia GET /auditoria/ocorrencias?de=<de>&ate=2026-11-20T14:00:00Z
    Então a resposta é <resposta>

    Exemplos:
      | de                   | resposta                              |
      | 2026-10-20T14:00:00Z | 200, com os 31 dias                   |
      | 2026-10-20T13:59:59Z | 422 PERIODO_LONGO com o campo "ate"   |

  @RN-314
  Esquema do Cenário: quem pede
    Quando <quem> envia GET /auditoria/saude
    Então a resposta é <resposta>

    Exemplos:
      | quem                                      | resposta              |
      | o administrador do deboaz                 | 200                   |
      | o Caio, com "MOD-17" com "U"              | 200                   |
      | o Rui, membro do time sem "MOD-17"        | 403 ACESSO_NEGADO     |
      | a conta master da EBFGA                   | 403 ACESSO_NEGADO     |
      | a staff Maria, com "MOD-03" com "U"       | 403 ACESSO_NEGADO     |
      | alguém sem o cabeçalho Authorization      | 401 SESSAO_TERMINADA  |

  @RN-314
  Esquema do Cenário: todo endereço recusa quem não usa
    Quando o Rui, sem "MOD-17", envia GET <endereco>
    Então a resposta é 403 ACESSO_NEGADO, sem o cabeçalho X-Trace-Id
    E "auditoria" e "auditoriaErros" não ganham registro, como a leitura da RN-270 do 17

    Exemplos:
      | endereco                                    |
      | /auditoria/saude/banco/historico            |
      | /auditoria/saude/ao-vivo                    |
      | /auditoria/metricas                         |
      | /auditoria/metricas/enderecos               |
      | /auditoria/acoes                            |
      | /auditoria/registros                        |
      | /auditoria/registros/ao-vivo                |
      | /auditoria/execucoes                        |
      | /auditoria/execucoes/trc-1d6e3f08           |
      | /auditoria/card                             |
      | /auditoria/ocorrencias                      |
      | /auditoria/ocorrencias/abertas              |
      | /auditoria/ocorrencias/ao-vivo              |

  @RN-315
  Esquema do Cenário: estado pela resposta simulada
    Dado a aplicação de homologação, com Teste__Relogio "sim", e o cache NO_AR
    E PUT /_teste/auditoria/sondas/cache com <sonda>
    Quando envia POST /_teste/auditoria/verificacao <vezes>
    E envia GET /auditoria/saude
    Então o "cache" vem com o estado "<estado>" e a falha <falha>

    Exemplos:
      | sonda                                  | vezes   | estado     | falha               |
      | "resposta": "NORMAL", "tempoMs": 1000  | 1 vez   | NO_AR      | null                |
      | "resposta": "NORMAL", "tempoMs": 1001  | 1 vez   | LENTO      | null                |
      | "resposta": "NORMAL", "tempoMs": 3000  | 1 vez   | LENTO      | null                |
      | "resposta": "NENHUMA"                  | 1 vez   | FORA_DO_AR | "SEM_RESPOSTA"      |
      | "resposta": "ERRO"                     | 2 vezes | LENTO      | "RESPOSTA_COM_ERRO" |
      | "resposta": "ERRO"                     | 3 vezes | FORA_DO_AR | "RESPOSTA_COM_ERRO" |
      | "resposta": "RECUSADA"                 | 2 vezes | LENTO      | "CONEXAO_RECUSADA"  |
      | "resposta": "RECUSADA"                 | 3 vezes | FORA_DO_AR | "CONEXAO_RECUSADA"  |

  @RN-315
  Cenário: sonda lenta sem o tempo
    Quando envia PUT /_teste/auditoria/sondas/cache com "resposta": "LENTA"
    Então a sonda responde com "tempoMs": 2000, e o cache fica LENTO na próxima verificação

  @RN-315
  Esquema do Cenário: agendamento parado
    Dado a aplicação de homologação, com Teste__Relogio "sim", às 13:42:00
    E PUT /_teste/auditoria/sondas/agendamento-sac com "ultimaExecucao": "<ultima>"
    Quando envia POST /_teste/auditoria/verificacao e GET /auditoria/saude
    Então o "agendamento-sac" vem com o estado "<estado>"

    Exemplos:
      | ultima                 | estado      |
      | 2026-11-20T13:40:00Z   | NO_AR       |
      | 2026-11-20T13:37:00Z   | LENTO       |
      | 2026-11-20T13:36:59Z   | FORA_DO_AR  |

  @RN-315
  Cenário: agendamento com "resposta" de falha
    Dado a aplicação de homologação, com Teste__Relogio "sim", às 13:42:00
    E PUT /_teste/auditoria/sondas/agendamento-sac com "ultimaExecucao": "2026-11-20T13:41:00Z"
    E "resposta": "ERRO" no mesmo corpo
    Quando envia POST /_teste/auditoria/verificacao e GET /auditoria/saude
    Então o "agendamento-sac" vem NO_AR, com "falha": null
    E para os agendamentos a sonda usa só "ultimaExecucao" e ignora "resposta"

  @RN-315
  Esquema do Cenário: onde os ganchos respondem
    Dado que "…/" abrevia "/_teste/auditoria/" nos exemplos
    E a aplicação iniciada em <ambiente>, com Teste__Relogio <flag>
    Quando envia <metodo> <endereco>, com "resposta": "NORMAL" no corpo do PUT
    Então a resposta é <resposta>

    Exemplos:
      | ambiente    | flag    | metodo | endereco         | resposta                                 |
      | produção    | "sim"   | PUT    | …/sondas/cache   | 404                                      |
      | homologação | ausente | POST   | …/verificacao    | 404                                      |
      | homologação | "sim"   | PUT    | …/sondas/cache   | 204                                      |
      | homologação | "sim"   | DELETE | …/sondas/cache   | 204, e o cache volta à verificação real  |
      | homologação | "sim"   | PUT    | …/sondas/fila    | 404 SERVICO_INEXISTENTE                  |
      | produção    | "sim"   | POST   | …/agendamento    | 404                                      |
      | homologação | "sim"   | POST   | …/agendamento    | 204, card do minuto e descarte já feitos |
      | produção    | "sim"   | POST   | …/reiniciar      | 404                                      |
      | homologação | "sim"   | POST   | …/reiniciar      | 204, sondas trocadas e medições zeradas  |

  @RN-315
  Esquema do Cenário: corpo do gancho das sondas
    Dado a aplicação de homologação, com Teste__Relogio "sim"
    Quando envia PUT /_teste/auditoria/sondas/cache com <corpo>
    Então a resposta é <resposta>

    Exemplos:
      | corpo                                  | resposta                                     |
      | "resposta": "NORMAL", "tempoMs": 3000  | 204                                          |
      | só "tempoMs": 500                      | 422 DADOS_INVALIDOS com o campo "resposta"   |
      | "resposta": "PARADA"                   | 422 DADOS_INVALIDOS com o campo "resposta"   |
      | "resposta": "NORMAL", "tempoMs": 3001  | 422 DADOS_INVALIDOS com o campo "tempoMs"    |

  @RN-316
  Esquema do Cenário: estado pelas medições
    Dado a aplicação de homologação, com Teste__Relogio "sim", às 13:42:00
    E POST /_teste/auditoria/medicoes com 250 pedidos a "GET /pagamento/faturas" em 13:40
    E desses, <erros> com o status 500 e o resto com 200 em <tempo>
    Quando envia POST /_teste/auditoria/verificacao <vezes> e GET /auditoria/saude
    Então "MOD-01" vem com o estado "<estado>"

    Exemplos:
      | erros | tempo   | vezes   | estado      |
      | 2     | 120 ms  | 3 vezes | NO_AR       |
      | 3     | 120 ms  | 1 vez   | LENTO       |
      | 3     | 120 ms  | 3 vezes | FORA_DO_AR  |
      | 0     | 1800 ms | 3 vezes | LENTO       |
      | 0     | 4000 ms | 2 vezes | LENTO       |
      | 0     | 4000 ms | 3 vezes | FORA_DO_AR  |

  @RN-316
  Esquema do Cenário: módulo de cada endereço medido
    Quando chega <pedido>
    Então a medição vai com o módulo "<modulo>"

    Exemplos:
      | pedido                                   | modulo  |
      | POST /auth/hub/entrar                    | MOD-03  |
      | GET /usuarios/{id}                       | MOD-03  |
      | POST /contas                             | MOD-03  |
      | GET /sac/chamados/{id}                   | MOD-02  |
      | POST /pagamento/faturas/{id}/pagamentos  | MOD-01  |
      | GET /catalogo/vitrine                    | MOD-14  |
      | GET /auditoria/registros                 | MOD-17  |

  @RN-317
  Esquema do Cenário: percentil pela faixa
    Dado a aplicação de homologação, com Teste__Relogio "sim"
    E POST /_teste/auditoria/medicoes com 100 pedidos a "GET /sac/chamados": <distribuicao>
    Quando envia GET /auditoria/metricas?janela=1h&modulo=MOD-02
    Então o resumo traz "p50Ms": <p50>, "p95Ms": <p95> e "p99Ms": <p99>

    Exemplos:
      | distribuicao                              | p50  | p95  | p99   |
      | 60 de 40 ms, 30 de 90, 6 de 200, 4 de 450 | 50   | 250  | 500   |
      | 49 de 50 ms e 51 de 51 ms                 | 100  | 100  | 100   |
      | 97 de 300 ms e 3 de 12000 ms              | 500  | 500  | null  |
      | 95 de 900 ms e 5 de 2600 ms               | 1000 | 1000 | 3000  |

  @RN-317
  Cenário: medição injetada já gravada
    Dado a aplicação de homologação, com Teste__Relogio "sim", às 13:42:00
    Quando envia POST /_teste/auditoria/medicoes com 10 pedidos a "GET /sac/chamados" em 13:41
    Então GET /auditoria/metricas?janela=1h&modulo=MOD-02 já conta os 10, sem /medicoes/gravar
    E a próxima verificação já os usa no estado do "MOD-02"
    E POST /_teste/auditoria/medicoes/gravar só grava o que os pedidos de verdade juntaram

  @RN-317
  Esquema do Cenário: medição injetada inválida
    Quando envia POST /_teste/auditoria/medicoes com <corpo>
    Então a resposta é 422 DADOS_INVALIDOS com o campo "<campo>"

    Exemplos:
      | corpo                   | campo     |
      | "duracaoMs": -1         | duracaoMs |
      | a lista vazia           | medicoes  |

  @RN-318
  Esquema do Cenário: resultado pelos registros
    Dado a aplicação de homologação, com Teste__Relogio "sim", às 13:42:00
    E POST /_teste/auditoria/registros gravou para "trc-90ab12cd": <registros>
    E todos com "acao": "MUDAR_ESTADO_CONTA"
    Quando envia GET /auditoria/execucoes/trc-90ab12cd às 13:42:00
    Então a resposta é 200 com "resultado": "<resultado>" e "duracaoMs": <duracao>

    Exemplos:
      | registros                                      | resultado    | duracao |
      | INICIADO 13:20:00.000, CONCLUIDO 13:20:00.184  | CONCLUIDA    | 184     |
      | INICIADO 13:20:00.000, ALERTA 13:20:00.050     | RECUSADA     | 50      |
      | INICIADO 13:20:00.000, ERRO 13:20:02.107       | FALHOU       | 2107    |
      | só INICIADO 13:37:01.000                       | EM_ANDAMENTO | null    |
      | só INICIADO 13:37:00.000                       | INTERROMPIDA | null    |

  @RN-318
  Cenário: gancho de registros fora do teste
    Dado a aplicação de produção, com Teste__Relogio "sim"
    Quando envia POST /_teste/auditoria/registros
    Então a resposta é 404 e "auditoria" não ganha registro

  @RN-318
  Cenário: gancho de registros com quem e conta
    Dado a aplicação de homologação, com Teste__Relogio "sim"
    Quando envia POST /_teste/auditoria/registros com 1 registro de "acao": "CRIAR_USUARIO"
    E "quem": "usr-ebfga-maria", "conta": "conta-ebfga" e "alvo": "usr-ebfga-lucas"
    Então a resposta é 201
    E GET /auditoria/registros?quem=usr-ebfga-maria&conta=ebfga traz o registro
    E com "quem" de nome "Maria Souza" e "conta" de sigla "EBFGA", lidos de usuarios e contas
    E GET /auditoria/execucoes?conta=ebfga conta a execução dele

  @RN-318
  Esquema do Cenário: corpo do gancho de registros
    Dado a aplicação de homologação, com Teste__Relogio "sim"
    Quando envia POST /_teste/auditoria/registros com <corpo>
    Então a resposta é <resposta>

    Exemplos:
      | corpo                                  | resposta                                     |
      | 1 registro sem "quem" nem "conta"      | 201, com "quem": null e "conta": null        |
      | 1 registro sem "acao"                  | 422 DADOS_INVALIDOS com o campo "acao"       |
      | "quem": "usr-nao-existe"               | 422 DADOS_INVALIDOS com o campo "quem"       |
      | "conta": "conta-nao-existe"            | 422 DADOS_INVALIDOS com o campo "conta"      |
      | a lista vazia                          | 422 DADOS_INVALIDOS com o campo "registros"  |

  @RN-319
  Cenário: catálogo das ações
    Quando o Caio envia GET /auditoria/acoes
    Então a resposta é 200 com uma lista em que cada ação vem assim:
      """
      [
        {
          "acao": "CRIAR_USUARIO",
          "nome": "Criar usuário",
          "modulo": "MOD-03",
          "semQuem": "SEM_LOGIN"
        },
        {
          "acao": "FECHAR_POR_PRAZO",
          "nome": "Fechar por prazo",
          "modulo": "MOD-02",
          "semQuem": "SISTEMA"
        }
      ]
      """

  @RN-319
  Esquema do Cenário: todas as ações
    Quando o Caio envia GET /auditoria/acoes
    Então a lista traz "<acao>" com o nome "<nome>", o módulo "<modulo>" e "semQuem" "<sem>"

    Exemplos:
      | acao                         | nome                              | modulo | sem        |
      | CRIAR_CONTA                  | Criar conta                       | MOD-03 | SEM_LOGIN  |
      | CRIAR_USUARIO                | Criar usuário                     | MOD-03 | SEM_LOGIN  |
      | EDITAR_USUARIO               | Editar usuário                    | MOD-03 | SEM_LOGIN  |
      | NOVA_GERACAO_SENHA           | Nova geração de senha             | MOD-03 | SEM_LOGIN  |
      | GERAR_SENHA                  | Gerar senha                       | MOD-03 | SEM_LOGIN  |
      | NOVO_CODIGO_ACESSO           | Novo código de acesso             | MOD-03 | SEM_LOGIN  |
      | DESATIVAR_USUARIO            | Desativar usuário                 | MOD-03 | SEM_LOGIN  |
      | REATIVAR_USUARIO             | Reativar usuário                  | MOD-03 | SEM_LOGIN  |
      | MUDAR_ESTADO_CONTA           | Mudar estado da conta             | MOD-03 | SEM_LOGIN  |
      | CODIGO_MASTER                | Código da conta master            | MOD-03 | SEM_LOGIN  |
      | SALVAR_TEMPO_SESSAO          | Salvar tempo de sessão            | MOD-03 | SEM_LOGIN  |
      | EDITAR_PERMISSOES            | Editar permissões                 | MOD-03 | SEM_LOGIN  |
      | ENTRAR_HUB                   | Entrar no hub                     | MOD-03 | SEM_LOGIN  |
      | ENTRAR_QUEST                 | Entrar no quest                   | MOD-03 | SEM_LOGIN  |
      | ENTRAR_SALA                  | Entrar na sala                    | MOD-03 | SEM_LOGIN  |
      | CONFIRMAR_SENHA              | Confirmar senha                   | MOD-03 | SEM_LOGIN  |
      | RENOVAR_SESSAO               | Renovar sessão                    | MOD-03 | SEM_LOGIN  |
      | SAIR                         | Sair                              | MOD-03 | SEM_LOGIN  |
      | ENCERRAR_SESSAO              | Encerrar sessão                   | MOD-03 | SEM_LOGIN  |
      | ENCERRAR_SESSOES             | Encerrar sessões                  | MOD-03 | SEM_LOGIN  |
      | ESQUECI_SENHA_MASTER         | Esqueci a senha da master         | MOD-03 | SEM_LOGIN  |
      | ENCERRAR_SESSOES_DO_USUARIO  | Encerrar sessões do usuário       | MOD-03 | SISTEMA    |
      | AVISAR_PERMISSOES_NOVAS      | Avisar permissões novas           | MOD-03 | SISTEMA    |
      | AVISAR_CONTRATACAO_MUDOU     | Avisar mudança na contratação     | MOD-03 | SISTEMA    |
      | ABRIR_CHAMADO                | Abrir chamado                     | MOD-02 | SEM_LOGIN  |
      | ENVIAR_MENSAGEM              | Enviar mensagem                   | MOD-02 | SEM_LOGIN  |
      | ASSUMIR_CHAMADO              | Assumir chamado                   | MOD-02 | SEM_LOGIN  |
      | MUDAR_STATUS                 | Mudar status do chamado           | MOD-02 | SEM_LOGIN  |
      | REABRIR_CHAMADO              | Reabrir chamado                   | MOD-02 | SEM_LOGIN  |
      | ENCERRAR_CHAMADO             | Encerrar chamado                  | MOD-02 | SEM_LOGIN  |
      | AVALIAR_CHAMADO              | Avaliar chamado                   | MOD-02 | SEM_LOGIN  |
      | SALVAR_PRAZOS_SAC            | Salvar prazos do SAC              | MOD-02 | SEM_LOGIN  |
      | FECHAR_POR_PRAZO             | Fechar por prazo                  | MOD-02 | SISTEMA    |
      | PAGAMENTO_CRIADO             | Criar pagamento                   | MOD-01 | SEM_LOGIN  |
      | PAGAMENTO_CANCELADO          | Cancelar pagamento                | MOD-01 | SEM_LOGIN  |
      | FATURA_CANCELADA             | Cancelar fatura                   | MOD-01 | SEM_LOGIN  |
      | ESTORNO_PEDIDO               | Pedir estorno                     | MOD-01 | SEM_LOGIN  |
      | CONFIGURACAO_SALVA           | Salvar configuração do Pagamento  | MOD-01 | SEM_LOGIN  |
      | AVISO_DO_BANCO               | Aviso do banco                    | MOD-01 | BANCO      |
      | FATURA_VENCIDA               | Fatura vencida                    | MOD-01 | SISTEMA    |
      | CONSULTAR_AUDITORIA          | Consultar a Auditoria             | MOD-17 | SEM_LOGIN  |

  @RN-319
  Cenário: ação gravada sem nome no catálogo
    Dado um registro da ação "PUBLICAR_AGENDA", que não está em GET /auditoria/acoes
    Quando o Caio envia GET /auditoria/registros
    Então o item vem com "acaoNome": "PUBLICAR_AGENDA" e "modulo": null

  @RN-320
  Esquema do Cenário: nenhum endereço muda a auditoria
    Quando o Caio envia <metodo> <endereco>
    Então a resposta é 405, sem corpo
    E "auditoria" e "auditoriaErros" ficam como estavam

    Exemplos:
      | metodo | endereco                            |
      | DELETE | /auditoria/registros                |
      | PUT    | /auditoria/registros                |
      | POST   | /auditoria/execucoes/trc-1d6e3f08   |
      | DELETE | /auditoria/execucoes/trc-1d6e3f08   |

  @RN-321
  Esquema do Cenário: período pedido
    Dado o relógio em 2026-11-20T13:42:00Z
    Quando o Caio envia GET /auditoria/<recurso>?de=<de>&ate=2026-11-20T13:42:00Z
    Então a resposta é <resposta>

    Exemplos:
      | recurso   | de                   | resposta                             |
      | registros | 2026-10-20T13:42:00Z | 200                                  |
      | registros | 2026-10-20T13:41:59Z | 422 PERIODO_LONGO com o campo "ate"  |
      | execucoes | 2026-11-13T13:42:00Z | 200                                  |
      | execucoes | 2026-11-13T13:41:59Z | 422 PERIODO_LONGO com o campo "ate"  |

  @RN-321
  Cenário: período invertido
    Quando o Caio envia GET /auditoria/registros com "de" depois de "ate"
    Então a resposta é 422 PERIODO_INVERTIDO com o campo "ate"

  @RN-321
  Esquema do Cenário: período que falta
    Quando o Caio envia GET /auditoria/registros<consulta>
    Então "de" e "ate" da resposta são <periodo>

    Exemplos:
      | consulta                   | periodo                                          |
      | sem de nem ate             | as últimas 24 h até agora                        |
      | ?de=2026-11-20T10:00:00Z   | de 2026-11-20T10:00:00Z até agora                |
      | ?ate=2026-11-19T10:00:00Z  | as 24 h antes de 2026-11-19T10:00:00Z            |

  @RN-322
  Cenário: card do time deboaz
    Dado o intermediador fora do ar e 4 erros, 31 alertas e 1 interrompida em 2026-11-20
    Quando o Caio envia GET /auditoria/card
    Então a resposta é 200 com:
      """
      {
        "modulo": "MOD-17",
        "perfil": "DEBOAZ",
        "versao": 1288,
        "atualizadoEm": "2026-11-20T13:42:00Z",
        "vazio": false,
        "selo": 1,
        "reduzida": {
          "valor": 1,
          "rotulo": "serviço fora do ar"
        },
        "normal": {
          "valor": 4,
          "rotulo": "erros hoje",
          "apoio": [
            {
              "nome": "Fora do ar",
              "valor": 1
            },
            {
              "nome": "Alertas",
              "valor": 31
            },
            {
              "nome": "Interrompidas",
              "valor": 1
            }
          ]
        }
      }
      """
    E traz também a expandida e as series de dia, semana e mes, 12 números cada

  @RN-322
  Esquema do Cenário: quem pede o card
    Quando <quem> envia GET /auditoria/card
    Então a resposta é <resposta>

    Exemplos:
      | quem                             | resposta                          |
      | o administrador do deboaz        | 200 com o perfil "DEBOAZ"         |
      | o Rui, sem "MOD-17"              | 403 ACESSO_NEGADO                 |
      | a conta master da EBFGA          | 403 ACESSO_NEGADO                 |

  @RN-322
  Cenário: ao vivo
    Quando o Caio envia GET /auditoria/card/ao-vivo?intervalo=30
    Então a resposta é 200, como na RN-279 de 15_modulos
    E o 1º evento "card" chega logo ao abrir, com o corpo de GET /auditoria/card
    E ?intervalo=10 é 422 DADOS_INVALIDOS com o campo "intervalo"

  @RN-322
  Cenário: card fechado pelo agendamento
    Dado a aplicação de homologação, com Teste__Relogio "sim", às 13:42:00
    E POST /_teste/auditoria/registros gravou 1 registro de nível ERRO em 13:41
    Quando envia POST /_teste/auditoria/agendamento e depois GET /auditoria/card
    Então "normal.valor" é 1 a mais do que antes, com "versao" maior

  @RN-324
  Esquema do Cenário: ocorrências pela sonda simulada
    Dado a aplicação de homologação, com Teste__Relogio "sim", e o cache NO_AR sem ocorrência
    E as verificações do cache com as respostas <respostas> na sonda, nessa ordem
    Quando envia GET /auditoria/ocorrencias?alvo=cache
    Então "itens" traz <itens>

    Exemplos:
      | respostas        | itens                                                       |
      | LENTA            | 1, com "de": "NO_AR" e "para": "LENTO"                      |
      | LENTA, LENTA     | 1, porque a 2ª verificação não muda o estado                |
      | LENTA, NENHUMA   | 2, a mais nova com "de": "LENTO" e "para": "FORA_DO_AR"     |
      | NENHUMA, NORMAL  | 2, a mais nova com "para": "NO_AR" e "duracaoMin"           |
      | NORMAL           | nenhum                                                      |

  @RN-324
  Cenário: módulo sem pedidos não abre ocorrência
    Dado o MOD-14 sem pedidos nos últimos 5 min
    Quando envia POST /_teste/auditoria/verificacao
    Então GET /auditoria/ocorrencias?alvo=MOD-14 traz "itens": []

  @RN-324
  Cenário: módulo que fica sem pedidos fecha a ocorrência
    Dado o MOD-01 LENTO desde 13:31:00, com a ocorrência aberta pelas medições injetadas
    E nenhum pedido ao MOD-01 de 13:31:00 a 13:36:00
    Quando envia POST /_teste/auditoria/verificacao às 13:36:00
    Então GET /auditoria/saude traz o "MOD-01" em "SEM_PEDIDOS"
    E GET /auditoria/ocorrencias?alvo=MOD-01 traz a mais nova com "de": "LENTO" e "para": "NO_AR"
    E "duracaoMin": 5, e GET /auditoria/ocorrencias/abertas não traz mais o MOD-01

  @RN-324
  Esquema do Cenário: estados fora das ocorrências
    Dado o cache FORA_DO_AR, com a ocorrência aberta
    Quando a verificação seguinte o mostra em "<estado>"
    Então a ocorrência fecha com "para": "NO_AR", porque <estado> conta como NO_AR

    Exemplos:
      | estado          |
      | SEM_VERIFICACAO |
      | DESLIGADO       |

  @RN-324
  Cenário: duas instâncias, uma ocorrência
    Dado 2 instâncias no ar e o cache que passa a LENTO na verificação das 13:31:00
    Quando envia GET /auditoria/ocorrencias?alvo=cache
    Então "itens" traz uma só ocorrência, com "quando": "2026-11-20T13:31:00Z"

  @RN-325
  Cenário: consulta gravada na auditoria
    Dado o token do Caio, com "MOD-17" com "U"
    Quando envia GET /auditoria/registros?nivel=ERRO às 13:42:05 e a resposta é 200
    Então a resposta traz o cabeçalho X-Trace-Id gerado pela aplicação, aqui o exemplo "trc-7a2c5e90"
    E GET /auditoria/registros?acao=CONSULTAR_AUDITORIA traz, entre os itens:
      """
      {
        "id": "6b2e9f0a4c1d4e7f8a9b0c1d2e3f4a5b",
        "tipo": "AUDITORIA",
        "traceId": "trc-7a2c5e90",
        "etapa": "CONCLUIDO",
        "nivel": null,
        "codigo": null,
        "acao": "CONSULTAR_AUDITORIA",
        "acaoNome": "Consultar a Auditoria",
        "modulo": "MOD-17",
        "quem": {
          "id": "usr-dboaz-caio",
          "nome": "Caio Lima"
        },
        "conta": null,
        "alvo": "/auditoria/registros?nivel=ERRO",
        "quando": "2026-11-20T13:42:05.118Z"
      }
      """
    E o "INICIADO" do mesmo traceId vem logo depois dele, por ser mais antigo
    E "traceId" é o do cabeçalho dessa resposta; "id" e "traceId" mudam a cada pedido

  @RN-325
  Esquema do Cenário: o que é gravado
    Dado os 4 passos de "trc-1d6e3f08" gravados por POST /_teste/auditoria/registros
    E cada um como na execução concluída do RF-202-api
    Quando o Caio envia GET <endereco> e a resposta é 200
    Então <resultado>

    Exemplos:
      | endereco                                      | resultado                                |
      | /auditoria/saude                              | grava, com o alvo "/auditoria/saude"     |
      | /auditoria/saude/banco/historico?periodo=7d   | grava, com esse alvo                     |
      | /auditoria/saude/ao-vivo                      | grava uma vez, ao abrir                  |
      | /auditoria/metricas/enderecos?janela=1h       | grava, com esse alvo                     |
      | /auditoria/registros/ao-vivo?nivel=ERRO       | grava uma vez, ao abrir                  |
      | /auditoria/execucoes/trc-1d6e3f08             | grava, com esse alvo                     |
      | /auditoria/ocorrencias?tipo=MODULO            | grava, com esse alvo                     |
      | /auditoria/acoes                              | não grava, sem X-Trace-Id                |
      | /auditoria/card                               | não grava, sem X-Trace-Id                |
      | /auditoria/ocorrencias/abertas                | não grava, sem X-Trace-Id                |
      | /auditoria/ocorrencias/ao-vivo                | não grava, sem X-Trace-Id                |

  @RN-325
  Cenário: alvo da consulta
    Quando o Caio envia GET /auditoria/registros?nivel=ERRO&conta=ebfga&depois=MjAy&limite=100
    Então o alvo gravado é "/auditoria/registros?conta=ebfga&nivel=ERRO"
    E os parâmetros vão em ordem alfabética, sem "depois" nem "limite"

  @RN-325
  Esquema do Cenário: a mesma consulta em menos de 5 min
    Dado o Caio com GET /auditoria/metricas?janela=1h gravado às 13:40:00
    Quando envia o mesmo GET às <hora>
    Então a resposta é 200, com o X-Trace-Id, e <resultado>

    Exemplos:
      | hora      | resultado                                     |
      | 13:44:59  | nenhum "CONSULTAR_AUDITORIA" novo             |
      | 13:45:00  | um "CONSULTAR_AUDITORIA" novo                 |

  @RN-325
  Esquema do Cenário: consulta recusada não é gravada
    Quando <quem> envia GET <endereco>
    Então a resposta é <resposta> e "auditoria" não ganha "CONSULTAR_AUDITORIA"

    Exemplos:
      | quem                    | endereco                       | resposta             |
      | o Rui, sem "MOD-17"     | /auditoria/registros           | 403 ACESSO_NEGADO    |
      | o Caio                  | /auditoria/metricas?janela=2h  | 422 DADOS_INVALIDOS  |

  @RNF-246
  Cenário: saúde ao vivo
    Quando o Caio envia GET /auditoria/saude/ao-vivo com Accept "text/event-stream"
    Então a resposta é 200 com Content-Type "text/event-stream" e Cache-Control "no-store"
    E o 1º evento "saude" chega logo ao abrir, com o corpo de GET /auditoria/saude
    E depois um evento "saude" a cada verificação, a cada 30 s, com "verificadoEm" novo

  @RNF-246
  Cenário: quarta conexão na saúde ao vivo
    Dado o Caio com 3 conexões em /auditoria/saude/ao-vivo
    Quando abre a 4ª
    Então a 4ª recebe 200 e a mais antiga recebe "encerrado" com "LIMITE_CONEXOES" e fecha

  @RNF-246
  Cenário: tempo limite da verificação
    Dado PUT /_teste/auditoria/sondas/banco com "resposta": "NENHUMA", em homologação
    Quando envia POST /_teste/auditoria/verificacao
    Então a resposta é 200 em até 4 s, porque cada serviço tem até 3 s para responder

  @RNF-247
  Cenário: pedidos medidos
    Dado a aplicação de homologação, com Teste__Relogio "sim"
    Quando a conta master envia 10 vezes GET /sac/chamados
    E envia POST /_teste/auditoria/medicoes/gravar
    Então GET /auditoria/metricas/enderecos?janela=1h&modulo=MOD-02 traz "GET /sac/chamados"
    E com 10 pedidos a mais do que antes

  @RNF-247
  Esquema do Cenário: pedidos que não entram nas métricas
    Quando chega <pedido> e o minuto é gravado
    Então nenhum endereço novo aparece em GET /auditoria/metricas/enderecos

    Exemplos:
      | pedido                                |
      | GET /health                           |
      | POST /_teste/auditoria/verificacao    |
      | GET /auditoria/registros/ao-vivo      |
      | GET /nao-existe                       |

  @RNF-249
  Cenário: registro novo no ao vivo
    Dado o Caio com GET /auditoria/registros/ao-vivo aberto
    Quando a Maria envia POST /usuarios do participante Lucas e a resposta é 201
    Então o Caio recebe os eventos "registro" do "INICIADO" e do "CONCLUIDO" em até 5 s

  @RNF-249
  Cenário: ocorrência nova no ao vivo
    Dado o Caio com GET /auditoria/ocorrencias/ao-vivo aberto
    Quando a verificação das 13:31:00 mostra o cache LENTO
    Então o evento "ocorrencia" chega até 13:31:05

  @RNF-249
  Cenário: aviso perdido não perde o registro
    Dado o cache fora do ar quando a Maria cria o Lucas
    Então os registros ficam gravados em "auditoria" e a criação responde 201 como sempre
    E GET /auditoria/registros os traz, mesmo sem o evento ao vivo

  @RNF-250
  Cenário: registros e execuções com muito volume
    Dado 5.000.000 registros em "auditoria" e 400.000 em "auditoriaErros", de 800 contas
    Quando o Caio envia GET /auditoria/registros?conta=ebfga&de=2026-10-20T13:42:00Z
    Então a resposta 200 chega em até 2 s, com 50 itens
    E GET /auditoria/execucoes?resultado=FALHOU,INTERROMPIDA de 7 dias chega em até 3 s

  @RNF-250
  Cenário: métricas de 30 dias
    Dado 90 dias de medições por hora de 60 endereços
    Quando o Caio envia GET /auditoria/metricas?janela=30d
    Então a resposta 200 chega em até 1 s, somada no banco, sem trazer cada hora à aplicação

  @RNF-251
  Cenário: pronta depois da partida
    Dado a partida de homologação concluída, com as coleções e os índices da Auditoria
    Quando envia GET /health, sem login
    Então a resposta é 200, sem corpo

  @RNF-251
  Cenário: índice que não pode ser criado
    Dado o banco recusa o índice acao_quando em "auditoria" na partida
    Quando envia GET /health, sem login
    Então a resposta é 200, sem corpo
    E o log traz um aviso com o nome do índice e da coleção, nunca o valor

  @RNF-252
  Cenário: saúde respondida sem o banco
    Dado o banco de dados sem resposta há 1 min e a sessão do Caio ativa
    Quando o Caio envia GET /auditoria/saude
    Então a resposta é 200 com o "banco" em "FORA_DO_AR", lida da última verificação
    E a consulta não fica gravada, e o log da aplicação avisa "consulta sem registro"
    E GET /auditoria/ocorrencias/abertas responde 200 com a ocorrência do "banco"
    E GET /auditoria/registros e GET /auditoria/ocorrencias respondem 503 BANCO_DE_DADOS_FORA
    E GET /auditoria/metricas e GET /auditoria/metricas/enderecos também respondem 503
    E a "mensagem" é "Os registros dependem do banco de dados, fora do ar."

  @RNF-252
  Cenário: ocorrência do banco guardada na volta
    Dado o banco FORA_DO_AR de 13:41:00 a 13:47:30
    Quando envia GET /auditoria/ocorrencias às 13:50:00
    Então "itens" traz a ida do "banco" a FORA_DO_AR com "quando": "2026-11-20T13:41:00Z"
    E a volta a NO_AR com "quando": "2026-11-20T13:47:30Z" e "duracaoMin": 6

  @fora-de-escopo
  Cenário: o que a Auditoria não faz
    Então a Auditoria não altera, apaga nem exporta registro
    E o aviso de queda fica só dentro da Auditoria, na faixa de ocorrências (RF-204)
    E não vai por e-mail, SMS, WhatsApp nem notificação do aparelho
    E não mostra os logs técnicos do ambiente, que ficam no Log Analytics (05 RNF-81)
    E não mostra a telemetria do Application Insights (05 RNF-82), nem a substitui
    E não mede o uso de CPU, memória ou disco das instâncias
    E com o cache fora do ar nenhuma sessão é conferida, e a Auditoria também não abre
    E não é usada pelas contas: só o administrador e o time deboaz a veem
