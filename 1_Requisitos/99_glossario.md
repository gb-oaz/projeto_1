---
projeto: "boaz"
documento: "99_glossario"
tipo: "Glossário"
titulo: "Glossário"
versao: "v46"
data: "2026-11-08 23:33"
status: "Em revisão"
objetivo: "dar um significado único aos termos do domínio usados nos documentos de requisito."
---

> **boaz · Glossário** · `99_glossario` · **v46** · 2026-11-08 23:33 · **Em revisão**

# Glossário

**Objetivo:** dar um significado único aos termos do domínio usados nos documentos de requisito.

---

<a id="termos"></a>

## Termos

| Termo | Definição |
|---|---|
| <a id="acesso-anonimo"></a>**Acesso anônimo** | acesso que vale só para uma sala aberta a participante anônimo e termina quando a sala fecha; não é usuário |
| <a id="administrador-do-deboaz"></a>**Administrador do deboaz** | dono da conta master do time deboaz, no topo da ordem de superior; a entrada e a senha dele ficam no Key Vault e só são trocadas ali; cria os membros do time e pede a nova geração de senha deles ([Módulo Usuários RN-190](17_modulo-usuario.md#RN-190)) |
| <a id="aparelho-conectado"></a>**Aparelho conectado** | navegador ou app com uma sessão ativa da pessoa no hub; ela vê os dela em Conta e pode sair de um ou de todos, até 5 ao mesmo tempo ([Autenticação `RF-156`](17_modulo-usuario-a-autenticacao.md#RF-156), [RN-247 `RN-247`](17_modulo-usuario-a-autenticacao.md#RN-247)) |
| <a id="blueprint"></a>**Blueprint** | desenho de baixa fidelidade de uma tela, feito no documento de UX, que mostra só a distribuição dos blocos, as ações e os espaços reservados aos módulos, no celular e na web; vem antes da vista |
| <a id="card-ao-vivo"></a>**Card ao vivo** | atualização dos cards de Módulos com a tela aberta, a cada 5, 15, 30, 60 ou 300 s, escolhida pela pessoa e válida em todos os aparelhos dela; desligado, o padrão é **na abertura**: os números só se atualizam ao abrir Módulos, sem conexão aberta ([Módulos `RN-279`](15_modulos.md#RN-279)) |
| <a id="cartao-do-mascote"></a>**Cartão do mascote** | forma de mascote em que o fundo colorido ocupa um cartão vertical ou horizontal e só o rosto se move |
| <a id="carteira"></a>**Carteira** | destino do quest com os módulos sempre disponíveis ao participante, em grade, até a instituição revogar; antes chamado Fixos |
| <a id="catalogo-de-modulos"></a>**Catálogo de módulos** | lista dos módulos que uma escola ou empresa pode contratar, cada um com preço próprio controlado no hub; aparece na aba A contratar de Módulos |
| <a id="catalogo"></a>**Catálogo** | módulo que existe só na conta do time deboaz, onde todo módulo do deboaz é catalogado com nome, tipo (da conta, contratável ou do time), custo, precificação e as instituições e os educadores autônomos que o contrataram; não confundir com o [catálogo de módulos](#catalogo-de-modulos), a vitrine da aba A contratar do hub |
| <a id="classe-de-janela"></a>**Classe de janela** | faixa de largura disponível para a tela (compact, medium, expanded, large ou extra-large) que define margem, colunas, painéis e navegação |
| <a id="codigo-da-sala"></a>**Código da sala** | código que o educador passa aos participantes para entrarem numa sala pelo quest |
| <a id="codigo-de-acesso"></a>**Código de acesso** | código único que a instituição gera para cada participante, no formato `<SIGLA>-<NOME CURTO>-<4 números>` (`EBFGA-LUCAS-0412`, sempre em maiúsculas), e que abre o perfil dele no quest sem senha; vale para o participante e para os responsáveis, e cada ano tem o seu ([Módulo Usuários RN-184](17_modulo-usuario.md#RN-184)) |
| <a id="codigo-de-liberacao"></a>**Código de liberação** | código de 6 números que acompanha cada geração de senha; quem pediu a geração o entrega pessoalmente, e a senha só aparece a quem informa a entrada e esse código |
| <a id="componente"></a>**Componente** | elemento de tela reutilizável do design system, como botão, campo de texto ou card, construído só com tokens |
| <a id="componente-comum"></a>**Componente comum** | componente usado por mais de um módulo, com uma única implementação compartilhada por frente |
| <a id="componente-composto"></a>**Componente composto** | bloco reutilizável que junta componentes e elementos de identidade visual, como uma barra de navegação com o logo, botões e imagens da marca |
| <a id="conta-master"></a>**Conta master** | conta que decide e paga a contratação de módulos de uma instituição; o educador autônomo é a conta master da própria conta |
| <a id="conta-qa"></a>**Conta qa** | conta de testes, com entrada iniciada por “qa”, que a aplicação cria ao iniciar só em homologação, uma para cada tipo de usuário, inclusive um administrador próprio (“qaadmin.dboaz@deboaz.com”), com a entrada, a senha e a lista de módulos iniciais lidas do Key Vault, e dados fictícios no formato do projeto; a cada inicialização volta à senha, ao estado ativo, às permissões e aos módulos iniciais; não existe em produção, onde as siglas iniciadas por “QA” ficam reservadas ao time ([Módulo Usuários RN-265](17_modulo-usuario.md#RN-265), [RN-268](17_modulo-usuario.md#RN-268)) |
| <a id="contas-acessadas"></a>**Contas acessadas** | contas que já entraram no hub num aparelho, listadas na tela Entrar, uma por instituição |
| <a id="deboaz"></a>**deboaz** | nome do produto: junta o nome Boaz com a gíria “de boa” |
| <a id="depoimento"></a>**Depoimento** | texto curto de uma instituição ou de um educador autônomo sobre o deboaz, escrito no hub e mostrado na landing page só com a autorização do autor |
| <a id="design-system"></a>**Design system** | conjunto da identidade visual, dos tokens e dos componentes do projeto, mantido em `0_Assets/design/` |
| <a id="educador"></a>**Educador** | quem cria quizzes e conduz salas no hub: professor, instrutor ou responsável por um treinamento; contrata pela instituição ou como autônomo, gerenciando só a própria conta, o conteúdo e os custos |
| <a id="expressao"></a>**Expressão** | estado emocional mostrado pelo rosto do mascote (feliz, acertou, errou, dúvida, esperando…), escolhido conforme a situação da tela |
| <a id="faixa-de-desconto"></a>**Faixa de desconto** | intervalo do total de usuários (staff + participantes) de uma conta com um percentual de desconto do módulo Usuários que vale só para os usuários dentro dele (desconto progressivo, como no imposto de renda); quanto mais usuários, maior ou igual o percentual |
| <a id="feed"></a>**Feed** | destino da navegação do hub e do quest com as publicações dos módulos, cada uma para toda a instituição ou para uma turma ou equipe; a pessoa vê as do público em que está; a conta do time deboaz não tem Feed |
| <a id="hub"></a>**Hub** | frente do produto onde o educador cria quizzes, conduz salas, acompanha relatórios e administra a conta |
| <a id="identidade-da-instituicao"></a>**Identidade da instituição** | módulo que uma escola ou empresa contrata para ter a identidade dela no lugar da padrão do deboaz; o símbolo do deboaz continua, menor |
| <a id="identidade-visual"></a>**Identidade visual** | temas de cor, tipografia, logos, ícones, imagens e animações que dão a cara do produto |
| <a id="landing-page"></a>**Landing page** | frente do produto que apresenta o deboaz a quem ainda não o usa |
| <a id="mascote"></a>**Mascote** | personagem da identidade visual com rosto expressivo, que reforça uma mensagem da tela; combina expressão, forma, proporção, movimento e cor |
| <a id="mascote-padrinho"></a>**Mascote padrinho** | mascote (expressão e forma) que representa um módulo, definido no documento dele e mostrado no card do catálogo, no destaque e na página do módulo |
| <a id="matricula"></a>**Matrícula** | número que identifica o staff ou o participante dentro da instituição (escola ou empresa), único nela: informado por quem cadastra ou gerado pelo sistema com 8 números; não é a entrada nem o código de acesso ([Módulo Usuários RN-188](17_modulo-usuario.md#RN-188)) |
| <a id="mensagens"></a>**Mensagens** | módulo em que participantes e responsáveis conversam entre si e com a instituição, no quest, e a instituição responde pelo hub; não fala com o time deboaz, que é o papel do [SAC](#sac) ([Módulos MOD-16](15_modulos.md#MOD-16)) |
| <a id="modulo"></a>**Módulo** | conjunto de telas e regras de uma mesma funcionalidade dentro de uma aplicação; é também a unidade contratável do catálogo de módulos ([Módulos](15_modulos.md)) |
| <a id="modulo-fixo"></a>**Módulo fixo** | módulo sempre disponível ao participante na [Carteira](#carteira) do quest, até a instituição revogar |
| <a id="modulo-usuarios"></a>**Módulo Usuários** | módulo da conta em que a instituição cria o staff e os participantes e decide as permissões, o educador autônomo cria os participantes e o time deboaz cria os membros do time e cuida das contas de instituição e de educador autônomo ([Módulo Usuários](17_modulo-usuario.md)) |
| <a id="modulos-da-conta"></a>**Módulos da conta** | módulos que são requisito de toda conta: entram sozinhos quando a conta é criada, sem contratação, e não podem ser retirados (Contas, Perfil, SAC, Turmas, Usuários e, para quem trabalha numa instituição, Sugestão; lista no [registro de módulos](15_modulos.md#RN-203)), mostrados depois dos contratados; podem ter custo, como o Usuários |
| <a id="nova-geracao-de-senha"></a>**Nova geração de senha** | pedido de um usuário superior para que o sistema gere uma senha nova para quem está abaixo dele; a senha aparece uma única vez, só para o próprio usuário, no próximo acesso |
| <a id="pagina-do-modulo"></a>**Página do módulo** | página da landing page que apresenta um módulo do catálogo, com estrutura fixa que o documento do módulo preenche |
| <a id="papel"></a>**Papel** | o que a pessoa é para a autorização — administrador do deboaz, membro do time, conta master, educador autônomo, staff, participante ou acesso anônimo — e que vai no token de acesso ([Autorização `RN-232`](17_modulo-usuario-b-autorizacao.md#RN-232)) |
| <a id="participante"></a>**Participante** | quem responde aos quizzes pelo quest: estudante ou colaborador de empresa |
| <a id="perfis-acessados"></a>**Perfis acessados** | perfis que já entraram no quest num aparelho, listados na tela de acesso, um por código de acesso |
| <a id="pessoa-delegada"></a>**Pessoa delegada** | quem trabalha para uma instituição e recebeu da administração a permissão de usar um módulo, pela aba Utilização |
| <a id="proporcao"></a>**Proporção** | variação do corpo do mascote (normal, alto, baixo, gordo ou magro); o rosto se ajusta para continuar cabendo |
| <a id="quest"></a>**Quest** | frente do produto onde o participante entra numa sala pelo código e responde aos quizzes |
| <a id="ranking"></a>**Ranking** | classificação dos participantes de uma sala pela pontuação |
| <a id="recorte"></a>**Recorte** | período de cada ponto da série do card: dia, semana ou mês, com Mês como padrão; a normal mostra os 5 últimos e a expandida os 12 últimos, e a escolha fica guardada em cada aparelho ([Módulos `RN-277`](15_modulos.md#RN-277)) |
| <a id="registro-de-modulos"></a>**Registro de módulos** | tabela em que todo módulo entra, com um ID `MOD-NN`, antes de ter documento próprio: nome, tipo, cobrança, estado e, para cada perfil, a frente e o objetivo ([Módulos RN-202](15_modulos.md#RN-202)) |
| <a id="sac"></a>**SAC** | módulo da conta que toda conta do hub tem para falar com o time deboaz (a conversa com pais e alunos é do [Mensagens](#mensagens)); o time atende pelo mesmo módulo, onde também chegam as mensagens enviadas sem login pela tela Entrar do hub, como o pedido de senha da conta master |
| <a id="sala"></a>**Sala** | sessão de um quiz aberta por um educador, em tempo real ou no ritmo de cada participante |
| <a id="sequencia-de-movimentos"></a>**Sequência de movimentos** | vários movimentos do mascote em seguida, num único laço (por exemplo, pular, girar e comemorar) |
| <a id="sessao"></a>**Sessão** | entrada de uma pessoa num aparelho, guardada no servidor, que termina depois do tempo sem uso, ao sair ou quando o usuário ou a conta muda de estado; é a fonte da verdade, e os tokens só provam que o aparelho é dono dela ([Autenticação `RN-225`](17_modulo-usuario-a-autenticacao.md#RN-225)) |
| <a id="sigla"></a>**Sigla** | código de 5 letras, único no sistema, que a instituição ou o educador autônomo define ao criar a conta; forma a entrada do staff e o apelido do acesso anônimo |
| <a id="simbolo"></a>**Símbolo** | a marca do deboaz sem o nome: um b e um d, uma barriga redonda com uma antena de cada lado, separadas dela por um vão (hastes longas na landing page e no hub, curtas no quest) |
| <a id="so-o-rosto"></a>**Só o rosto** | mascote sem corpo, só olhos, sobrancelhas e boca com contorno de adesivo, para usar em botões, chips e no meio de um texto |
| <a id="staff"></a>**Staff** | quem trabalha para uma instituição e entra no hub com o usuário que ela criou no módulo Usuários, com as permissões que ela decidiu |
| <a id="tarefa"></a>**Tarefa** | atividade que um módulo entrega ao participante no quest, com ou sem prazo, listada em Tarefas da que vence primeiro à que vence por último |
| <a id="tema-de-cor"></a>**Tema de cor** | combinação de cores da identidade que gera os mesmos papéis de cor nos modos claro, escuro e alto contraste; o padrão é Branco e Preto |
| <a id="time-deboaz"></a>**Time deboaz** | equipe dona do deboaz, que atende as instituições pelo [SAC](#sac), cuida das contas de instituição e de educador autônomo no [módulo Usuários](#modulo-usuarios) da conta do time e recebe módulos próprios por código, sem custos |
| <a id="token"></a>**Token** | valor nomeado do design system (cor, tamanho, fonte, canto, sombra ou tempo) que telas e componentes usam no lugar de valores soltos |
| <a id="token-de-acesso"></a>**Token de acesso** | credencial assinada, de 15 min, que o aparelho envia em cada pedido; leva a identidade da sessão, o papel e os módulos com as abas, para a web e o mobile montarem a casca ([Autenticação `RNF-215`](17_modulo-usuario-a-autenticacao.md#RNF-215), [Autorização `RNF-219`](17_modulo-usuario-b-autorizacao.md#RNF-219)) |
| <a id="token-de-renovacao"></a>**Token de renovação** | valor aleatório de uso único que troca o token de acesso vencido por um novo sem pedir a senha; cada uso o substitui, e usar um já trocado encerra a sessão ([Autenticação `RNF-213`](17_modulo-usuario-a-autenticacao.md#RNF-213)) |
| <a id="usuario-superior"></a>**Usuário superior** | quem pede a nova geração de senha, desativa e reativa os usuários abaixo dele; a ordem é time deboaz, conta master, staff com o módulo Usuários, demais staff e participantes |
| <a id="versao-das-permissoes"></a>**Versão das permissões** | número da sessão que sobe a cada mudança nas permissões da pessoa; o token com a versão anterior é recusado e o aparelho renova para receber as permissões novas ([Autorização `RN-239`](17_modulo-usuario-b-autorizacao.md#RN-239)) |
| <a id="vista"></a>**Vista** | desenho de uma tela, feito e aprovado antes do código, mostrando os componentes e os elementos de identidade que ela vai apresentar |

---

<a id="historico"></a>

## Histórico

| Versão | Data | Mudança |
|---|---|---|
| **v46** | 2026-11-08 23:33 | Termos Card ao vivo (com na abertura, Módulos `RN-279`) e Recorte (dia, semana e mês do card, Módulos `RN-277`); status → Em revisão |
| v45 | 2026-11-07 02:55 | Status → Aprovado, junto com o Módulo Usuários v57; termos sem mudança |
| v44 | 2026-11-07 02:38 | Termo Conta qa: administrador qa próprio, lista de módulos iniciais do Key Vault, dados fictícios, volta ao estado inicial a cada inicialização e siglas “QA” reservadas em produção (Módulo Usuários v56 `RN-265`, `RN-267`, `RN-268`, `RN-269`, `RN-179`) |
| v43 | 2026-11-06 23:15 | Termo Conta qa (Módulo Usuários `RN-265`); status → Em revisão |
| v42 | 2026-11-05 12:34 | Formato `.md`: migrado de `.html` sem mudar cenários, telas nem diagramas; links entre documentos apontam para `.md` |
| v41 | 2026-11-03 22:11 | Aprovado, sem pendências em aberto |
| v40 | 2026-11-01 18:32 | Casca v07 do tema (Gherkin quebra a linha); termos sem mudança; status → Em revisão |
| v39 | 2026-11-01 12:35 | Aprovado |
| v38 | 2026-11-01 12:19 | Termos da autenticação e da autorização: aparelho conectado, papel, sessão, token de acesso, token de renovação e versão das permissões |
| v37 | 2026-11-01 03:57 | Aprovado |

---

<sub>boaz · v46 · 2026-11-08 23:33 · `1_Requisitos/99_glossario.md`</sub>
