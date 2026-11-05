# language: pt
# Cenários de API de 1_Requisitos/17_modulo-usuario.md (v44), copiados do requisito:
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
      | a staff Ana               | o João                    | 403 ACESSO_NEGADO |

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
  Esquema do Cenário: ação auditada
    Quando a Maria envia <pedido>
    Então a auditoria guarda a Maria, a ação, o alvo e a hora, até 5 anos depois

    Exemplos:
      | pedido                                          |
      | POST /usuarios do participante Lucas            |
      | POST /usuarios/usr-ebfga-ana/nova-geracao-senha |
      | PUT /usuarios/usr-ebfga-joao/permissoes         |
      | POST /usuarios/usr-ebfga-paulo/desativar        |

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
      | sigla | resposta                                |
      | ebfga | 409 SIGLA_EM_USO                        |
      | DBOAZ | 422 SIGLA_INVALIDA, reservada ao time   |
      | EBF   | 422 SIGLA_INVALIDA, precisa de 5 letras |
      | COLSA | 201                                     |

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
  Cenário: geração de senha, acesso anônimo e auditoria
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
        "quando": "2026-03-20T15:00:00Z",
        "guardarAte": "2031-03-20T15:00:00Z"
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
