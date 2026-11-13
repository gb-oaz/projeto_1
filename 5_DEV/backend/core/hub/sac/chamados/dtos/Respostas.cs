using System.Text.Json.Serialization;
using Boaz.Core.Hub.Sac.Chamados.Domain;

namespace Boaz.Core.Hub.Sac.Chamados.Dtos;

/// <summary>201 de POST /sac/chamados (RF-171).</summary>
/// <param name="Id">SAC-000042.</param>
/// <param name="Status">ABERTO.</param>
/// <param name="Categoria">A categoria.</param>
/// <param name="AbertoEm">Quando abriu.</param>
/// <param name="PrazoResposta">O prazo de primeira resposta (RN-289, RNF-229).</param>
/// <param name="Responsavel">null na abertura.</param>
public sealed record ChamadoAbertoDto(string Id, string Status, string Categoria, DateTimeOffset AbertoEm, DateTimeOffset PrazoResposta,
    string? Responsavel)
{
    /// <summary>O chamado recém-aberto.</summary>
    public static ChamadoAbertoDto De(DadosDoChamado d) =>
        new(d.Id.Valor, d.Status.Texto(), d.Categoria.Texto(), d.AbertoEm, d.PrazoResposta, d.Responsavel?.Nome);
}

/// <summary>Uma página de GET /sac/chamados: os itens do lado de quem pede, o total e a página.</summary>
/// <param name="Itens">Até 50 (<see cref="ResumoDaContaDto"/> ou <see cref="ResumoDoTimeDto"/>).</param>
/// <param name="Total">Quantos há com os filtros.</param>
/// <param name="Pagina">A página.</param>
public sealed record ListaDeChamadosDto(IReadOnlyList<object> Itens, long Total, int Pagina);

/// <summary>Um item da lista da conta (RF-172).</summary>
/// <param name="Id">SAC-000042.</param>
/// <param name="Assunto">O assunto.</param>
/// <param name="Categoria">A categoria.</param>
/// <param name="Status">O status.</param>
/// <param name="AtualizadoEm">A última mudança.</param>
/// <param name="NovaResposta">Resposta do time que a conta não abriu.</param>
public sealed record ResumoDaContaDto(string Id, string Assunto, string Categoria, string Status, DateTimeOffset AtualizadoEm, bool NovaResposta)
{
    /// <summary>O item do chamado.</summary>
    public static ResumoDaContaDto De(DadosDoChamado d) =>
        new(d.Id.Valor, d.Assunto, d.Categoria.Texto(), d.Status.Texto(), d.AtualizadoEm, d.NaoLidoPelaConta);
}

/// <summary>A conta dona, nos itens do time.</summary>
/// <param name="Id">O id da conta.</param>
/// <param name="Sigla">EBFGA.</param>
/// <param name="Nome">Escola Bento Freitas.</param>
/// <param name="Tipo">INSTITUICAO ou AUTONOMO.</param>
public sealed record ContaDto(string Id, string Sigla, string Nome, string Tipo)
{
    /// <summary>A conta do chamado; null no pedido sem login.</summary>
    public static ContaDto? De(ContaDoChamado? c) => c is null ? null : new(c.Id, c.Sigla, c.Nome, c.Tipo);
}

/// <summary>Um item da fila do time (RF-174, RF-179).</summary>
/// <param name="Id">SAC-000042.</param>
/// <param name="Assunto">O assunto.</param>
/// <param name="Categoria">A categoria.</param>
/// <param name="Status">O status.</param>
/// <param name="AtualizadoEm">A última mudança.</param>
/// <param name="NovaResposta">Mensagem da conta que o time não abriu.</param>
/// <param name="Conta">A conta dona; null no pedido sem login.</param>
/// <param name="Responsavel">Quem do time tem o chamado.</param>
/// <param name="PrazoResposta">O prazo de primeira resposta.</param>
/// <param name="Atrasado">Sem a primeira resposta depois do prazo (RN-289).</param>
/// <param name="Origem">HUB ou ENTRAR_SEM_LOGIN.</param>
/// <param name="Pedido">"ajuda-000087", só sem login.</param>
/// <param name="Instituicao">A instituição informada, só sem login.</param>
/// <param name="Contato">O contato informado, só sem login.</param>
/// <param name="Nota">A nota da avaliação, se houver.</param>
public sealed record ResumoDoTimeDto(
    string Id, string Assunto, string Categoria, string Status, DateTimeOffset AtualizadoEm, bool NovaResposta, ContaDto? Conta,
    string? Responsavel, DateTimeOffset PrazoResposta, bool Atrasado, string Origem,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Pedido,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Instituicao,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Contato,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Nota)
{
    /// <summary>O item do chamado, com o atrasado calculado agora.</summary>
    public static ResumoDoTimeDto De(DadosDoChamado d, DateTimeOffset agora) =>
        new(d.Id.Valor, d.Assunto, d.Categoria.Texto(), d.Status.Texto(), d.AtualizadoEm, d.NaoLidoPeloTime, ContaDto.De(d.Conta),
            d.Responsavel?.Nome, d.PrazoResposta, Chamado.EstaAtrasado(d, agora), d.Origem.Tipo.Texto(), d.Origem.Pedido,
            d.Origem.Instituicao, d.Origem.Contato, d.Avaliacao?.Nota);
}

