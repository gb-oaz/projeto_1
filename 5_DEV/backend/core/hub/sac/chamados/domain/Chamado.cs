namespace Boaz.Core.Hub.Sac.Chamados.Domain;

/// <summary>
/// O estado guardado do chamado (sac-documentos v01, coleção chamados).
/// </summary>
/// <param name="Id">SAC-000042.</param>
/// <param name="Versao">+1 a cada mudança.</param>
/// <param name="Conta">A conta dona; null no pedido sem login.</param>
/// <param name="AbertoPor">O usuário que abriu; null no pedido sem login.</param>
/// <param name="Origem">Hub ou o pedido sem login.</param>
/// <param name="Categoria">A categoria, que deu o prazo.</param>
/// <param name="Assunto">De 5 a 120 caracteres.</param>
/// <param name="Descricao">De 20 a 5.000 caracteres.</param>
/// <param name="RelacionadoA">Um chamado anterior da mesma conta.</param>
/// <param name="Anexos">Os anexos da abertura.</param>
/// <param name="Status">O status (RN-290).</param>
/// <param name="Responsavel">Quem do time tem o chamado.</param>
/// <param name="AbertoEm">Quando abriu.</param>
/// <param name="AtualizadoEm">A última mudança.</param>
/// <param name="PrazoResposta">O prazo de primeira resposta, fixo na abertura (RN-289).</param>
/// <param name="PrimeiraRespostaEm">A primeira mensagem do time à conta; null até ela.</param>
/// <param name="EsperandoTimeDesde">Desde quando espera o time; null na vez da conta.</param>
/// <param name="AguardandoDesde">Desde quando aguarda a conta.</param>
/// <param name="ResolvidoEm">Quando o time resolveu.</param>
/// <param name="FechadoEm">Quando fechou.</param>
/// <param name="Fechamento">Como fechou.</param>
/// <param name="Avaliacao">A avaliação única.</param>
/// <param name="NaoLidoPelaConta">Resposta nova do time que a conta não abriu.</param>
/// <param name="NaoLidoPeloTime">Mensagem nova da conta que o time não abriu.</param>
public sealed record DadosDoChamado(
    NumeroDoChamado Id, long Versao, ContaDoChamado? Conta, string? AbertoPor, Origem Origem, Categoria Categoria,
    string Assunto, string Descricao, NumeroDoChamado? RelacionadoA, IReadOnlyList<string> Anexos,
    StatusDoChamado Status, Autor? Responsavel, DateTimeOffset AbertoEm, DateTimeOffset AtualizadoEm, DateTimeOffset PrazoResposta,
    DateTimeOffset? PrimeiraRespostaEm, DateTimeOffset? EsperandoTimeDesde, DateTimeOffset? AguardandoDesde,
    DateTimeOffset? ResolvidoEm, DateTimeOffset? FechadoEm, Fechamento? Fechamento, Avaliacao? Avaliacao,
    bool NaoLidoPelaConta, bool NaoLidoPeloTime)
{
    /// <summary>Até quando fica guardado: 5 anos depois de fechado (RNF-231).</summary>
    public DateTimeOffset? GuardarAte => FechadoEm?.AddYears(LimitesDoChamado.AnosGuardado);
}

/// <summary>
/// O chamado (agregado): as mudanças de status da RN-290, o responsável, a
/// conversa e os prazos. Cada mudança soma 1 à versão e acumula em
/// <see cref="Novos"/> os itens da conversa que ela criou; a recusa é uma
/// <see cref="FalhaDoSac"/> e não muda nada.
/// </summary>
/// <param name="dados">O estado guardado.</param>
public sealed class Chamado(DadosDoChamado dados)
{
    const string Encerramos = "Encerramos por falta de resposta. Se ainda precisar, toque em Reabrir.";

    readonly List<ItemDaConversa> novos = [];

    /// <summary>O estado atual.</summary>
    public DadosDoChamado Dados { get; private set; } = dados;

    /// <summary>A versão lida, antes das mudanças desta execução.</summary>
    public long VersaoLida { get; } = dados.Versao;

