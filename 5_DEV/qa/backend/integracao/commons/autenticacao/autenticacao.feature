# language: pt
# Cenários de API de 1_Requisitos/17_modulo-usuario-a-autenticacao.md (v26), copiados do requisito:
# mudou lá, muda aqui. Cada cenário leva a tag do ID que cobre.
Funcionalidade: Autenticação
  1_Requisitos/17_modulo-usuario-a-autenticacao.md · Cenários de API

  @formas-de-entrar
  Esquema do Cenário: um endereço de entrada por frente
    Quando <quem> envia POST <endereço> com <credencial>
    Então a resposta é 200 com tokenAcesso, tipo "Bearer" e expiraEm 900

    Exemplos:
      | quem                          | endereço           | credencial                |
      | conta master, educador, staff | /auth/hub/entrar   | entrada, senha e aparelho |
      | membro do time deboaz         | /auth/hub/entrar   | entrada, senha e aparelho |
      | participante ou responsável   | /auth/quest/entrar | código, aparelho e quem   |
      | pessoa sem código de acesso   | /auth/sala/entrar  | código da sala            |

  @RF-134
  Cenário: staff entra pelo navegador
    Dado a staff "maria.ebfga@deboaz.com", ativa, da escola "EBFGA", ativa
    Quando envia POST /auth/hub/entrar com:
      """
      {
        "entrada": "maria.ebfga@deboaz.com",
        "senha": "••••••••",
        "aparelho": "NAVEGADOR"
      }
      """
    Então a resposta é 200 com tokenAcesso, tipo "Bearer" e expiraEm 900
    E vem o cookie deboaz_renovacao, HttpOnly, Secure e SameSite=Strict, só para /auth/renovar

  @RF-134
  Cenário: staff entra pelo app
    Quando envia o mesmo pedido com o aparelho "APP"
    Então a resposta é 200 com o tokenRenovacao no corpo, sem cookie

  @RF-135
  Cenário: a mãe entra com o código do filho
    Quando envia POST /auth/quest/entrar com:
      """
      {
        "codigo": "EBFGA-LUCAS-0412",
        "aparelho": "APP",
        "quem": {
          "tipo": "RESPONSAVEL",
          "parentesco": "Mãe"
        }
      }
      """
    Então a resposta é 200 com o token de acesso do Lucas
    E o token traz em quem o tipo "RESPONSAVEL" e o parentesco "Mãe"

  @RF-136
  Cenário: entra e a sala fecha
    Quando envia POST /auth/sala/entrar com a sala "482107", aberta
    Então a resposta é 200 com um token de papel "ANONIMO", só com o módulo e o id da sala
    E quando o professor fecha a sala, esse token recebe 401 em qualquer pedido

  @RF-136
  Cenário: sala fechada
    Quando envia POST /auth/sala/entrar com uma sala fechada ou inexistente
    Então a resposta é 404 SALA_FECHADA

  @RF-137
  Cenário: renovação no navegador
    Dado a sessão 3f9c2a71 da Maria ativa e o cookie deboaz_renovacao dela
    Quando envia POST /auth/renovar com o cookie
    Então a resposta é 200 com um tokenAcesso novo e um cookie de renovação novo

  @RF-137
  Cenário: renovação no app
    Quando envia POST /auth/renovar com:
      """
      {
        "tokenRenovacao": "3f9c2a71.Qm9h…"
      }
      """
    Então a resposta é 200 com tokenAcesso e tokenRenovacao novos

  @RF-137
  Cenário: tempo sem uso passou
    Dado a sessão da Maria sem pedidos há 9 h, com o tempo de 8 h da escola
    Quando envia POST /auth/renovar
    Então a resposta é 401 SESSAO_TERMINADA com "Sua sessão terminou. Entre de novo."

  @RF-138
  Cenário: sair do navegador
    Dado a Maria com sessões no navegador e no app
    Quando envia POST /auth/sair com o token do navegador
    Então a resposta é 204 e o cookie de renovação é apagado
    E o token do navegador recebe 401 no próximo pedido, e o do app continua valendo

  @RF-139
  Esquema do Cenário: mesma resposta para senha errada e entrada inexistente
    Quando envia POST /auth/hub/entrar com a entrada <entrada> e <senha>
    Então a resposta é 401 ENTRADA_INVALIDA com "Entrada ou senha incorretas."
    E uma tentativa errada é contada para <entrada>

    Exemplos:
      | entrada                  | senha            |
      | "maria.ebfga@deboaz.com" | uma senha errada |
      | "zeca.ebfga@deboaz.com"  | qualquer senha   |

  @RF-140
  Esquema do Cenário: conta ou usuário fora de uso
    Dado <situação>
    Quando a Maria envia POST /auth/hub/entrar com a entrada e a senha certas
    Então a resposta é 403 ACESSO_INDISPONIVEL e nenhuma sessão é aberta

    Exemplos:
      | situação                  |
      | a escola "EBFGA" suspensa |
      | a Maria desativada        |

  @RF-158
  Cenário: quinta senha errada
    Dado 4 senhas erradas seguidas para "maria.ebfga@deboaz.com"
    Quando a quinta senha errada chega às 09:00
    Então a resposta é 429 ENTRADA_BLOQUEADA com:
      """
      {
        "tentarEm": "2026-11-01T09:15:00Z"
      }
      """
    E até as 09:15 a senha certa também recebe 429

  @RF-141
  Cenário: staff desativado
    Dado a Maria com sessões no navegador e no app
    Quando a conta master envia POST /usuarios/usr-ebfga-maria/desativar
    Então os tokens das duas sessões recebem 401 no próximo pedido
    E POST /auth/renovar de cada aparelho recebe 401 SESSAO_TERMINADA

  @RF-143
  Cenário: comprovante da senha anotada
    Dado o comprovante "c7e1…" recebido junto com a senha gerada
    Quando envia POST /auth/hub/confirmar-senha com:
      """
      {
        "comprovante": "c7e1…"
      }
      """
    Então a resposta é 200 com o token de acesso e a renovação
    E o mesmo comprovante, enviado de novo, recebe 401 COMPROVANTE_INVALIDO

  @RF-156
  Cenário: lista das sessões
    Dado a Maria com sessões no navegador e no app
    Quando envia GET /auth/sessoes com o token do navegador
    Então a resposta é 200 com:
      """
      [
        {
          "sid": "3f9c2a71",
          "aparelho": "NAVEGADOR",
          "ultimaAcao": "2026-11-01T09:42:00Z",
          "esteAparelho": true
        },
        {
          "sid": "7d20b9e4",
          "aparelho": "APP",
          "ultimaAcao": "2026-11-01T08:10:00Z",
          "esteAparelho": false
        }
      ]
      """

  @RF-157
  Cenário: sair de um aparelho
    Quando a Maria envia DELETE /auth/sessoes/3f9c2a71 com o token do app
    Então a resposta é 204
    E o token do navegador recebe 401 no próximo pedido, e o do app continua valendo

  @RF-157
  Cenário: sessão de outra pessoa
    Quando a Maria envia DELETE /auth/sessoes/{sid} com o sid de uma sessão do João
    Então a resposta é 404 SESSAO_NAO_ENCONTRADA

  @RF-157
  Cenário: sair de todos
    Quando a Maria envia DELETE /auth/sessoes
    Então a resposta é 204
    E os tokens e as renovações de todas as sessões dela recebem 401

  @RN-224
  Cenário: a sessão vale mais que o token
    Dado um token de acesso da Maria emitido há 5 min
    E a sessão desse token encerrada no servidor
    Quando envia qualquer pedido com esse token
    Então a resposta é 401

  @RN-225
  Cenário: navegador e app
    Dado a Maria com uma sessão no navegador
    Quando envia POST /auth/hub/entrar com o aparelho "APP"
    Então GET /auth/sessoes lista duas sessões, uma por aparelho
    E cada sessão guarda a conta, a frente, o aparelho, quem entrou, o pv e a última ação

  @RN-226
  Esquema do Cenário: tempo sem uso de cada pessoa
    Dado <quem> com uma sessão no <aparelho>
    Quando passa sem pedidos mais que o tempo <tempo>
    Então o próximo pedido recebe 401 e POST /auth/renovar recebe 401 SESSAO_TERMINADA

    Exemplos:
      | quem           | aparelho  | tempo                 |
      | staff          | navegador | do staff no navegador |
      | staff          | app       | do staff no app       |
      | participante   | app       | do participante       |
      | conta master   | navegador | das contas master     |
      | membro do time | navegador | dos membros do time   |

  @RN-226
  Cenário: cada pedido conta como uso
    Dado a Maria no navegador, com o tempo de 8 h da escola
    Quando ela faz um pedido a cada 7 h
    Então a sessão continua e POST /auth/renovar recebe 200

  @RN-227
  Esquema do Cenário: token da outra frente
    Dado um token de acesso de aud "<aud>"
    Quando é usado num endereço do <frente>
    Então a resposta é 401

    Exemplos:
      | aud   | frente |
      | hub   | quest  |
      | quest | hub    |

  @RN-228
  Cenário: a mãe entra como o filho
    Quando a Carla envia POST /auth/quest/entrar com o código do Lucas e o parentesco "Mãe"
    Então o token tem o sub "usr-ebfga-lucas" e, em quem, o responsável e o parentesco "Mãe"
    E ela faz pela API o que o Lucas faz, menos o que cada módulo restringir ao responsável

  @RN-229
  Cenário: tokens da sessão
    Quando a Maria envia POST /auth/hub/entrar
    Então recebe um token de acesso com expiraEm 900, que não fica guardado no servidor
    E um token de renovação de uso único, que vale enquanto a sessão estiver ativa

  @RN-230
  Cenário: desativar encerra
    Dado a Maria com uma sessão no navegador
    Quando a conta master envia POST /usuarios/usr-ebfga-maria/desativar
    Então POST /auth/renovar da Maria recebe 401 SESSAO_TERMINADA

  @RN-230
  Cenário: mudar permissão não encerra
    Dado a Maria com uma sessão no navegador
    Quando a conta master envia PUT /usuarios/usr-ebfga-maria/permissoes
    Então o token antigo recebe 401 PERMISSOES_DESATUALIZADAS
    E POST /auth/renovar da Maria recebe 200 com um token de pv novo

  @RN-231
  Cenário: sala aberta sem tempo sem uso
    Dado um token de papel "ANONIMO" da sala 482107
    Quando passa 2 h sem pedidos, com a sala aberta
    Então os pedidos à sala continuam com 200
    E os pedidos a perfil, tarefa ou outra sala recebem 403

  @RN-247
  Cenário: sexta sessão do hub
    Dado a Maria com 5 sessões do hub abertas
    Quando envia POST /auth/hub/entrar de um sexto aparelho
    Então a resposta é 200 e a sessão de última ação mais antiga termina
    E POST /auth/renovar daquela sessão recebe 401 SESSAO_TERMINADA

  @RN-247
  Cenário: quest sem limite
    Dado o código do Lucas usado em POST /auth/quest/entrar de 6 aparelhos
    Então as 6 sessões continuam e cada POST /auth/renovar recebe 200

  @RN-272
  Esquema do Cenário: execução que muda a sessão, auditada do começo ao fim
    Quando envia <método> /auth/<endereço>
    E a resposta é de sucesso
    Então "auditoria" ganha "INICIADO" e "CONCLUIDO" da ação "<acao>", com o mesmo traceId
    E o "CONCLUIDO" vai com quem "<quem>", contaId "conta-ebfga" e alvo "<alvo>"
    E a resposta traz o cabeçalho X-Trace-Id com esse traceId

    Exemplos:
      | método | endereço            | acao             | quem                    | alvo            |
      | POST   | hub/entrar          | ENTRAR_HUB       | usr-ebfga-maria         | 3f9c2a71        |
      | POST   | quest/entrar        | ENTRAR_QUEST     | usr-ebfga-lucas         | 7a20d5e4        |
      | POST   | sala/entrar         | ENTRAR_SALA      | anon-ebfga-482107-13465 | 482107          |
      | POST   | hub/confirmar-senha | CONFIRMAR_SENHA  | usr-ebfga-maria         | 3f9c2a71        |
      | POST   | renovar             | RENOVAR_SESSAO   | usr-ebfga-maria         | 3f9c2a71        |
      | POST   | sair                | SAIR             | usr-ebfga-maria         | 3f9c2a71        |
      | DELETE | sessoes/3f9c2a71    | ENCERRAR_SESSAO  | usr-ebfga-maria         | 3f9c2a71        |
      | DELETE | sessoes             | ENCERRAR_SESSOES | usr-ebfga-maria         | usr-ebfga-maria |

  @RN-272
  Esquema do Cenário: alvo conhecido desde o começo
    Quando a Maria envia <método> /auth/<endereço> com o token da sessão 3f9c2a71
    Então o "INICIADO" da ação "<acao>" já vai com a Maria, a "conta-ebfga" e o alvo "<alvo>"
    E o "CONCLUIDO", com o mesmo traceId, repete quem, contaId e alvo

    Exemplos:
      | método | endereço         | acao             | alvo            |
      | POST   | sair             | SAIR             | 3f9c2a71        |
      | DELETE | sessoes/9d41b0c3 | ENCERRAR_SESSAO  | 9d41b0c3        |
      | DELETE | sessoes          | ENCERRAR_SESSOES | usr-ebfga-maria |

  @RN-272
  Cenário: quem ainda não foi identificado
    Quando a Maria envia POST /auth/hub/entrar com a entrada e a senha certas
    Então o registro "INICIADO" da ação "ENTRAR_HUB" vai com quem, contaId e alvo null
    E só o "CONCLUIDO", depois da senha conferida, vai com a Maria, a conta e a sessão

  @RN-273
  Esquema do Cenário: recusa vira alerta ou erro
    Quando chega <pedido>
    Então a resposta é <resposta>, com o cabeçalho X-Trace-Id
    E "auditoria" fica só com o "INICIADO" da ação "<acao>", com esse traceId
    E "auditoriaErros" ganha o código da resposta e o nivel "<nivel>", com esse traceId
    E quem e contaId vão null, porque a pessoa não foi identificada

    Exemplos:
      | pedido                               | resposta                 | acao            | nivel  |
      | senha errada em /auth/hub/entrar     | 401 ENTRADA_INVALIDA     | ENTRAR_HUB      | ALERTA |
      | 5ª senha errada em /auth/hub/entrar  | 429 ENTRADA_BLOQUEADA    | ENTRAR_HUB      | ALERTA |
      | código errado em /auth/quest/entrar  | 401 CODIGO_INVALIDO      | ENTRAR_QUEST    | ALERTA |
      | comprovante repetido                 | 401 COMPROVANTE_INVALIDO | CONFIRMAR_SENHA | ALERTA |
      | sessão sem uso há 9 h renovada       | 401 SESSAO_TERMINADA     | RENOVAR_SESSAO  | ALERTA |
      | entrada na sala fechada              | 404 SALA_FECHADA         | ENTRAR_SALA     | ALERTA |
      | Redis fora do ar em /auth/hub/entrar | 500 ERRO_INESPERADO      | ENTRAR_HUB      | ERRO   |

  @RN-273
  Esquema do Cenário: recusa depois de identificar a pessoa
    Dado <situação>
    Quando chega <método> /auth/<endereço>
    Então a resposta é <resposta>, com o cabeçalho X-Trace-Id
    E "auditoriaErros" ganha o código da resposta e o nivel "ALERTA", com esse traceId
    E esse registro vai com quem "<quem>" e contaId "conta-ebfga"

    Exemplos:
      | situação       | método | endereço            | resposta                  | quem            |
      | EBFGA suspensa | POST   | hub/entrar          | 403 ACESSO_INDISPONIVEL   | usr-ebfga-maria |
      | EBFGA suspensa | POST   | quest/entrar        | 403 ACESSO_INDISPONIVEL   | usr-ebfga-lucas |
      | EBFGA suspensa | POST   | hub/confirmar-senha | 403 ACESSO_INDISPONIVEL   | usr-ebfga-maria |
      | sessão do João | DELETE | sessoes/9d41b0c3    | 404 SESSAO_NAO_ENCONTRADA | usr-ebfga-maria |

  @RN-273
  Esquema do Cenário: token inválido recusado antes do caso de uso
    Quando chega <pedido> com um token de acesso vencido
    Então a resposta é 401, recusada antes do caso de uso
    E a resposta não traz o cabeçalho X-Trace-Id
    E "auditoria" e "auditoriaErros" não ganham registro, como o 422 da RN-271 do 17

    Exemplos:
      | pedido            |
      | POST /auth/sair   |
      | GET /auth/sessoes |

  @RN-273
  Esquema do Cenário: nenhum segredo na auditoria
    Quando envia <pedido>
    Então nenhum registro de "auditoria" nem de "auditoriaErros" traz <segredo>

    Exemplos:
      | pedido                           | segredo                                    |
      | POST /auth/hub/entrar da Maria   | a senha nem o token de acesso              |
      | POST /auth/quest/entrar da Carla | o código "EBFGA-LUCAS-0412"                |
      | POST /auth/hub/confirmar-senha   | o comprovante "c7e1…"                      |
      | POST /auth/renovar da Maria      | o tokenRenovacao nem o cookie de renovação |

  @RNF-207
  Cenário: assinatura RS256
    Quando a Maria envia POST /auth/hub/entrar
    Então o token de acesso é um JWT assinado com RS256, com o kid no cabeçalho
    E a chave RSA de 2048 bits que assina nunca sai do Key Vault
    E GET /auth/chaves devolve a chave pública desse kid, sem autenticação

  @RNF-209
  Cenário: renovação dentro do tempo da sessão
    Dado a Maria no hub, com o tempo de 8 h da escola
    Quando o token de acesso dela passa de 15 min
    Então POST /auth/renovar devolve um token novo
    E a renovação funciona enquanto ela não passar 8 h sem pedidos

  @RNF-203
  Esquema do Cenário: mudança que invalida o token
    Dado a Maria com sessões no navegador e no app
    Quando <mudança>
    Então o token emitido antes recebe 401 no próximo pedido, nos dois aparelhos

    Exemplos:
      | mudança                                       |
      | a conta master desativa a Maria               |
      | o time deboaz suspende a escola               |
      | a conta master pede uma nova geração de senha |
      | a conta master muda as permissões dela        |
      | a escola descontrata um módulo que ela usa    |

  @RNF-204
  Esquema do Cenário: tempo do bloqueio
    Dado <bloqueios> bloqueios seguidos para "maria.ebfga@deboaz.com"
    Quando a quinta senha errada seguida chega a POST /auth/hub/entrar
    Então a resposta é 429 ENTRADA_BLOQUEADA, com tentarEm daqui a <tempo>

    Exemplos:
      | bloqueios | tempo  |
      | 0         | 15 min |
      | 1         | 30 min |
      | 2         | 1 h    |
      | 7         | 24 h   |

  @RNF-204
  Cenário: código de acesso também bloqueia
    Quando o código "EBFGA-LUCAS-0412" chega errado 5 vezes seguidas a POST /auth/quest/entrar
    Então a resposta é 429 ENTRADA_BLOQUEADA, com tentarEm daqui a 15 min

  @RNF-213
  Cenário: troca a cada uso
    Dado a Maria com o token de renovação "3f9c2a71.A"
    Quando envia POST /auth/renovar com "3f9c2a71.A"
    Então recebe "3f9c2a71.B", o id da sessão seguido de 256 bits aleatórios
    E "3f9c2a71.A" deixa de valer
    E a sessão guarda só o resumo irreversível do token novo

  @RNF-213
  Cenário: renovação copiada
    Dado o token de renovação "3f9c2a71.A" já trocado por "3f9c2a71.B"
    Quando alguém envia POST /auth/renovar com "3f9c2a71.A"
    Então a resposta é 401 SESSAO_TERMINADA e a sessão 3f9c2a71 termina

  @RNF-215
  Cenário: token da Maria no navegador
    Quando a Maria envia POST /auth/hub/entrar com o aparelho "NAVEGADOR"
    Então o token de acesso traz esta identidade, sem nome, documento, matrícula nem senha:
      """
      {
        "alg": "RS256",
        "typ": "JWT",
        "kid": "deboaz-2026-11"
      }
      {
        "iss": "deboaz",
        "aud": "hub",
        "sub": "usr-ebfga-maria",
        "sid": "3f9c2a71",
        "conta": "conta-ebfga",
        "sigla": "EBFGA",
        "tipo": "STAFF",
        "aparelho": "NAVEGADOR",
        "iat": 1793262120,
        "exp": 1793263020,
        "jti": "a81c0e5d",
        "papel": "STAFF",
        "mods": {
          "MOD-03": "CU",
          "MOD-04": "U",
          "MOD-05": "U"
        },
        "pv": 7
      }
      """
    E papel, mods e pv são os da autorização

  @RNF-215
  Cenário: quem entrou no quest
    Quando a Carla envia POST /auth/quest/entrar com o código do Lucas, como mãe
    Então o token traz em quem o responsável e o parentesco "Mãe"

  @RNF-216
  Esquema do Cenário: token recusado
    Dado a Maria com um token <problema>
    Quando envia um pedido com esse token
    Então a resposta é 401

    Exemplos:
      | problema                          |
      | com a assinatura errada           |
      | vencido                           |
      | do quest                          |
      | de uma sessão encerrada           |
      | com a versão de permissões antiga |

  @RNF-217
  Cenário: sessão da Maria
    Quando a Maria entra no hub pelo navegador, com o tempo de 8 h da escola
    Então a sessão fica no Redis na chave sessao:3f9c2a71, expirando em 8 h e refeita a cada pedido:
      """
      {
        "usuarioId": "usr-ebfga-maria",
        "contaId": "conta-ebfga",
        "sigla": "EBFGA",
        "frente": "HUB",
        "aparelho": "NAVEGADOR",
        "quemEntrou": null,
        "versaoPermissoes": 7,
        "resumoRenovacao": "(resumo irreversível do token de renovação)",
        "criadaEm": "2026-11-01T09:00:00Z",
        "ultimaAcao": "2026-11-01T09:42:00Z"
      }
      """
    E o índice sessoes:usr-ebfga-maria lista as sessões dela, para encerrar todas de uma vez

  @RNF-217
  Cenário: tentativas erradas
    Quando alguém erra a senha de "maria.ebfga@deboaz.com"
    Então a tentativa e o bloqueio ficam no Redis, com expiração igual ao tempo do bloqueio

  @RNF-218
  Cenário: troca a cada 90 dias
    Dado tokens assinados com a chave "deboaz-2026-11"
    Quando a chave é trocada por "deboaz-2027-02" depois de 90 dias
    Então os tokens novos levam o kid "deboaz-2027-02" e GET /auth/chaves traz as duas
    E os assinados com "deboaz-2026-11" continuam aceitos até vencer, sem encerrar sessões

  @RNF-218
  Cenário: troca por vazamento
    Dado a suspeita de vazamento da chave "deboaz-2026-11"
    Quando a chave é trocada na hora
    Então os tokens assinados com "deboaz-2026-11" recebem 401
    E POST /auth/renovar devolve tokens assinados com a chave nova

  @fora-de-escopo
  Cenário: sem provedor externo nem segundo fator
    Quando a Maria entra no hub
    Então entra só com a entrada e a senha, sem provedor externo nem segundo fator

  @fora-de-escopo
  Cenário: sem biometria no app
    Quando a Maria abre o app de novo, com a sessão ativa
    Então continua conectada pelo token de renovação guardado no aparelho, sem biometria

  @fora-de-escopo
  Cenário: sem aparelhos conectados no quest
    Quando o Lucas abre Perfil no quest
    Então não há a lista de aparelhos conectados nem "Sair de todos os aparelhos"
