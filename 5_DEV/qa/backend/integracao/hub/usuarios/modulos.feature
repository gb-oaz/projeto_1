# language: pt
# Cenários de API de 1_Requisitos/15_modulos.md (v29), copiados do requisito:
# mudou lá, muda aqui. Cada cenário leva a tag do ID que cobre.
Funcionalidade: Módulos · card do Usuários
  1_Requisitos/15_modulos.md · Cenários de API

  @RN-276
  Esquema do Cenário: campos do card
    Dado a conta master da EBFGA com o token de acesso e o "MOD-03" contratado
    Quando envia GET /usuarios/card
    Então a resposta é 200 e traz <campo> com <conteudo>

    Exemplos:
      | campo        | conteudo                                                     |
      | modulo       | o ID do módulo, "MOD-03"                                     |
      | perfil       | o perfil de quem pede, "INSTITUICAO"                         |
      | versao       | o número que sobe a cada mudança dos números                 |
      | atualizadoEm | a data e a hora da última mudança, em UTC                    |
      | vazio        | true quando o módulo ainda não tem dados                     |
      | selo         | o número do selo, 0 sem nada a avisar                        |
      | reduzida     | valor e rotulo do dado macro                                 |
      | normal       | valor, rotulo e até 3 apoio, cada um com nome e valor        |
      | expandida    | até 3 linhas em lista, cada uma com nome e valor             |
      | series       | dia, semana e mes, 12 números cada, do mais antigo ao atual  |

  @RN-276
  Cenário: todos os tamanhos e nenhum ícone
    Quando a conta master envia GET /usuarios/card
    Então a resposta traz a reduzida, a normal e a expandida, sem pedir o tamanho
    E não traz o ícone, que vem do Catálogo

  @RN-276
  Cenário: perfil fora da ficha
    Dado a ficha do Usuários sem o participante entre os perfis do card
    Quando o Lucas Andrade envia GET /usuarios/card
    Então a resposta é 403 ACESSO_NEGADO

  @RN-278
  Cenário: atualizado na mesma execução da mudança
    Dado o card do Usuários da EBFGA na versao 418, com 298 participantes ativos
    Quando o staff João envia POST /usuarios e cria o participante Lucas Andrade com 201
    # requisito: "Então," não é palavra-chave do Gherkin (o Reqnroll não compila); vírgula tirada aqui até o deboaz-po corrigir
    Então na mesma execução, o card guardado passa à versao 419 com 299 participantes
    E o próximo GET /usuarios/card traz 299, lido do card guardado

  @RN-278
  Cenário: fechamento do dia, da semana e do mês
    Dado o card do Usuários da EBFGA com 400 dias, 106 semanas e 24 meses guardados
    Quando chega 00:00 de 2027-03-01, segunda-feira, no horário de Brasília
    Então o card guardado abre o dia 2027-03-01, a semana dele e o mês 2027-03
    E sai o dia, a semana e o mês mais antigos, para manter 400, 106 e 24

  @RN-278
  Cenário: números que mudam só com o tempo
    Dado o Lucas Andrade com o último acesso em 2026-10-01, sem mudança desde então
    Quando a conta master envia GET /usuarios/card em 2026-11-08
    Então o Lucas conta em "Sem acesso há 30 dias"
    E essa contagem, o selo e "Códigos vencidos" são refeitos na leitura e no fechamento do dia
    E a versao não sobe por eles

  @RN-279
  Cenário: conectar com o intervalo
    Dado a conta master da EBFGA com o token de acesso
    Quando envia GET /usuarios/card/ao-vivo?intervalo=30 com Accept "text/event-stream"
    Então a resposta é 200 e a conexão fica aberta
    E o primeiro evento "card" traz o mesmo corpo de GET /usuarios/card, com id igual à versao
    E a cada 30 s chega um evento "card", se a versao mudou
    E um ": ping" chega a cada 30 s, qualquer que seja o intervalo, para manter a conexão

  @RN-279
  Esquema do Cenário: intervalo pedido
    Quando a conta master envia GET /usuarios/card/ao-vivo <pedido>
    Então <resposta>

    Exemplos:
      | pedido          | resposta                                                     |
      | ?intervalo=5    | é 200, com um "card" a cada 5 s, se a versao mudou           |
      | ?intervalo=300  | é 200, com um "card" a cada 300 s e um ": ping" a cada 30 s  |
      | sem o parâmetro | é 200, como ?intervalo=300, o padrão                         |
      | ?intervalo=10   | é 422 DADOS_INVALIDOS com o campo "intervalo"                |
      | ?intervalo=0    | é 422 DADOS_INVALIDOS com o campo "intervalo"                |

  @RN-279
  Cenário: quarta conexão do mesmo usuário
    Dado a conta master com 3 conexões ao vivo abertas: celular, tablet e computador
    Quando ela envia GET /usuarios/card/ao-vivo de um segundo computador
    Então a 4ª conexão recebe 200 e fica aberta
    E a mais antiga recebe o evento "encerrado" com o código "LIMITE_CONEXOES" e fecha