/// <summary>Quem escreveu, nos itens da conversa.</summary>
/// <param name="Tipo">CONTA, TIME ou SISTEMA.</param>
/// <param name="Nome">O nome.</param>
public sealed record AutorDto(string Tipo, string Nome)
{
    /// <summary>O autor do item.</summary>
    public static AutorDto De(Autor a) => new(a.Tipo.Texto(), a.Nome);
}

/// <summary>
/// Uma mensagem ou nota interna da conversa, igual em GET /sac/chamados/{id} e
/// no evento "mensagem" do ao vivo (RNF-230).
/// </summary>
/// <param name="Id">msg-000318.</param>
/// <param name="Tipo">MENSAGEM ou NOTA_INTERNA.</param>
/// <param name="Interna">Nota interna (só o time a vê).</param>
/// <param name="Autor">Quem escreveu.</param>
/// <param name="Texto">O texto.</param>
/// <param name="Anexos">Os anexos.</param>
/// <param name="EnviadaEm">Quando.</param>
public sealed record MensagemDto(string Id, string Tipo, bool Interna, AutorDto Autor, string Texto, IReadOnlyList<string> Anexos, DateTimeOffset EnviadaEm)
{
    /// <summary>O item da conversa.</summary>
    public static MensagemDto De(ItemDaConversa i) =>
        new(i.Id, i.Tipo.Texto(), i.Tipo == TipoDeItem.NotaInterna, AutorDto.De(i.Autor), i.Texto ?? "", i.Anexos, i.Em);
}

/// <summary>Um evento da linha do tempo (RN-297).</summary>
/// <param name="Id">msg-000317.</param>
/// <param name="Evento">ABERTO, ASSUMIU, TROCOU_RESPONSAVEL, STATUS ou AVALIADO.</param>
/// <param name="De">O valor anterior.</param>
/// <param name="Para">O valor novo.</param>
/// <param name="Autor">Quem fez ("sistema" nos prazos).</param>
/// <param name="Em">Quando.</param>
public sealed record EventoDto(string Id, string Evento, string? De, string? Para, AutorDto Autor, DateTimeOffset Em)
{
    /// <summary>O evento.</summary>
    public static EventoDto Do(ItemDaConversa i) => new(i.Id, i.Evento!.Value.Texto(), i.De, i.Para, AutorDto.De(i.Autor), i.Em);
}

/// <summary>A avaliação da conta (RN-296).</summary>
/// <param name="Nota">De 1 a 5.</param>
/// <param name="Comentario">Até 500 caracteres.</param>
/// <param name="Em">Quando.</param>
public sealed record AvaliacaoDto(int Nota, string? Comentario, DateTimeOffset Em)
{
    /// <summary>A avaliação; null se não há.</summary>
    public static AvaliacaoDto? De(Avaliacao? a) => a is null ? null : new(a.Nota, a.Comentario, a.Em);
}

