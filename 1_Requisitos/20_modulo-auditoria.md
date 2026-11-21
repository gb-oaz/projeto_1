---
projeto: "boaz"
documento: "20_modulo-auditoria"
tipo: "Funcionalidade"
titulo: "Auditoria"
versao: "v08"
data: "2026-11-21 00:14"
status: "Rascunho"
objetivo: "permitir que o time deboaz veja, num só lugar, a saúde de cada serviço, o tempo de resposta de cada módulo e cada registro da auditoria, ao vivo ou por período, para achar o que está fora do ar e validar o caminho de cada execução."
---

> **boaz · Funcionalidade** · `20_modulo-auditoria` · **v08** · 2026-11-21 00:14 · **Rascunho**

# Auditoria

**Objetivo:** permitir que o time deboaz veja, num só lugar, a saúde de cada serviço, o tempo de resposta de cada módulo e cada registro da auditoria, ao vivo ou por período, para achar o que está fora do ar e validar o caminho de cada execução.

**Sumário:** [Cenários de tela](#cenarios-tela) · [Cenários de API](#cenarios-api) · [Telas](#telas) · [Diagramas](#diagramas) · [Histórico](#historico)

---

<a id="cenarios-tela"></a>

## Cenários de tela

<a id="exemplo-queda"></a>

### Exemplo de uma queda do começo ao fim

> Registro do módulo: [15_modulos `MOD-17`](15_modulos.md#MOD-17)

```gherkin
Cenário: o intermediador de pagamento que parou de responder
  Dado o Caio Lima, do time deboaz, com a Auditoria aberta em produção, em 2026-11-20
  E o Caio na aba Registros
  Quando às 10:30:00 o intermediador de pagamento não responde em 3 s
  Então em até 5 s a faixa de ocorrências mostra "Intermediador de pagamento fora do ar desde 10:30"
  E a aba Saúde mostra "1 serviço fora do ar · Produção", sem o Caio recarregar
  E às 10:31 o Pagamento aparece "Lento" em Módulos, com "4,0 % de erros"
  E na aba Registros, com o nível "Erro", o Caio vê "BANCO_INDISPONIVEL" de "Criar pagamento"
  E ao tocar no registro, vê a execução "trc-4b7e01c9" com o resultado "Falhou"
  E às 10:52 o intermediador volta e a linha mostra "No ar" e "Fora do ar por 22 min"
  E a faixa mostra "Intermediador de pagamento voltou às 10:52, após 22 min fora do ar"
```

<a id="RF-194"></a>

### `RF-194` Saúde dos serviços

```gherkin
Cenário: um serviço fora do ar
  Dado o Caio Lima, membro do time com a Utilização da Auditoria, em produção
  E em 2026-11-20 às 10:42 o intermediador de pagamento fora do ar desde 10:30
  Quando abre a Auditoria
  Então vê a aba "Saúde" com o aviso "1 serviço fora do ar · Produção"
  E o "Intermediador de pagamento" no topo da lista, com "Fora do ar desde 10:30"
  E abaixo dele "Sem resposta em 3 s"
  E os outros serviços em seguida, cada um com o estado e o tempo da última resposta
  E no fim da lista "Verificado às 10:42:30 · 2 instâncias · versão 1.4.2"

Cenário: tudo no ar
  Dado todos os serviços no ar, em homologação
  Quando o Caio abre a Auditoria
  Então vê "Tudo no ar · Homologação", com o mascote Orgulhoso
  E o mascote aparece animado, e parado quando o aparelho pede menos movimento

Cenário: serviço desligado no ambiente
  Dado o armazenamento de objetos desligado no ambiente local
  Quando o Caio abre a aba Saúde
  Então a linha "Armazenamento de anexos" mostra "Desligado neste ambiente", sem selo
  E essa linha não conta no aviso do topo

Esquema do Cenário: aviso do topo
  Dado <situacao>, em produção
  Quando o Caio abre a Auditoria
  Então vê o aviso "<aviso>" com o selo <selo>

  Exemplos:
    | situacao                                   | aviso                            | selo       |
    | o intermediador fora do ar e o cache lento | 1 serviço fora do ar · Produção  | Fora do ar |
    | o intermediador e o cache fora do ar       | 2 serviços fora do ar · Produção | Fora do ar |
    | o cache lento e os outros no ar            | 1 serviço lento · Produção       | Lento      |
    | o cache e o banco de dados lentos          | 2 serviços lentos · Produção     | Lento      |
```

<a id="RF-195"></a>

### `RF-195` Histórico de um serviço

```gherkin
Cenário: quedas das últimas 24 h
  Dado o intermediador de pagamento fora do ar hoje de 10:30 a 10:52 e de 03:10 a 03:12
  Quando o Caio toca na linha "Intermediador de pagamento" às 11:00
  Então vê "Disponível 98,33 % nas últimas 24 h"
  E as quedas "Hoje, 10:30 a 10:52 · 22 min" e "Hoje, 03:10 a 03:12 · 2 min", a mais nova no topo
  E o tempo médio de resposta em série, um ponto por hora

Esquema do Cenário: período do histórico
  Quando o Caio escolhe "<periodo>" no histórico do intermediador
  Então vê a disponibilidade e as quedas <de quando>
  E a série com <pontos>

  Exemplos:
    | periodo         | de quando                | pontos                   |
    | Últimas 24 h    | das últimas 24 h         | 24 pontos, um por hora   |
    | Últimos 7 dias  | dos últimos 7 dias       | 7 pontos, um por dia     |
    | Últimos 30 dias | dos últimos 30 dias      | 30 pontos, um por dia    |

Cenário: sem quedas
  Dado o banco de dados sem queda nos últimos 7 dias
  Quando o Caio abre o histórico do banco de dados em "Últimos 7 dias"
  Então vê "Disponível 100 % nos últimos 7 dias" e "Nenhuma queda neste período"
```

<a id="RF-196"></a>

### `RF-196` Módulos no ar

```gherkin
Cenário: módulos nos últimos 5 minutos
  Dado em 2026-11-20 às 10:42 o Pagamento com 250 pedidos nos últimos 5 min, 10 com erro 5xx
  E essa é a 1ª verificação seguida com erros acima de 1 %
  Quando o Caio abre a aba Saúde
  Então vê a seção "Módulos" com o Pagamento "Lento", "4,0 % de erros" e "p95 até 500 ms"
  E o SAC "No ar" com "p95 até 250 ms", e o Catálogo com "Sem pedidos em 5 min"
  E cada módulo com o número de pedidos dos últimos 5 min

Cenário: abrir as métricas do módulo
  Quando o Caio toca no Pagamento em Módulos
  Então a aba "Métricas" abre com o módulo "Pagamento" e a janela "Última hora"
```

<a id="RF-197"></a>

### `RF-197` Tempo de resposta

```gherkin
Cenário: última hora de todos os módulos
  Dado 1.240 pedidos entre 09:42 e 10:42 de 2026-11-20, 31 com erro 4xx e 2 com erro 5xx
  Quando o Caio abre a aba Métricas
  Então vê a janela "Última hora" marcada e "1.240 pedidos · 20,7 por minuto"
  E "p50 até 100 ms", "p95 até 500 ms" e "p99 até 1 s"
  E "Erros 4xx 31" e "Erros 5xx 2 · 0,16 %"
  E o gráfico de pedidos e do p95 minuto a minuto, com 60 pontos

Esquema do Cenário: janelas
  Quando o Caio escolhe a janela "<janela>"
  Então o gráfico mostra <pontos>

  Exemplos:
    | janela          | pontos                    |
    | Última hora     | 60 pontos, um por minuto  |
    | Últimas 24 h    | 24 pontos, um por hora    |
    | Últimos 7 dias  | 7 pontos, um por dia      |
    | Últimos 30 dias | 30 pontos, um por dia     |

Cenário: filtro por módulo e ao vivo
  Quando o Caio escolhe o módulo "SAC" no filtro
  Então os números e o gráfico contam só os pedidos do SAC
  E com "Ao vivo" marcado, os números e o gráfico se atualizam a cada 1 min
```

<a id="RF-198"></a>

### `RF-198` Tempo de cada endereço

```gherkin
Cenário: endereços mais lentos
  Dado a janela "Última hora" e o filtro "Todos os módulos"
  Quando o Caio rola até "Endereços"
  Então vê até 20 endereços, o de maior p95 no topo
  E a linha "POST /pagamento/faturas/{id}/pagamentos" com "42 pedidos" e "p95 até 3 s"
  E "Erros 5xx 2" e o selo Lento, porque o p95 passou de 1 s

Esquema do Cenário: ordem dos endereços
  Quando o Caio escolhe a ordem "<ordem>"
  Então o primeiro endereço é <primeiro>

  Exemplos:
    | ordem        | primeiro                                          |
    | Mais lentos  | o de maior p95                                    |
    | Mais pedidos | o que recebeu mais pedidos na janela              |
    | Mais erros   | o de mais erros 5xx; no empate, o de mais 4xx     |

Cenário: endereço sem dado de ninguém
  Quando o Caio vê a lista de endereços
  Então cada endereço aparece pelo modelo, como "GET /sac/chamados/{id}"
  E nenhum mostra o número de um chamado, de uma fatura ou de um usuário
```

<a id="RF-199"></a>

### `RF-199` Registros da auditoria

> Regras da auditoria: [17_modulo-usuario `RN-178`](17_modulo-usuario.md#RN-178), [`RN-270`](17_modulo-usuario.md#RN-270) e [`RN-271`](17_modulo-usuario.md#RN-271)

```gherkin
Cenário: últimas 24 h, o mais novo no topo
  Dado a staff Maria Souza, da EBFGA, criando o participante Lucas em 2026-11-20 às 10:41:07
  Quando o Caio abre a aba Registros às 10:42
  Então vê o período "Últimas 24 h" e o registro mais novo no topo
  E a linha "10:41:07 · Criar usuário · Concluído", com "Maria Souza · EBFGA"
  E o alvo "usr-ebfga-lucas", e logo abaixo "10:41:07 · Criar usuário · Iniciado"
  E cada linha mostra a hora, a ação, a etapa ou o nível, quem, a conta e o alvo

Cenário: registro de recusa
  Dado o pedido da Maria recusado com MATRICULA_EM_USO às 10:39:52
  Quando o Caio vê a lista
  Então a linha "10:39:52 · Criar usuário · Alerta" mostra o código "MATRICULA_EM_USO"
  E as linhas "Alerta" e "Erro" têm o selo do nível

Cenário: mais registros
  Dado 130 registros nas últimas 24 h
  Quando o Caio rola até o fim da lista
  Então os próximos 50 aparecem embaixo, sem perder a rolagem
  E no fim dos 130 aparece "Fim dos registros deste período"
```

<a id="RF-200"></a>

### `RF-200` Filtros e busca dos registros

```gherkin
Esquema do Cenário: filtros dos registros
  Dado o período "Últimas 24 h"
  Quando o Caio escolhe <filtro>
  Então vê só <resultado>

  Exemplos:
    | filtro                   | resultado                                           |
    | o nível "Erro"           | os registros de falha inesperada                    |
    | o nível "Alerta"         | as recusas previstas pela regra                     |
    | o módulo "SAC"           | os registros das ações do SAC                       |
    | a ação "Criar usuário"   | os "Iniciado", "Concluído" e recusas dessa ação     |
    | a conta "EBFGA"          | os registros com a conta da EBFGA                   |
    | quem "Maria Souza"       | o que a Maria fez                                   |
    | o código "MATRICULA_EM_USO" | as recusas com esse código                       |

Cenário: buscar pelo código da execução
  Dado o código "trc-4b7e01c9" informado pela Rita Gomes, da EBFGA, num chamado do SAC
  Quando o Caio digita "trc-4b7e01c9" na busca "Buscar pelo código da execução" dos Registros
  Então abre a execução "trc-4b7e01c9", sem depender do período escolhido

Cenário: nenhum registro
  Quando o Caio escolhe o período de 2026-11-01 00:00 a 2026-11-02 00:00 e a conta "COLSA"
  Então vê "Nenhum registro neste período", com o mascote Sonolento
  E o botão "Limpar filtros", que volta às "Últimas 24 h" sem filtro
```

<a id="RF-201"></a>

### `RF-201` Registros ao vivo

```gherkin
Cenário: ligar o ao vivo
  Dado o Caio na aba Registros, com o período "Últimas 24 h"
  Quando toca em "Ao vivo"
  Então o seletor de período some e aparece "Ao vivo · chegando agora"
  E cada registro novo entra no topo em até 5 s
  E os filtros de nível, módulo, ação e conta continuam valendo

Cenário: pausar ou rolar para baixo
  Dado o Caio com "Ao vivo" marcado
  Quando toca em "Pausar" ou rola a lista para baixo
  Então a lista para de mudar, e os registros que chegam somam no botão "12 novos", no topo
  E tocar em "12 novos" mostra os 12 no topo e volta a seguir ao vivo

Cenário: lista ao vivo cheia
  Dado 200 registros na lista ao vivo
  Quando chega o 201º
  Então o mais antigo sai do fim da lista
  E tocar em "Período" volta à consulta, com todos os registros guardados
```

<a id="RF-202"></a>

### `RF-202` Execução do começo ao fim

```gherkin
Cenário: execução concluída
  Dado a execução "trc-1d6e3f08" da Maria, que desativou o Paulo às 10:20:14
  Quando o Caio toca num registro dessa execução
  Então vê "Desativar usuário · Concluída em 184 ms", com "Maria Souza · EBFGA"
  E a linha do tempo com "Desativar usuário · Iniciado · 10:20:14,102"
  E recuadas, "Encerrar sessões do usuário · Iniciado" e "Concluído", ações internas
  E por último "Desativar usuário · Concluído · 10:20:14,286"
  E o botão "Copiar código", que copia "trc-1d6e3f08"

Cenário: execução que falhou
  Dado a execução "trc-4b7e01c9" da Rita Gomes, recusada com 503 às 10:41:32
  Quando o Caio abre a execução
  Então vê "Criar pagamento · Falhou em 2,1 s" com o selo de erro
  E o último passo "Erro · BANCO_INDISPONIVEL · 10:41:32,207", sem "Concluído"

Cenário: execução não encontrada
  Quando o Caio busca "trc-00000000"
  Então vê "Execução não encontrada" e "Confira o código e tente de novo."
```

<a id="RF-203"></a>

### `RF-203` Execuções com problema

```gherkin
Cenário: contagem e lista das execuções com problema
  Dado 412 execuções nas últimas 24 h: 398 concluídas, 9 recusadas, 4 que falharam
  E 1 interrompida
  Quando o Caio abre a aba Execuções
  Então vê "Concluídas 398", "Recusadas 9", "Falharam 4" e "Interrompidas 1"
  E o filtro "Com problema" marcado, com as 4 que falharam e a interrompida
  E a mais nova no topo, cada linha com a hora, a ação, o resultado, quem e a conta

Cenário: execução interrompida
  Dado a execução "trc-90ab12cd" de "Mudar estado da conta", iniciada às 09:58
  E sem "Concluído" nem erro até 10:42
  Quando o Caio abre a execução
  Então vê "Mudar estado da conta · Interrompida" com o selo de erro
  E "Iniciada há 44 min, sem conclusão nem erro"

Esquema do Cenário: filtro do resultado
  Quando o Caio escolhe o filtro "<filtro>"
  Então vê só as execuções <quais>

  Exemplos:
    | filtro        | quais                                            |
    | Com problema  | que falharam ou foram interrompidas              |
    | Recusadas     | recusadas pela regra                             |
    | Concluídas    | concluídas                                       |
    | Todas         | de todos os resultados, até as em andamento      |
```

<a id="RF-204"></a>

### `RF-204` Faixa de ocorrências ao vivo

```gherkin
Cenário: serviço que cai com a Auditoria aberta
  Dado o Caio Lima na aba Métricas, em produção, em 2026-11-20
  E a Ana Prado, do time deboaz, na aba Execuções, em outro computador
  Quando às 10:30:00 o intermediador de pagamento fica fora do ar
  Então em até 5 s os dois veem no topo do módulo a faixa de erro
  E nela "Intermediador de pagamento fora do ar desde 10:30", sem recarregar
  E o botão "Ocorrências" do topo passa a mostrar o selo "1"
  E o leitor de tela anuncia "Intermediador de pagamento fora do ar"

Esquema do Cenário: o que a faixa mostra
  Dado o Caio com a Auditoria aberta em qualquer aba
  Quando <alvo> <mudanca>
  Então a faixa mostra "<texto>" com o selo <selo>

  Exemplos:
    | alvo        | mudanca             | texto                              | selo    |
    | o cache     | fica lento às 10:31 | Cache lento desde 10:31            | alerta  |
    | o Pagamento | fica lento às 10:31 | Pagamento lento desde 10:31        | alerta  |
    | o cache     | cai às 10:33        | Cache fora do ar desde 10:33       | erro    |
    | o cache     | volta às 10:41      | Cache voltou às 10:41, após 10 min | sucesso |

Cenário: mais de uma ocorrência aberta
  Dado o intermediador fora do ar desde 10:30 e o cache e o Pagamento lentos desde 10:31
  Quando o Caio vê a faixa
  Então ela mostra a mais grave e, entre as iguais, a mais nova
  E "Intermediador de pagamento fora do ar desde 10:30" com "+2 ocorrências"
  E o selo do botão "Ocorrências" mostra "3"
  E tocar na faixa abre a lista de ocorrências em tela cheia

Cenário: a volta some da faixa
  Dado a faixa com "Cache voltou às 10:41, após 10 min"
  Quando passam 10 min ou o Caio toca em "Dispensar"
  Então a volta sai da faixa só para ele, e a faixa some se nada mais estiver aberto
  E uma ocorrência aberta não tem "Dispensar": fica até o serviço ou o módulo voltar

Cenário: abrir a Auditoria com ocorrência aberta
  Dado o intermediador fora do ar desde 10:30
  Quando a Ana abre a Auditoria às 10:45
  Então a faixa já aparece com "Intermediador de pagamento fora do ar desde 10:30"

Cenário: faixa só nas quatro abas
  Dado o Caio no histórico do "Banco de dados", aberto pela aba Saúde
  Quando o cache fica lento às 10:31
  Então a faixa não aparece no histórico, nem na tela de uma execução
  E o selo do botão "Ocorrências" do topo passa a "1" em até 5 s
  E ao voltar à aba Saúde a faixa mostra "Cache lento desde 10:31"
```

<a id="RF-205"></a>

### `RF-205` Lista de ocorrências

```gherkin
Cenário: ocorrências das últimas 24 h
  Dado a queda do intermediador de 10:30 a 10:52 e o cache lento de 10:31 a 10:41
  Quando o Caio toca em "Ocorrências" às 11:00
  Então a lista abre em tela cheia, com "Voltar" para a aba de onde ele veio
  E vê o período "Últimas 24 h" e a ocorrência mais nova no topo
  E "10:52 · Intermediador de pagamento · Fora do ar → No ar · 22 min"
  E "10:41 · Cache · Lento → No ar · 10 min"
  E "10:31 · Cache · No ar → Lento" e "10:30 · Intermediador de pagamento · No ar → Fora do ar"
  E as abertas no topo da lista, em "Abertas agora", antes das outras

Esquema do Cenário: filtros das ocorrências
  Quando o Caio escolhe <filtro> na lista de ocorrências
  Então vê só <resultado>

  Exemplos:
    | filtro                                  | resultado                                  |
    | "Serviços"                              | as ocorrências dos serviços                |
    | "Módulos"                               | as ocorrências dos módulos                 |
    | o "Intermediador de pagamento"          | as do intermediador                        |
    | o estado "Fora do ar"                   | as mudanças para fora do ar                |
    | o estado "Lento"                        | as mudanças para lento                     |
    | o estado "No ar"                        | as voltas, com a duração                   |
    | o período "Últimos 7 dias"              | as dos últimos 7 dias                      |

Cenário: nenhuma ocorrência
  Dado nenhum serviço nem módulo saiu de "No ar" nos últimos 7 dias
  Quando o Caio abre a lista em "Últimos 7 dias"
  Então vê "Nenhuma ocorrência neste período", com o mascote Orgulhoso

Cenário: ocorrência chegando com a lista aberta
  Dado o Caio com a lista de ocorrências aberta em "Últimas 24 h"
  Quando o cache fica lento às 11:05
  Então "11:05 · Cache · No ar → Lento" entra em "Abertas agora" em até 5 s
```

<a id="RN-314"></a>

### `RN-314` Quem usa a Auditoria

```gherkin
Esquema do Cenário: quem vê a Auditoria
  Quando <quem> abre Módulos no hub
  Então <resultado>

  Exemplos:
    | quem                                    | resultado                                    |
    | o administrador do deboaz               | vê o card da Auditoria e as quatro abas      |
    | o membro do time Caio, com a Auditoria  | vê o card da Auditoria e as quatro abas      |
    | o membro do time Rui, sem a Auditoria   | não vê o card da Auditoria                   |
    | a conta master da EBFGA                 | não vê o card da Auditoria                   |
    | a staff Maria, da EBFGA                 | não vê o card da Auditoria                   |
    | o educador autônomo Pedro Reis          | não vê o card da Auditoria                   |
    | o participante Lucas, no quest          | não vê a Auditoria                           |

Cenário: abas do módulo
  Quando o Caio abre a Auditoria
  Então vê as abas "Saúde", "Métricas", "Registros" e "Execuções", nessa ordem
  E vê os registros de todas as contas e os feitos sem login ou pelo sistema

Cenário: acesso retirado com a Auditoria aberta
  Dado o Caio com a aba Registros ao vivo
  Quando o administrador retira dele a Utilização da Auditoria
  Então o Caio vê o snackbar "Ao vivo pausado: sem acesso a este módulo"
  E na próxima ação volta a Módulos, sem o card da Auditoria
```

<a id="RN-315"></a>

### `RN-315` Estado de um serviço

```gherkin
Esquema do Cenário: estado pela verificação
  Dado a verificação a cada 30 s, com até 3 s para a resposta
  E o "Banco de dados" "No ar"
  Quando o banco de dados <resposta>
  Então a linha mostra "<estado>"

  Exemplos:
    | resposta                                         | estado                     |
    | responde em 12 ms                                | No ar                      |
    | responde em 1 s                                  | No ar                      |
    | responde em 1,001 s                              | Lento                      |
    | responde em 3 s                                  | Lento                      |
    | não responde em 3 s                              | Fora do ar na hora         |
    | responde com erro, pela 1ª ou 2ª vez seguida     | Lento                      |
    | responde com erro, pela 3ª vez seguida           | Fora do ar desde a 1ª      |
    | recusa a conexão 3 vezes seguidas                | Fora do ar desde a 1ª      |
    | responde em até 1 s, depois de fora do ar        | No ar                      |

Esquema do Cenário: o que cada serviço mostra
  Quando o Caio vê a linha "<servico>"
  Então ela mostra <o que>

  Exemplos:
    | servico                     | o que                                                   |
    | Aplicação                   | as instâncias no ar e a versão de cada uma              |
    | Banco de dados              | o estado e o tempo da última resposta                   |
    | Cache                       | o estado e o tempo da última resposta                   |
    | Armazenamento de anexos     | o estado e o tempo, ou "Desligado neste ambiente"       |
    | Verificação de anexos       | o estado e o tempo, ou "Desligado neste ambiente"       |
    | Intermediador de pagamento  | o estado e o tempo, ou "Desligado neste ambiente"       |
    | Agendamento do SAC          | o estado e "Última execução às 10:42"                   |
    | Agendamento do Pagamento    | o estado e "Última execução às 10:42"                   |

Esquema do Cenário: agendamentos que rodam a cada 1 min
  Dado a última execução do Agendamento do SAC há <tempo>
  Então a linha mostra "<estado>"

  Exemplos:
    | tempo          | estado      |
    | 2 min          | No ar       |
    | 2 min e 1 s    | Lento       |
    | 5 min          | Lento       |
    | 5 min e 1 s    | Fora do ar  |

Cenário: agendamento que terminou com erro
  Dado a última execução do Agendamento do SAC há 1 min, terminada com erro
  Então a linha mostra "No ar", porque o estado conta só a hora da última execução
  E o erro aparece em Registros e em Execuções, na ação "Fechar por prazo"
```

<a id="RN-316"></a>

### `RN-316` Estado de um módulo

```gherkin
Esquema do Cenário: estado pelos pedidos dos últimos 5 min
  Dado o Pagamento com <pedidos> pedidos nos últimos 5 min, <erros> com erro 5xx
  E o p95 desses pedidos <p95>, na <verificacao> verificação seguida assim
  Então o Pagamento aparece "<estado>" em Módulos

  Exemplos:
    | pedidos | erros | p95         | verificacao | estado                   |
    | 0       | 0     | sem medida  | 1ª          | Sem pedidos em 5 min     |
    | 250     | 0     | até 250 ms  | 1ª          | No ar                    |
    | 250     | 2     | até 250 ms  | 3ª          | No ar                    |
    | 250     | 0     | até 1 s     | 3ª          | No ar                    |
    | 250     | 3     | até 250 ms  | 1ª          | Lento                    |
    | 250     | 0     | até 3 s     | 3ª          | Lento                    |
    | 250     | 0     | até 5 s     | 2ª          | Lento                    |
    | 250     | 3     | até 250 ms  | 3ª          | Fora do ar               |
    | 250     | 0     | até 5 s     | 3ª          | Fora do ar               |

Cenário: o que conta como falha de um módulo
  Então uma verificação tem falha quando os erros 5xx passam de 1 % ou o p95 passa de 3 s
  E o módulo fica "Lento" na 1ª e na 2ª verificação seguida com falha
  E "Fora do ar" na 3ª, com "desde" na hora da 1ª
  E volta a "No ar" na 1ª verificação sem falha, com o p95 até 1 s

Esquema do Cenário: módulo de cada endereço
  Quando chega um pedido a <endereco>
  Então ele conta no módulo "<modulo>"

  Exemplos:
    | endereco                                        | modulo     |
    | /auth/hub/entrar, /usuarios, /contas            | Usuários   |
    | /sac/chamados, /sac/anexos, /sac/card           | SAC        |
    | /pagamento/faturas, /pagamento/recebimentos     | Pagamento  |
    | /catalogo/vitrine                               | Catálogo   |
    | /auditoria/saude, /auditoria/registros          | Auditoria  |
```

<a id="RN-317"></a>

### `RN-317` Faixas do tempo de resposta

```gherkin
Esquema do Cenário: percentil pela faixa
  Dado as faixas até 50 ms, 100 ms, 250 ms, 500 ms, 1 s, 3 s, 5 s, 10 s e acima de 10 s
  E 100 pedidos <distribuicao>
  Quando o Caio vê as métricas desses pedidos
  Então vê "p50 <p50>", "p95 <p95>" e "p99 <p99>"

  Exemplos:
    | distribuicao                                   | p50       | p95       | p99           |
    | 60 até 50 ms, 30 até 100, 6 até 250, 4 até 500 | até 50 ms | até 250 ms| até 500 ms    |
    | 49 até 50 ms e 51 até 100 ms                   | até 100 ms| até 100 ms| até 100 ms    |
    | 97 até 500 ms e 3 de 12 s                      | até 500 ms| até 500 ms| acima de 10 s |

Cenário: o percentil é o fim da faixa
  Então "p95 até 250 ms" quer dizer que 95 % dos pedidos terminaram em até 250 ms
  E o tempo exato de cada pedido não é guardado
```

<a id="RN-318"></a>

### `RN-318` Resultado de uma execução

> Ações internas: [17_modulo-usuario `RN-275`](17_modulo-usuario.md#RN-275)

```gherkin
Esquema do Cenário: resultado pelos registros do traceId
  Dado os registros da execução "trc-90ab12cd": <registros>
  Quando o Caio vê a execução às 10:42
  Então o resultado é "<resultado>"

  Exemplos:
    | registros                                         | resultado      |
    | "Iniciado" às 10:20 e "Concluído" da mesma ação   | Concluída      |
    | "Iniciado" às 10:20 e um "Alerta"                 | Recusada       |
    | "Iniciado" às 10:20 e um "Erro"                   | Falhou         |
    | só o "Iniciado", às 10:37:01                      | Em andamento   |
    | só o "Iniciado", às 10:37:00                      | Interrompida   |

Cenário: ação principal e ações internas
  Dado a execução "trc-1d6e3f08" com "Desativar usuário" e "Encerrar sessões do usuário"
  Então a ação principal é a do primeiro "Iniciado", "Desativar usuário"
  E o resultado e a duração vêm dela, do "Iniciado" ao "Concluído" ou ao erro
  E "Encerrar sessões do usuário" aparece recuada, como ação interna
```

<a id="RN-319"></a>

### `RN-319` Nome e módulo de cada ação

```gherkin
Esquema do Cenário: ação com nome
  Quando um registro da ação "<acao>" aparece na Auditoria
  Então mostra "<nome>", do módulo "<modulo>"
  E sem quem, mostra "<sem quem>"

  Exemplos:
    | acao                | nome                  | modulo    | sem quem  |
    | CRIAR_USUARIO       | Criar usuário         | Usuários  | Sem login |
    | CRIAR_CONTA         | Criar conta           | Usuários  | Sem login |
    | ABRIR_CHAMADO       | Abrir chamado         | SAC       | Sem login |
    | FECHAR_POR_PRAZO    | Fechar por prazo      | SAC       | Sistema   |
    | PAGAMENTO_CRIADO    | Criar pagamento       | Pagamento | Sem login |
    | AVISO_DO_BANCO      | Aviso do banco        | Pagamento | Banco     |
    | FATURA_VENCIDA      | Fatura vencida        | Pagamento | Sistema   |
    | CONSULTAR_AUDITORIA | Consultar a Auditoria | Auditoria | Sem login |

Cenário: ação nova sem nome
  Dado a ação "PUBLICAR_AGENDA", de um módulo novo, sem nome na Auditoria
  Quando um registro dela aparece
  Então mostra o próprio código "PUBLICAR_AGENDA", do módulo "Outros"
```

<a id="RN-320"></a>

### `RN-320` Só leitura

> Auditoria imutável: [17_modulo-usuario `RNF-206`](17_modulo-usuario.md#RNF-206)

```gherkin
Cenário: nada se altera pela Auditoria
  Quando o Caio abre um registro ou uma execução
  Então não há botão para editar, apagar nem reenviar
  E a consulta dele fica registrada na auditoria, como na RN-325
```

<a id="RN-321"></a>

### `RN-321` Período das consultas

```gherkin
Esquema do Cenário: período personalizado
  Quando o Caio escolhe na aba <aba> o período de <de> a <ate>
  Então <resultado>

  Exemplos:
    | aba       | de               | ate              | resultado                              |
    | Registros | 2026-10-20 10:42 | 2026-11-20 10:42 | vê os registros desses 31 dias         |
    | Registros | 2026-10-19 10:42 | 2026-11-20 10:42 | vê "Escolha um período de até 31 dias" |
    | Execuções | 2026-11-13 10:42 | 2026-11-20 10:42 | vê as execuções desses 7 dias          |
    | Execuções | 2026-11-12 10:42 | 2026-11-20 10:42 | vê "Escolha um período de até 7 dias"  |
    | Registros | 2026-11-20 10:42 | 2026-11-19 10:42 | vê "O fim deve ser depois do começo"   |

Cenário: períodos prontos
  Quando o Caio abre o seletor de período em Registros ou Execuções
  Então vê "Última hora", "Últimas 24 h", já marcado, "Últimos 7 dias" e "Personalizado"
  E as horas aparecem no horário de Brasília
```

<a id="RN-322"></a>

### `RN-322` Card da Auditoria

> Card comum: [15_modulos `RN-276`](15_modulos.md#RN-276), com os recortes de [`RN-277`](15_modulos.md#RN-277) e a atualização de [`RN-279`](15_modulos.md#RN-279)

```gherkin
Esquema do Cenário: dados do card por tamanho
  Dado o card da Auditoria do Caio em 2026-11-20, com o intermediador fora do ar
  E 4 erros e 31 alertas hoje, 1 execução interrompida e 7 erros ontem
  Quando está na <tamanho>
  Então mostra <dados>

  Exemplos:
    | tamanho   | dados                                                              |
    | mínima    | o ícone e o selo "1": serviços fora do ar                          |
    | reduzida  | 1 · serviço fora do ar                                             |
    | normal    | 4 erros hoje, "-3 no dia", Fora do ar 1, Alertas 31, Interrompidas 1 |
    | expandida | a lista Fora do ar 1, Erros hoje 4 e Interrompidas 1, e a série    |

Cenário: tudo no ar
  Dado todos os serviços no ar e nenhum erro hoje
  Então o card mostra o ícone sem selo, "0 erros hoje" e "Tudo no ar"

Cenário: série do card
  Quando o Caio vê o card na normal, no recorte "Dia"
  Então a série mostra os erros de cada um dos 5 últimos dias
```

<a id="RN-323"></a>

### `RN-323` Carregando, erro e sem conexão

> Queda do ao vivo: [15_modulos `RN-282`](15_modulos.md#RN-282)

```gherkin
Esquema do Cenário: estados de cada aba
  Quando a aba <aba> <situacao>
  Então mostra <tela>

  Exemplos:
    | aba       | situacao         | tela                                                        |
    | Saúde     | carrega          | as linhas dos serviços em esqueleto, sem números            |
    | Registros | carrega          | 8 linhas em esqueleto                                       |
    | Métricas  | recebe um erro   | "Não foi possível carregar a Auditoria." e "Tentar de novo" |
    | Execuções | recebe um erro   | "Não foi possível carregar a Auditoria." e "Tentar de novo" |
    | Saúde     | fica sem conexão | "Sem conexão. Dados de 10:42:30." e os últimos números      |
    | Registros | perde o ao vivo  | "reconectando…" no topo e a lista como estava               |

Cenário: ao vivo da Saúde
  Dado o Caio com a aba Saúde aberta
  Então ela já abre com "Ao vivo" marcado e se atualiza a cada verificação
  E tocar em "Pausar" mantém os números até ele tocar em "Ao vivo" de novo
```

<a id="RN-324"></a>

### `RN-324` O que vira ocorrência

```gherkin
Esquema do Cenário: mudança de estado
  Dado <alvo> em "<antes>"
  Quando a verificação o mostra em "<depois>"
  Então <resultado>

  Exemplos:
    | alvo            | antes      | depois          | resultado                                 |
    | o cache         | No ar      | Lento           | abre a ocorrência "No ar → Lento"         |
    | o cache         | Lento      | Fora do ar      | a ocorrência passa a "Lento → Fora do ar" |
    | o cache         | Fora do ar | No ar           | fecha, com a duração desde "No ar"        |
    | o Pagamento     | No ar      | Fora do ar      | abre a ocorrência "No ar → Fora do ar"    |
    | o cache         | Lento      | Lento           | nenhuma ocorrência nova                   |
    | o Catálogo      | No ar      | Sem pedidos     | nenhuma ocorrência                        |
    | o armazenamento | Desligado  | Desligado       | nenhuma ocorrência                        |
    | o Pagamento     | Lento      | Sem pedidos     | fecha, como volta a "No ar"               |
    | o cache         | Fora do ar | Sem verificação | fecha, como volta a "No ar"               |
    | o armazenamento | Fora do ar | Desligado       | fecha, como volta a "No ar"               |


Cenário: só três estados nas ocorrências
  Então "Sem pedidos", "Sem verificação" e "Desligado" contam como "No ar" nas ocorrências
  E por isso o tráfego que para fecha a ocorrência aberta do módulo, com a duração até ali

Cenário: uma ocorrência por mudança, entre todas as instâncias
  Dado 2 instâncias da aplicação no ar
  Quando o cache fica lento
  Então a lista de ocorrências ganha uma só "No ar → Lento", com a hora da verificação
  E todos os membros do time com a Auditoria aberta veem a mesma faixa

Cenário: a duração da volta
  Dado o cache "Lento" às 10:31 e "Fora do ar" às 10:33
  Quando ele volta a "No ar" às 10:41
  Então a volta mostra "após 10 min", contados desde que saiu de "No ar"
```

<a id="RN-325"></a>

### `RN-325` Consulta auditada

> Exceção à leitura não auditada: [17_modulo-usuario `RN-178`](17_modulo-usuario.md#RN-178)

```gherkin
Esquema do Cenário: cada consulta fica registrada
  Dado o Caio Lima com a Auditoria aberta em 2026-11-20
  Quando ele <consulta> às 10:42:05
  Então os registros mostram "10:42:05 · Consultar a Auditoria · Concluído" do Caio
  E com o alvo "<alvo>"

  Exemplos:
    | consulta                              | alvo                                               |
    | abre a aba Saúde                      | /auditoria/saude/ao-vivo                           |
    | abre o histórico do intermediador     | /auditoria/saude/intermediador-pagamento/historico |
    | abre a aba Métricas                   | /auditoria/metricas?janela=1h                      |
    | filtra os Registros pelo nível "Erro" | /auditoria/registros?nivel=ERRO                    |
    | liga os Registros ao vivo             | /auditoria/registros/ao-vivo                       |
    | abre a execução "trc-4b7e01c9"        | /auditoria/execucoes/trc-4b7e01c9                  |
    | abre a lista de ocorrências           | /auditoria/ocorrencias                             |

Cenário: a mesma consulta seguida conta uma vez
  Dado o Caio com a aba Métricas ao vivo, que se atualiza a cada 1 min
  Quando passam 10 min sem ele mudar a janela nem o módulo
  Então os registros mostram 2 "Consultar a Auditoria" dele, um a cada 5 min
  E a mesma consulta da mesma pessoa em menos de 5 min não ganha registro novo

Cenário: o que não é consulta
  Quando o Caio vê o card da Auditoria no hub ou a faixa de ocorrências
  Então nada disso ganha registro
  E uma consulta recusada, sem acesso ou com dado inválido, também não, como na RN-270 do 17

Cenário: quem leu a Auditoria
  Dado a Ana e o Caio consultando a Auditoria hoje
  Quando o administrador filtra os Registros pela ação "Consultar a Auditoria"
  Então vê quem consultou, o que consultou, pelo alvo, e quando
```

<a id="RNF-246"></a>

### `RNF-246` Verificação a cada 30 s

```gherkin
Cenário: frequência e tempo limite
  Então cada serviço é verificado a cada 30 s, com até 3 s para responder
  E cada módulo é avaliado na mesma verificação, pelos pedidos dos últimos 5 min
  E a aba Saúde aberta mostra a verificação nova em até 5 s depois dela, sem recarregar

Cenário: cada instância se verifica
  Dado 2 instâncias da aplicação no ar
  Então cada uma verifica os serviços por conta própria
  E só uma delas por vez guarda a verificação no histórico
```

<a id="RNF-247"></a>

### `RNF-247` Medição de todo pedido

```gherkin
Cenário: o que é medido
  Então todo pedido aos módulos tem o tempo medido da chegada ao fim da resposta
  E entra nas métricas em até 2 min
  E a medição guarda o método, o modelo do endereço, o módulo, a faixa do tempo e o status

Cenário: o que não é medido nem guardado
  Então os pedidos a /health, aos ganchos de teste e a endereços que não existem ficam fora
  E as conexões ao vivo ficam fora, porque ficam abertas por minutos
  E a medição não guarda quem pediu, a conta, o corpo nem o identificador do endereço
```

<a id="RNF-248"></a>

### `RNF-248` Guarda das medições

> Guarda da auditoria: [17_modulo-usuario `RN-178`](17_modulo-usuario.md#RN-178)

```gherkin
Esquema do Cenário: por quanto tempo cada dado fica
  Quando o <dado> passa de <guarda>
  Então o agendamento da Auditoria o apaga na hora seguinte

  Exemplos:
    | dado                                  | guarda                     |
    | verificação de um serviço             | 31 dias                    |
    | queda de um serviço                   | 31 dias                    |
    | medição por minuto                    | 2 dias                     |
    | medição por hora                      | 90 dias                    |
    | dia do card da Auditoria              | 400 dias, como na RN-278   |
    | ocorrência de um serviço ou módulo    | 31 dias                    |

Cenário: registros da auditoria
  Então os registros de "auditoria" e "auditoriaErros" seguem guardados por 5 anos
  E a Auditoria nunca os apaga
```

<a id="RNF-249"></a>

### `RNF-249` Ao vivo em até 5 s

```gherkin
Cenário: registro novo na tela
  Dado o Caio com a aba Registros ao vivo
  Quando a Maria cria o participante Lucas
  Então os registros "Iniciado" e "Concluído" aparecem na tela do Caio em até 5 s

Cenário: ocorrência nova em qualquer aba
  Dado o Caio com a Auditoria aberta na aba Métricas, Registros, Execuções ou Saúde
  Quando o cache muda de estado na verificação das 10:31:00
  Então a faixa de ocorrências mostra a mudança em até 5 s
  E a faixa não usa um aparelho a mais: cada aparelho segue com uma só conexão ao vivo

Cenário: mais de 3 aparelhos
  Dado o Caio com Registros ao vivo no celular, no tablet e no computador
  Quando abre Registros ao vivo num segundo computador
  Então o celular, o mais antigo, mostra "Ao vivo pausado: aberto em outros 3 aparelhos"
```

<a id="RNF-251"></a>

### `RNF-251` Coleções e índices da Auditoria prontos na partida

> Mesma regra da partida: [17_modulo-usuario `RNF-227`](17_modulo-usuario.md#RNF-227)

```gherkin
Esquema do Cenário: coleção pronta antes do primeiro pedido
  Dado o banco de homologação sem as coleções da Auditoria
  Quando a aplicação inicia
  Então antes de aceitar o primeiro pedido, a coleção "<colecao>" existe
  E tem os índices <indices>

  Exemplos:
    | colecao            | indices                                                    |
    | verificacoesSaude  | servico+quando↓, guardarAte                                |
    | quedasServico      | servico+inicio↓, guardarAte                                |
    | medicoesMinuto     | minuto, modulo+minuto, guardarAte                          |
    | medicoesHora       | hora, modulo+hora, guardarAte                              |
    | cardAuditoria      | guardarAte                                                 |
    | ocorrencias        | quando↓, alvo+quando↓, para+quando↓, guardarAte            |
    | auditoria          | quando↓, acao+quando↓, contaId+quando↓, quem+quando↓       |
    | auditoriaErros     | quando↓, nivel+quando↓, codigo+quando↓, acao+quando↓       |

Cenário: índice que não pode ser criado
  Dado o banco recusa o índice acao_quando em "auditoria" na partida
  Quando a aplicação inicia
  Então ela aceita pedidos e o log avisa o nome do índice e da coleção, nunca um valor
  E nenhum índice da Auditoria é único, então nenhum deles para a partida
```

<a id="RNF-252"></a>

### `RNF-252` Saúde com o banco de dados fora

```gherkin
Cenário: banco fora do ar
  Dado o Caio com a Auditoria aberta e o banco de dados sem resposta desde 10:41
  Quando a verificação das 10:42 termina
  Então a aba Saúde mostra "Banco de dados" com "Fora do ar desde 10:41"
  E a faixa de ocorrências mostra "Banco de dados fora do ar desde 10:41" em até 5 s
  E as abas Registros e Execuções mostram "Os registros dependem do banco de dados, fora do ar."
  E a aba Métricas mantém os números já carregados, até 10:41, e o aviso é só a faixa
  E se o Caio troca a janela, a aba Métricas mostra "Não foi possível carregar a Auditoria."
  E a ocorrência do banco entra na lista de ocorrências quando ele volta, com a hora 10:41
```

---

<a id="cenarios-api"></a>

## Cenários de API

<a id="formato-comum"></a>

### Formato comum

```gherkin
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

Cenário: datas, tempos e quem usa
  Então toda data e hora segue a ISO 8601 em UTC, como "2026-11-20T13:42:30Z"
  E a hora de um registro vai com milissegundos, como "2026-11-20T13:20:14.102Z"
  E dias, semanas e meses das séries começam às 00:00 do horário de Brasília
  E todo tempo vai em milissegundos inteiros, em campos terminados em "Ms"
  E estado, resultado, tipo, nível e ação vão em maiúsculas, como "FORA_DO_AR"
  E só o papel "ADMINISTRADOR" e o "MEMBRO_TIME" com "MOD-17" com "U" usam a Auditoria
  E todo endereço de /auditoria é GET
  E a consulta atendida traz o cabeçalho X-Trace-Id e fica na auditoria, como na RN-325

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
```

<a id="RF-194-api"></a>

### `RF-194` Saúde dos serviços

```gherkin
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
```

<a id="RF-195-api"></a>

### `RF-195` Histórico de um serviço

```gherkin
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

Cenário: queda ainda aberta
  Dado o intermediador fora do ar desde 2026-11-20T13:30:00Z
  Quando o Caio envia GET /auditoria/saude/intermediador-pagamento/historico às 13:42
  Então a primeira queda vem com "fim": null e "duracaoMin": 12
```

<a id="RF-196-api"></a>

### `RF-196` Módulos no ar

```gherkin
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
```

<a id="RF-197-api"></a>

### `RF-197` Tempo de resposta

```gherkin
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
```

<a id="RF-198-api"></a>

### `RF-198` Tempo de cada endereço

```gherkin
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
```

<a id="RF-199-api"></a>

### `RF-199` Registros da auditoria

```gherkin
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

Cenário: registro de recusa
  Dado a criação do Lucas recusada com 409 MATRICULA_EM_USO às 13:39:52
  Quando o Caio envia GET /auditoria/registros
  Então o item do erro vem com "tipo": "ERRO", "etapa": null
  E "nivel": "ALERTA" e "codigo": "MATRICULA_EM_USO"

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
```

<a id="RF-200-api"></a>

### `RF-200` Filtros e busca dos registros

```gherkin
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

Cenário: busca pelo traceId
  Quando o Caio envia GET /auditoria/registros?traceId=trc-4b7e01c9
  Então a resposta é 200 com todos os registros desse traceId, sem o limite do período
  E "de" e "ate" vêm null

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
```

<a id="RF-201-api"></a>

### `RF-201` Registros ao vivo

> Conexão ao vivo: [15_modulos `RN-279`](15_modulos.md#RN-279-api)

```gherkin
Cenário: registros chegando
  Dado o Caio com GET /auditoria/registros/ao-vivo?nivel=ERRO aberto
  Quando a execução "trc-4b7e01c9" grava um erro em "auditoriaErros"
  Então a resposta, 200 com Content-Type "text/event-stream" e Cache-Control "no-store"
  E recebe o evento "registro", com id igual ao do registro e o corpo de um item da RF-199
  E um ": ping" chega a cada 30 s

Esquema do Cenário: filtros do ao vivo
  Quando o Caio envia GET /auditoria/registros/ao-vivo<filtro>
  Então a resposta é <resposta>

  Exemplos:
    | filtro                            | resposta                                         |
    | ?modulo=MOD-01&tipo=AUDITORIA     | 200, só com os registros de auditoria do Pagamento |
    | ?conta=ebfga                      | 200, só com os registros da EBFGA                |
    | ?de=2026-11-20T13:00:00Z          | 422 DADOS_INVALIDOS com o campo "de"             |
    | ?traceId=trc-4b7e01c9             | 422 DADOS_INVALIDOS com o campo "traceId"        |

Cenário: quarta conexão do mesmo usuário
  Dado o Caio com 3 conexões em /auditoria/registros/ao-vivo
  Quando abre a 4ª
  Então a 4ª recebe 200 e a mais antiga recebe "encerrado" com "LIMITE_CONEXOES" e fecha
```

<a id="RF-202-api"></a>

### `RF-202` Execução do começo ao fim

```gherkin
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

Cenário: execução que falhou
  Dado "trc-4b7e01c9" gravado pelo gancho com a ação "PAGAMENTO_CRIADO": INICIADO às 13:41:30.000
  E ERRO às 13:41:32.107, com o nível ERRO e o código "BANCO_INDISPONIVEL"
  Quando o Caio envia GET /auditoria/execucoes/trc-4b7e01c9
  Então a resposta é 200 com "resultado": "FALHOU", "codigo": "BANCO_INDISPONIVEL"
  E "duracaoMs": 2107, do "INICIADO" ao registro de erro
  E o último passo com "tipo": "ERRO" e "nivel": "ERRO"

Esquema do Cenário: execução pedida
  Quando o Caio envia GET /auditoria/execucoes/<traceId>
  Então a resposta é <resposta>

  Exemplos:
    | traceId        | resposta                                   |
    | trc-00000000   | 404 EXECUCAO_INEXISTENTE                   |
    | abc            | 422 DADOS_INVALIDOS com o campo "traceId"  |
```

<a id="RF-203-api"></a>

### `RF-203` Execuções com problema

```gherkin
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

Esquema do Cenário: filtros das execuções
  Quando o Caio envia GET /auditoria/execucoes<consulta>
  Então a resposta é <resposta>

  Exemplos:
    | consulta                   | resposta                                         |
    | ?resultado=RECUSADA        | 200, só com as recusadas                         |
    | sem parâmetros             | 200, com todas, a mais nova primeiro, 50 por página |
    | ?modulo=MOD-02&conta=ebfga | 200, só com as do SAC da EBFGA                   |
    | ?resultado=PERDIDA         | 422 DADOS_INVALIDOS com o campo "resultado"      |
```

<a id="RF-204-api"></a>

### `RF-204` Faixa de ocorrências ao vivo

> Conexão ao vivo: [15_modulos `RN-279`](15_modulos.md#RN-279-api)

```gherkin
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

Cenário: ocorrências ao vivo
  Dado o Caio com GET /auditoria/ocorrencias/ao-vivo aberto
  Então a resposta é 200 com Content-Type "text/event-stream" e Cache-Control "no-store"
  E o 1º evento, logo ao abrir, é "abertas", com o corpo de GET /auditoria/ocorrencias/abertas
  E quando o cache passa a LENTO, o evento "ocorrencia" chega em até 5 s com um item
  E um ": ping" chega a cada 30 s

Esquema do Cenário: a mesma ocorrência nos outros ao vivo da Auditoria
  Dado o Caio com GET <conexao> aberto
  Quando o cache passa a LENTO
  Então recebe também o evento "ocorrencia" em até 5 s, com o mesmo corpo

  Exemplos:
    | conexao                                  |
    | /auditoria/saude/ao-vivo                 |
    | /auditoria/registros/ao-vivo?nivel=ERRO  |

Cenário: conexão que conta no limite do usuário
  Dado o Caio com 3 conexões ao vivo da Auditoria, entre saúde, registros e ocorrências
  Quando abre GET /auditoria/ocorrencias/ao-vivo mais uma vez
  Então a mais antiga recebe "encerrado" com "LIMITE_CONEXOES" e fecha
```

<a id="RF-205-api"></a>

### `RF-205` Lista de ocorrências

```gherkin
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

Esquema do Cenário: limite de 31 dias, sem depender do relógio
  Quando o Caio envia GET /auditoria/ocorrencias?de=<de>&ate=2026-11-20T14:00:00Z
  Então a resposta é <resposta>

  Exemplos:
    | de                   | resposta                              |
    | 2026-10-20T14:00:00Z | 200, com os 31 dias                   |
    | 2026-10-20T13:59:59Z | 422 PERIODO_LONGO com o campo "ate"   |
```

<a id="RN-314-api"></a>

### `RN-314` Quem usa a Auditoria

```gherkin
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
```

<a id="RN-315-api"></a>

### `RN-315` Estado de um serviço

```gherkin
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

Cenário: sonda lenta sem o tempo
  Quando envia PUT /_teste/auditoria/sondas/cache com "resposta": "LENTA"
  Então a sonda responde com "tempoMs": 2000, e o cache fica LENTO na próxima verificação

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

Cenário: agendamento com "resposta" de falha
  Dado a aplicação de homologação, com Teste__Relogio "sim", às 13:42:00
  E PUT /_teste/auditoria/sondas/agendamento-sac com "ultimaExecucao": "2026-11-20T13:41:00Z"
  E "resposta": "ERRO" no mesmo corpo
  Quando envia POST /_teste/auditoria/verificacao e GET /auditoria/saude
  Então o "agendamento-sac" vem NO_AR, com "falha": null
  E para os agendamentos a sonda usa só "ultimaExecucao" e ignora "resposta"

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
```

<a id="RN-316-api"></a>

### `RN-316` Estado de um módulo

```gherkin
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
```

<a id="RN-317-api"></a>

### `RN-317` Faixas do tempo de resposta

```gherkin
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

Cenário: medição injetada já gravada
  Dado a aplicação de homologação, com Teste__Relogio "sim", às 13:42:00
  Quando envia POST /_teste/auditoria/medicoes com 10 pedidos a "GET /sac/chamados" em 13:41
  Então GET /auditoria/metricas?janela=1h&modulo=MOD-02 já conta os 10, sem /medicoes/gravar
  E a próxima verificação já os usa no estado do "MOD-02"
  E POST /_teste/auditoria/medicoes/gravar só grava o que os pedidos de verdade juntaram

Esquema do Cenário: medição injetada inválida
  Quando envia POST /_teste/auditoria/medicoes com <corpo>
  Então a resposta é 422 DADOS_INVALIDOS com o campo "<campo>"

  Exemplos:
    | corpo                   | campo     |
    | "duracaoMs": -1         | duracaoMs |
    | a lista vazia           | medicoes  |
```

<a id="RN-318-api"></a>

### `RN-318` Resultado de uma execução

```gherkin
Esquema do Cenário: resultado pelos registros
  Dado a aplicação de homologação, com Teste__Relogio "sim", às 13:42:00
  E POST /_teste/auditoria/registros gravou para "trc-90ab12cd": <registros>
  E todos com "acao": "MUDAR_ESTADO_CONTA"
  Quando envia GET /auditoria/execucoes/trc-90ab12cd
  Então a resposta é 200 com "resultado": "<resultado>" e "duracaoMs": <duracao>

  Exemplos:
    | registros                                      | resultado    | duracao |
    | INICIADO 13:20:00.000, CONCLUIDO 13:20:00.184  | CONCLUIDA    | 184     |
    | INICIADO 13:20:00.000, ALERTA 13:20:00.050     | RECUSADA     | 50      |
    | INICIADO 13:20:00.000, ERRO 13:20:02.107       | FALHOU       | 2107    |
    | só INICIADO 13:37:01.000                       | EM_ANDAMENTO | null    |
    | só INICIADO 13:37:00.000                       | INTERROMPIDA | null    |

Cenário: gancho de registros fora do teste
  Dado a aplicação de produção, com Teste__Relogio "sim"
  Quando envia POST /_teste/auditoria/registros
  Então a resposta é 404 e "auditoria" não ganha registro

Cenário: gancho de registros com quem e conta
  Dado a aplicação de homologação, com Teste__Relogio "sim"
  Quando envia POST /_teste/auditoria/registros com 1 registro de "acao": "CRIAR_USUARIO"
  E "quem": "usr-ebfga-maria", "conta": "conta-ebfga" e "alvo": "usr-ebfga-lucas"
  Então a resposta é 201
  E GET /auditoria/registros?quem=usr-ebfga-maria&conta=ebfga traz o registro
  E com "quem" de nome "Maria Souza" e "conta" de sigla "EBFGA", lidos de usuarios e contas
  E GET /auditoria/execucoes?conta=ebfga conta a execução dele

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
```

<a id="RN-319-api"></a>

### `RN-319` Nome e módulo de cada ação

```gherkin
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

Cenário: ação gravada sem nome no catálogo
  Dado um registro da ação "PUBLICAR_AGENDA", que não está em GET /auditoria/acoes
  Quando o Caio envia GET /auditoria/registros
  Então o item vem com "acaoNome": "PUBLICAR_AGENDA" e "modulo": null
```

<a id="RN-320-api"></a>

### `RN-320` Só leitura

```gherkin
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
```

<a id="RN-321-api"></a>

### `RN-321` Período das consultas

```gherkin
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

Cenário: período invertido
  Quando o Caio envia GET /auditoria/registros com "de" depois de "ate"
  Então a resposta é 422 PERIODO_INVERTIDO com o campo "ate"

Esquema do Cenário: período que falta
  Quando o Caio envia GET /auditoria/registros<consulta>
  Então "de" e "ate" da resposta são <periodo>

  Exemplos:
    | consulta                   | periodo                                          |
    | sem de nem ate             | as últimas 24 h até agora                        |
    | ?de=2026-11-20T10:00:00Z   | de 2026-11-20T10:00:00Z até agora                |
    | ?ate=2026-11-19T10:00:00Z  | as 24 h antes de 2026-11-19T10:00:00Z            |
```

<a id="RN-322-api"></a>

### `RN-322` Card da Auditoria

> Campos comuns do card: [15_modulos `RN-276`](15_modulos.md#RN-276-api) e ao vivo em [`RN-279`](15_modulos.md#RN-279-api)

```gherkin
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

Esquema do Cenário: quem pede o card
  Quando <quem> envia GET /auditoria/card
  Então a resposta é <resposta>

  Exemplos:
    | quem                             | resposta                          |
    | o administrador do deboaz        | 200 com o perfil "DEBOAZ"         |
    | o Rui, sem "MOD-17"              | 403 ACESSO_NEGADO                 |
    | a conta master da EBFGA          | 403 ACESSO_NEGADO                 |

Cenário: ao vivo
  Quando o Caio envia GET /auditoria/card/ao-vivo?intervalo=30
  Então a resposta é 200, como na RN-279 de 15_modulos
  E o 1º evento "card" chega logo ao abrir, com o corpo de GET /auditoria/card
  E ?intervalo=10 é 422 DADOS_INVALIDOS com o campo "intervalo"

Cenário: card fechado pelo agendamento
  Dado a aplicação de homologação, com Teste__Relogio "sim", às 13:42:00
  E POST /_teste/auditoria/registros gravou 1 registro de nível ERRO em 13:41
  Quando envia POST /_teste/auditoria/agendamento e depois GET /auditoria/card
  Então "normal.valor" é 1 a mais do que antes, com "versao" maior
```

<a id="RN-324-api"></a>

### `RN-324` O que vira ocorrência

```gherkin
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

Cenário: módulo sem pedidos não abre ocorrência
  Dado o MOD-14 sem pedidos nos últimos 5 min
  Quando envia POST /_teste/auditoria/verificacao
  Então GET /auditoria/ocorrencias?alvo=MOD-14 traz "itens": []

Cenário: módulo que fica sem pedidos fecha a ocorrência
  Dado o MOD-01 LENTO desde 13:31:00, com a ocorrência aberta pelas medições injetadas
  E nenhum pedido ao MOD-01 de 13:31:00 a 13:36:00
  Quando envia POST /_teste/auditoria/verificacao às 13:36:00
  Então GET /auditoria/saude traz o "MOD-01" em "SEM_PEDIDOS"
  E GET /auditoria/ocorrencias?alvo=MOD-01 traz a mais nova com "de": "LENTO" e "para": "NO_AR"
  E "duracaoMin": 5, e GET /auditoria/ocorrencias/abertas não traz mais o MOD-01

Esquema do Cenário: estados fora das ocorrências
  Dado o cache FORA_DO_AR, com a ocorrência aberta
  Quando a verificação seguinte o mostra em "<estado>"
  Então a ocorrência fecha com "para": "NO_AR", porque <estado> conta como NO_AR

  Exemplos:
    | estado          |
    | SEM_VERIFICACAO |
    | DESLIGADO       |

Cenário: duas instâncias, uma ocorrência
  Dado 2 instâncias no ar e o cache que passa a LENTO na verificação das 13:31:00
  Quando envia GET /auditoria/ocorrencias?alvo=cache
  Então "itens" traz uma só ocorrência, com "quando": "2026-11-20T13:31:00Z"
```

<a id="RN-325-api"></a>

### `RN-325` Consulta auditada

```gherkin
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

Esquema do Cenário: o que é gravado
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

Cenário: alvo da consulta
  Quando o Caio envia GET /auditoria/registros?nivel=ERRO&conta=ebfga&depois=MjAy&limite=100
  Então o alvo gravado é "/auditoria/registros?conta=ebfga&nivel=ERRO"
  E os parâmetros vão em ordem alfabética, sem "depois" nem "limite"

Esquema do Cenário: a mesma consulta em menos de 5 min
  Dado o Caio com GET /auditoria/metricas?janela=1h gravado às 13:40:00
  Quando envia o mesmo GET às <hora>
  Então a resposta é 200, com o X-Trace-Id, e <resultado>

  Exemplos:
    | hora      | resultado                                     |
    | 13:44:59  | nenhum "CONSULTAR_AUDITORIA" novo             |
    | 13:45:00  | um "CONSULTAR_AUDITORIA" novo                 |

Esquema do Cenário: consulta recusada não é gravada
  Quando <quem> envia GET <endereco>
  Então a resposta é <resposta> e "auditoria" não ganha "CONSULTAR_AUDITORIA"

  Exemplos:
    | quem                    | endereco                       | resposta             |
    | o Rui, sem "MOD-17"     | /auditoria/registros           | 403 ACESSO_NEGADO    |
    | o Caio                  | /auditoria/metricas?janela=2h  | 422 DADOS_INVALIDOS  |
```

<a id="RNF-246-api"></a>

### `RNF-246` Verificação a cada 30 s

```gherkin
Cenário: saúde ao vivo
  Quando o Caio envia GET /auditoria/saude/ao-vivo com Accept "text/event-stream"
  Então a resposta é 200 com Content-Type "text/event-stream" e Cache-Control "no-store"
  E o 1º evento "saude" chega logo ao abrir, com o corpo de GET /auditoria/saude
  E depois um evento "saude" a cada verificação, a cada 30 s, com "verificadoEm" novo
  E a 4ª conexão do mesmo usuário fecha a mais antiga com "encerrado" e "LIMITE_CONEXOES"

Cenário: tempo limite da verificação
  Dado PUT /_teste/auditoria/sondas/banco com "resposta": "NENHUMA", em homologação
  Quando envia POST /_teste/auditoria/verificacao
  Então a resposta é 200 em até 4 s, porque cada serviço tem até 3 s para responder
```

<a id="RNF-247-api"></a>

### `RNF-247` Medição de todo pedido

```gherkin
Cenário: pedidos medidos
  Dado a aplicação de homologação, com Teste__Relogio "sim"
  Quando a conta master envia 10 vezes GET /sac/chamados
  E envia POST /_teste/auditoria/medicoes/gravar
  Então GET /auditoria/metricas/enderecos?janela=1h&modulo=MOD-02 traz "GET /sac/chamados"
  E com 10 pedidos a mais do que antes

Esquema do Cenário: pedidos que não entram nas métricas
  Quando chega <pedido> e o minuto é gravado
  Então nenhum endereço novo aparece em GET /auditoria/metricas/enderecos

  Exemplos:
    | pedido                                |
    | GET /health                           |
    | POST /_teste/auditoria/verificacao    |
    | GET /auditoria/registros/ao-vivo      |
    | GET /nao-existe                       |
```

<a id="RNF-249-api"></a>

### `RNF-249` Ao vivo em até 5 s

```gherkin
Cenário: registro novo no ao vivo
  Dado o Caio com GET /auditoria/registros/ao-vivo aberto
  Quando a Maria envia POST /usuarios do participante Lucas e a resposta é 201
  Então o Caio recebe os eventos "registro" do "INICIADO" e do "CONCLUIDO" em até 5 s

Cenário: ocorrência nova no ao vivo
  Dado o Caio com GET /auditoria/ocorrencias/ao-vivo aberto
  Quando a verificação das 13:31:00 mostra o cache LENTO
  Então o evento "ocorrencia" chega até 13:31:05

Cenário: aviso perdido não perde o registro
  Dado o cache fora do ar quando a Maria cria o Lucas
  Então os registros ficam gravados em "auditoria" e a criação responde 201 como sempre
  E GET /auditoria/registros os traz, mesmo sem o evento ao vivo
```

<a id="RNF-250"></a>

### `RNF-250` Consulta com muitos registros

```gherkin
Cenário: registros e execuções com muito volume
  Dado 5.000.000 registros em "auditoria" e 400.000 em "auditoriaErros", de 800 contas
  Quando o Caio envia GET /auditoria/registros?conta=ebfga&de=2026-10-20T13:42:00Z
  Então a resposta 200 chega em até 2 s, com 50 itens
  E GET /auditoria/execucoes?resultado=FALHOU,INTERROMPIDA de 7 dias chega em até 3 s

Cenário: métricas de 30 dias
  Dado 90 dias de medições por hora de 60 endereços
  Quando o Caio envia GET /auditoria/metricas?janela=30d
  Então a resposta 200 chega em até 1 s, somada no banco, sem trazer cada hora à aplicação
```

<a id="RNF-251-api"></a>

### `RNF-251` Coleções e índices da Auditoria prontos na partida

```gherkin
Cenário: pronta depois da partida
  Dado a partida de homologação concluída, com as coleções e os índices da Auditoria
  Quando envia GET /health, sem login
  Então a resposta é 200, sem corpo

Cenário: índice que não pode ser criado
  Dado o banco recusa o índice acao_quando em "auditoria" na partida
  Quando envia GET /health, sem login
  Então a resposta é 200, sem corpo
  E o log traz um aviso com o nome do índice e da coleção, nunca o valor
```

<a id="RNF-252-api"></a>

### `RNF-252` Saúde com o banco de dados fora

```gherkin
Cenário: saúde respondida sem o banco
  Dado o banco de dados sem resposta há 1 min e a sessão do Caio ativa
  Quando o Caio envia GET /auditoria/saude
  Então a resposta é 200 com o "banco" em "FORA_DO_AR", lida da última verificação
  E a consulta não fica gravada, e o log da aplicação avisa "consulta sem registro"
  E GET /auditoria/ocorrencias/abertas responde 200 com a ocorrência do "banco"
  E GET /auditoria/registros e GET /auditoria/ocorrencias respondem 503 BANCO_DE_DADOS_FORA
  E GET /auditoria/metricas e GET /auditoria/metricas/enderecos também respondem 503
  E a "mensagem" é "Os registros dependem do banco de dados, fora do ar."

Cenário: ocorrência do banco guardada na volta
  Dado o banco FORA_DO_AR de 13:41:00 a 13:47:30
  Quando envia GET /auditoria/ocorrencias às 13:50:00
  Então "itens" traz a ida do "banco" a FORA_DO_AR com "quando": "2026-11-20T13:41:00Z"
  E a volta a NO_AR com "quando": "2026-11-20T13:47:30Z" e "duracaoMin": 6
```

<a id="fora-de-escopo"></a>

### Fora de escopo

```gherkin
Cenário: o que a Auditoria não faz
  Então a Auditoria não altera, apaga nem exporta registro
  E o aviso de queda fica só dentro da Auditoria, na faixa de ocorrências (RF-204)
  E não vai por e-mail, SMS, WhatsApp nem notificação do aparelho
  E não mostra os logs técnicos do ambiente, que ficam no Log Analytics (05 RNF-81)
  E não mostra a telemetria do Application Insights (05 RNF-82), nem a substitui
  E não mede o uso de CPU, memória ou disco das instâncias
  E com o cache fora do ar nenhuma sessão é conferida, e a Auditoria também não abre
  E não é usada pelas contas: só o administrador e o time deboaz a veem
```

---

<a id="telas"></a>

## Telas

### Web

**Hub · Auditoria · Saúde · web · vista** — `2_Diagramas/vista-hub-auditoria-saude-web_v02_20261120-1935.svg`

[![Web, medium e expanded: aba Saúde com o botão Ocorrências e a faixa da mais grave, "1 serviço fora do ar · Produção" ao vivo e pausado, serviço fora do ar com "Sem resposta em 3 s", cache lento, módulos dos últimos 5 min com o Pagamento lento, histórico de um serviço com disponibilidade e quedas, sem quedas, voltou, tudo no ar com o Orgulhoso, desligado neste ambiente, carregando, sem conexão, banco de dados fora e o card da Auditoria (RF-194, RF-195, RF-196, RN-314, RN-315, RN-316, RN-322, RN-323, RNF-246, RNF-252)](../2_Diagramas/vista-hub-auditoria-saude-web_v02_20261120-1935.svg)](../2_Diagramas/vista-hub-auditoria-saude-web_v02_20261120-1935.svg)

**Hub · Auditoria · Métricas · web · vista** — `2_Diagramas/vista-hub-auditoria-metricas-web_v01_20261120-1442.svg`

[![Web, medium e expanded: aba Métricas com módulo e Ao vivo, janelas de 1 h a 30 dias, pedidos por minuto, p50, p95, p99 e erros com selo pelo limite de 1 s, gráfico de pedidos com o p95, endereços pela régua das 9 faixas com o selo Lento, ordem Mais lentos, Mais pedidos e Mais erros, SAC ao vivo e erro ao carregar (RF-197, RF-198, RN-317, RN-323)](../2_Diagramas/vista-hub-auditoria-metricas-web_v01_20261120-1442.svg)](../2_Diagramas/vista-hub-auditoria-metricas-web_v01_20261120-1442.svg)

**Hub · Auditoria · Registros · web · vista** — `2_Diagramas/vista-hub-auditoria-registros-web_v01_20261120-1442.svg`

[![Web, medium e expanded: aba Registros com "Buscar pelo código da execução", Últimas 24 h e Ao vivo, filtros de nível, módulo, ação, conta, quem e código, Alerta e Erro com selo e código, períodos e personalizado até 31 dias, ao vivo com 12 novos e reconectando, nenhum registro com o Sonolento, fim dos registros, acesso retirado, mais de 3 aparelhos e banco de dados fora (RF-199, RF-200, RF-201, RN-314, RN-319, RN-320, RN-321, RN-323, RNF-249, RNF-252)](../2_Diagramas/vista-hub-auditoria-registros-web_v01_20261120-1442.svg)](../2_Diagramas/vista-hub-auditoria-registros-web_v01_20261120-1442.svg)

**Hub · Auditoria · Execuções · web · vista** — `2_Diagramas/vista-hub-auditoria-execucoes-web_v02_20261120-1922.svg`

[![Web, medium e expanded: aba Execuções com a contagem de concluídas, recusadas, falharam e interrompidas, filtros Com problema, Recusadas, Concluídas e Todas, execução concluída com as ações internas, que falhou com o último passo de erro, interrompida, não encontrada, personalizado até 7 dias, erro ao carregar e banco de dados fora (RF-202, RF-203, RN-318, RN-320, RN-321, RN-323, RNF-252)](../2_Diagramas/vista-hub-auditoria-execucoes-web_v02_20261120-1922.svg)](../2_Diagramas/vista-hub-auditoria-execucoes-web_v02_20261120-1922.svg)

**Hub · Auditoria · Ocorrências · web · vista** — `2_Diagramas/vista-hub-auditoria-ocorrencias-web_v02_20261120-1935.svg`

[![Web, medium e expanded: faixa de ocorrências no topo das quatro abas com a mais grave e +2 ocorrências, de erro, de alerta e de volta com Dispensar, botão Ocorrências com o selo das abertas; lista em tela cheia com Abertas agora, período até 31 dias e ao vivo, filtros Serviços, Módulos, alvo e estado, ocorrência chegando em até 5 s, nenhuma ocorrência com o Orgulhoso e período acima de 31 dias, sem e-mail (RF-204, RF-205, RN-324, RN-325)](../2_Diagramas/vista-hub-auditoria-ocorrencias-web_v02_20261120-1935.svg)](../2_Diagramas/vista-hub-auditoria-ocorrencias-web_v02_20261120-1935.svg)

### PWA

**Hub · Auditoria · Saúde · pwa · vista** — `2_Diagramas/vista-hub-auditoria-saude-pwa_v02_20261120-1935.svg`

[![PWA, compact da web instalada: aba Saúde com o botão Ocorrências e a faixa da mais grave, "1 serviço fora do ar · Produção" ao vivo e pausado, serviço fora do ar com "Sem resposta em 3 s", cache lento, módulos dos últimos 5 min com o Pagamento lento, histórico de um serviço com disponibilidade e quedas, sem quedas, voltou, tudo no ar com o Orgulhoso, desligado neste ambiente, carregando, sem conexão, banco de dados fora e o card da Auditoria (RF-194, RF-195, RF-196, RN-314, RN-315, RN-316, RN-322, RN-323, RNF-246, RNF-252)](../2_Diagramas/vista-hub-auditoria-saude-pwa_v02_20261120-1935.svg)](../2_Diagramas/vista-hub-auditoria-saude-pwa_v02_20261120-1935.svg)

**Hub · Auditoria · Métricas · pwa · vista** — `2_Diagramas/vista-hub-auditoria-metricas-pwa_v01_20261120-1442.svg`

[![PWA, compact da web instalada: aba Métricas com módulo e Ao vivo, janelas de 1 h a 30 dias, pedidos por minuto, p50, p95, p99 e erros com selo pelo limite de 1 s, gráfico de pedidos com o p95, endereços pela régua das 9 faixas com o selo Lento, ordem Mais lentos, Mais pedidos e Mais erros, SAC ao vivo e erro ao carregar (RF-197, RF-198, RN-317, RN-323)](../2_Diagramas/vista-hub-auditoria-metricas-pwa_v01_20261120-1442.svg)](../2_Diagramas/vista-hub-auditoria-metricas-pwa_v01_20261120-1442.svg)

**Hub · Auditoria · Registros · pwa · vista** — `2_Diagramas/vista-hub-auditoria-registros-pwa_v01_20261120-1442.svg`

[![PWA, compact da web instalada: aba Registros com "Buscar pelo código da execução", Últimas 24 h e Ao vivo, filtros de nível, módulo, ação, conta, quem e código, Alerta e Erro com selo e código, períodos e personalizado até 31 dias, ao vivo com 12 novos e reconectando, nenhum registro com o Sonolento, fim dos registros, acesso retirado, mais de 3 aparelhos e banco de dados fora (RF-199, RF-200, RF-201, RN-314, RN-319, RN-320, RN-321, RN-323, RNF-249, RNF-252)](../2_Diagramas/vista-hub-auditoria-registros-pwa_v01_20261120-1442.svg)](../2_Diagramas/vista-hub-auditoria-registros-pwa_v01_20261120-1442.svg)

**Hub · Auditoria · Execuções · pwa · vista** — `2_Diagramas/vista-hub-auditoria-execucoes-pwa_v02_20261120-1922.svg`

[![PWA, compact da web instalada: aba Execuções com a contagem de concluídas, recusadas, falharam e interrompidas, filtros Com problema, Recusadas, Concluídas e Todas, execução concluída com as ações internas, que falhou com o último passo de erro, interrompida, não encontrada, personalizado até 7 dias, erro ao carregar e banco de dados fora (RF-202, RF-203, RN-318, RN-320, RN-321, RN-323, RNF-252)](../2_Diagramas/vista-hub-auditoria-execucoes-pwa_v02_20261120-1922.svg)](../2_Diagramas/vista-hub-auditoria-execucoes-pwa_v02_20261120-1922.svg)

**Hub · Auditoria · Ocorrências · pwa · vista** — `2_Diagramas/vista-hub-auditoria-ocorrencias-pwa_v02_20261120-1922.svg`

[![PWA, compact da web instalada: faixa de ocorrências no topo das quatro abas com a mais grave e +2 ocorrências, de erro, de alerta e de volta com Dispensar, botão Ocorrências com o selo das abertas; lista em tela cheia com Abertas agora, período até 31 dias e ao vivo, filtros Serviços, Módulos, alvo e estado, ocorrência chegando em até 5 s, nenhuma ocorrência com o Orgulhoso e período acima de 31 dias, sem e-mail (RF-204, RF-205, RN-324, RN-325)](../2_Diagramas/vista-hub-auditoria-ocorrencias-pwa_v02_20261120-1922.svg)](../2_Diagramas/vista-hub-auditoria-ocorrencias-pwa_v02_20261120-1922.svg)

### Mobile

**Hub · Auditoria · Saúde · mobile · vista** — `2_Diagramas/vista-hub-auditoria-saude-mobile_v02_20261120-1935.svg`

[![Mobile, compact do app: aba Saúde com o botão Ocorrências e a faixa da mais grave, "1 serviço fora do ar · Produção" ao vivo e pausado, serviço fora do ar com "Sem resposta em 3 s", cache lento, módulos dos últimos 5 min com o Pagamento lento, histórico de um serviço com disponibilidade e quedas, sem quedas, voltou, tudo no ar com o Orgulhoso, desligado neste ambiente, carregando, sem conexão, banco de dados fora e o card da Auditoria (RF-194, RF-195, RF-196, RN-314, RN-315, RN-316, RN-322, RN-323, RNF-246, RNF-252)](../2_Diagramas/vista-hub-auditoria-saude-mobile_v02_20261120-1935.svg)](../2_Diagramas/vista-hub-auditoria-saude-mobile_v02_20261120-1935.svg)

**Hub · Auditoria · Métricas · mobile · vista** — `2_Diagramas/vista-hub-auditoria-metricas-mobile_v01_20261120-1442.svg`

[![Mobile, compact do app: aba Métricas com módulo e Ao vivo, janelas de 1 h a 30 dias, pedidos por minuto, p50, p95, p99 e erros com selo pelo limite de 1 s, gráfico de pedidos com o p95, endereços pela régua das 9 faixas com o selo Lento, ordem Mais lentos, Mais pedidos e Mais erros, SAC ao vivo e erro ao carregar (RF-197, RF-198, RN-317, RN-323)](../2_Diagramas/vista-hub-auditoria-metricas-mobile_v01_20261120-1442.svg)](../2_Diagramas/vista-hub-auditoria-metricas-mobile_v01_20261120-1442.svg)

**Hub · Auditoria · Registros · mobile · vista** — `2_Diagramas/vista-hub-auditoria-registros-mobile_v01_20261120-1442.svg`

[![Mobile, compact do app: aba Registros com "Buscar pelo código da execução", Últimas 24 h e Ao vivo, filtros de nível, módulo, ação, conta, quem e código, Alerta e Erro com selo e código, períodos e personalizado até 31 dias, ao vivo com 12 novos e reconectando, nenhum registro com o Sonolento, fim dos registros, acesso retirado, mais de 3 aparelhos e banco de dados fora (RF-199, RF-200, RF-201, RN-314, RN-319, RN-320, RN-321, RN-323, RNF-249, RNF-252)](../2_Diagramas/vista-hub-auditoria-registros-mobile_v01_20261120-1442.svg)](../2_Diagramas/vista-hub-auditoria-registros-mobile_v01_20261120-1442.svg)

**Hub · Auditoria · Execuções · mobile · vista** — `2_Diagramas/vista-hub-auditoria-execucoes-mobile_v02_20261120-1922.svg`

[![Mobile, compact do app: aba Execuções com a contagem de concluídas, recusadas, falharam e interrompidas, filtros Com problema, Recusadas, Concluídas e Todas, execução concluída com as ações internas, que falhou com o último passo de erro, interrompida, não encontrada, personalizado até 7 dias, erro ao carregar e banco de dados fora (RF-202, RF-203, RN-318, RN-320, RN-321, RN-323, RNF-252)](../2_Diagramas/vista-hub-auditoria-execucoes-mobile_v02_20261120-1922.svg)](../2_Diagramas/vista-hub-auditoria-execucoes-mobile_v02_20261120-1922.svg)

**Hub · Auditoria · Ocorrências · mobile · vista** — `2_Diagramas/vista-hub-auditoria-ocorrencias-mobile_v02_20261120-1922.svg`

[![Mobile, compact do app: faixa de ocorrências no topo das quatro abas com a mais grave e +2 ocorrências, de erro, de alerta e de volta com Dispensar, botão Ocorrências com o selo das abertas; lista em tela cheia com Abertas agora, período até 31 dias e ao vivo, filtros Serviços, Módulos, alvo e estado, ocorrência chegando em até 5 s, nenhuma ocorrência com o Orgulhoso e período acima de 31 dias, sem e-mail (RF-204, RF-205, RN-324, RN-325)](../2_Diagramas/vista-hub-auditoria-ocorrencias-mobile_v02_20261120-1922.svg)](../2_Diagramas/vista-hub-auditoria-ocorrencias-mobile_v02_20261120-1922.svg)
---

<a id="diagramas"></a>

## Diagramas

**Auditoria · casos de uso** — `2_Diagramas/auditoria-casos-de-uso_v02_20261120-1413.svg`

[![Casos de uso da Auditoria: o membro do time com MOD-17, e o administrador por generalização, vê a saúde dos serviços e dos módulos, o histórico de um serviço, o tempo de resposta e de cada endereço, os registros por período e ao vivo, as execuções com problema, cada execução do começo ao fim, as ocorrências e o card, e recebe a ocorrência na faixa; o relógio verifica os serviços e módulos, abre e fecha ocorrências, grava as medições e fecha o card; cada pedido é medido, cada registro novo é avisado e toda consulta é registrada](../2_Diagramas/auditoria-casos-de-uso_v02_20261120-1413.svg)](../2_Diagramas/auditoria-casos-de-uso_v02_20261120-1413.svg)

**Auditoria · sequência com as telas** — `2_Diagramas/auditoria-sequencia_v03_20261120-1936.svg`

[![Sequência da Auditoria com as telas pwa: abrir com o acesso conferido, a consulta auditada e a faixa das ocorrências abertas; verificação de 30 s com a trava e sondas de até 3 s, ocorrência gravada, publicada e entregue ao vivo em todas as abas, e a volta com Dispensar; lista de ocorrências em tela cheia com filtros e ao vivo; registros por período e ao vivo com a consulta auditada e o limite de 3 aparelhos; execuções e uma execução pelo traceId; métricas medidas por pedido, gravadas por minuto e lidas por janela; e o banco de dados fora, com a saúde respondida, 503 nas leituras e as ocorrências gravadas na volta](../2_Diagramas/auditoria-sequencia_v03_20261120-1936.svg)](../2_Diagramas/auditoria-sequencia_v03_20261120-1936.svg)

**Auditoria · contrato de API** — `2_Diagramas/auditoria-contrato-api_v04_20261120-1924.svg`

[![Contrato de API da Auditoria: formato comum e mensagens de erro, saúde, histórico de um serviço e saúde ao vivo, métricas e endereços, ações, card e card ao vivo, registros e registros ao vivo, execuções e execução pelo traceId, ocorrências abertas, ao vivo e por período, a consulta auditada com X-Trace-Id e os ganchos de teste em /_teste/auditoria](../2_Diagramas/auditoria-contrato-api_v04_20261120-1924.svg)](../2_Diagramas/auditoria-contrato-api_v04_20261120-1924.svg)

**Auditoria · classes** — `2_Diagramas/auditoria-classes_v03_20261120-1914.svg`

[![Classes da Auditoria em core/hub/auditoria: saúde (serviço, verificação, queda, módulo), métricas (faixas, histograma, medição, acumulador, módulo do endereço, janela), registros, execuções e ocorrências (registro lido, período, filtro, catálogo de ações, execução, ocorrência, consulta), card, FalhaDaAuditoria, casos de uso e portas de saída, e os adaptadores do ms_mono (controllers, ganchos, agendamento, middleware, sondas, banco, Redis e AuditoriaComAviso)](../2_Diagramas/auditoria-classes_v03_20261120-1914.svg)](../2_Diagramas/auditoria-classes_v03_20261120-1914.svg)

**Auditoria · documentos** — `2_Diagramas/auditoria-documentos_v02_20261120-1413.svg`

[![Modelo de documentos da Auditoria: verificacoesSaude, quedasServico, ocorrencias, medicoesMinuto, medicoesHora e cardAuditoria com exemplos, guarda e índices; auditoria, auditoriaErros, usuarios e contas do 17 só lidas, com os índices novos; e as chaves auditoria:* no Redis](../2_Diagramas/auditoria-documentos_v02_20261120-1413.svg)](../2_Diagramas/auditoria-documentos_v02_20261120-1413.svg)

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v08** | 2026-11-21 00:14 | Lacunas do backend: agendamento conta só a hora da última execução e ignora a falha (`RN-315`, `RN-315-api`); Sem pedidos, Sem verificação e Desligado contam como No ar nas ocorrências e fecham a aberta (`RN-324`, `RN-324-api`) |
| v07 | 2026-11-20 19:37 | Telas: vistas da saúde (Web, PWA e Mobile) e de ocorrências (Web) v02, com a faixa de status no topo dos cards; sequência com as telas v03 |
| v06 | 2026-11-20 19:28 | Gherkin: "E," sem vírgula depois da palavra-chave em `RF-202` e `RF-194-api`, para o parser do qa |
| v05 | 2026-11-20 19:25 | Lacunas do qa nos cenários de API: ordem dos serviços no mesmo estado (`RF-194-api`); páginas num período fechado, sem a própria consulta (`RF-199-api`); preparo pelo gancho com a ação, quem, conta e alvo (`RF-202-api`, `RN-318-api`); limite de 31 dias com o fim explícito (`RF-205-api`); ganchos separados do corpo das sondas (`RN-315-api`); medição injetada já gravada e lista vazia (`RN-317-api`); traceId de exemplo (`RN-325-api`); contrato v04 (ganchos), sequência v02 e vistas de execuções e ocorrências v02 (Caio Lima · EBFGA) |
| v04 | 2026-11-20 19:20 | Telas: as 15 vistas v01 (saúde, métricas, registros, execuções e ocorrências, em Web, PWA e Mobile); lacunas das vistas fechadas: aviso do topo com só lentos (`RF-194`), busca "Buscar pelo código da execução" (`RF-200`), lista de ocorrências em tela cheia pelo botão ou pela faixa (`RF-204`, `RF-205`), faixa só nas quatro abas (`RF-204`), filtro de estado Fora do ar, Lento e No ar (`RF-205`), Métricas com o banco fora só com a faixa e 503 em /metricas (`RNF-252`, `RNF-252-api`); diagramas: sequência com as telas v01, contrato v03 e classes v03 (sondas até 3 s); Em aberto sai |
| v03 | 2026-11-20 14:13 | Decisões do usuário: limites dos estados (No ar até 1 s, Lento até 3 s ou erro acima de 1 %, Fora do ar sem resposta em 3 s ou na 3ª falha seguida; Com falhas sai) em `RN-315`, `RN-316`, `RNF-246`, faixa de 3 s em `RN-317`; consulta auditada (`RN-325`, CONSULTAR_AUDITORIA, X-Trace-Id); ocorrências dentro da Auditoria (`RF-204` faixa ao vivo, `RF-205` lista, `RN-324`), com os pares -api, `RNF-248`, `RNF-249`, `RNF-251`, `RNF-252` e `fora-de-escopo`; diagramas v01 → v02 |
| v02 | 2026-11-20 12:44 | Diagramas: casos de uso, contrato de API, classes e documentos v01; gancho POST /_teste/auditoria/agendamento (`RN-315-api`) e cenário do card fechado pelo agendamento (`RN-322-api`); sequência com as telas depois das vistas |
| v01 | 2026-11-20 12:13 | Criação: saúde dos serviços e dos módulos, tempo de resposta, registros da auditoria por período e ao vivo e execuções do começo ao fim, só para o time deboaz (`RF-194` a `RF-203`), regras (`RN-314` a `RN-323`) e medidas (`RNF-246` a `RNF-252`), com as âncoras -api; telas e diagramas a desenhar |

---

<sub>boaz · v08 · 2026-11-21 00:14 · `1_Requisitos/20_modulo-auditoria.md`</sub>