    /// <summary>Os itens da conversa criados pelas mudanças desta execução.</summary>
    public IReadOnlyList<ItemDaConversa> Novos => novos;

    /// <summary>Aberto, Em atendimento ou Aguardando cliente: conta no limite de 10 (RN-294).</summary>
    public bool EmAndamento => EstaEmAndamento(Dados.Status);

    /// <summary>Os status em andamento.</summary>
    public static bool EstaEmAndamento(StatusDoChamado status) =>
        status is StatusDoChamado.Aberto or StatusDoChamado.EmAtendimento or StatusDoChamado.AguardandoCliente;

    // ── criação ──────────────────────────────────────────────────────────────

    /// <summary>A conta abre o chamado no hub (RF-171): Aberto, com o prazo em horas úteis (RN-289, RNF-229).</summary>
    public static Chamado Abrir(NumeroDoChamado id, ContaDoChamado conta, Autor autor, Categoria categoria, string assunto, string descricao,
        NumeroDoChamado? relacionadoA, IReadOnlyList<string> anexos, int horasUteis, DateTimeOffset agora)
    {
        if (categoria == Categoria.SenhaContaMaster) throw FalhaDoSac.DadosInvalidos("categoria");
        return Novo(new DadosDoChamado(id, 0, conta, autor.UsuarioId, Origem.DoHub, categoria, assunto, descricao, relacionadoA, anexos,
            StatusDoChamado.Aberto, null, agora, agora, CalendarioUtil.Somar(agora, horasUteis), null, agora, null, null, null, null, null,
            false, true), autor, agora);
    }

    /// <summary>O pedido de senha sem login vira o chamado na criação (RF-179, 17 RN-274): sem conta e sem conversa.</summary>
    public static Chamado DoPedidoDeSenha(NumeroDoChamado id, string instituicao, string contato, string aparelho, int horasUteis,
        DateTimeOffset agora)
    {
        var origem = new Origem(TipoDeOrigem.EntrarSemLogin, id.Pedido(), instituicao, contato, aparelho);
        var autor = new Autor(TipoDeAutor.Conta, null, instituicao);
        return Novo(new DadosDoChamado(id, 0, null, null, origem, Categoria.SenhaContaMaster, $"Senha da conta master · {instituicao}",
            $"Pedido sem login da tela Entrar. Contato: {contato}.", null, [], StatusDoChamado.Aberto, null, agora, agora,
            CalendarioUtil.Somar(agora, horasUteis), null, agora, null, null, null, null, null, false, true), autor, agora);
    }

    static Chamado Novo(DadosDoChamado dados, Autor autor, DateTimeOffset agora)
    {
        var chamado = new Chamado(dados);
        chamado.Mudar(d => d, agora, Evento(autor, TipoDeEvento.Aberto, null, StatusDoChamado.Aberto.Texto(), agora));
        return chamado;
    }

    // ── o time ───────────────────────────────────────────────────────────────

    /// <summary>
    /// O time assume (RF-175): Aberto passa a Em atendimento. Com outro
    /// responsável, só substituindo; assumir não cumpre o prazo (RN-289).
    /// </summary>
    public void Assumir(Autor time, bool substituir, DateTimeOffset agora)
    {
        GarantirAberto();
        var atual = Dados.Responsavel;
        if (atual is not null && !atual.Mesmo(time) && !substituir) throw FalhaDoSac.JaAssumido(atual.Nome);
        if (atual is not null && atual.Mesmo(time) && Dados.Status != StatusDoChamado.Aberto) return;
        var itens = new List<ItemDaConversa>();
        if (atual is null || !atual.Mesmo(time))
        {
            itens.Add(Evento(time, atual is null ? TipoDeEvento.Assumiu : TipoDeEvento.TrocouResponsavel, atual?.Nome, time.Nome, agora));
        }
        var status = Dados.Status == StatusDoChamado.Aberto ? StatusDoChamado.EmAtendimento : Dados.Status;
        if (status != Dados.Status) itens.Add(Evento(time, TipoDeEvento.Status, Dados.Status.Texto(), status.Texto(), agora));
        Mudar(d => d with { Responsavel = time, Status = status }, agora, [.. itens]);
    }

