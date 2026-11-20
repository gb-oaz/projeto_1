# language: pt
# Cenários de API de 1_Requisitos/19_modulo-pagamento.md (v05), copiados do requisito:
# mudou lá, muda aqui. Cada cenário leva a tag do ID que cobre.
Funcionalidade: Pagamento
  1_Requisitos/19_modulo-pagamento.md · Cenários de API

  @formato-comum
  Cenário: resposta de erro
    Quando um pedido ao Pagamento é recusado
    Então o corpo traz o código, a mensagem e, quando houver, o campo:
      """
      {
        "codigo": "DADOS_INVALIDOS",
        "mensagem": "O campo \"forma\" está faltando ou é inválido.",
        "campo": "forma",
        "traceId": "trc-7f3a9c21"
      }
      """
    E o "traceId" do corpo é o mesmo do cabeçalho X-Trace-Id ([`RNF-243`](#RNF-243))

  @formato-comum
  Cenário: identificadores, datas e valores
    Então a fatura vai no endereço pelo número, como "FAT-000402", e o pagamento como "PAG-000771"
    E toda data e hora segue a ISO 8601 em UTC, como "2026-12-03T13:00:00Z"
    E o vencimento e a competência vão sem hora, como "2026-12-10" e "2026-11"
    E todo valor vai em reais, com duas casas, como 3698.33
    E status e forma vão em maiúsculas, como "A_PAGAR" e "CARTAO_CREDITO"
    E o pagador usa "MOD-01" com "U"; o recebedor é só o papel "ADMINISTRADOR"
    E o "MOD-01" com "U" do administrador só abre o card; as rotas do recebedor checam o papel
    E toda resposta do Pagamento, de sucesso ou de erro, traz o cabeçalho X-Trace-Id

  @formato-comum
  Esquema do Cenário: mensagem de cada código de erro
    Quando a resposta do Pagamento é <resposta>
    Então a "mensagem" do corpo é "<mensagem>"

    Exemplos:
      | resposta                     | mensagem                                                    |
      | 403 ACESSO_NEGADO            | Você não tem permissão para esta ação.                      |
      | 404 FATURA_INEXISTENTE       | Fatura não encontrada.                                      |
      | 404 PAGAMENTO_INEXISTENTE    | Pagamento não encontrado.                                   |
      | 404 BOLETO_INEXISTENTE       | Este pagamento não tem boleto.                              |
      | 409 FATURA_PAGA              | Esta fatura já está paga.                                   |
      | 409 FATURA_CANCELADA         | Fatura cancelada não recebe pagamento.                      |
      | 409 PAGAMENTO_EM_ABERTO      | Esta fatura já tem um pagamento em andamento.               |
      | 409 PAGAMENTO_CONFIRMADO     | Pagamento já confirmado pelo banco: não pode ser cancelado. |
      | 409 PAGAMENTO_NAO_CONFIRMADO | Este pagamento ainda não foi confirmado pelo banco.         |
      | 409 ESTORNO_EM_ANDAMENTO     | Aguarde o estorno em andamento.                             |
      | 409 ESTORNO_PELO_SAC         | O estorno de boleto é pedido pela própria conta no SAC.     |
      | 412 CONFIGURACAO_MUDOU       | A configuração mudou. Recarregue e salve de novo.           |
      | 422 FORMA_INDISPONIVEL       | Esta forma de pagamento não está disponível agora.          |
      | 422 FORMA_OBRIGATORIA        | Mantenha ao menos uma forma ligada.                         |
      | 428 VERSAO_AUSENTE           | Envie a versão lida da configuração no If-Match.            |
      | 401 ASSINATURA_INVALIDA      | Assinatura do aviso inválida.                               |
      | 503 BANCO_INDISPONIVEL       | Não conseguimos falar com o banco agora. Nada foi cobrado.  |

  @formato-comum
  Esquema do Cenário: mensagens com valor
    Quando a resposta do Pagamento é <caso>
    Então a "mensagem" do corpo é "<mensagem>"

    Exemplos:
      | caso                                | mensagem                                            |
      | 422 DADOS_INVALIDOS, campo "forma"  | O campo "forma" está faltando ou é inválido.        |
      | 422 ESTORNO_ACIMA_DO_SALDO, 2698.33 | Valor acima do saldo de R$ 2.698,33.                |
      | 409 FATURA_PAGA, ao cancelar        | Fatura paga não pode ser cancelada. Faça o estorno. |

  @RF-182
  Cenário: lista da instituição
    Dado o token da staff Rita, da EBFGA, com "MOD-01" com "U"
    Quando envia GET /pagamento/faturas?situacao=A_PAGAR&pagina=1
    Então a resposta é 200 com itens, total e resumo, até 50 itens por página
    E as faturas "VENCIDA" vêm antes das "A_PAGAR", a competência mais nova primeiro
    E o item da FAT-000402 é:
      """
      {
        "id": "FAT-000402",
        "competencia": "2026-11",
        "valor": 3698.33,
        "vencimento": "2026-12-10",
        "status": "A_PAGAR",
        "pagamentoEmAberto": null
      }
      """
    E o resumo é:
      """
      {
        "aPagar": 3698.33,
        "vencidas": 0,
        "proximoVencimento": "2026-12-10"
      }
      """

  @RF-182
  Esquema do Cenário: situação pedida
    Quando a conta master envia GET /pagamento/faturas?situacao=<situacao>
    Então a resposta é <resposta>

    Exemplos:
      | situacao | resposta                                          |
      | A_PAGAR  | 200 com as faturas "A_PAGAR" e "VENCIDA"          |
      | PAGAS    | 200 com as faturas "PAGA"                         |
      | TODAS    | 200 com as faturas de todos os status             |
      | ABERTAS  | 422 DADOS_INVALIDOS com o campo "situacao"        |

  @RF-182
  Cenário: sem faturas
    Quando o Pedro Reis, sem faturas, envia GET /pagamento/faturas?situacao=TODAS
    Então a resposta é 200 com itens vazio e total 0

  @RF-183
  Cenário: fatura a pagar
    Dado o token da staff Rita, da EBFGA, com "MOD-01" com "U"
    Quando envia GET /pagamento/faturas/FAT-000402
    Então a resposta é 200 com:
      """
      {
        "id": "FAT-000402",
        "conta": {
          "id": "conta-ebfga",
          "sigla": "EBFGA",
          "nome": "Escola Bento Freitas"
        },
        "competencia": "2026-11",
        "emitidaEm": "2026-12-01T03:00:00Z",
        "vencimento": "2026-12-10",
        "status": "A_PAGAR",
        "itens": [
          {
            "moduloId": "MOD-03",
            "nome": "Usuários",
            "cobrancaId": "cob-ebfga-MOD-03-2026-11",
            "valor": 2224.41
          },
          {
            "moduloId": "MOD-05",
            "nome": "Quiz",
            "cobrancaId": "cob-ebfga-MOD-05-2026-11",
            "valor": 1473.92
          }
        ],
        "valor": 3698.33,
        "pagaEm": null,
        "devolvido": 0.00,
        "juros": null,
        "totalAPagar": 3698.33,
        "pagamentos": []
      }
      """

  @RF-183
  Cenário: fatura vencida com juros
    Dado a FAT-000371, do Pedro, vencida em 2026-11-10, com 0,1 % ao dia
    Quando o Pedro envia GET /pagamento/faturas/FAT-000371 em 2026-11-14
    Então a resposta é 200 com o status "VENCIDA", o valor 294.00 e:
      """
      {
        "juros": {
          "taxaAoDia": 0.1,
          "dias": 4,
          "valor": 1.18
        },
        "totalAPagar": 295.18
      }
      """
    E itens traz o "MOD-03" Usuários com 147.00 e o "MOD-05" Quiz com 147.00
    E com a taxa 0, juros vem null e totalAPagar 294.00

  @RF-183
  Cenário: fatura cancelada
    Quando o Pedro envia GET /pagamento/faturas/FAT-000371, cancelada em 2026-11-14
    Então a resposta é 200 com o status "CANCELADA", "canceladaEm" e o motivo
    E "motivo" é "Cobrança em dobro do Quiz"

  @RF-183
  Esquema do Cenário: fatura de outra conta ou que não existe
    Quando a conta master da EBFGA envia GET /pagamento/faturas/<id>
    Então a resposta é 404 FATURA_INEXISTENTE, sem dizer se a fatura existe

    Exemplos:
      | id         | fatura                   |
      | FAT-000371 | do Pedro Reis            |
      | FAT-999999 | que não existe           |

  @RF-184
  Cenário: formas aceitas
    Dado o boleto desligado pelo administrador do deboaz
    Quando a Rita envia GET /pagamento/formas
    Então a resposta é 200 com:
      """
      {
        "formas": [
          "PIX",
          "CARTAO_CREDITO",
          "CARTAO_DEBITO"
        ],
        "cartao": {
          "intermediador": "MERCADO_PAGO",
          "chavePublica": "APP_USR-3f1c…"
        },
        "enderecoDoBoleto": null
      }
      """
    E "chavePublica" é a chave pública do intermediador, que a tela usa para gerar o token
    E fora de produção, com o intermediador falso, vem "intermediador": "FALSO"

  @RF-184
  Cenário: último endereço do boleto da conta
    Dado o boleto PAG-000785 da EBFGA gerado com o endereço da Rua Sete
    Quando a Rita envia GET /pagamento/formas
    Então "enderecoDoBoleto" traz:
      """
      {
        "cep": "96015-000",
        "logradouro": "Rua Sete de Setembro",
        "numero": "1020",
        "bairro": "Centro",
        "cidade": "Pelotas",
        "uf": "RS"
      }
      """

  @RF-184
  Esquema do Cenário: pedido de pagamento recusado
    Quando a Rita envia POST /pagamento/faturas/FAT-000402/pagamentos com <pedido>
    Então a resposta é <resposta>

    Exemplos:
      | pedido                                  | resposta                                  |
      | sem a forma                             | 422 DADOS_INVALIDOS com o campo "forma"   |
      | a forma "CHEQUE"                        | 422 DADOS_INVALIDOS com o campo "forma"   |
      | a forma "BOLETO", desligada             | 422 FORMA_INDISPONIVEL                    |
      | "PIX", com o boleto "AGUARDANDO" aberto | 409 PAGAMENTO_EM_ABERTO                   |

  @RF-184
  Cenário: cancelar o boleto para trocar de forma
    Dado o boleto PAG-000785 da FAT-000402 "AGUARDANDO"
    Quando a Rita envia POST /pagamento/pagamentos/PAG-000785/cancelar
    Então a resposta é 200 com o status "CANCELADO"
    E POST /pagamento/faturas/FAT-000402/pagamentos com "PIX" passa a ser aceito

  @RF-185
  Cenário: código PIX gerado
    Dado o token da staff Rita, da EBFGA, com "MOD-01" com "U"
    Quando envia POST /pagamento/faturas/FAT-000402/pagamentos em 2026-12-03T13:00:00Z com:
      """
      {
        "forma": "PIX"
      }
      """
    Então a resposta é 201 com:
      """
      {
        "id": "PAG-000772",
        "faturaId": "FAT-000402",
        "forma": "PIX",
        "valor": 3698.33,
        "status": "AGUARDANDO",
        "criadoEm": "2026-12-03T13:00:00Z",
        "expiraEm": "2026-12-03T13:30:00Z",
        "pix": {
          "copiaECola": "00020101021226…6304A1B2",
          "qrCodeSvg": "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 33 33\">…</svg>"
        }
      }
      """

  @RF-185
  Cenário: QR Code desenhado pelo deboaz
    Então "qrCodeSvg" é o QR Code do "copiaECola" desenhado em vetor, com quadrados do SVG
    E não é a imagem do banco embrulhada no SVG

  @RF-185
  Cenário: consultar o PIX
    Quando a Rita envia GET /pagamento/pagamentos/PAG-000772
    Então a resposta é 200 com o mesmo corpo e o status atual

  @RF-185
  Cenário: PIX expirado
    Dado o PAG-000772 sem pagamento até 2026-12-03T13:30:00Z
    Quando a Rita envia GET /pagamento/pagamentos/PAG-000772
    Então a resposta é 200 com o status "EXPIRADO" e sem o "pix"
    E a fatura FAT-000402 segue "A_PAGAR" e aceita um novo POST de pagamento

  @RF-186
  Cenário: crédito aprovado
    Dado o token do cartão "tok_9f2c71", gerado pela tela direto no banco
    Quando a Rita envia POST /pagamento/faturas/FAT-000402/pagamentos com:
      """
      {
        "forma": "CARTAO_CREDITO",
        "cartao": {
          "token": "tok_9f2c71",
          "metodo": "visa",
          "emissor": "25"
        }
      }
      """
    E "metodo" e "emissor" vêm do intermediador junto com o token; "emissor" pode faltar
    Então a resposta é 201 com:
      """
      {
        "id": "PAG-000773",
        "faturaId": "FAT-000402",
        "forma": "CARTAO_CREDITO",
        "valor": 3698.33,
        "status": "PAGO",
        "pagoEm": "2026-12-03T13:42:00Z",
        "cartao": {
          "bandeira": "VISA",
          "final": "4242"
        }
      }
      """

  @RF-186
  Cenário: crédito recusado
    Quando o banco recusa o cartão
    Então a resposta é 201 com o status "RECUSADO" e o motivo "RECUSADO_PELO_EMISSOR"
    E a fatura FAT-000402 segue "A_PAGAR"

  @RF-186
  Esquema do Cenário: cartão em análise ou inválido
    Quando a Rita envia o pagamento com <cartao>
    Então a resposta é <resposta>

    Exemplos:
      | cartao                               | resposta                                       |
      | um cartão em análise no banco        | 201 com o status "PROCESSANDO"                 |
      | o token "tok_vencido", já usado      | 422 DADOS_INVALIDOS com o campo "cartao.token" |
      | o token sem o "metodo"               | 422 DADOS_INVALIDOS com o campo "cartao.metodo" |
      | sem o cartao                         | 422 DADOS_INVALIDOS com o campo "cartao"       |

  @RF-187
  Cenário: débito aguardando a confirmação
    Quando a Rita envia POST /pagamento/faturas/FAT-000402/pagamentos em 2026-12-03T13:00:00Z com:
      """
      {
        "forma": "CARTAO_DEBITO",
        "cartao": {
          "token": "tok_d41b07",
          "metodo": "debmaster"
        }
      }
      """
    Então a resposta é 201 com:
      """
      {
        "id": "PAG-000774",
        "faturaId": "FAT-000402",
        "forma": "CARTAO_DEBITO",
        "valor": 3698.33,
        "status": "AGUARDANDO",
        "expiraEm": "2026-12-03T13:10:00Z",
        "debito": {
          "confirmacaoUrl": "https://banco.exemplo/confirmar/d41b07"
        },
        "cartao": {
          "bandeira": "MASTERCARD",
          "final": "5100"
        }
      }
      """

  @RF-187
  Esquema do Cenário: fim do débito
    Dado o PAG-000774 "AGUARDANDO"
    Quando <evento>
    Então GET /pagamento/pagamentos/PAG-000774 traz o status "<status>"

    Exemplos:
      | evento                                             | status    |
      | o banco confirma às 13:04                          | PAGO      |
      | o banco recusa                                     | RECUSADO  |
      | chega 2026-12-03T13:10:00Z sem a confirmação       | EXPIRADO  |

  @RF-188
  Cenário: boleto gerado
    Quando a Rita envia POST /pagamento/faturas/FAT-000402/pagamentos em 2026-12-03T13:00:00Z com:
      """
      {
        "forma": "BOLETO",
        "endereco": {
          "cep": "96015-000",
          "logradouro": "Rua Sete de Setembro",
          "numero": "1020",
          "bairro": "Centro",
          "cidade": "Pelotas",
          "uf": "RS"
        }
      }
      """
    Então a resposta é 201 com:
      """
      {
        "id": "PAG-000785",
        "faturaId": "FAT-000402",
        "forma": "BOLETO",
        "valor": 3698.33,
        "status": "AGUARDANDO",
        "boleto": {
          "vencimento": "2026-12-10",
          "linhaDigitavel": "23793.38128 60000.000003 00000.000400 1 12960000369833",
          "codigoBarras": "23791129600003698333381260000000000000000400",
          "pdf": "/pagamento/pagamentos/PAG-000785/boleto"
        }
      }
      """
    E o boleto sai em nome da conta, "Escola Bento Freitas", com o documento 12.345.678/0001-90

  @RF-188
  Cenário: PDF do boleto
    Quando a Rita envia GET /pagamento/pagamentos/PAG-000785/boleto
    Então a resposta é 200 com Content-Type "application/pdf"
    E Content-Disposition "attachment; filename=\"boleto-FAT-000402.pdf\""
    E o PDF traz a linha digitável e o código de barras desenhado, no padrão intercalado 2 de 5

  @RF-188
  Esquema do Cenário: endereço do boleto recusado
    Quando a Rita envia o boleto da FAT-000402 <endereco>
    Então a resposta é 422 DADOS_INVALIDOS com o campo "<campo>"

    Exemplos:
      | endereco                    | campo        |
      | sem o endereco              | endereco     |
      | com o CEP "9601-500"        | endereco.cep |
      | com a UF "XX"               | endereco.uf  |

  @RF-188
  Esquema do Cenário: fim do boleto
    Dado o PAG-000785 "AGUARDANDO", com vencimento em 2026-12-10
    Quando <evento>
    Então GET /pagamento/pagamentos/PAG-000785 traz <resultado>

    Exemplos:
      | evento                                           | resultado                               |
      | o banco confirma a compensação de 2026-12-04     | o status "PAGO" e pagoEm de 2026-12-04  |
      | chega 2026-12-16 sem compensação                 | o status "EXPIRADO"                     |

  @RF-188
  Cenário: PDF de boleto de um PIX
    Dado o PIX PAG-000772 da FAT-000402, já "EXPIRADO", sem boleto em aberto
    Quando a Rita envia GET /pagamento/pagamentos/PAG-000772/boleto
    Então a resposta é 404 BOLETO_INEXISTENTE

  @RF-189
  Cenário: baixar o comprovante
    Dado o PAG-000655, "PAGO", da FAT-000318
    Quando a conta master envia GET /pagamento/pagamentos/PAG-000655/comprovante
    Então a resposta é 200 com Content-Type "application/pdf"
    E Content-Disposition "attachment; filename=\"comprovante-FAT-000318.pdf\""

  @RF-189
  Cenário: pagamento sem confirmação
    Quando a Rita envia GET /pagamento/pagamentos/PAG-000785/comprovante, do boleto "AGUARDANDO"
    Então a resposta é 409 PAGAMENTO_NAO_CONFIRMADO

  @RF-190
  Cenário: resumo da competência
    Dado o token do administrador do deboaz, em 2026-11-14
    Quando envia GET /pagamento/recebimentos/resumo?competencia=2026-10
    Então a resposta é 200 com:
      """
      {
        "competencia": "2026-10",
        "faturado": {
          "valor": 212450.30,
          "faturas": 128
        },
        "recebido": {
          "valor": 198120.50,
          "faturas": 119
        },
        "aReceber": {
          "valor": 0.00,
          "faturas": 0
        },
        "vencido": {
          "valor": 14329.80,
          "faturas": 9
        },
        "inadimplencia": 6.7,
        "duplicidades": 0,
        "porForma": [
          {
            "forma": "PIX",
            "valor": 120300.00,
            "faturas": 74
          },
          {
            "forma": "CARTAO_CREDITO",
            "valor": 41250.50,
            "faturas": 25
          },
          {
            "forma": "CARTAO_DEBITO",
            "valor": 6570.00,
            "faturas": 5
          },
          {
            "forma": "BOLETO",
            "valor": 30000.00,
            "faturas": 15
          }
        ],
        "porTipoDeConta": [
          {
            "tipo": "INSTITUICAO",
            "recebido": 188920.50,
            "pagas": 90,
            "vencido": 13153.80,
            "vencidas": 6
          },
          {
            "tipo": "AUTONOMO",
            "recebido": 9200.00,
            "pagas": 29,
            "vencido": 1176.00,
            "vencidas": 3
          }
        ]
      }
      """

  @RF-190
  Cenário: por competência
    Quando o administrador envia GET /pagamento/recebimentos/competencias?ate=2026-10
    Então a resposta é 200 com as 12 competências de "2025-11" a "2026-10", a mais nova primeiro
    E cada uma com competencia, faturado, recebido, vencido e inadimplencia

  @RF-190
  Esquema do Cenário: quem pede os recebimentos
    Quando <quem> envia GET /pagamento/recebimentos/resumo?competencia=2026-10
    Então a resposta é <resposta>

    Exemplos:
      | quem                                  | resposta                                     |
      | o membro do time Caio                 | 403 ACESSO_NEGADO                            |
      | a conta master da EBFGA               | 403 ACESSO_NEGADO                            |
      | o administrador, com "2026-13"        | 422 DADOS_INVALIDOS com o campo "competencia" |

  @RF-191
  Cenário: vencidas de outubro
    Dado o token do administrador do deboaz, em 2026-11-14
    Quando envia GET /pagamento/recebimentos/faturas?competencia=2026-10&situacao=VENCIDA&pagina=1
    Então a resposta é 200 só com faturas "VENCIDA" de "2026-10" e total com quantas são
    E o item da FAT-000371 é:
      """
      {
        "id": "FAT-000371",
        "conta": {
          "id": "conta-pedro-reis",
          "nome": "Pedro Reis",
          "tipo": "AUTONOMO"
        },
        "competencia": "2026-10",
        "valor": 294.00,
        "vencimento": "2026-11-10",
        "status": "VENCIDA",
        "diasDeAtraso": 4
      }
      """

  @RF-191
  Cenário: busca
    Quando o administrador envia GET /pagamento/recebimentos/faturas?busca=EBFGA&situacao=TODAS
    Então a resposta é 200 com a FAT-000402, a FAT-000318 e a FAT-000247
    E a busca vale para a sigla, o nome da conta e o número da fatura

  @RF-191
  Cenário: ordem das vencidas
    Dado três faturas vencidas de "2026-10": A, com vencimento em 2026-11-08
    E B e C, com vencimento em 2026-11-10, B emitida antes de C e com número menor
    Quando o administrador pede as vencidas de "2026-10", como no cenário acima
    Então A vem antes de B, e B antes de C

  @RF-191
  Cenário: detalhe de uma fatura para o recebedor
    Quando o administrador envia GET /pagamento/recebimentos/faturas/FAT-000402
    Então a resposta é 200 com o mesmo corpo de GET /pagamento/faturas/FAT-000402 da conta
    E a FAT-999999 dá 404 FATURA_INEXISTENTE

  @RF-191
  Cenário: cancelar uma fatura
    Quando o administrador envia POST /pagamento/recebimentos/faturas/FAT-000371/cancelar com:
      """
      {
        "motivo": "Cobrança em dobro do Quiz"
      }
      """
    Então a resposta é 200 com o status "CANCELADA" e o motivo
    E GET /pagamento/faturas/FAT-000371 do Pedro traz o status "CANCELADA" e o motivo

  @RF-191
  Esquema do Cenário: cancelamento recusado
    Quando o administrador envia POST /pagamento/recebimentos/faturas/<id>/cancelar <corpo>
    Então a resposta é <resposta>

    Exemplos:
      | id         | corpo                       | resposta                                   |
      | FAT-000318 | com o motivo, fatura "PAGA" | 409 FATURA_PAGA                            |
      | FAT-000371 | sem o motivo                | 422 DADOS_INVALIDOS com o campo "motivo"   |
      | FAT-999999 | com o motivo                | 404 FATURA_INEXISTENTE                     |

  @RF-192
  Cenário: estornar parte de um pagamento
    Dado o token do administrador do deboaz e o PAG-000773 "PAGO", de 3698.33
    Quando envia POST /pagamento/recebimentos/pagamentos/PAG-000773/estornos com:
      """
      {
        "valor": 1473.92,
        "motivo": "Quiz cancelado"
      }
      """
    Então a resposta é 201 com:
      """
      {
        "id": "EST-000012",
        "pagamentoId": "PAG-000773",
        "valor": 1473.92,
        "motivo": "Quiz cancelado",
        "status": "PENDENTE",
        "pedidoEm": "2026-12-04T12:00:00Z"
      }
      """
    E GET /pagamento/recebimentos/pagamentos/PAG-000773 traz o status "ESTORNO_PENDENTE"

  @RF-192
  Cenário: pagamento com os estornos, para o recebedor
    Dado o EST-000012 confirmado pelo banco em 2026-12-04T15:10:00Z
    Quando o administrador envia GET /pagamento/recebimentos/pagamentos/PAG-000773
    Então a resposta é 200 com:
      """
      {
        "id": "PAG-000773",
        "faturaId": "FAT-000402",
        "conta": {
          "id": "conta-ebfga",
          "sigla": "EBFGA",
          "nome": "Escola Bento Freitas"
        },
        "forma": "CARTAO_CREDITO",
        "valor": 3698.33,
        "status": "ESTORNADO_PARCIAL",
        "pagoEm": "2026-12-03T13:42:00Z",
        "pagoPor": "Rita Gomes",
        "cartao": {
          "bandeira": "VISA",
          "final": "4242"
        },
        "estornado": 1473.92,
        "saldo": 2224.41,
        "estornos": [
          {
            "id": "EST-000012",
            "valor": 1473.92,
            "motivo": "Quiz cancelado",
            "status": "CONCLUIDO",
            "pedidoEm": "2026-12-04T12:00:00Z",
            "concluidoEm": "2026-12-04T15:10:00Z"
          }
        ],
        "traceId": "trc-1d6e3f08"
      }
      """
    E GET /pagamento/pagamentos/PAG-000773 da conta traz a mesma lista "estornos"

  @RF-192
  Esquema do Cenário: resposta do banco ao estorno
    Quando o banco <resposta do banco> o EST-000012
    Então GET /pagamento/recebimentos/pagamentos/PAG-000773 traz o status "<status>"
    E o EST-000012 em "estornos" com o status "<estorno>"

    Exemplos:
      | resposta do banco | status            | estorno   |
      | confirma          | ESTORNADO_PARCIAL | CONCLUIDO |
      | recusa            | PAGO              | RECUSADO  |

  @RF-192
  Cenário: boleto não se estorna por aqui
    Quando o administrador envia POST /pagamento/recebimentos/pagamentos/PAG-000790/estornos
    Então a resposta é 409 ESTORNO_PELO_SAC, porque o PAG-000790 é boleto
    E nenhum chamado é aberto: quem pagou abre pelo POST /sac/chamados, na categoria "COBRANCA"
    E o administrador que tenta abrir em nome da conta recebe 403, como no 18 RF-171-api

  @RF-192
  Cenário: pagador não estorna
    Quando a conta master envia POST /pagamento/recebimentos/pagamentos/PAG-000773/estornos
    Então a resposta é 403 ACESSO_NEGADO

  @RF-193
  Cenário: configuração atual
    Dado o token do administrador do deboaz
    Quando envia GET /pagamento/recebimentos/configuracao
    Então a resposta é 200 com:
      """
      {
        "versao": 4,
        "formas": {
          "PIX": true,
          "CARTAO_CREDITO": true,
          "CARTAO_DEBITO": true,
          "BOLETO": true
        },
        "jurosAoDia": 0,
        "toleranciaAvisoDias": 0,
        "diaVencimento": 10,
        "prazos": {
          "pixMinutos": 30,
          "debitoMinutos": 10,
          "boletoDiasUteis": 3
        }
      }
      """

  @RF-193
  Cenário: desligar o boleto
    Dado a configuração lida pelo administrador, como a versão 4 acima
    Quando ele envia PUT /pagamento/recebimentos/configuracao com If-Match da versão lida, "4"
    E o mesmo corpo, com "BOLETO": false
    Então a resposta é 200 com a versão lida + 1, a 5, e o boleto false
    E GET /pagamento/formas da conta master deixa de trazer "BOLETO"
    E os boletos "AGUARDANDO" seguem válidos até pagos ou expirados

  @RF-193
  Esquema do Cenário: configuração recusada
    Quando <quem> envia PUT /pagamento/recebimentos/configuracao com <corpo>
    Então a resposta é <resposta>, com o campo citado quando há

    Exemplos:
      | quem           | corpo                     | resposta                                 |
      | o admin        | as quatro formas false    | 422 FORMA_OBRIGATORIA                    |
      | o admin        | sem "formas.BOLETO"       | 422 DADOS_INVALIDOS, "formas.BOLETO"     |
      | o admin        | "jurosAoDia": 1.01        | 422 DADOS_INVALIDOS, "jurosAoDia"        |
      | o admin        | "toleranciaAvisoDias": 31 | 422 DADOS_INVALIDOS, "toleranciaAvisoDias" |
      | o admin        | "prazos.pixMinutos": 4    | 422 DADOS_INVALIDOS, "prazos.pixMinutos" |
      | o admin        | "diaVencimento": 15       | 422 DADOS_INVALIDOS, "diaVencimento"     |
      | o admin        | If-Match da lida - 1      | 412 CONFIGURACAO_MUDOU                   |
      | o admin        | sem If-Match              | 428 VERSAO_AUSENTE                       |
      | a conta master | o corpo válido            | 403 ACESSO_NEGADO                        |

  @RN-298
  Esquema do Cenário: quem usa cada parte
    Quando <quem> envia GET <endereco>
    Então a resposta é <resposta>

    Exemplos:
      | quem                                 | endereco                         | resposta          |
      | a staff Rita, com "MOD-01" com "U"   | /pagamento/faturas               | 200, da EBFGA     |
      | a staff Maria, sem "MOD-01"          | /pagamento/faturas               | 403 ACESSO_NEGADO |
      | o administrador do deboaz            | /pagamento/faturas               | 403 ACESSO_NEGADO |
      | o membro do time Caio                | /pagamento/faturas               | 403 ACESSO_NEGADO |
      | o participante Lucas                 | /pagamento/faturas               | 403 ACESSO_NEGADO |
      | a staff Rita                         | /pagamento/recebimentos/faturas  | 403 ACESSO_NEGADO |

  @RN-298
  Esquema do Cenário: pedidos de quem não pode
    Quando <quem> envia <pedido>
    Então a resposta é 403 ACESSO_NEGADO e nada muda

    Exemplos:
      | quem                 | pedido                                                   |
      | o participante Lucas | POST /pagamento/faturas/FAT-000402/pagamentos com "PIX"  |
      | o participante Lucas | POST /pagamento/pagamentos/PAG-000785/cancelar           |
      | a staff Maria        | POST /pagamento/pagamentos/PAG-000785/cancelar           |
      | a staff Rita         | GET /pagamento/recebimentos/faturas/FAT-000402           |
      | a staff Rita         | GET /pagamento/recebimentos/pagamentos/PAG-000773        |

  @RN-298
  Cenário: sem sessão válida
    Quando alguém envia GET /pagamento/faturas sem o cabeçalho Authorization
    Então a resposta é 401 SESSAO_TERMINADA

  @RN-299
  Cenário: conta master delega à Rita
    Dado as permissões da Rita lidas pela conta master, na versão que estiverem
    Quando a conta master envia PUT /usuarios/usr-ebfga-rita/permissoes com If-Match dessa versão e:
      """
      {
        "mods": {
          "MOD-01": "U"
        }
      }
      """
    Então a resposta é 200 com a versão lida + 1 e "MOD-01" com "U"
    E o token novo da Rita tem "MOD-01" com "U"

  @RN-299
  Esquema do Cenário: concessões recusadas
    Quando <quem> envia PUT /usuarios/<id>/permissoes com "MOD-01" com "<abas>"
    Então a resposta é 422 com:
      """
      {
        "codigo": "DADOS_INVALIDOS",
        "mensagem": "<mensagem>",
        "campo": "mods.MOD-01"
      }
      """

    Exemplos:
      | quem            | id             | abas | mensagem                                |
      | a conta master  | usr-ebfga-rita | CU   | O Pagamento só tem a aba Utilização.    |
      | a Rita, com Adm.| usr-ebfga-bia  | U    | Só a conta master concede o Pagamento.  |
      | o administrador | usr-dboaz-caio | U    | O módulo MOD-01 não pode ser concedido. |

  @RN-301
  Esquema do Cenário: pagamento de fatura que não aceita
    Dado a <id>, da conta dona, com o status <fatura>
    Quando a conta dona envia POST /pagamento/faturas/<id>/pagamentos com "PIX"
    Então a resposta é <resposta> e a fatura segue <fatura>

    Exemplos:
      | id         | fatura      | resposta             |
      | FAT-000318 | "PAGA"      | 409 FATURA_PAGA      |
      | FAT-000371 | "CANCELADA" | 409 FATURA_CANCELADA |

  @RN-301
  Cenário: fatura vencida aceita pagamento, com os juros
    Dado a FAT-000371, do Pedro, "VENCIDA" desde 2026-11-11, de 294.00, com 0,1 % ao dia
    Quando o Pedro envia POST /pagamento/faturas/FAT-000371/pagamentos com "PIX" em 2026-11-14
    Então a resposta é 201 com o valor 295.18: 294.00 da fatura e 1.18 de juros de 4 dias

  @RN-301
  Cenário: virada para vencida
    Dado a FAT-000402 "A_PAGAR" com vencimento em 2026-12-10
    Quando chega 2026-12-11T03:00:00Z sem pagamento
    Então GET /pagamento/faturas/FAT-000402 traz o status "VENCIDA" e o mesmo valor 3698.33
    E POST /auth/hub/entrar da Maria, staff da EBFGA, segue com 200: a conta não é desativada

  @RN-303
  Cenário: dois pedidos ao mesmo tempo
    Dado a FAT-000402 sem pagamento em aberto
    Quando a conta master e a Rita enviam POST /pagamento/faturas/FAT-000402/pagamentos juntas
    Então só uma recebe 201
    E a outra recebe 409 PAGAMENTO_EM_ABERTO

  @RN-303
  Cenário: pagamento em duplicidade
    Dado a FAT-000402 "PAGA" pelo PIX PAG-000781 e o boleto PAG-000790 "CANCELADO"
    Quando o banco confirma a compensação do PAG-000790
    Então GET /pagamento/pagamentos/PAG-000790 traz o status "PAGO" e "duplicidade": true
    E a FAT-000402 segue "PAGA", com pagaEm do PAG-000781
    E GET /pagamento/recebimentos/resumo?competencia=2026-11 traz "duplicidades": 1

  @RN-303
  Cenário: cancelar pagamento confirmado
    Quando a Rita envia POST /pagamento/pagamentos/PAG-000773/cancelar, do crédito "PAGO"
    Então a resposta é 409 PAGAMENTO_CONFIRMADO

  @RN-304
  Cenário: tentativas no detalhe
    Dado a FAT-000402 com o crédito recusado, o PIX expirado e o crédito pago de 2026-12-03
    Quando a Rita envia GET /pagamento/faturas/FAT-000402
    Então pagamentos traz os 3, o mais novo primeiro, cada um com o mesmo valor 3698.33:
      """
      [
        {
          "id": "PAG-000773",
          "forma": "CARTAO_CREDITO",
          "status": "PAGO",
          "criadoEm": "2026-12-03T13:42:00Z",
          "pagoPor": "Rita Gomes",
          "traceId": "trc-1d6e3f08"
        },
        {
          "id": "PAG-000772",
          "forma": "PIX",
          "status": "EXPIRADO",
          "criadoEm": "2026-12-03T13:00:00Z",
          "pagoPor": null,
          "traceId": "trc-83b5c1a9"
        },
        {
          "id": "PAG-000771",
          "forma": "CARTAO_CREDITO",
          "status": "RECUSADO",
          "criadoEm": "2026-12-03T12:40:00Z",
          "pagoPor": null,
          "traceId": "trc-52f0e7c4"
        }
      ]
      """

  @RN-305
  Cenário: aviso do banco com assinatura válida
    Dado o PAG-000781 "AGUARDANDO", com o identificador externo "pix_8c1e5d" no banco
    Quando o banco envia POST /pagamento/notificacoes com a assinatura válida, pago às 13:12
    Então a resposta é 200
    E o PAG-000781 fica "PAGO" e a FAT-000402 "PAGA", com pagaEm "2026-12-03T13:12:00Z"

  @RN-305
  Esquema do Cenário: avisos que não mudam nada
    Quando chega POST /pagamento/notificacoes <caso>
    Então a resposta é <resposta> e nenhum pagamento ou fatura muda
    E a coleção "avisosDoBanco" guarda o aviso com o "resultado" "<resultado>" e o traceId

    Exemplos:
      | caso                              | resposta                | resultado    |
      | com a assinatura inválida         | 401 ASSINATURA_INVALIDA | RECUSADO     |
      | repetido, já processado           | 200                     | REPETIDO     |
      | de pagamento externo desconhecido | 200                     | DESCONHECIDO |

  @RN-305
  Cenário: ocorrências para o time
    Então os avisos "RECUSADO" e "DESCONHECIDO" de "avisosDoBanco" são o que o time deboaz confere
    E o 401 ASSINATURA_INVALIDA também vai a "auditoriaErros", como na 17 RN-270
    E o aviso repetido não soma nada em dobro

  @RN-305
  Cenário: o pagador não confirma
    Quando a Rita envia POST /pagamento/pagamentos/PAG-000781/confirmar
    Então a resposta é 404, porque o endereço não existe
    E só o aviso do banco ou a consulta do deboaz ao banco confirma um pagamento

  @RN-306
  Esquema do Cenário: valor do estorno
    Dado o PAG-000773 de 3698.33, com <devolvido> já estornado
    Quando o administrador envia POST /pagamento/recebimentos/pagamentos/PAG-000773/estornos
    E o valor <valor>
    Então a resposta é <resposta>

    Exemplos:
      | devolvido | valor   | resposta                                         |
      | 1000.00   | 2698.33 | 201                                              |
      | 1000.00   | 2698.34 | 422 ESTORNO_ACIMA_DO_SALDO, com o saldo 2698.33  |
      | 0.00      | 0.00    | 422 DADOS_INVALIDOS com o campo "valor"          |
      | 0.00      | 10.005  | 422 DADOS_INVALIDOS com o campo "valor"          |

  @RN-306
  Cenário: estorno já em andamento
    Dado o EST-000012 "PENDENTE" do PAG-000773
    Quando o administrador envia outro POST de estorno do PAG-000773
    Então a resposta é 409 ESTORNO_EM_ANDAMENTO

  @RN-307
  Cenário: card da instituição
    Dado o token da staff Rita, da EBFGA, com "MOD-01" com "U", em 2026-12-03
    Quando envia GET /pagamento/card
    Então a resposta é 200 com:
      """
      {
        "modulo": "MOD-01",
        "perfil": "INSTITUICAO",
        "versao": 12,
        "atualizadoEm": "2026-12-01T03:00:00Z",
        "vazio": false,
        "selo": 1,
        "reduzida": {
          "valor": 3698.33,
          "rotulo": "a pagar"
        },
        "normal": {
          "valor": 3698.33,
          "rotulo": "Vence em 10/12",
          "apoio": [
            {
              "nome": "Vencidas",
              "valor": 0
            },
            {
              "nome": "Pago no ano",
              "valor": 39810.44
            }
          ]
        }
      }
      """
    E traz também a expandida e as séries "dia", "semana" e "mes", 12 números cada
    E a cada mudança de uma fatura que o card lê, "versao" passa à versão lida + 1

  @RN-307
  Esquema do Cenário: pontos de cada série
    Quando <perfil> lê a série "<serie>" do card
    Então cada ponto é <ponto>, do mais antigo ao atual

    Exemplos:
      | perfil         | serie  | ponto                                                     |
      | o deboaz       | dia    | o recebido no dia, pela data do pagamento                 |
      | o deboaz       | semana | o recebido nos 7 dias que terminam no dia do ponto        |
      | o deboaz       | mes    | o recebido no mês                                         |
      | a instituição  | dia    | o pago pela conta no dia                                  |
      | a instituição  | semana | o pago pela conta nos 7 dias que terminam no dia do ponto |
      | a instituição  | mes    | o valor da fatura da competência, sem as canceladas       |

  @RN-307
  Esquema do Cenário: tendência do deboaz na normal
    Dado o recebido de <atual> no mês e de <anterior> no mês anterior
    Quando o administrador envia GET /pagamento/card
    Então "normal.rotulo" é "<rotulo>"

    Exemplos:
      | atual     | anterior  | rotulo                       |
      | 186400.00 | 180800.00 | +3,1 % no mês                |
      | 170000.00 | 180800.00 | -6,0 % no mês                |
      | 186400.00 | 0.00      | Sem recebido no mês anterior |

  @RN-307
  Esquema do Cenário: selo da instituição
    Dado a FAT-000402 "A_PAGAR" com vencimento em 2026-12-10, sem fatura vencida
    Quando a Rita envia GET /pagamento/card em <dia>
    Então "selo" é <selo>, porque conta a pagar em até 5 dias úteis

    Exemplos:
      | dia        | selo |
      | 2026-12-02 | 0    |
      | 2026-12-03 | 1    |

  @RN-307
  Esquema do Cenário: quem pede o card
    Quando <quem> envia GET /pagamento/card
    Então a resposta é <resposta>

    Exemplos:
      | quem                          | resposta                              |
      | o administrador do deboaz     | 200 com o perfil "DEBOAZ" e o selo 9  |
      | o Pedro Reis                  | 200 com o perfil "AUTONOMO"           |
      | o membro do time Caio         | 403 ACESSO_NEGADO                     |
      | a staff Maria, sem "MOD-01"   | 403 ACESSO_NEGADO                     |

  @RN-307
  Cenário: ao vivo
    Quando a conta master envia GET /pagamento/card/ao-vivo?intervalo=30
    Então a resposta é 200, como em RN-279 de 15_modulos
    E traz Content-Type "text/event-stream" e Cache-Control "no-store"
    E o 1º evento "card" chega logo ao abrir, com o corpo de GET /pagamento/card

  @RN-309
  Esquema do Cenário: ações auditadas
    Quando <quem> envia <pedido> e a resposta é de sucesso
    Então a coleção "auditoria" ganha os registros da ação "<acao>", como na RN-178

    Exemplos:
      | quem    | pedido                                                      | acao                |
      | a Rita  | POST /pagamento/faturas/FAT-000402/pagamentos com "PIX"     | PAGAMENTO_CRIADO    |
      | a Rita  | POST /pagamento/pagamentos/PAG-000785/cancelar              | PAGAMENTO_CANCELADO |
      | o admin | POST /pagamento/recebimentos/faturas/FAT-000371/cancelar    | FATURA_CANCELADA    |
      | o admin | POST /pagamento/recebimentos/pagamentos/PAG-000773/estornos | ESTORNO_PEDIDO      |
      | o admin | PUT /pagamento/recebimentos/configuracao                    | CONFIGURACAO_SALVA  |
      | o banco | POST /pagamento/notificacoes                                | AVISO_DO_BANCO      |
      | sistema | a virada para "VENCIDA" e o aviso de atraso                 | FATURA_VENCIDA      |

  @RN-310
  Cenário: PIX da fatura vencida com juros
    Dado a FAT-000371, de 294.00, vencida em 2026-11-10, com 0,1 % ao dia
    Quando o Pedro envia POST /pagamento/faturas/FAT-000371/pagamentos em 2026-11-14 com "PIX"
    Então a resposta é 201 com o valor 295.18 e:
      """
      {
        "valorFatura": 294.00,
        "juros": 1.18
      }
      """
    E o valor do pagamento não muda depois de gerado, mesmo que vire o dia

  @RN-310
  Cenário: taxa mudada depois do vencimento
    Dado a FAT-000371 vencida em 2026-11-10 com 0,1 % ao dia
    Quando o administrador salva "jurosAoDia": 0.2 em 2026-11-12
    Então GET /pagamento/faturas/FAT-000371 segue com "taxaAoDia": 0.1

  @RN-311
  Cenário: aviso do Pedro
    Dado a FAT-000371 vencida em 2026-11-10, a tolerância 0 e os juros de 0,1 % ao dia
    Quando o Pedro envia GET /pagamento/avisos em 2026-11-14
    Então a resposta é 200 com:
      """
      {
        "itens": [
          {
            "tipo": "FATURA_EM_ATRASO",
            "faturaId": "FAT-000371",
            "vencimento": "2026-11-10",
            "valorFatura": 294.00,
            "juros": 1.18,
            "totalAPagar": 295.18,
            "desde": "2026-11-11T03:00:00Z"
          }
        ],
        "total": 1
      }
      """

  @RN-311
  Esquema do Cenário: quando o aviso existe
    Dado a FAT-000371 vencida em 2026-11-10 e a tolerância de <tolerancia> dias
    Quando o Pedro envia GET /pagamento/avisos em <dia>
    Então total é <total>

    Exemplos:
      | tolerancia | dia                    | total |
      | 3          | 2026-11-13T12:00:00Z   | 0     |
      | 3          | 2026-11-14T12:00:00Z   | 1     |
      | 0          | 2026-11-14, já paga    | 0     |

  @RN-311
  Cenário: avisos de outra conta e do time
    Quando o administrador do deboaz envia GET /pagamento/avisos
    Então a resposta é 403 ACESSO_NEGADO

  @RN-313
  Cenário: desativada, a fatura segue devida
    Dado a conta-pedro-reis com a FAT-000371 "VENCIDA" de 294.00 e juros de 0,1 % ao dia
    Quando o administrador do deboaz envia POST /contas/conta-pedro-reis/estado com:
      """
      {
        "estado": "DESATIVADA"
      }
      """
    Então a resposta é 200
    E GET /pagamento/recebimentos/faturas?busca=Pedro&situacao=TODAS em 2026-11-20 traz:
      """
      {
        "id": "FAT-000371",
        "conta": {
          "id": "conta-pedro-reis",
          "nome": "Pedro Reis",
          "tipo": "AUTONOMO"
        },
        "competencia": "2026-10",
        "valor": 294.00,
        "vencimento": "2026-11-10",
        "status": "VENCIDA",
        "diasDeAtraso": 10
      }
      """

  @RN-313
  Cenário: reativada, o aviso volta com os juros até hoje
    Dado a conta-pedro-reis desativada com a FAT-000371 "VENCIDA" de 294.00
    E os juros de 0,1 % ao dia, vigentes no vencimento, em 2026-11-10
    Quando a conta volta ao estado "ATIVA" e o Pedro envia GET /pagamento/avisos em 2026-11-20
    Então a resposta é 200 com total 1 e o item, com 10 dias de juros, de 11/11 a 20/11:
      """
      {
        "tipo": "FATURA_EM_ATRASO",
        "faturaId": "FAT-000371",
        "vencimento": "2026-11-10",
        "valorFatura": 294.00,
        "juros": 2.94,
        "totalAPagar": 296.94,
        "desde": "2026-11-11T03:00:00Z"
      }
      """

  @RN-313
  Cenário: exclusão recusada com fatura em aberto
    Dado a conta-pedro-reis com a FAT-000371 "VENCIDA"
    Quando o administrador do deboaz envia POST /contas/conta-pedro-reis/estado com "ENCERRADA"
    Então a resposta é 409 com:
      """
      {
        "codigo": "FATURA_EM_ABERTO",
        "mensagem": "Esta conta tem fatura em aberto e não pode ser excluída."
      }
      """

  @RN-313
  Cenário: inadimplência sozinha não bloqueia
    Dado a conta-pedro-reis "ATIVA" com a FAT-000371 "VENCIDA" há 30 dias
    Quando o Pedro envia POST /auth/hub/entrar com a senha certa
    Então a resposta é 200

  @RNF-238
  Cenário: pagamento ao vivo
    Dado a Rita com GET /pagamento/pagamentos/PAG-000781/ao-vivo aberto
    Quando o aviso do banco confirma o PAG-000781
    Então o evento "pagamento" com o status "PAGO" chega em até 5 s
    E traz Content-Type "text/event-stream" e Cache-Control "no-store"
    E o fluxo fecha depois do status final: "PAGO", "RECUSADO", "EXPIRADO" ou "CANCELADO"
    E cada evento traz o traceId da execução que mudou o status ([`RNF-244`](#RNF-244))

  @RNF-239
  Esquema do Cenário: dados do cartão no pedido
    Quando a Rita envia POST /pagamento/faturas/FAT-000402/pagamentos com o cartão <campo>
    Então a resposta é 422 DADOS_INVALIDOS com o campo "<campo>"
    E nada desse pedido é guardado nem escrito em log

    Exemplos:
      | campo          |
      | cartao.numero  |
      | cartao.cvv     |
      | cartao.validade |

  @RNF-241
  Cenário: banco sem resposta
    Quando a Rita envia POST /pagamento/faturas/FAT-000402/pagamentos com "PIX"
    E o banco não responde em 15 s
    Então a resposta é 503 BANCO_INDISPONIVEL
    E nenhum pagamento fica "AGUARDANDO" e a FAT-000402 segue "A_PAGAR"

  @RNF-242
  Cenário: o deboaz troca o intermediador de pagamento
    Dado o PAG-000785, boleto "AGUARDANDO", que guarda "intermediador": "MERCADO_PAGO"
    Quando uma nova implantação do ambiente liga outro intermediador
    Então faturas, status, códigos de erro e endereços do Pagamento não mudam
    E os novos pagamentos vão ao intermediador ligado e guardam o nome dele
    E o PAG-000785 segue com o Mercado Pago: avisos e consultas dele vão a ele
    E isso vale até o PAG-000785 ficar "PAGO", "EXPIRADO" ou "CANCELADO"

  @RNF-242
  Cenário: intermediador de cada ambiente
    Então em produção o intermediador é sempre o "MERCADO_PAGO", com as chaves do Key Vault
    E fora de produção é o "MERCADO_PAGO" de teste, ou o "FALSO" com o relógio de teste ligado
    E o "FALSO" também vale quando o ambiente não tem a chave de acesso do Mercado Pago
    E em produção, sem as chaves, a aplicação não inicia

  @RNF-242
  Esquema do Cenário: de qual intermediador é o aviso
    Quando chega POST /pagamento/notificacoes com <cabecalho>
    Então o aviso é conferido como do <intermediador>
    E o pagamento é achado pelo id externo e pelo intermediador que o gerou

    Exemplos:
      | cabecalho                                         | intermediador                     |
      | x-signature "ts=…,v1=…" e x-request-id            | "MERCADO_PAGO"                    |
      | X-Assinatura "sha256=…"                           | "FALSO", só fora de produção      |
      | nenhum dos dois                                   | nenhum: 401 ASSINATURA_INVALIDA   |

  @RNF-243
  Esquema do Cenário: X-Trace-Id em toda resposta do Pagamento
    Dado que "…/" abrevia "/pagamento/faturas/" nos exemplos
    Quando <quem> envia <pedido>
    Então a resposta é <resposta>, com o cabeçalho X-Trace-Id
    E o valor segue o formato "trc-" mais 8 hexadecimais, como "trc-1d6e3f08"
    E cada execução tem o seu, diferente do de qualquer outra

    Exemplos:
      | quem    | pedido                                               | resposta                |
      | a Rita  | GET /pagamento/faturas?situacao=A_PAGAR              | 200                     |
      | a Rita  | POST …/FAT-000402/pagamentos com "forma": "PIX"      | 201                     |
      | a Rita  | POST …/FAT-000318/pagamentos com "forma": "PIX"      | 409 FATURA_PAGA         |
      | a Rita  | POST …/FAT-000402/pagamentos com o corpo "forma=PIX" | 422 DADOS_INVALIDOS     |
      | alguém  | GET /pagamento/faturas sem Authorization             | 401 SESSAO_TERMINADA    |
      | o banco | POST /pagamento/notificacoes com assinatura inválida | 401 ASSINATURA_INVALIDA |
      | o admin | GET /pagamento/recebimentos/resumo?competencia=2026-10 | 200                   |
      | a Rita  | GET /pagamento/pagamentos/PAG-000781/ao-vivo         | 200, na abertura        |

  @RNF-243
  Cenário: o mesmo traceId no corpo do erro
    Quando a Rita envia POST /pagamento/faturas/FAT-000402/pagamentos sem a forma
    Então a resposta é 422 com o cabeçalho X-Trace-Id "trc-7f3a9c21" e:
      """
      {
        "codigo": "DADOS_INVALIDOS",
        "mensagem": "O campo \"forma\" está faltando ou é inválido.",
        "campo": "forma",
        "traceId": "trc-7f3a9c21"
      }
      """
    E o 503 BANCO_INDISPONIVEL e o 500 também trazem o traceId no corpo

  @RNF-243
  Esquema do Cenário: traceId de origem recebido no pedido
    Dado o 503 BANCO_INDISPONIVEL da FAT-000402 com o traceId "trc-9a20d5be"
    Quando a Rita envia o pagamento de novo com o cabeçalho X-Trace-Id <recebido>
    Então a resposta traz um X-Trace-Id novo, "trc-1d6e3f08"
    E <efeito>

    Exemplos:
      | recebido       | efeito                                                          |
      | "trc-9a20d5be" | a execução nova guarda "trc-9a20d5be" como traceIdOrigem        |
      | "abc"          | o valor fora do formato é ignorado e o pedido segue sem recusa  |

  @RNF-244
  Cenário: traceId gravado em cada transação
    Dado a execução "trc-1d6e3f08" do POST que cria o PAG-000773 da FAT-000402
    Então o PAG-000773 guarda o traceId "trc-1d6e3f08" de quando foi criado
    E cada mudança de status do PAG-000773 entra no histórico com o traceId de quem a fez
    E o mesmo vale para a fatura, o estorno e o aviso do banco
    E a auditoria da RN-309 e os erros da 17 RN-270 dessa execução têm "trc-1d6e3f08"

  @RNF-244
  Cenário: traceId levado ao intermediador
    Quando o deboaz pede ao intermediador a cobrança do PAG-000773
    Então o pedido leva o cabeçalho X-Trace-Id "trc-1d6e3f08"
    E os metadados da cobrança levam o pagamento "PAG-000773" e o traceId "trc-1d6e3f08"

  @RNF-244
  Esquema do Cenário: aviso do banco com o seu traceId
    Quando o banco envia POST /pagamento/notificacoes do "pix_8c1e5d" <como>
    Então a resposta é 200 com o X-Trace-Id "trc-6b0c42f1", novo
    E avisosDoBanco guarda "trc-6b0c42f1", o id externo "pix_8c1e5d" e o "PAG-000781"
    E <ligacao>

    Exemplos:
      | como                                  | ligacao                                             |
      | sem traceId                           | o PAG-000781 liga "pix_8c1e5d" a "trc-6b0c42f1"     |
      | com "trc-2e7d90aa" de volta nos dados | "trc-2e7d90aa", da criação, fica como traceIdOrigem |

  @RNF-244
  Cenário: traceId nos eventos ao vivo
    Dado a Rita com GET /pagamento/pagamentos/PAG-000781/ao-vivo aberto
    Quando o aviso do banco da execução "trc-6b0c42f1" confirma o PAG-000781
    Então o evento "pagamento" traz:
      """
      {
        "id": "PAG-000781",
        "status": "PAGO",
        "traceId": "trc-6b0c42f1"
      }
      """

  @RNF-244
  Cenário: execuções do agendamento
    Quando o agendamento vira a FAT-000371 para "VENCIDA" em 2026-11-11T03:00:00Z
    Então essa execução tem traceId próprio, como "trc-0f3b6a7d"
    E ele fica na mudança de status da FAT-000371 e na auditoria FATURA_VENCIDA

  @RNF-244
  Cenário: achar uma transação pelo traceId
    Dado o traceId "trc-1d6e3f08"
    Quando o time deboaz procura o que aconteceu nessa execução
    Então encontra o PAG-000773, a FAT-000402, a auditoria e os erros com esse traceId

  @RNF-245
  Cenário: números somados sem ler fatura por fatura
    Dado 20.000 faturas de 800 contas, em 24 competências
    Quando o administrador envia GET /pagamento/card ou GET /pagamento/recebimentos/resumo
    Então a resposta 200 chega em até 1 segundo, com competencia=2026-10 no resumo
    E os valores e as contagens são somados no banco de dados, sem trazer as faturas à aplicação

  @RNF-245
  Cenário: card da conta
    Dado a EBFGA com 24 faturas entre as 20.000
    Quando a Rita envia GET /pagamento/card
    Então a resposta 200 chega em até 1 segundo e só as faturas da EBFGA são lidas

  @fora-de-escopo
  Cenário: o que o Pagamento não faz
    Então não há PIX Automático, débito automático nem cobrança recorrente no cartão
    E não há parcelamento, cartão guardado para a próxima vez, outra moeda nem banco do exterior
    E não há divisão do valor entre recebedores nem nota fiscal
    E preço, desconto e plano anual são do Catálogo, e o cálculo de cada cobrança também
    E participante, responsável e acesso anônimo não pagam nada no deboaz
    E não há aviso por e-mail, SMS ou WhatsApp: toda comunicação é no próprio módulo
    E a falta de pagamento, sozinha, não desativa a conta nem bloqueia a entrada
    E desativar a conta não apaga a fatura em aberto ([`RN-313`](#RN-313))
    E multa por atraso não existe: só os juros ao dia da Configuração
