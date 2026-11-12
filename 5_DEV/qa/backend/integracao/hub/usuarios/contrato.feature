# language: pt
# Respostas do contrato 2_Diagramas/usuarios-contrato-api_v15_20261109-0912.svg que
# os requisitos ainda não trazem como cenário. Escritos pelo qa a partir do
# contrato e propostos ao deboaz-po: quando o requisito os trouxer, saem daqui
# e entram por cópia em usuarios.feature ou modulos.feature.
Funcionalidade: Contrato do Usuários · card, ao vivo, preferências e relógio de teste
  2_Diagramas/usuarios-contrato-api_v15_20261109-0912.svg · respostas sem cenário no requisito

  @contrato-v15 @RN-189
  Esquema do Cenário: card sem sessão válida
    Quando envia GET /usuarios/card <credencial>
    Então a resposta é 401 SESSAO_TERMINADA

    Exemplos:
      | credencial                      |
      | sem token                       |
      | com um token inválido           |
      | com a sessão da Maria terminada |

  @contrato-v15 @RN-280
  Esquema do Cenário: nomes do apoio e da lista, nesta ordem
    Quando <quem> pede o card com ao menos um usuário criado
    Então a normal traz no apoio <apoio>
    E a expandida traz na lista <lista>

    Exemplos:
      | quem                      | apoio                                    | lista                                                       |
      | o administrador do deboaz | "instituições", "autônomos", "suspensas" | "Master sem acesso há 30 dias", "Desativadas", "Encerradas" |
      | a conta master da EBFGA   | "staff", "participantes", "pendentes"    | "Sem acesso há 30 dias", "Desativados", "Códigos vencidos"  |
      | a staff Maria             | "staff", "participantes", "pendentes"    | "Sem acesso há 30 dias", "Desativados", "Códigos vencidos"  |
      | o educador autônomo Pedro | "criados no mês", "pendentes"            | "Sem acesso há 30 dias", "Desativados", "Códigos vencidos"  |

  @contrato-v15 @RN-281
  Esquema do Cenário: ao vivo sem sessão válida
    Quando envia GET /usuarios/card/ao-vivo?intervalo=30 <credencial>
    Então a resposta é 401 SESSAO_TERMINADA e nenhuma conexão fica aberta

    Exemplos:
      | credencial            |
      | sem token             |
      | com um token inválido |

  @contrato-v15 @RN-279
  Cenário: fluxo ao vivo sem cache
    Dado a conta master da EBFGA com o token de acesso
    Quando envia GET /usuarios/card/ao-vivo?intervalo=30 com Accept "text/event-stream"
    Então a resposta é 200 e a conexão fica aberta
    E o cabeçalho Cache-Control traz "no-store"

  @contrato-v15 @RN-279
  Cenário: card só quando a versao muda
    Quando a conta master envia GET /usuarios/card/ao-vivo ?intervalo=5
    Então depois do primeiro "card", nenhum outro chega em 3 intervalos sem mudança da versao

  @contrato-v15 @RN-283
  Esquema do Cenário: preferências sem sessão válida
    Quando envia <metodo> /usuarios/eu/preferencias <credencial>
    Então a resposta é 401 SESSAO_TERMINADA

    Exemplos:
      | metodo | credencial                      |
      | GET    | sem token                       |
      | PUT    | sem token                       |
      | GET    | com um token inválido           |
      | PUT    | com a sessão da Maria terminada |

  @contrato-v15 @RN-283
  Cenário: PUT sem o corpo inteiro
    Quando a conta master envia PUT /usuarios/eu/preferencias só com atualizacaoCards "AO_VIVO"
    Então a resposta é 422 DADOS_INVALIDOS com o campo "intervaloAoVivo"
    E a mensagem diz que o campo "intervaloAoVivo" está faltando ou é inválido

  @contrato-v15 @RN-278
  Cenário: relógio de teste muda a hora da aplicação e volta ao real
    Quando o relógio de teste vai para "2027-02-28T23:50:00-03:00"
    E a conta master cria um participante
    Então o card traz atualizadoEm perto de "2027-03-01T02:50:00Z"
    Quando o relógio de teste volta ao real
    Então a resposta é 204
    E depois de outro participante, o card traz atualizadoEm perto da hora real

  @contrato-v15 @RN-278
  Esquema do Cenário: endereços de teste não existem fora do teste
    Dado a aplicação iniciada em <ambiente>, com Teste__Relogio <flag>
    Quando envia <pedido> a ela
    Então ela responde 404

    Exemplos:
      | ambiente    | flag    | pedido                  |
      | produção    | "sim"   | PUT /_teste/relogio     |
      | produção    | "sim"   | POST /_teste/fechamento |
      | homologação | ausente | PUT /_teste/relogio     |
      | homologação | ausente | POST /_teste/fechamento |
