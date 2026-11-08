# language: pt
# Cenários de API de 1_Requisitos/17_modulo-usuario.md (v74), copiados do requisito:
# mudou lá, muda aqui. Cada cenário leva a tag do ID que cobre.
Funcionalidade: Usuários
  1_Requisitos/17_modulo-usuario.md · Cenários de API

  @RF-104
  Cenário: escola cria a conta
    Quando envia POST /contas, sem login, com:
      """
      {
        "tipo": "INSTITUICAO",
        "nome": "Escola Bento Freitas",
        "documento": "12.345.678/0001-90",
        "responsavel": "Gustavo Boaz de Moura Souza",
        "telefone": "(51) 99999-0000",
        "dominio": "objetivo_unidade1",
        "sigla": "EBFGA"
      }
      """
    Então a resposta é 201 com:
      """
      {
        "contaId": "conta-ebfga",
        "login": "objetivo_unidade1@deboaz.com",
        "senha": "Gu#Boaz507318",
        "comprovante": "c7e1…"
      }
      """
    E POST /auth/hub/confirmar-senha com o comprovante abre a sessão da conta master

  @RF-104
  Cenário: sigla já usada
    Dado uma conta com a sigla "EBFGA"
    Quando envia POST /contas com a sigla "EBFGA"
    Então a resposta é 409 SIGLA_EM_USO e nenhuma conta é criada

  @RF-104
  Cenário: domínio já usado
    Quando envia POST /contas com o domínio "objetivo_unidade1", já usado
    Então a resposta é 409 DOMINIO_EM_USO

  @RF-106
  Cenário: secretária cadastrada pela escola
    Dado o token da conta master da EBFGA, com "MOD-03" com "CU"
    Quando envia POST /usuarios com:
      """
      {
        "tipo": "STAFF",
        "nome": "Maria Souza",
        "nomeCurto": "maria",
        "matricula": "20260087"
      }
      """
    Então a resposta é 201 com:
      """
      {
        "id": "usr-ebfga-maria",
        "login": "maria.ebfga@deboaz.com",
        "estado": "PENDENTE",
        "codigoLiberacao": "482913",
        "venceEm": "2026-11-04T22:06:00Z"
      }
      """

  @RF-106
  Cenário: educador autônomo não cria staff
    Dado o token do educador autônomo Pedro
    Quando envia POST /usuarios com o tipo "STAFF"
    Então a resposta é 403 ACESSO_NEGADO

  @RF-106
  Cenário: nome curto repetido
    Quando a conta master envia POST /usuarios com o nome curto "maria", já usado na EBFGA
    Então a resposta é 409 NOME_CURTO_EM_USO

  @RF-108
  Cenário: aluno novo com dois responsáveis
    Dado o token do staff João, com "MOD-03" com "U"
    Quando envia POST /usuarios com:
      """
      {
        "tipo": "PARTICIPANTE",
        "nome": "Lucas Andrade",
        "nomeCurto": "lucas",
        "matricula": null,
        "responsaveis": [
          {
            "nome": "Carla Andrade",
            "parentesco": "MAE"
          },
          {
            "nome": "Paulo Andrade",
            "parentesco": "PAI"
          }
        ]
      }
      """
    Então a resposta é 201 com o login "EBFGA-LUCAS-" seguido de 4 números, sem senha
    E POST /auth/quest/entrar com esse código abre o perfil do Lucas

  @RF-109
  Cenário: primeiro acesso do staff
    Dado o staff "gustavo.ebfga@deboaz.com" pendente, com o código de liberação "482913"
    Quando envia POST /usuarios/gerar-senha, sem login, com:
      """
      {
        "entrada": "gustavo.ebfga@deboaz.com",
        "codigoLiberacao": "482913"
      }
      """
    Então a resposta é 200 com:
      """
      {
        "senha": "Gu#Boaz507318",
        "comprovante": "c7e1…"
      }
      """
    E o mesmo pedido, enviado de novo, recebe 401 CODIGO_INVALIDO

  @RF-109
  Cenário: código errado
    Quando envia POST /usuarios/gerar-senha com o código "111111"
    Então a resposta é 401 CODIGO_INVALIDO, sem a senha, e conta uma tentativa errada

  @RF-109
  Cenário: quinto código errado
    Dado 4 códigos errados seguidos para "gustavo.ebfga@deboaz.com"
    Quando o quinto chega a POST /usuarios/gerar-senha
    Então a resposta é 429 ENTRADA_BLOQUEADA com tentarEm

  @RF-110
  Cenário: professor esqueceu a senha
    Dado o token do staff João, com "MOD-03" com "U", superior do Paulo
    Quando envia POST /usuarios/usr-ebfga-paulo/nova-geracao-senha
    Então a resposta é 201 com codigoLiberacao e venceEm
    E POST /auth/hub/entrar com a senha antiga do Paulo recebe 401 ENTRADA_INVALIDA
    E os tokens das sessões abertas do Paulo recebem 401

  @RF-110
  Cenário: par não pede a geração
    Quando o João envia POST /usuarios/usr-ebfga-rui/nova-geracao-senha, de um par dele
    Então a resposta é 403 ACESSO_NEGADO

  @RF-110
  Cenário: participante não tem senha
    Quando a conta master envia POST /usuarios/usr-ebfga-lucas/nova-geracao-senha
    Então a resposta é 422 SEM_SENHA

  @RF-111
  Cenário: conta master esqueceu a senha
    Quando envia POST /contas/esqueci-senha-master, sem login, com:
      """
      {
        "instituicao": "Escola Bento Freitas",
        "contato": "(51) 99999-0000"
      }
      """
    Então a resposta é 202 e a mensagem entra no SAC do time deboaz

  @RF-112
  Cenário: professor que saiu da escola
    Dado o Paulo com uma sessão no app
    Quando a conta master envia POST /usuarios/usr-ebfga-paulo/desativar
    Então a resposta é 204
    E o token do app do Paulo recebe 401 no próximo pedido
    E GET /usuarios?tipo=STAFF&estado=DESATIVADO traz o Paulo

  @RF-112
  Cenário: reativar
    Quando a conta master envia POST /usuarios/usr-ebfga-paulo/reativar
    Então a resposta é 204 e o Paulo volta a ATIVO com as mesmas permissões

  @RF-112
  Cenário: já desativado
    Quando a conta master envia POST /usuarios/usr-ebfga-paulo/desativar de novo
    Então a resposta é 409 ESTADO_IGUAL

  @RF-113
  Cenário: falta de pagamento
    Dado o token do membro do time Caio, com "MOD-03" com "U"
    Quando envia POST /contas/conta-ebfga/estado com:
      """
      {
        "estado": "DESATIVADA"
      }
      """
    Então a resposta é 200 com o estado "DESATIVADA"
    E POST /auth/hub/entrar da Maria e POST /auth/quest/entrar do Lucas recebem 403
    E GET /contas?estado=DESATIVADA traz a EBFGA

  @RF-113
  Cenário: pagamento regularizado
    Quando o Caio envia POST /contas/conta-ebfga/estado com o estado "ATIVA"
    Então a resposta é 200 e a Maria e o Lucas voltam a entrar com as mesmas permissões

  @RF-114
  Cenário: ler e salvar o tempo da escola
    Dado o token da conta master da EBFGA, com "MOD-03" com "CU"
    Quando envia PUT /contas/conta-ebfga/tempo-sessao com:
      """
      {
        "staffNavegador": "PT4H",
        "staffApp": "P7D",
        "participante": "P30D"
      }
      """
    Então a resposta é 200 com o mesmo corpo
    E GET /contas/conta-ebfga/tempo-sessao devolve "PT4H" no staff do navegador

  @RF-114
  Cenário: sem a aba Configuração
    Quando a Maria, com "MOD-03" só com "U", envia GET /contas/conta-ebfga/tempo-sessao
    Então a resposta é 403 ACESSO_NEGADO

  @RF-152
  Cenário: time deboaz muda o tempo da conta master no navegador
    Dado o token do administrador do deboaz, com "MOD-03" com "CU"
    Quando envia PUT /time/tempo-sessao com:
      """
      {
        "membrosNavegador": "PT8H",
        "membrosApp": "PT8H",
        "mastersNavegador": "PT4H",
        "mastersApp": "PT8H"
      }
      """
    Então a resposta é 200 com o mesmo corpo

  @RF-152
  Cenário: valor fora dos limites
    Quando envia PUT /time/tempo-sessao com "mastersNavegador" igual a "PT30H"
    Então a resposta é 422 FORA_DOS_LIMITES com:
      """
      {
        "campo": "mastersNavegador",
        "minimo": "PT15M",
        "maximo": "PT24H"
      }
      """
    E GET /time/tempo-sessao continua com "PT4H"

  @RF-153
  Cenário: nome curto do staff
    Dado a Maria com uma sessão no navegador
    Quando a conta master envia PATCH /usuarios/usr-ebfga-maria com:
      """
      {
        "nomeCurto": "mariasouza"
      }
      """
    Então a resposta é 200 com o login "mariasouza.ebfga@deboaz.com"
    E o token do navegador da Maria recebe 401 no próximo pedido
    E POST /auth/hub/entrar com o login novo e a mesma senha recebe 200

  @RF-153
  Cenário: responsável novo
    Quando a conta master envia PATCH /usuarios/usr-ebfga-lucas com a mãe e o pai em responsaveis
    Então GET /usuarios/usr-ebfga-lucas traz os dois responsáveis e o mesmo login

  @RF-153
  Cenário: nome curto repetido · RF-153
    Quando a conta master envia PATCH /usuarios/usr-ebfga-ana com o nome curto "maria"
    Então a resposta é 409 NOME_CURTO_EM_USO e nada muda

  @RF-154
  Cenário: família perdeu o código
    Dado a Carla com uma sessão do quest pelo código "EBFGA-LUCAS-0412"
    Quando a conta master envia POST /usuarios/usr-ebfga-lucas/novo-codigo-acesso
    Então a resposta é 200 com o login "EBFGA-LUCAS-" seguido de 4 números novos
    E o token da Carla recebe 401 e POST /auth/quest/entrar com o código antigo recebe 401

  @RF-154
  Cenário: só participante
    Quando a conta master envia POST /usuarios/usr-ebfga-maria/novo-codigo-acesso
    Então a resposta é 422 SO_PARTICIPANTE

  @RF-155
  Cenário: membro só com o SAC
    Dado o membro do time Caio Lima, sem módulos
    Quando o administrador envia PUT /usuarios/usr-dboaz-caio/permissoes com "MOD-02" com "U"
    Então a resposta é 200
    E POST /auth/renovar do Caio devolve um token só com "MOD-02" com "U"

  @RN-163
  Esquema do Cenário: quem é usuário
    Quando <quem> entra
    Então <é usuário>

    Exemplos:
      | quem                            | é usuário                                     |
      | a conta master da EBFGA         | GET /usuarios/{id} traz o tipo "MASTER"       |
      | o educador autônomo Pedro       | GET /usuarios/{id} traz o tipo "MASTER"       |
      | a staff Maria                   | GET /usuarios/{id} traz o tipo "STAFF"        |
      | o Lucas                         | GET /usuarios/{id} traz o tipo "PARTICIPANTE" |
      | o membro do time Caio           | GET /usuarios/{id} traz o tipo "MEMBRO_TIME"  |
      | a Carla, mãe do Lucas           | não é usuário: fica nos responsaveis do Lucas |
      | uma pessoa sem código numa sala | não é usuário: acesso anônimo                 |

  @RN-164
  Cenário: staff não se cadastra
    Quando envia POST /contas com o tipo "STAFF"
    Então a resposta é 422 DADOS_INVALIDOS com o campo "tipo"

  @RN-164
  Cenário: só o administrador cria membros
    Dado o token do membro do time Caio, com "MOD-03" com "U"
    Quando envia POST /usuarios com o tipo "MEMBRO_TIME"
    Então a resposta é 403 ACESSO_NEGADO

  @RN-165
  Cenário: autônomo só com participantes
    Dado o token do educador autônomo Pedro
    Quando envia GET /usuarios?tipo=STAFF
    Então a resposta é 403 ACESSO_NEGADO
    E GET /usuarios?tipo=PARTICIPANTE recebe 200

  @RN-166
  Cenário: descontratou e recontratou
    Dado o Paulo com "MOD-06" com "U"
    Quando a escola descontrata a Presença
    Então GET /usuarios/usr-ebfga-paulo/permissoes não traz "MOD-06" em mods
    E quando a escola recontrata a Presença, "MOD-06" volta com "U"

  @RN-166
  Cenário: salvar módulo não contratado
    Quando a conta master envia PUT /usuarios/usr-ebfga-paulo/permissoes com "MOD-12"
    Então a resposta é 422 MODULO_NAO_CONTRATADO

  @RN-167
  Cenário: conta master tem tudo
    Quando a conta master envia GET /usuarios/usr-ebfga-maria/permissoes
    Então concediveis traz todos os módulos contratados pela escola, com "CU"

  @RN-167
  Cenário: staff concede só o que tem
    Dado o João com "MOD-03" com "CU" e "MOD-05" com "U"
    Quando envia GET /usuarios/usr-ebfga-ana/permissoes
    Então concediveis traz só "MOD-03" com "CU" e "MOD-05" com "U"

  @RN-168
  Cenário: staff que administra usuários
    Dado a conta master tendo salvado "MOD-03" com "CU" para o João
    Quando o João envia POST /usuarios para criar a staff Ana
    Então a resposta é 201
    E PUT /usuarios/usr-ebfga-ana/permissoes do João aceita só as abas que ele tem

  @RN-169
  Esquema do Cenário: quem age sobre quem
    Quando <quem> envia POST /usuarios/{id}/desativar de <alvo>
    Então a resposta é <resposta>

    Exemplos:
      | quem                      | alvo                      | resposta          |
      | o administrador           | o membro Caio             | 204               |
      | a conta master            | o João, com Adm. usuários | 204               |
      | o João, com Adm. usuários | a staff Ana               | 204               |
      | o João, com Adm. usuários | o Lucas                   | 204               |
      | o João, com Adm. usuários | o Rui, também com Adm.    | 403 ACESSO_NEGADO |
      | o qaadmin                 | o admin                   | 403 ACESSO_NEGADO |
      | o admin                   | o qaadmin                 | 403 ACESSO_NEGADO |
      | a staff Ana               | o João                    | 403 ACESSO_NEGADO |

  @RN-169
  Esquema do Cenário: administrador sobre outro administrador
    Dado o token de "<quem>" em homologação
    Quando envia POST /usuarios/{id}/<ação> de "<alvo>"
    Então a resposta é 403 ACESSO_NEGADO
    E "<alvo>" continua com o mesmo estado e a mesma senha

    Exemplos:
      | quem                     | ação               | alvo                     |
      | qaadmin.dboaz@deboaz.com | reativar           | admin.dboaz@deboaz.com   |
      | qaadmin.dboaz@deboaz.com | nova-geracao-senha | admin.dboaz@deboaz.com   |
      | admin.dboaz@deboaz.com   | reativar           | qaadmin.dboaz@deboaz.com |
      | admin.dboaz@deboaz.com   | nova-geracao-senha | qaadmin.dboaz@deboaz.com |

  @RN-169
  Cenário: time deboaz e a conta master
    Quando o membro do time Caio envia POST /contas/conta-ebfga/codigo-master
    Então a nova geração da conta master da EBFGA segue a RN-172

  @RN-170
  Cenário: senha só para o próprio usuário
    Quando a conta master envia POST /usuarios para criar a Maria
    Então a resposta traz o código de liberação e nenhum campo de senha
    E GET /usuarios/usr-ebfga-maria não traz senha nem resumo da senha
    E a senha só sai em POST /usuarios/gerar-senha, para quem tem o código

  @RN-170
  Cenário: exceção do administrador
    Então a senha do administrador do deboaz fica no Key Vault, fora de qualquer endereço

  @RN-171
  Esquema do Cenário: senha gerada
    Quando POST /usuarios/gerar-senha gera a senha de "<nome>"
    Então a senha tem o formato de "<exemplo>"

    Exemplos:
      | nome                        | exemplo        |
      | Gustavo Boaz de Moura Souza | Gu#Boaz507318  |
      | Maria da Silva              | Ma!Silva204871 |
      | Xuxa                        | Xu@Xa182482    |

  @RN-171
  Cenário: o que muda a cada geração
    Quando duas gerações de senha saem para o Gustavo Boaz de Moura Souza
    Então as duas começam com "Gu" e têm "Boaz"
    E o caractere especial e os 6 números são sorteados de novo

  @RN-172
  Cenário: conferência pelo SAC
    Dado o token da Ana Lima, do time deboaz, com "MOD-03" com "U"
    Quando envia POST /contas/conta-ebfga/codigo-master com:
      """
      {
        "documento": "12.345.678/0001-90",
        "responsavel": "Gustavo Boaz de Moura Souza",
        "telefone": "(51) 99999-0000",
        "ultimoPagamento": null
      }
      """
    Então a resposta é 201 com:
      """
      {
        "codigoLiberacao": "482913",
        "contato": "(51) 99999-0000",
        "venceEm": "2026-11-04T22:06:00Z"
      }
      """

  @RN-172
  Cenário: conferência incompleta
    Quando envia o CNPJ e o telefone certos e a data do último pagamento errada
    Então a resposta é 422 CONFERENCIA_INCOMPLETA e nenhum código é gerado

  @RN-173
  Esquema do Cenário: estado da conta
    Quando o time deboaz envia POST /contas/conta-ebfga/estado com "<estado>"
    Então a resposta é 200 e o estado significa <motivo>

    Exemplos:
      | estado     | motivo                                        |
      | ATIVA      | uso normal                                    |
      | SUSPENSA   | decisão temporária do time deboaz             |
      | DESATIVADA | falta de pagamento, volta com a regularização |
      | ENCERRADA  | pedido do cliente                             |

  @RN-173
  Cenário: conta encerrada não volta
    Dado a conta da EBFGA encerrada
    Quando o time deboaz envia POST /contas/conta-ebfga/estado com "ATIVA"
    Então a resposta é 409 TRANSICAO_INVALIDA

  @RN-174
  Cenário: conta encerrada
    Quando o time deboaz envia POST /contas/conta-ebfga/estado com "ENCERRADA"
    Então os dados da escola são apagados em cascata
    E fica só o registro mínimo para histórico, auditoria e cobrança
    E esse registro é anonimizado depois de 5 anos

  @RN-174
  Cenário: usuário desativado continua na lista
    Quando a conta master envia POST /usuarios/usr-ebfga-paulo/desativar
    Então GET /usuarios/usr-ebfga-paulo continua com 200 e o estado "DESATIVADO"

  @RN-175
  Esquema do Cenário: quem não entra
    Dado <situação>
    Quando <quem> envia POST <endereço>
    Então a resposta é 403 ACESSO_INDISPONIVEL

    Exemplos:
      | situação           | quem                  | endereço           |
      | a EBFGA suspensa   | a staff Maria         | /auth/hub/entrar   |
      | a EBFGA desativada | o Lucas               | /auth/quest/entrar |
      | a EBFGA encerrada  | a Carla, mãe do Lucas | /auth/quest/entrar |
      | o Paulo desativado | o Paulo               | /auth/hub/entrar   |

  @RN-178
  Esquema do Cenário: execução auditada do começo ao fim
    Quando a Maria envia <pedido>
    E a resposta é de sucesso
    Então a coleção "auditoria" ganha 2 registros com o mesmo traceId
    E o primeiro com a etapa "INICIADO", o segundo com a etapa "CONCLUIDO"
    E os dois com a Maria, a ação "<acao>", o alvo e a hora, guardados até 5 anos depois

    Exemplos:
      | pedido                                          | acao               |
      | POST /usuarios do participante Lucas            | CRIAR_USUARIO      |
      | POST /usuarios/usr-ebfga-ana/nova-geracao-senha | NOVA_GERACAO_SENHA |
      | PUT /usuarios/usr-ebfga-joao/permissoes         | EDITAR_PERMISSOES  |
      | POST /usuarios/usr-ebfga-paulo/desativar        | DESATIVAR_USUARIO  |

  @RN-178
  Cenário: permissões auditadas nas duas coleções
    Quando a conta master envia PUT /usuarios/usr-ebfga-joao/permissoes
    E a resposta é de sucesso
    Então a coleção "auditoria" ganha "INICIADO" e "CONCLUIDO" da ação "EDITAR_PERMISSOES"
    E "registrosPermissao" ganha a versão nova das permissões do João, como na RN-256

  @RN-178
  Cenário: conta criada sem login
    Quando envia POST /contas, sem login, com a sigla "COLSA"
    E a resposta é 201 com o contaId "conta-colsa"
    Então a coleção "auditoria" ganha o registro "INICIADO" da ação "CRIAR_CONTA"
    E esse registro vai sem quem, sem contaId e sem alvo
    E o registro "CONCLUIDO", com o mesmo traceId, vai com contaId e alvo "conta-colsa", sem quem

  @RN-178
  Esquema do Cenário: leitura não é auditada
    Quando a Maria envia <pedido>
    Então a resposta é 200
    E as coleções "auditoria" e "auditoriaErros" não ganham registro

    Exemplos:
      | pedido                                                       |
      | GET /usuarios?tipo=PARTICIPANTE                              |
      | GET /usuarios/usr-ebfga-lucas                                |
      | GET /contas/sigla-sugerida?nome=Colégio Santo Antônio        |

  @RN-270
  Cenário: recusa no meio da execução
    Dado uma conta com a sigla "EBFGA"
    Quando envia POST /contas, sem login, com a sigla "EBFGA"
    E a resposta é 409 SIGLA_EM_USO
    Então a coleção "auditoria" fica só com o registro "INICIADO" da ação "CRIAR_CONTA"
    E a coleção "auditoriaErros" ganha 1 registro com o código "SIGLA_EM_USO" e o nivel "ALERTA"
    E os dois registros têm o mesmo traceId
    E nenhum registro "CONCLUIDO" é gravado com esse traceId

  @RN-270
  Esquema do Cenário: alerta ou erro, gravado antes da recusa
    Quando a Maria envia POST /usuarios/usr-ebfga-paulo/desativar
    E <situação>
    Então a resposta é <resposta>
    E "auditoriaErros" ganha o registro com o código "<codigo>" e o nivel "<nivel>"
    E esse registro é gravado antes de a recusa sair do caso de uso

    Exemplos:
      | situação                        | resposta             | codigo           | nivel  |
      | o Paulo já está desativado      | 409 ESTADO_IGUAL     | ESTADO_IGUAL     | ALERTA |
      | o banco cai no meio da gravação | 500 ERRO_INESPERADO  | ERRO_INESPERADO  | ERRO   |

  @RN-270
  Cenário: falha inesperada
    Quando a Maria envia POST /usuarios do participante Lucas
    E o banco cai no meio da gravação
    Então a resposta é 500 ERRO_INESPERADO com a mensagem "Algo deu errado. Tente de novo mais tarde."
    E "auditoriaErros" ganha o registro com o código "ERRO_INESPERADO" e o nivel "ERRO"

  @RN-270
  Cenário: guardado e imutável como a auditoria
    Dado o registro "auderr-000031" da sigla "EBFGA" repetida, gravado em 2026-11-08
    Então ele fica guardado até 2031-11-08, 5 anos depois, como na RN-178
    E não pode ser alterado nem apagado, como na RNF-206

  @RN-270
  Cenário: leitura recusada não é auditada
    Quando o time deboaz envia GET /contas/conta-zzzzz
    Então a resposta é 404 CONTA_INEXISTENTE
    E as coleções "auditoria" e "auditoriaErros" não ganham registro

  @RN-271
  Cenário: duas execuções, dois traceIds
    Quando a Maria envia POST /usuarios do participante Lucas
    E depois envia POST /usuarios do participante Pedro
    Então cada execução tem um traceId próprio, diferente do da outra
    E os registros "INICIADO" e "CONCLUIDO" de cada uma repetem o traceId dela

  @RN-271
  Esquema do Cenário: traceId no cabeçalho da resposta
    Quando a Maria envia <pedido>
    Então a resposta é <resposta> com o cabeçalho X-Trace-Id
    E o valor de X-Trace-Id é o traceId gravado em <registros>

    Exemplos:
      | pedido                               | resposta         | registros                    |
      | POST /usuarios do participante Lucas | 201              | "INICIADO" e "CONCLUIDO"     |
      | POST /contas com a sigla "EBFGA"     | 409 SIGLA_EM_USO | "INICIADO" e "auditoriaErros" |

  @RN-271
  Cenário: acompanhamento só pela auditoria
    Dado o traceId "trc-7f3a9c21" de uma execução de POST /contas recusada com 409 SIGLA_EM_USO
    Quando o time deboaz procura o que aconteceu nessa execução
    Então encontra tudo nas coleções "auditoria" e "auditoriaErros" pelo traceId "trc-7f3a9c21"
    E o caso de uso não escreve nada em log; o log da partida, da RNF-226 e da RNF-227, continua

  @RN-179
  Cenário: sigla sugerida
    Quando envia GET /contas/sigla-sugerida?nome=Escola Bento Freitas Gonçalves Araújo
    Então a resposta é 200 com:
      """
      {
        "sigla": "EBFGA",
        "livre": true
      }
      """

  @RN-179
  Esquema do Cenário: sigla recusada
    Dado a sigla "EBFGA" já usada
    Quando envia POST /contas com a sigla "<sigla>"
    Então a resposta é <resposta>

    Exemplos:
      | sigla | resposta                                            |
      | ebfga | 409 SIGLA_EM_USO                                    |
      | DBOAZ | 422 SIGLA_INVALIDA, reservada ao time               |
      | QAEDU | em produção, 422 SIGLA_INVALIDA, reservada ao time  |
      | EBF   | 422 SIGLA_INVALIDA, precisa de 5 letras             |
      | COLSA | 201                                                 |
      | colsa | 201, conta "conta-colsa" com a sigla "COLSA"        |

  @RN-242
  Esquema do Cenário: documento de cada conta
    Quando envia POST /contas com o tipo "<tipo>" e o documento <documento>
    Então a resposta é <resposta>

    Exemplos:
      | tipo        | documento               | resposta                               |
      | INSTITUICAO | CNPJ 12.345.678/0001-90 | 201                                    |
      | AUTONOMO    | CPF 123.456.789-09      | 201                                    |
      | AUTONOMO    | CNPJ 12.345.678/0001-90 | 422 DADOS_INVALIDOS, campo "documento" |

  @RN-180
  Cenário: maiúsculas e minúsculas
    Quando envia POST /auth/hub/entrar com a entrada "Maria.EEJSA@deboaz.com"
    Então a resposta é 200 e o token é de "maria.eejsa@deboaz.com"

  @RN-180
  Esquema do Cenário: login de cada usuário
    Quando <quem> é criado
    Então o login é "<login>"

    Exemplos:
      | quem                     | login                        |
      | a conta master da EBFGA  | objetivo_unidade1@deboaz.com |
      | a staff Maria, da EEJSA  | maria.eejsa@deboaz.com       |
      | o membro do time Gustavo | gustavo.dboaz@deboaz.com     |

  @RN-181
  Cenário: primeiro acesso da Maria
    Quando a conta master envia POST /usuarios para criar a Maria
    Então a resposta 201 traz o código de liberação "482913" uma única vez
    E GET /usuarios/usr-ebfga-maria não traz o código
    E POST /usuarios/gerar-senha só devolve a senha com "maria.ebfga@deboaz.com" e "482913"

  @RN-182
  Esquema do Cenário: código vencido
    Dado o código de liberação da Maria pedido há <tempo>
    Quando ela envia POST /usuarios/gerar-senha com o código
    Então a resposta é <resposta>

    Exemplos:
      | tempo | resposta           |
      | 71 h  | 200 com a senha    |
      | 73 h  | 410 CODIGO_VENCIDO |

  @RN-183
  Esquema do Cenário: limites e padrão
    Quando a conta master envia PUT /contas/conta-ebfga/tempo-sessao com <campo>
    Então aceita de <mínimo> a <máximo>, com padrão <padrão>
    E fora disso a resposta é 422 FORA_DOS_LIMITES

    Exemplos:
      | campo          | mínimo | máximo | padrão |
      | staffNavegador | PT15M  | PT12H  | PT8H   |
      | staffApp       | P1D    | P30D   | P7D    |
      | participante   | P1D    | P90D   | P30D   |

  @RN-243
  Esquema do Cenário: limites e padrão · RN-243
    Quando o time deboaz envia PUT /time/tempo-sessao com <campos>
    Então aceita de <mínimo> a <máximo>, com padrão <padrão>

    Exemplos:
      | campos                        | mínimo | máximo | padrão |
      | membrosNavegador e membrosApp | PT15M  | PT12H  | PT8H   |
      | mastersNavegador e mastersApp | PT15M  | PT24H  | PT8H   |

  @RN-243
  Cenário: escola não muda a própria conta master
    Quando a conta master da EBFGA envia PUT /time/tempo-sessao
    Então a resposta é 403 ACESSO_NEGADO

  @RN-244
  Cenário: nome curto do participante
    Quando a conta master envia PATCH /usuarios/usr-ebfga-lucas com o nome curto "lucasa"
    Então a resposta é 200 com um login novo, como "EBFGA-LUCASA-7731"
    E o código "EBFGA-LUCAS-0412" deixa de valer

  @RN-244
  Cenário: matrícula repetida
    Quando a conta master envia PATCH /usuarios/usr-ebfga-ana com a matrícula "20260087", da Maria
    Então a resposta é 409 MATRICULA_EM_USO e nada muda

  @RN-245
  Cenário: o anterior deixa de valer
    Dado o Lucas no tablet e a Carla no celular com "EBFGA-LUCAS-0412"
    Quando a conta master envia POST /usuarios/usr-ebfga-lucas/novo-codigo-acesso
    Então os tokens do tablet e do celular recebem 401 no próximo pedido
    E POST /auth/quest/entrar com "EBFGA-LUCAS-0412" recebe 401 CODIGO_INVALIDO

  @RN-246
  Cenário: começa sem nada
    Quando o administrador envia POST /usuarios com o tipo "MEMBRO_TIME" para o Caio Reis
    Então a resposta é 201
    E GET /usuarios/usr-dboaz-caio/permissoes traz mods vazio

  @RN-184
  Cenário: formato do código
    Quando a EBFGA envia POST /usuarios do participante de nome curto "lucas"
    Então o login é "EBFGA-LUCAS-" seguido de 4 números, como "EBFGA-LUCAS-0412"
    E o login é único no sistema e vem em maiúsculas

  @RN-184
  Cenário: minúsculas aceitas
    Quando a Carla envia POST /auth/quest/entrar com o código "ebfga-lucas-0412"
    Então a resposta é 200 com o token do Lucas, sem senha

  @RN-185
  Esquema do Cenário: busca nas listas
    Quando <quem> envia GET <endereço>?busca=<termo>
    Então itens traz <resultado>

    Exemplos:
      | quem           | endereço  | termo              | resultado               |
      | a conta master | /usuarios | maria.ebfga        | a Maria                 |
      | a conta master | /usuarios | 20260087           | a Maria, pela matrícula |
      | a conta master | /usuarios | EBFGA-LUCAS-0412   | o Lucas                 |
      | o time deboaz  | /contas   | EBFGA              | a Escola Bento Freitas  |
      | o time deboaz  | /contas   | 12.345.678/0001-90 | a Escola Bento Freitas  |

  @RN-185
  Cenário: ordem, filtro e página
    Quando a conta master envia GET /usuarios?tipo=STAFF&estado=DESATIVADO&pagina=1
    Então itens traz só o staff desativado, de A a Z, até 50 por página, com o total

  @RN-186
  Cenário: participante com quem entrou
    Dado a Carla, mãe do Lucas, entrou em 2026-11-01 às 07:55 UTC
    Quando a conta master envia GET /usuarios/usr-ebfga-lucas
    Então ultimoAcesso é "2026-11-01T07:55:00Z" e ultimoAcessoQuem é "MAE"

  @RN-186
  Cenário: último acesso da conta
    Quando o time deboaz envia GET /contas?busca=EBFGA
    Então o item da EBFGA traz ultimoAcessoMaster

  @RN-187
  Cenário: só o cadastro
    Quando o time deboaz envia GET /contas/conta-ebfga
    Então a resposta é 200 com:
      """
      {
        "id": "conta-ebfga",
        "tipo": "INSTITUICAO",
        "nome": "Escola Bento Freitas",
        "documento": "12.345.678/0001-90",
        "sigla": "EBFGA",
        "loginMaster": "objetivo_unidade1@deboaz.com",
        "responsavel": "Gustavo Boaz de Moura Souza",
        "telefone": "(51) 99999-0000",
        "estado": "ATIVA",
        "criadaEm": "2026-03-12T13:05:00Z",
        "ultimoPagamento": "2026-10-05",
        "ultimoAcessoMaster": "2026-11-01T08:15:00Z"
      }
      """
    E a resposta não traz usuários, turmas nem conteúdos da escola

  @RN-187
  Cenário: conta que não existe
    Quando envia GET /contas/conta-zzzzz
    Então a resposta é 404 CONTA_INEXISTENTE

  @RN-188
  Cenário: gerar automaticamente
    Quando a conta master envia POST /usuarios do Lucas com a matrícula null
    Então GET /usuarios/{id} do Lucas traz uma matrícula de 8 números, única na escola

  @RN-188
  Cenário: matrícula que a escola já usa
    Quando a conta master envia POST /usuarios da Maria com a matrícula "20260087"
    Então GET /usuarios/usr-ebfga-maria traz a matrícula "20260087"

  @RN-188
  Cenário: dois participantes sem matrícula no autônomo
    Dado o token do educador autônomo Pedro, com o participante Lucas sem matrícula
    Quando envia POST /usuarios da participante Bia, também sem matrícula
    Então a resposta é 201, e não 409 MATRICULA_EM_USO
    E o Lucas e a Bia ficam os dois sem matrícula

  @RN-189
  Cenário: dados do card
    Dado a conta master da EBFGA, com 37 staff e 298 participantes ativos
    Quando envia GET /usuarios/card
    Então a resposta é 200 com:
      """
      {
        "selo": 2,
        "dado": 335,
        "rotulo": "usuários ativos",
        "noMes": 12,
        "meses": [
          301,
          310,
          318,
          323,
          335
        ],
        "staff": 37,
        "participantes": 298
      }
      """

  @RN-189
  Esquema do Cenário: dado de cada perfil
    Quando <quem> envia GET /usuarios/card
    Então dado e rotulo são <dado>

    Exemplos:
      | quem                      | dado                        |
      | a conta master da EBFGA   | 335 e "usuários ativos"     |
      | o educador autônomo Pedro | 32 e "participantes ativos" |
      | o administrador do deboaz | 112 e "contas ativas"       |

  @RN-190
  Cenário: senha fora das telas
    Então a entrada e a senha do administrador do deboaz ficam num segredo do Key Vault
    E só são definidas e trocadas ali, sem endereço que as mude
    E o sistema guarda só o resumo irreversível da senha

  @RN-190
  Cenário: membro esqueceu a senha
    Quando o administrador envia POST /usuarios/usr-dboaz-caio/nova-geracao-senha
    Então a resposta é 201 com o código de liberação para entregar ao Caio

  @RN-190
  Cenário: única conta criada no início, em todo ambiente
    Dado o Key Vault do ambiente com a entrada "admin.dboaz@deboaz.com"
    E a senha do administrador
    Quando a aplicação inicia pela primeira vez, em homologação ou em produção
    Então o administrador do deboaz passa a existir, ativo, com essa entrada e essa senha
    E POST /auth/hub/entrar com elas devolve 200 e o papel "ADMINISTRADOR"

  @RN-265
  Esquema do Cenário: papel de cada conta qa no hub
    Dado a aplicação de homologação iniciada
    Quando envia POST /auth/hub/entrar com a entrada "<entrada>" e a senha do Key Vault
    Então a resposta é 200 e o token tem o papel "<papel>"

    Exemplos:
      | entrada                   | papel         |
      | qaadmin.dboaz@deboaz.com  | ADMINISTRADOR |
      | qamembro.dboaz@deboaz.com | MEMBRO_TIME   |
      | qa_instituicao@deboaz.com | MASTER        |
      | qa_autonomo@deboaz.com    | AUTONOMO      |
      | qastaff.qains@deboaz.com  | STAFF         |

  @RN-265
  Cenário: participante qa no quest
    Dado a aplicação de homologação iniciada
    Quando envia POST /auth/quest/entrar com o código "QAINS-QAPART-0001" do Key Vault
    Então a resposta é 200 e o token tem o papel "PARTICIPANTE"

  @RN-265
  Esquema do Cenário: tipo de cada conta qa, lido por quem tem acesso
    Dado o token de "<quem>"
    Quando envia GET /usuarios/{id} de "<alvo>"
    Então a resposta é 200 com o tipo "<tipo>"

    Exemplos:
      | quem                      | alvo                      | tipo         |
      | qa_instituicao@deboaz.com | qa_instituicao@deboaz.com | MASTER       |
      | qa_autonomo@deboaz.com    | qa_autonomo@deboaz.com    | MASTER       |
      | qaadmin.dboaz@deboaz.com  | qamembro.dboaz@deboaz.com | MEMBRO_TIME  |
      | qa_instituicao@deboaz.com | qastaff.qains@deboaz.com  | STAFF        |
      | qa_instituicao@deboaz.com | QAINS-QAPART-0001         | PARTICIPANTE |

  @RN-266
  Esquema do Cenário: contas que a inicialização garante em cada ambiente
    Quando a aplicação de <ambiente> inicia
    Então existem <contas>

    Exemplos:
      | ambiente    | contas                                                        |
      | homologação | o administrador, as 6 contas qa e as contas "QAINS" e "QAAUT" |
      | produção    | só o administrador, na conta "DBOAZ"                          |

  @RN-266
  Cenário: segredo qa em produção por engano
    Dado o Key Vault de produção com a entrada "qastaff.qains@deboaz.com" e uma senha
    Quando a aplicação de produção inicia
    Então nenhuma conta qa nem as contas "QAINS" e "QAAUT" são criadas
    E POST /auth/hub/entrar com "qastaff.qains@deboaz.com" recebe 401 ENTRADA_INVALIDA

  @RN-267
  Cenário: segunda inicialização
    Dado a staff "qastaff.qains@deboaz.com" criada na inicialização de 2026-11-05
    Quando a aplicação de homologação inicia de novo em 2026-11-06
    Então GET /usuarios?tipo=STAFF da "QAINS" traz uma só "qastaff.qains@deboaz.com"
    E a auditoria não ganha um registro novo de criação dela

  @RN-267
  Cenário: senha trocada no Key Vault
    Dado a senha de "qa_instituicao@deboaz.com" trocada no Key Vault
    Quando a aplicação inicia
    Então POST /auth/hub/entrar aceita só a senha nova
    E a senha antiga recebe 401 ENTRADA_INVALIDA

  @RN-267
  Esquema do Cenário: conta qa mudada por um teste volta ao estado inicial
    Dado <mudança> durante um teste
    Quando a aplicação de homologação inicia de novo
    Então <volta>
    E só as contas qa voltam ao estado inicial
    E "admin.dboaz@deboaz.com" fica como estava, sem volta nem mudança vinda do "qaadmin"

    Exemplos:
      | mudança                                     | volta                                         |
      | a staff "qastaff.qains" desativada          | a staff fica ativa                            |
      | a senha do "qamembro.dboaz" gerada de novo  | vale só a senha do Key Vault                  |
      | "MOD-05" tirado das abas do "qastaff.qains" | o "qastaff" só com as abas da parte da staff  |
      | "MOD-02" concedido ao "qamembro.dboaz"      | o "qamembro" só com as abas da parte do time  |

  @RN-268
  Cenário: permissões iniciais lidas do Key Vault
    Dado o Key Vault de homologação com a lista de módulos iniciais:
      """
      {
        "staff": {
          "mods": {
            "MOD-03": "CU",
            "MOD-04": "U",
            "MOD-05": "U"
          }
        },
        "time": {
          "mods": {
            "MOD-03": "U",
            "MOD-14": "U"
          }
        }
      }
      """
    Quando a aplicação de homologação inicia
    Então GET /usuarios/{id}/permissoes do "qastaff.qains@deboaz.com" traz os mods de "staff"
    E GET /usuarios/{id}/permissoes do "qamembro.dboaz@deboaz.com" traz os mods de "time"

  @RN-268
  Cenário: contas qa com os módulos da lista contratados
    Dado a mesma lista no Key Vault de homologação
    Quando "qa_instituicao@deboaz.com" envia GET /usuarios/{id}/permissoes do "qastaff"
    Então concediveis traz "MOD-03", "MOD-04" e "MOD-05", da parte "staff", com "CU"
    E traz também os módulos da conta "MOD-08" Turmas e "MOD-09" Tarefas, que entram sozinhos

  @RN-268
  Cenário: parte do time não vira contratação
    Dado a mesma lista no Key Vault de homologação
    Quando a aplicação de homologação inicia
    Então "QAINS" e "QAAUT" não contratam "MOD-14" nem nenhum módulo da parte "time"

  @RN-268
  Cenário: produção sem módulos da inicialização
    Dado a aplicação de produção iniciada
    Quando a conta master da EBFGA envia GET /usuarios/usr-ebfga-maria/permissoes
    Então concediveis traz só os módulos contratados pela escola

  @RNF-201
  Cenário: ninguém lê a senha
    Dado a senha da Maria guardada
    Quando o time deboaz envia qualquer pedido sobre a Maria
    Então nenhuma resposta traz a senha
    E no banco existe só o resumo irreversível, de onde a senha não pode ser lida

  @RNF-202
  Esquema do Cenário: senha válida
    Quando o servidor confere a senha "<senha>"
    Então ela é <resultado>

    Exemplos:
      | senha         | resultado               |
      | Gu#Boaz507318 | aceita                  |
      | Gu#B5         | recusada, menos de 8    |
      | gu#boaz507318 | recusada, sem maiúscula |
      | GuBoaz507318  | recusada, sem especial  |
      | Gu#Boazxyzwq  | recusada, sem número    |

  @RNF-205
  Cenário: quarto envio no dia
    Dado 3 pedidos POST /contas/esqueci-senha-master do mesmo aparelho nas últimas 24 h
    Quando chega o quarto
    Então a resposta é 429 LIMITE_ATENDIMENTO com tentarEm, 24 h depois do primeiro

  @RNF-206
  Cenário: registro não muda
    Dado o registro "aud-ebfga-000154" da criação do Lucas
    Quando o time deboaz procura alterar ou apagar o registro
    Então nenhum endereço da API faz isso
    E o banco recusa a alteração e a remoção

  @RNF-225
  Cenário: entradas e senhas entregues na partida
    Dado o Key Vault de homologação com a entrada e a senha de cada conta inicial
    Quando a aplicação de homologação inicia
    Então recebe cada entrada, senha e código como variável de ambiente lida do Key Vault
    E nenhuma delas está no código, no repositório, na imagem nem no pipeline

  @RNF-225
  Cenário: senha fora de registros e respostas
    Quando a aplicação cria ou confere uma conta inicial
    Então nenhum log, registro de auditoria ou resposta traz a senha ou o código
    E no banco fica só o resumo irreversível da senha, como na RNF-201

  @RNF-226
  Esquema do Cenário: aplicação não inicia sem o segredo
    Dado a aplicação de <ambiente> sem <segredo>
    Quando ela inicia
    Então para antes de aceitar o primeiro pedido
    E o log diz o nome da variável que falta, nunca um valor

    Exemplos:
      | ambiente    | segredo                                   |
      | produção    | a senha do administrador                  |
      | homologação | a entrada do administrador                |
      | homologação | a senha do administrador qa "qaadmin"     |
      | homologação | a senha da staff "qastaff.qains"          |
      | homologação | o código do participante "QAINS-QAPART"   |
      | homologação | a lista de módulos iniciais das contas qa |

  @RNF-226
  Cenário: senha fraca no Key Vault
    Dado a senha "Gu#B5" no Key Vault para "qastaff.qains@deboaz.com"
    Quando a aplicação de homologação inicia
    Então para antes de aceitar o primeiro pedido, porque a senha não segue a RNF-202

  @RNF-227
  Esquema do Cenário: coleção pronta antes do primeiro pedido
    Dado o banco de homologação vazio
    Quando a aplicação inicia
    Então antes de aceitar o primeiro pedido, a coleção "<colecao>" existe
    E tem os índices <indices>

    Exemplos:
      | colecao            | indices                                                       |
      | contas             | sigla único, dominio único, documentoNumeros                  |
      | usuarios           | login único, contaId+nomeCurto único                          |
      | usuarios           | contaId+matricula único só com matrícula                      |
      | usuarios           | contaId+tipo+estado+nome                                      |
      | auditoria          | contaId+quando, traceId, sem prazo de expiração               |
      | auditoriaErros     | traceId, contaId+quando, sem prazo de expiração               |
      | geracoesSenha      | usuarioId+pedidaEm↓, resumoComprovante, contaId+pedidaEm      |
      | registrosPermissao | usuarioId+em↓+versao↓                                         |
      | pedidosAjuda       | aparelho+quando, o limite por aparelho da RNF-205             |

  @RNF-227
  Cenário: segunda partida não duplica
    Dado a aplicação de homologação iniciada em 2026-11-05, com as 7 coleções e seus índices
    Quando ela inicia de novo em 2026-11-06
    Então cada coleção continua uma só, com os mesmos índices, nenhum repetido
    E nenhum documento das coleções é apagado nem duplicado

  @RNF-227
  Esquema do Cenário: índice que não pode ser criado
    Dado <impedimento> na coleção "<colecao>"
    Quando a aplicação inicia
    Então o índice <indice> não é criado
    E o log cita o nome do índice e da coleção, nunca o valor que impediu
    E a aplicação <resultado>

    Exemplos:
      | colecao       | indice            | impedimento                | resultado                 |
      | contas        | sigla único       | duas contas com "EBFGA"    | para antes do 1º pedido   |
      | usuarios      | login único       | dois usuários com um login | para antes do 1º pedido   |
      | contas        | sigla único       | índice único de outro nome | para antes do 1º pedido   |
      | geracoesSenha | resumoComprovante | o banco recusa o índice    | aceita pedidos, com aviso |

  @RNF-227
  Cenário: pronta depois da partida
    Dado a partida de homologação concluída, com as 7 coleções e seus índices prontos
    E os módulos gravados e as contas iniciais garantidas
    Quando envia GET /health, sem login, sem corpo nem cabeçalho
    Então a resposta é 200, sem corpo

  @RNF-227
  Esquema do Cenário: sem resposta antes ou sem a partida
    Dado a aplicação de homologação <situacao>
    Quando envia GET /health, sem login
    Então não há resposta, a conexão é recusada
    E o processo <processo>

    Exemplos:
      | situacao                                         | processo             |
      | ainda na partida                                 | segue na partida     |
      | sem o índice único de sigla em "contas"          | termina com código 1 |
      | sem a senha do administrador, como na RNF-226    | termina com código 1 |
      | com a senha fraca "Gu#B5" de "qastaff.qains"     | termina com código 1 |
      | com o banco fora do ar                           | termina com código 1 |

  @RNF-227
  Cenário: índice não único que não pode ser criado
    Dado o banco recusa o índice resumoComprovante em "geracoesSenha" na partida
    Quando envia GET /health, sem login
    Então a resposta é 200, sem corpo
    E o log traz um aviso com o nome do índice e da coleção, nunca o valor

  @exemplo-entidades
  Cenário: escola EBFGA cadastrada
    Dado a Escola Bento Freitas, a conta master, a Maria e o Lucas, com datas em UTC
    Então a conta é:
      """
      {
        "_id": "conta-ebfga",
        "sigla": "EBFGA",
        "dominio": "objetivo_unidade1",
        "nome": "Escola Bento Freitas",
        "tipo": "INSTITUICAO",
        "estado": "ATIVA",
        "documento": "12.345.678/0001-90",
        "responsavel": "Gustavo Boaz de Moura Souza",
        "telefone": "(51) 99999-0000",
        "criadaEm": "2026-03-12T13:05:00Z",
        "ultimoAcessoMaster": "2026-10-29T11:15:00Z",
        "tempoSessao": {
          "staffNavegador": "PT8H",
          "staffApp": "P7D",
          "participante": "P30D"
        }
      }
      """
    E a conta master é:
      """
      {
        "_id": "usr-ebfga-master",
        "sigla": "EBFGA",
        "contaId": "conta-ebfga",
        "tipo": "MASTER",
        "nome": "Escola Bento Freitas",
        "nomeCurto": "objetivo_unidade1",
        "estado": "ATIVO",
        "login": "objetivo_unidade1@deboaz.com",
        "resumoSenha": "(resumo irreversível)",
        "criadoEm": "2026-03-12T13:05:00Z",
        "ultimoAcesso": "2026-10-29T11:15:00Z"
      }
      """
    E a staff é:
      """
      {
        "_id": "usr-ebfga-maria",
        "sigla": "EBFGA",
        "contaId": "conta-ebfga",
        "tipo": "STAFF",
        "nome": "Maria Souza",
        "nomeCurto": "maria",
        "estado": "ATIVO",
        "login": "maria.ebfga@deboaz.com",
        "matricula": "20260087",
        "resumoSenha": "(resumo irreversível)",
        "permissoes": {
          "versao": 7,
          "pv": 7,
          "mods": {
            "MOD-03": "CU",
            "MOD-04": "U",
            "MOD-05": "U"
          },
          "suspensos": []
        },
        "criadoEm": "2026-03-14T10:20:00Z",
        "ultimoAcesso": "2026-10-29T09:42:00Z"
      }
      """
    E o participante é:
      """
      {
        "_id": "usr-ebfga-lucas",
        "sigla": "EBFGA",
        "contaId": "conta-ebfga",
        "tipo": "PARTICIPANTE",
        "nome": "Lucas Andrade",
        "nomeCurto": "lucas",
        "estado": "ATIVO",
        "login": "EBFGA-LUCAS-0412",
        "codigoGeradoEm": "2026-03-20T15:00:00Z",
        "matricula": "48201755",
        "responsaveis": [
          {
            "nome": "Carla Andrade",
            "parentesco": "MAE"
          },
          {
            "nome": "Paulo Andrade",
            "parentesco": "PAI"
          }
        ],
        "criadoEm": "2026-03-20T15:00:00Z",
        "ultimoAcesso": "2026-10-28T22:10:00Z"
      }
      """

  @exemplo-entidades
  Cenário: geração de senha, acesso anônimo, auditoria e erro
    Então a geração de senha da Maria é:
      """
      {
        "_id": "ger-ebfga-maria-0001",
        "sigla": "EBFGA",
        "usuarioId": "usr-ebfga-maria",
        "pedidaPor": "usr-ebfga-master",
        "resumoCodigo": "(resumo irreversível)",
        "resumoComprovante": "(resumo irreversível)",
        "pedidaEm": "2026-10-27T13:00:00Z",
        "senhaMostradaEm": "2026-10-27T13:12:00Z",
        "confirmadaEm": "2026-10-27T13:13:00Z"
      }
      """
    E o acesso anônimo da sala 482107 é:
      """
      {
        "_id": "anon-ebfga-482107-13465",
        "sigla": "EBFGA",
        "sala": "482107",
        "apelido": "PUPIL-EBFGA-13465"
      }
      """
    E o registro de auditoria é:
      """
      {
        "_id": "aud-ebfga-000154",
        "contaId": "conta-ebfga",
        "quem": "usr-ebfga-maria",
        "acao": "CRIAR_USUARIO",
        "alvo": "usr-ebfga-lucas",
        "traceId": "trc-1b8e4d02",
        "etapa": "CONCLUIDO",
        "quando": "2026-03-20T15:00:00Z",
        "guardarAte": "2031-03-20T15:00:00Z"
      }
      """
    E o registro de erro da sigla repetida é:
      """
      {
        "_id": "auderr-000031",
        "traceId": "trc-7f3a9c21",
        "contaId": null,
        "quem": null,
        "acao": "CRIAR_CONTA",
        "alvo": null,
        "codigo": "SIGLA_EM_USO",
        "nivel": "ALERTA",
        "quando": "2026-11-08T01:10:00Z",
        "guardarAte": "2031-11-08T01:10:00Z"
      }
      """

  @fora-de-escopo
  Cenário: o que este documento não trata
    Então login por Google ou Microsoft e segundo fator ficam de fora (decisão de 2026-10-29)
    E senha escolhida pela pessoa e envio de senha ou código por e-mail ficam de fora
    E recuperação por perguntas de segurança fica de fora
    E turma e ano do participante ficam no módulo de turmas
    E o formato do código da sala fica no módulo de aula ao vivo
    E usuário em várias unidades da mesma instituição fica para a fase de escala
