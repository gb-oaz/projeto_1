# language: pt
# Cenários de API de 1_Requisitos/17_modulo-usuario-b-autorizacao.html (v16), copiados do requisito:
# mudou lá, muda aqui. Cada cenário leva a tag do ID que cobre.
Funcionalidade: Autorização
  1_Requisitos/17_modulo-usuario-b-autorizacao.html · Cenários de API

  @RF-144
  Cenário: staff com dois módulos
    Dado a Maria com Administrar usuários e a Utilização da Sugestão e do Quiz
    Quando envia POST /auth/hub/entrar com a entrada e a senha dela
    Então o token de acesso tem o papel "STAFF"
    E os mods "MOD-03" com "CU", "MOD-04" com "U" e "MOD-05" com "U"

  @RF-145
  Cenário: módulo não contratado
    Dado a Maria com a Utilização da Presença marcada
    E a escola sem a Presença contratada
    Quando envia POST /auth/hub/entrar
    Então o token dela não tem "MOD-06"

  @RF-145
  Cenário: membro do time sem o Catálogo
    Dado o membro do time Caio Lima com a Utilização do SAC e de Usuários marcadas
    Quando envia POST /auth/hub/entrar
    Então o token tem o papel "MEMBRO_TIME", "MOD-02" e "MOD-03" com "U", e não tem "MOD-14"

  @RF-145
  Cenário: staff sem a Sugestão marcada
    Dado o staff João Lima só com a Utilização do Quiz
    Quando envia POST /auth/hub/entrar
    Então o token dele não tem "MOD-04"

  @RF-147
  Cenário: ação sem a aba
    Dado o token da Maria sem "MOD-03"
    Quando envia POST /usuarios para criar um staff
    Então a resposta é 403 ACESSO_NEGADO e nenhum usuário é criado

  @RF-150
  Cenário: mãe no perfil do filho
    Dado a escola com a Presença e a Carteirinha contratadas
    Quando a Carla, mãe do Lucas, envia POST /auth/quest/entrar com o código dele
    Então o token tem o papel "PARTICIPANTE" e, em quem, o parentesco "Mãe"
    E os mods de Tarefas, Perfil, Presença e Carteirinha, todos com "U"

  @RF-151
  Cenário: só a sala
    Quando envia POST /auth/sala/entrar com a sala 482107 do Quiz
    Então o token tem o papel "ANONIMO", só "MOD-05" com "U" e a sala 482107
    E os pedidos à Carteira, a Tarefas e ao Perfil recebem 403 ACESSO_NEGADO

  @RF-160
  Cenário: staff com Administrar usuários
    Dado o João com Administrar usuários e só a Utilização do Quiz
    Quando envia GET /usuarios/usr-ebfga-ana/permissoes
    Então a resposta é 200 com:
      """
      {
        "usuario": "usr-ebfga-ana",
        "versao": 2,
        "mods": {},
        "concediveis": [
          {
            "mod": "MOD-03",
            "nome": "Usuários",
            "abas": "CU"
          },
          {
            "mod": "MOD-05",
            "nome": "Quiz",
            "abas": "U"
          }
        ]
      }
      """

  @RF-160
  Cenário: pessoa que não está abaixo
    Quando o João envia GET /usuarios/usr-ebfga-rui/permissoes, de um par dele
    Então a resposta é 403 ACESSO_NEGADO

  @RF-161
  Cenário: quem concede perdeu a aba
    Dado a conta master tendo tirado a Presença do João
    Quando o João envia PUT /usuarios/usr-ebfga-ana/permissoes com If-Match "2" e:
      """
      {
        "mods": {
          "MOD-05": "U",
          "MOD-06": "U"
        }
      }
      """
    Então a resposta é 403 PERMISSAO_ACIMA com:
      """
      {
        "codigo": "PERMISSAO_ACIMA",
        "mensagem": "Você não tem mais o módulo Presença para conceder.",
        "mod": "MOD-06",
        "abas": "U"
      }
      """
    E GET /usuarios/usr-ebfga-ana/permissoes continua com a versão 2, sem mudança

  @RF-161
  Cenário: módulo que a pessoa não usa
    Quando a conta master envia PUT /usuarios/usr-ebfga-maria/permissoes com:
      """
      {
        "mods": {
          "MOD-13": "U"
        }
      }
      """
    Então a resposta é 422 DADOS_INVALIDOS com:
      """
      {
        "codigo": "DADOS_INVALIDOS",
        "mensagem": "Maria Souza não usa o módulo Perfil.",
        "campo": "mods.MOD-13"
      }
      """

  @RF-161
  Cenário: módulo que não existe
    Quando a conta master envia PUT /usuarios/usr-ebfga-maria/permissoes com:
      """
      {
        "mods": {
          "MOD-99": "U"
        }
      }
      """
    Então a resposta é 422 DADOS_INVALIDOS com:
      """
      {
        "codigo": "DADOS_INVALIDOS",
        "mensagem": "O módulo MOD-99 não existe.",
        "campo": "mods.MOD-99"
      }
      """

  @RF-162
  Cenário: duas pessoas salvando
    Dado as permissões da Maria lidas na versão 6 pela conta master e pelo João
    E a conta master tendo salvado antes, passando à versão 7
    Quando o João envia PUT /usuarios/usr-ebfga-maria/permissoes com If-Match "6"
    Então a resposta é 412 VERSAO_DESATUALIZADA com:
      """
      {
        "codigo": "VERSAO_DESATUALIZADA",
        "mensagem": "As permissões de Maria Souza mudaram agora há pouco. Confira e salve.",
        "versao": 7
      }
      """

  @RN-232
  Esquema do Cenário: papel de cada pessoa
    Quando <quem> envia POST <endereço>
    Então o token tem o papel "<papel>"

    Exemplos:
      | quem                            | endereço           | papel         |
      | o administrador do deboaz       | /auth/hub/entrar   | ADMINISTRADOR |
      | o membro do time Caio Lima      | /auth/hub/entrar   | MEMBRO_TIME   |
      | a conta master da EBFGA         | /auth/hub/entrar   | MASTER        |
      | o educador autônomo Pedro Reis  | /auth/hub/entrar   | AUTONOMO      |
      | a staff Maria Souza             | /auth/hub/entrar   | STAFF         |
      | o participante Lucas Andrade    | /auth/quest/entrar | PARTICIPANTE  |
      | a Carla, mãe do Lucas           | /auth/quest/entrar | PARTICIPANTE  |
      | uma pessoa sem código de acesso | /auth/sala/entrar  | ANONIMO       |

  @RN-233
  Esquema do Cenário: módulos de cada papel
    Quando <quem> entra no <frente>
    Então o token tem em mods <módulos> com as abas <abas>

    Exemplos:
      | quem           | frente | módulos                                                   | abas     |
      | administrador  | hub    | SAC, Usuários e Catálogo                                  | C e U    |
      | membro do time | hub    | os marcados entre SAC, Usuários e Catálogo                | marcadas |
      | conta master   | hub    | todos os da conta e os contratados usados no hub          | C e U    |
      | autônomo       | hub    | todos os da conta e os contratados usados no hub          | C e U    |
      | staff          | hub    | os marcados pela conta master                             | marcadas |
      | participante   | quest  | Tarefas, Perfil e os que participante ou responsável usam | U        |
      | responsável    | quest  | os mesmos do participante                                 | U        |
      | anônimo        | quest  | só o módulo da sala                                       | U        |

  @RN-235
  Cenário: registro de outra conta
    Dado a Maria, da escola "EBFGA", com "MOD-03" com "U"
    Quando envia GET /usuarios/usr-colsa-bruno, de um usuário da escola "COLSA"
    Então a resposta é 404 USUARIO_INEXISTENTE, e não 403

  @RN-235
  Cenário: time deboaz em outra conta
    Quando o membro do time Caio, com "MOD-03" com "U", envia GET /contas/conta-ebfga
    Então a resposta é 200 com o cadastro da conta

  @RN-236
  Cenário: casca adulterada
    Dado a Maria com "MOD-03" só com "C"
    E a casca adulterada mostrando a aba Utilização de Usuários
    Quando envia GET /usuarios?tipo=STAFF
    Então a resposta é 403 ACESSO_NEGADO

  @RN-237
  Cenário: a autorização só conhece módulo, aba e papel
    Dado o professor João com "MOD-06" com "U"
    Quando envia um pedido da Presença
    Então a autorização libera o módulo
    E quais turmas ele vê é regra do documento da Presença

  @RN-238
  Cenário: descontratação e recontratação
    Dado a Maria com "MOD-06" com "U"
    Quando a escola descontrata a Presença
    Então GET /usuarios/usr-ebfga-maria/permissoes não traz "MOD-06" em mods
    E o próximo token da Maria vem sem "MOD-06"
    E quando a escola contrata a Presença de novo, "MOD-06" volta com "U"

  @RN-239
  Cenário: token com versão antiga
    Dado a Maria com um token de pv 6
    E a conta master tendo salvado as permissões dela, passando à versão 7
    Quando ela envia um pedido com o token de pv 6
    Então a resposta é 401 PERMISSOES_DESATUALIZADAS
    E POST /auth/renovar devolve um token de pv 7

  @RN-241
  Cenário: confere no servidor, não no token
    Dado o João com um token que ainda tem "MOD-06"
    E a conta master tendo tirado a Presença dele há pouco
    Quando envia PUT /usuarios/usr-ebfga-ana/permissoes com "MOD-06" com "U"
    Então a resposta é 403 PERMISSAO_ACIMA, conferida com as permissões atuais do João

  @RN-251
  Esquema do Cenário: abas de uma permissão
    Quando a conta master salva para a Maria <abas> do Quiz
    Então o token novo da Maria tem "MOD-05" com "<valor>"

    Exemplos:
      | abas                      | valor |
      | Configuração              | C     |
      | Utilização                | U     |
      | Configuração e Utilização | CU    |

  @RN-251
  Cenário: Administrar usuários
    Quando a conta master salva Administrar usuários para a Maria
    Então o token novo da Maria tem "MOD-03" com "CU"

  @RN-251
  Cenário: nenhuma aba
    Quando a conta master envia PUT /usuarios/usr-ebfga-maria/permissoes sem "MOD-05"
    Então GET /usuarios/usr-ebfga-maria/permissoes não traz "MOD-05"

  @RN-251
  Cenário: o que cada aba libera
    Então a Configuração, "C", libera criar e configurar no módulo, como criar um Quiz
    E a Utilização, "U", libera usar o que foi criado, como ver e participar de um Quiz
    E quem cria recebe "C", como a conta master, o autônomo e a staff que dá aula
    E o participante e o responsável só recebem "U"

  @RN-252
  Esquema do Cenário: concessões permitidas
    Quando <quem concede> envia PUT /usuarios/{id}/permissoes de <quem recebe>
    Então a resposta é <resposta>

    Exemplos:
      | quem concede     | quem recebe          | resposta                        |
      | o administrador  | o membro Caio        | 200                             |
      | a conta master   | a staff Maria        | 200                             |
      | o João, com Adm. | a Ana, abaixo dele   | 200, só com as abas que ele tem |
      | o João, com Adm. | ele mesmo            | 403 ACESSO_NEGADO               |
      | o João, com Adm. | a Maria, par dele    | 403 ACESSO_NEGADO               |
      | a staff Ana      | o João, acima dela   | 403 ACESSO_NEGADO               |
      | a conta master   | o participante Lucas | 403 ACESSO_NEGADO               |

  @RN-252
  Esquema do Cenário: abaixo é quem a pessoa criou, direta ou indiretamente
    Dado o João com Administrar usuários e só a Utilização do Quiz
    Quando envia GET /usuarios/<id>/permissoes, de <quem>
    Então a resposta é <resposta>

    Exemplos:
      | id            | quem                                     | resposta          |
      | usr-ebfga-ana | a Ana, que ele criou                     | 200               |
      | usr-ebfga-rui | o Rui, criado pela conta master como ele | 403 ACESSO_NEGADO |

  @RN-252
  Cenário: staff repassa Administrar usuários
    Dado o João com Administrar usuários e só a Utilização do Quiz
    Quando o João envia PUT /usuarios/usr-ebfga-ana/permissoes com If-Match "2" e:
      """
      {
        "mods": {
          "MOD-03": "CU"
        }
      }
      """
    Então a resposta é 200 com:
      """
      {
        "versao": 3,
        "mods": {
          "MOD-03": "CU"
        }
      }
      """

  @RN-252
  Cenário: staff repassa Administrar usuários só com as abas que tem
    Dado o João com "MOD-03" só com "U"
    Quando o João envia PUT /usuarios/usr-ebfga-ana/permissoes com If-Match "2" e:
      """
      {
        "mods": {
          "MOD-03": "CU"
        }
      }
      """
    Então a resposta é 403 PERMISSAO_ACIMA com:
      """
      {
        "codigo": "PERMISSAO_ACIMA",
        "mensagem": "Você não tem mais o módulo Usuários para conceder.",
        "mod": "MOD-03",
        "abas": "CU"
      }
      """

  @RN-253
  Cenário: quem concedeu é desativado
    Dado a Ana com "MOD-05" com "U" concedida pelo João
    Quando a conta master envia POST /usuarios/usr-ebfga-joao/desativar
    Então GET /usuarios/usr-ebfga-ana/permissoes continua com "MOD-05" com "U"

  @RN-254
  Cenário: desmarcar retira
    Dado a Maria com "MOD-03" com "CU" e "MOD-06" com "U"
    Quando a conta master envia PUT /usuarios/usr-ebfga-maria/permissoes com:
      """
      {
        "mods": {
          "MOD-03": "CU"
        }
      }
      """
    Então a resposta é 200 e "MOD-06" sai das permissões da Maria

  @RN-255
  Cenário: salvar com mudança
    Dado a Maria na versão de permissões 7, com sessões no navegador e no app
    Quando a conta master salva com o Quiz marcado
    Então a resposta é 200 com a versão 8
    E POST /auth/renovar das duas sessões devolve tokens de pv 8

  @RN-255
  Cenário: salvar sem mudança
    Quando a conta master salva as permissões da Maria sem mudar nada
    Então a resposta é 200 com a versão 7

  @RN-256
  Cenário: o que fica registrado
    Dado a conta master tendo marcado a Presença da Maria em 2026-11-01 às 13:20 UTC
    Quando envia GET /usuarios/usr-ebfga-maria/permissoes/historico
    Então a resposta é 200 com:
      """
      [
        {
          "em": "2026-11-01T13:20:00Z",
          "por": "usr-ebfga-master",
          "versao": 8,
          "antes": {
            "MOD-06": null
          },
          "depois": {
            "MOD-06": "U"
          }
        }
      ]
      """
    E nenhum endereço apaga ou altera esse registro

  @RN-256
  Cenário: a própria pessoa não vê
    Quando a Maria envia GET /usuarios/usr-ebfga-maria/permissoes/historico
    Então a resposta é 403 ACESSO_NEGADO

  @RNF-208
  Cenário: ação autorizada pelo token
    Dado o token da Maria com o papel "STAFF" e "MOD-05" com "U"
    Quando envia um pedido do Quiz
    Então o pedido é autorizado pelo papel e pelos mods do token, sem ler as permissões

  @RNF-219
  Cenário: claims de autorização
    Quando a Maria envia POST /auth/hub/entrar
    Então o token tem as claims papel, mods e pv:
      """
      {
        "aud": "hub",
        "sub": "usr-ebfga-maria",
        "papel": "STAFF",
        "mods": {
          "MOD-03": "CU",
          "MOD-04": "U",
          "MOD-05": "U"
        },
        "pv": 7
      }
      """
    E o token inteiro, com a assinatura RS256 de 342 caracteres, cabe em 4 KB

  @RNF-219
  Cenário: responsável no quest
    Quando a Carla envia POST /auth/quest/entrar com o código do Lucas
    Então o token do quest é:
      """
      {
        "aud": "quest",
        "sub": "usr-ebfga-lucas",
        "papel": "PARTICIPANTE",
        "quem": {
          "tipo": "RESPONSAVEL",
          "parentesco": "Mãe"
        },
        "mods": {
          "MOD-06": "U",
          "MOD-07": "U",
          "MOD-09": "U",
          "MOD-13": "U"
        },
        "pv": 3
      }
      """

  @RNF-220
  Cenário: rota sem declaração
    Dado uma rota nova sem módulo, aba ou papel declarado
    Quando alguém a chama com um token válido
    Então a resposta é 403 ACESSO_NEGADO
    E o teste automatizado de rotas falha no build

  @RNF-222
  Esquema do Cenário: versão no salvamento
    Dado as permissões da Maria na versão 7
    Quando a conta master envia PUT /usuarios/usr-ebfga-maria/permissoes com <versão>
    Então a resposta é <resposta>

    Exemplos:
      | versão       | resposta                                 |
      | If-Match "7" | 200, gravado                             |
      | If-Match "6" | 412 VERSAO_DESATUALIZADA, nada é gravado |
      | sem If-Match | 428 VERSAO_AUSENTE, nada é gravado       |

  @RNF-223
  Cenário: tudo junto
    Quando a conta master envia PUT /usuarios/usr-ebfga-maria/permissoes
    Então as permissões, o registro e a versão nova são gravados numa só operação
    E a versão nova chega às sessões da Maria em até 1 s depois da resposta

  @RNF-224
  Cenário: código da recusa
    Dado a Maria com um token de versão de permissões antiga
    Quando envia qualquer pedido com esse token
    Então a resposta é 401 PERMISSOES_DESATUALIZADAS
    E POST /auth/renovar devolve o token novo, sem encerrar a sessão

  @fora-de-escopo
  Cenário: o que este documento não trata
    Então como a pessoa prova quem é e quanto tempo a sessão vale fica em Autenticação
    E limites dentro de um módulo, como turmas ou registros, ficam no documento do módulo
    E o desenho da tela Permissões fica nas vistas de Usuários
    E permissões por ação, como ver, criar, editar e apagar, ficam em cada módulo