/// <summary>
/// 200 de GET /sac/chamados/{id} (RF-173): o chamado, as mensagens e a linha
/// do tempo do lado de quem pede. A conta não vê as notas internas, o atrasado
/// nem a origem do pedido sem login.
/// </summary>
/// <param name="Id">SAC-000042.</param>
/// <param name="Assunto">O assunto.</param>
/// <param name="Descricao">A descrição.</param>
/// <param name="Categoria">A categoria.</param>
/// <param name="Status">O status.</param>
/// <param name="Conta">A conta dona; null no pedido sem login.</param>
/// <param name="RelacionadoA">O chamado relacionado.</param>
/// <param name="Anexos">Os anexos da abertura.</param>
/// <param name="Responsavel">Quem do time tem o chamado.</param>
/// <param name="AbertoEm">Quando abriu.</param>
/// <param name="AtualizadoEm">A última mudança.</param>
/// <param name="PrazoResposta">O prazo de primeira resposta.</param>
/// <param name="ResolvidoEm">Quando resolveu.</param>
/// <param name="FechadoEm">Quando fechou.</param>
/// <param name="Avaliacao">A avaliação.</param>
/// <param name="PodeAvaliar">A conta ainda pode avaliar (RN-296).</param>
/// <param name="PodeReabrir">A conta ainda pode reabrir (RN-290).</param>
/// <param name="Mensagens">Mensagens (e, para o time, notas internas), na ordem.</param>
/// <param name="LinhaDoTempo">Os eventos, na ordem.</param>
/// <param name="Atrasado">Só o time: sem a primeira resposta depois do prazo.</param>
/// <param name="Origem">Só o time: HUB ou ENTRAR_SEM_LOGIN.</param>
/// <param name="Pedido">Só o time, sem login: "ajuda-000087".</param>
/// <param name="Instituicao">Só o time, sem login.</param>
/// <param name="Contato">Só o time, sem login.</param>
public sealed record ChamadoDto(
    string Id, string Assunto, string Descricao, string Categoria, string Status, ContaDto? Conta, string? RelacionadoA,
    IReadOnlyList<string> Anexos, string? Responsavel, DateTimeOffset AbertoEm, DateTimeOffset AtualizadoEm, DateTimeOffset PrazoResposta,
    DateTimeOffset? ResolvidoEm, DateTimeOffset? FechadoEm, AvaliacaoDto? Avaliacao, bool PodeAvaliar, bool PodeReabrir,
    IReadOnlyList<MensagemDto> Mensagens, IReadOnlyList<EventoDto> LinhaDoTempo,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Atrasado,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Origem,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Pedido,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Instituicao,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Contato)
{
    /// <summary>O chamado e os itens que quem pede vê.</summary>
    public static ChamadoDto De(Chamado chamado, IReadOnlyList<ItemDaConversa> itens, Visao visao, DateTimeOffset agora)
    {
        var d = chamado.Dados;
        var doTime = visao == Visao.Time;
        var visiveis = itens.Where(i => doTime || i.VisivelParaConta).ToList();
        return new(d.Id.Valor, d.Assunto, d.Descricao, d.Categoria.Texto(), d.Status.Texto(), ContaDto.De(d.Conta), d.RelacionadoA?.Valor,
            d.Anexos, d.Responsavel?.Nome, d.AbertoEm, d.AtualizadoEm, d.PrazoResposta, d.ResolvidoEm, d.FechadoEm,
            AvaliacaoDto.De(d.Avaliacao), !doTime && chamado.PodeAvaliar(agora), !doTime && chamado.PodeReabrir(agora),
            [.. visiveis.Where(i => i.Tipo != TipoDeItem.Evento).Select(MensagemDto.De)],
            [.. visiveis.Where(i => i.Tipo == TipoDeItem.Evento).Select(EventoDto.Do)],
            doTime ? chamado.Atrasado(agora) : null,
            doTime ? d.Origem.Tipo.Texto() : null,
            doTime ? d.Origem.Pedido : null,
            doTime ? d.Origem.Instituicao : null,
            doTime ? d.Origem.Contato : null);
    }
}

/// <summary>201 de POST /sac/chamados/{id}/mensagens (RF-173).</summary>
/// <param name="Id">msg-000318.</param>
/// <param name="Autor">O nome de quem escreveu.</param>
/// <param name="EnviadaEm">Quando.</param>
/// <param name="StatusChamado">O status do chamado depois da mensagem.</param>
public sealed record MensagemEnviadaDto(string Id, string Autor, DateTimeOffset EnviadaEm, string StatusChamado);

/// <summary>200 de POST /sac/chamados/{id}/assumir e /reabrir (RF-175, RF-177).</summary>
/// <param name="Id">SAC-000042.</param>
/// <param name="Status">O status.</param>
/// <param name="Responsavel">Quem do time tem o chamado.</param>
public sealed record ResponsavelDto(string Id, string Status, string? Responsavel)
{
    /// <summary>Do chamado.</summary>
    public static ResponsavelDto De(DadosDoChamado d) => new(d.Id.Valor, d.Status.Texto(), d.Responsavel?.Nome);
}

/// <summary>200 de POST /sac/chamados/{id}/status e /encerrar (RF-176, RF-177).</summary>
/// <param name="Id">SAC-000042.</param>
/// <param name="Status">O status novo.</param>
/// <param name="ResolvidoEm">Quando resolveu, se resolvido.</param>
public sealed record StatusDto(string Id, string Status, DateTimeOffset? ResolvidoEm)
{
    /// <summary>Do chamado.</summary>
    public static StatusDto De(DadosDoChamado d) => new(d.Id.Valor, d.Status.Texto(), d.ResolvidoEm);
}

/// <summary>201 de POST /sac/chamados/{id}/avaliacao (RF-178).</summary>
/// <param name="Id">SAC-000042.</param>
/// <param name="Status">FECHADO.</param>
/// <param name="Avaliacao">A avaliação gravada.</param>
public sealed record AvaliadoDto(string Id, string Status, AvaliacaoDto Avaliacao);

/// <summary>A abertura da conversa ao vivo (RNF-230): o chamado conferido para quem pede e de onde seguir.</summary>
/// <param name="Id">O chamado.</param>
/// <param name="Visao">O lado de quem pede.</param>
/// <param name="Desde">O número da última mensagem já entregue.</param>
public sealed record ConversaAoVivo(NumeroDoChamado Id, Visao Visao, long Desde);