    /// <summary>
    /// O time escreve (RF-175): a nota interna não muda nada para a conta; a
    /// mensagem cumpre o prazo (RN-289) e, de Aberto, assume e passa a Em
    /// atendimento. O pedido sem login não tem conversa (RF-179).
    /// </summary>
    public void Responder(Autor time, string texto, IReadOnlyList<string> anexos, bool interna, DateTimeOffset agora)
    {
        GarantirAberto();
        if (interna)
        {
            Mudar(d => d, agora, Item(TipoDeItem.NotaInterna, time, texto, anexos, agora));
            return;
        }
        if (!Dados.Origem.TemConversa) throw FalhaDoSac.SemConversa();
        var itens = new List<ItemDaConversa> { Item(TipoDeItem.Mensagem, time, texto, anexos, agora) };
        var responsavel = Dados.Responsavel ?? time;
        if (Dados.Responsavel is null) itens.Add(Evento(time, TipoDeEvento.Assumiu, null, time.Nome, agora));
        var status = Dados.Status == StatusDoChamado.Aberto ? StatusDoChamado.EmAtendimento : Dados.Status;
        if (status != Dados.Status) itens.Add(Evento(time, TipoDeEvento.Status, Dados.Status.Texto(), status.Texto(), agora));
        Mudar(d => d with
        {
            Status = status,
            Responsavel = responsavel,
            PrimeiraRespostaEm = d.PrimeiraRespostaEm ?? agora,
            EsperandoTimeDesde = null,
            NaoLidoPelaConta = true,
        }, agora, [.. itens]);
    }

    /// <summary>
    /// O time pede informação ou resolve (RF-176, RN-290), sempre com a
    /// mensagem: de Em atendimento para Aguardando cliente ou Resolvido. O
    /// pedido sem login resolve de Aberto, e a mensagem fica como nota interna
    /// (RF-179, RN-289: o prazo fica cumprido).
    /// </summary>
    public void MudarStatus(Autor time, StatusDoChamado para, string texto, DateTimeOffset agora)
    {
        GarantirAberto();
        var semConversa = !Dados.Origem.TemConversa;
        var de = Dados.Status;
        var permitida = para switch
        {
            StatusDoChamado.AguardandoCliente => de == StatusDoChamado.EmAtendimento && !semConversa,
            StatusDoChamado.Resolvido => de == StatusDoChamado.EmAtendimento || (semConversa && de == StatusDoChamado.Aberto),
            _ => false,
        };
        if (!permitida)
        {
            throw de == StatusDoChamado.Aberto && para == StatusDoChamado.Resolvido
                ? FalhaDoSac.TransicaoInvalida("Responda à conta antes de resolver.")
                : FalhaDoSac.TransicaoInvalida();
        }
        var itens = new List<ItemDaConversa> { Item(semConversa ? TipoDeItem.NotaInterna : TipoDeItem.Mensagem, time, texto, [], agora) };
        if (Dados.Responsavel is null) itens.Add(Evento(time, TipoDeEvento.Assumiu, null, time.Nome, agora));
        itens.Add(Evento(time, TipoDeEvento.Status, de.Texto(), para.Texto(), agora));
        Mudar(d => d with
        {
            Status = para,
            Responsavel = d.Responsavel ?? time,
            PrimeiraRespostaEm = d.PrimeiraRespostaEm ?? agora,
            EsperandoTimeDesde = null,
            AguardandoDesde = para == StatusDoChamado.AguardandoCliente ? agora : null,
            ResolvidoEm = para == StatusDoChamado.Resolvido ? agora : d.ResolvidoEm,
            NaoLidoPelaConta = !semConversa || d.NaoLidoPelaConta,
        }, agora, [.. itens]);
    }

    // ── a conta ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A conta responde (RF-173): Aguardando cliente volta a Em atendimento; em
    /// Resolvido, a conta reabre em vez de responder (RN-290).
    /// </summary>
    public void ResponderDaConta(Autor conta, string texto, IReadOnlyList<string> anexos, DateTimeOffset agora)
    {
        GarantirAberto();
        if (Dados.Status == StatusDoChamado.Resolvido) throw FalhaDoSac.TransicaoInvalida("Chamado resolvido: toque em Reabrir para continuar.");
        var itens = new List<ItemDaConversa> { Item(TipoDeItem.Mensagem, conta, texto, anexos, agora) };
        var status = Dados.Status == StatusDoChamado.AguardandoCliente ? StatusDoChamado.EmAtendimento : Dados.Status;
        if (status != Dados.Status) itens.Add(Evento(conta, TipoDeEvento.Status, Dados.Status.Texto(), status.Texto(), agora));
        Mudar(d => d with
        {
            Status = status,
            AguardandoDesde = null,
            EsperandoTimeDesde = d.EsperandoTimeDesde ?? agora,
            NaoLidoPeloTime = true,
        }, agora, [.. itens]);
    }

    /// <summary>A conta reabre o resolvido em até 7 dias, com o mesmo responsável (RF-177, RN-290); não confere o limite (RN-294).</summary>
    public void Reabrir(Autor conta, string texto, DateTimeOffset agora)
    {
        GarantirAberto();
        if (Dados.Status != StatusDoChamado.Resolvido) throw FalhaDoSac.TransicaoInvalida();
        if (agora > Dados.ResolvidoEm!.Value + LimitesDoChamado.SeteDias) throw FalhaDoSac.PrazoReaberturaVencido();
        Mudar(d => d with
        {
            Status = StatusDoChamado.EmAtendimento,
            ResolvidoEm = null,
            EsperandoTimeDesde = agora,
            NaoLidoPeloTime = true,
        }, agora,
            Item(TipoDeItem.Mensagem, conta, texto, [], agora),
            Evento(conta, TipoDeEvento.Status, StatusDoChamado.Resolvido.Texto(), StatusDoChamado.EmAtendimento.Texto(), agora));
    }

    /// <summary>A conta encerra o chamado ainda em andamento: Fechado, pela conta (RF-177, RN-290).</summary>
    public void Encerrar(Autor conta, DateTimeOffset agora)
    {
        GarantirAberto();
        if (!EmAndamento) throw FalhaDoSac.TransicaoInvalida();
        var de = Dados.Status;
        Mudar(d => d with
        {
            Status = StatusDoChamado.Fechado,
            Fechamento = Domain.Fechamento.PelaConta,
            FechadoEm = agora,
            EsperandoTimeDesde = null,
            AguardandoDesde = null,
        }, agora, Evento(conta, TipoDeEvento.Status, de.Texto(), StatusDoChamado.Fechado.Texto(), agora));
    }

    /// <summary>
    /// A conta avalia uma vez (RF-178, RN-296): o resolvido fecha, confirmado;
    /// o fechado pelo prazo aceita a avaliação por 7 dias; o encerrado pela
    /// conta, nunca.
    /// </summary>
    public void Avaliar(Autor conta, int nota, string? comentario, DateTimeOffset agora)
    {
        if (Dados.Avaliacao is not null) throw FalhaDoSac.JaAvaliado();
        var avaliacao = new Avaliacao(nota, comentario, agora);
        var avaliado = Evento(conta, TipoDeEvento.Avaliado, null, nota.ToString(System.Globalization.CultureInfo.InvariantCulture), agora);
        if (Dados.Status == StatusDoChamado.Resolvido)
        {
            Mudar(d => d with
            {
                Status = StatusDoChamado.Fechado,
                Fechamento = Domain.Fechamento.Confirmado,
                FechadoEm = agora,
                Avaliacao = avaliacao,
            }, agora, Evento(conta, TipoDeEvento.Status, StatusDoChamado.Resolvido.Texto(), StatusDoChamado.Fechado.Texto(), agora), avaliado);
            return;
        }
        if (Dados.Status != StatusDoChamado.Fechado || Dados.Fechamento != Domain.Fechamento.Prazo) throw FalhaDoSac.TransicaoInvalida();
        if (agora > Dados.FechadoEm!.Value + LimitesDoChamado.SeteDias) throw FalhaDoSac.PrazoAvaliacaoVencido();
        Mudar(d => d with { Avaliacao = avaliacao }, agora, avaliado);
    }

    // ── o tempo ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Os 7 dias (RN-290), pelo agendamento: Aguardando cliente sem resposta
    /// passa a Resolvido, com a mensagem do sistema; Resolvido sem reabrir
    /// passa a Fechado. Devolve se mudou.
    /// </summary>
    public bool AplicarPrazos(DateTimeOffset agora)
    {
        if (Dados.Status == StatusDoChamado.AguardandoCliente && agora >= Dados.AguardandoDesde!.Value + LimitesDoChamado.SeteDias)
        {
            Mudar(d => d with
            {
                Status = StatusDoChamado.Resolvido,
                ResolvidoEm = agora,
                AguardandoDesde = null,
                NaoLidoPelaConta = true,
            }, agora,
                Item(TipoDeItem.Mensagem, Autor.Sistema, Encerramos, [], agora),
                Evento(Autor.Sistema, TipoDeEvento.Status, StatusDoChamado.AguardandoCliente.Texto(), StatusDoChamado.Resolvido.Texto(), agora));
            return true;
        }
        if (Dados.Status == StatusDoChamado.Resolvido && agora >= Dados.ResolvidoEm!.Value + LimitesDoChamado.SeteDias)
        {
            Mudar(d => d with
            {
                Status = StatusDoChamado.Fechado,
                Fechamento = Domain.Fechamento.Prazo,
                FechadoEm = agora,
            }, agora, Evento(Autor.Sistema, TipoDeEvento.Status, StatusDoChamado.Resolvido.Texto(), StatusDoChamado.Fechado.Texto(), agora));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Atrasado (RN-289): sem a primeira resposta do time depois do prazo, ainda
    /// em andamento. Calculado na leitura; a conta não vê.
    /// </summary>
    public bool Atrasado(DateTimeOffset agora) => EstaAtrasado(Dados, agora);

    /// <summary>A mesma regra, sobre o estado guardado.</summary>
    public static bool EstaAtrasado(DadosDoChamado dados, DateTimeOffset agora) =>
        dados.PrimeiraRespostaEm is null && EstaEmAndamento(dados.Status) && agora > dados.PrazoResposta;

    /// <summary>A conta ainda pode reabrir: Resolvido há até 7 dias.</summary>
    public bool PodeReabrir(DateTimeOffset agora) =>
        Dados.Status == StatusDoChamado.Resolvido && agora <= Dados.ResolvidoEm!.Value + LimitesDoChamado.SeteDias;

    /// <summary>A conta ainda pode avaliar: Resolvido, ou fechado pelo prazo há até 7 dias, sem avaliação.</summary>
    public bool PodeAvaliar(DateTimeOffset agora) =>
        Dados.Avaliacao is null && (Dados.Status == StatusDoChamado.Resolvido
            || (Dados.Fechamento == Domain.Fechamento.Prazo && agora <= Dados.FechadoEm!.Value + LimitesDoChamado.SeteDias));

    // ── apoio ────────────────────────────────────────────────────────────────

    void GarantirAberto()
    {
        if (Dados.Status == StatusDoChamado.Fechado) throw FalhaDoSac.ChamadoFechado();
    }

    void Mudar(Func<DadosDoChamado, DadosDoChamado> mudanca, DateTimeOffset agora, params ItemDaConversa[] itens)
    {
        var versao = Dados.Versao + 1;
        Dados = mudanca(Dados) with { Versao = versao, AtualizadoEm = agora };
        novos.AddRange(itens.Select(i => i with { Versao = versao }));
    }

    static ItemDaConversa Item(TipoDeItem tipo, Autor autor, string texto, IReadOnlyList<string> anexos, DateTimeOffset agora) =>
        new("", 0, tipo, autor, texto, anexos, null, null, null, agora);

    static ItemDaConversa Evento(Autor autor, TipoDeEvento evento, string? de, string? para, DateTimeOffset agora) =>
        new("", 0, TipoDeItem.Evento, autor, null, [], evento, de, para, agora);
}
