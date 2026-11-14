using Boaz.Core.Hub.Sac.Chamados.Domain;

namespace Boaz.Core.Hub.Sac.Chamados.Dtos;

/// <summary>De que lado do SAC quem pede está (RN-288).</summary>
public enum Visao
{
    /// <summary>A conta master ou o autônomo: só os chamados da conta.</summary>
    Conta,
    /// <summary>O time deboaz (membro com MOD-02 ou administrador): todos.</summary>
    Time,
}

/// <summary>Quem pede ao SAC, como o token conferido diz.</summary>
/// <param name="UsuarioId">O sub do token.</param>
/// <param name="Papel">MASTER, AUTONOMO, MEMBRO_TIME, ADMINISTRADOR…</param>
/// <param name="ContaId">A conta do token.</param>
public sealed record QuemPede(string UsuarioId, string Papel, string ContaId)
{
    /// <summary>
    /// O lado de quem pede: a conta master e o autônomo abrem e acompanham os
    /// chamados da conta; o membro do time e o administrador atendem. O resto
    /// não usa o SAC (403 ACESSO_NEGADO).
    /// </summary>
    public Visao Visao => Papel switch
    {
        "MASTER" or "AUTONOMO" => Visao.Conta,
        "MEMBRO_TIME" or "ADMINISTRADOR" => Visao.Time,
        _ => throw FalhaDoSac.AcessoNegado(),
    };

    /// <summary>Só o time.</summary>
    public void SoTime()
    {
        if (Visao != Visao.Time) throw FalhaDoSac.AcessoNegado();
    }

    /// <summary>Só a conta.</summary>
    public void SoConta()
    {
        if (Visao != Visao.Conta) throw FalhaDoSac.AcessoNegado();
    }
}

/// <summary>O corpo de POST /sac/chamados (RF-171).</summary>
/// <param name="Categoria">A categoria, menos SENHA_CONTA_MASTER.</param>
/// <param name="Assunto">De 5 a 120 caracteres.</param>
/// <param name="Descricao">De 20 a 5.000 caracteres.</param>
/// <param name="RelacionadoA">Um chamado anterior da conta, ou null.</param>
/// <param name="Anexos">Até 5 anexos enviados antes por POST /sac/anexos.</param>
public sealed record PedidoAbrirChamado(string? Categoria, string? Assunto, string? Descricao, string? RelacionadoA, IReadOnlyList<string?>? Anexos)
{
    /// <summary>Confere a forma do pedido (RN-291, RN-292).</summary>
    public (Categoria Categoria, string Assunto, string Descricao, NumeroDoChamado? RelacionadoA, IReadOnlyList<string> Anexos) Validar()
    {
        var categoria = Domain.Textos.CategoriaDe(Categoria) is { } c && c != Domain.Categoria.SenhaContaMaster
            ? c
            : throw FalhaDoSac.DadosInvalidos("categoria");
        var assunto = Validacao.Texto(Assunto, "assunto", LimitesDoChamado.AssuntoMinimo, LimitesDoChamado.AssuntoMaximo,
            $"O campo \"assunto\" deve ter de {LimitesDoChamado.AssuntoMinimo} a {LimitesDoChamado.AssuntoMaximo} caracteres.");
        var descricao = Validacao.Texto(Descricao, "descricao", LimitesDoChamado.DescricaoMinima, LimitesDoChamado.DescricaoMaxima,
            "O campo \"descricao\" deve ter de 20 a 5.000 caracteres.");
        NumeroDoChamado? relacionado = RelacionadoA is null ? null
            : NumeroDoChamado.Ler(RelacionadoA) ?? throw FalhaDoSac.DadosInvalidos("relacionadoA");
        return (categoria, assunto, descricao, relacionado, Validacao.Anexos(Anexos));
    }
}

/// <summary>O corpo de POST /sac/chamados/{id}/mensagens (RF-173, RF-175).</summary>
/// <param name="Texto">De 1 a 5.000 caracteres.</param>
/// <param name="Anexos">Até 5 anexos.</param>
/// <param name="Interna">Nota interna: só o time.</param>
public sealed record PedidoMensagem(string? Texto, IReadOnlyList<string?>? Anexos, bool? Interna)
{
    /// <summary>Confere a forma (RN-291, RN-292).</summary>
    public (string Texto, IReadOnlyList<string> Anexos, bool Interna) Validar() =>
        (Validacao.Texto(Texto, "texto", 1, LimitesDoChamado.MensagemMaxima, "O campo \"texto\" deve ter de 1 a 5.000 caracteres."),
            Validacao.Anexos(Anexos), Interna ?? false);
}

/// <summary>O corpo de POST /sac/chamados/{id}/assumir (RF-175).</summary>
/// <param name="Substituir">Assumir no lugar de outro responsável.</param>
public sealed record PedidoAssumir(bool? Substituir);

/// <summary>O corpo de POST /sac/chamados/{id}/status (RF-176).</summary>
/// <param name="Status">AGUARDANDO_CLIENTE ou RESOLVIDO.</param>
/// <param name="Mensagem">A mensagem à conta, obrigatória.</param>
public sealed record PedidoStatus(string? Status, string? Mensagem)
{
    /// <summary>Confere a forma: um status conhecido e a mensagem.</summary>
    public (StatusDoChamado Status, string Mensagem) Validar() =>
        (Domain.Textos.StatusDe(Status) ?? throw FalhaDoSac.DadosInvalidos("status"),
            Validacao.Texto(Mensagem, "mensagem", 1, LimitesDoChamado.MensagemMaxima, "O campo \"mensagem\" deve ter de 1 a 5.000 caracteres."));
}

/// <summary>O corpo de POST /sac/chamados/{id}/reabrir (RF-177).</summary>
/// <param name="Mensagem">Por que reabre, obrigatória.</param>
public sealed record PedidoReabrir(string? Mensagem)
{
    /// <summary>Confere a mensagem.</summary>
    public string Validar() =>
        Validacao.Texto(Mensagem, "mensagem", 1, LimitesDoChamado.MensagemMaxima, "O campo \"mensagem\" deve ter de 1 a 5.000 caracteres.");
}

/// <summary>O corpo de POST /sac/chamados/{id}/avaliacao (RF-178).</summary>
/// <param name="Nota">De 1 a 5, obrigatória.</param>
/// <param name="Comentario">Até 500 caracteres.</param>
public sealed record PedidoAvaliacao(int? Nota, string? Comentario)
{
    /// <summary>Confere a nota e o comentário (RN-291, RN-296).</summary>
    public (int Nota, string? Comentario) Validar()
    {
        var nota = Nota is >= LimitesDoChamado.NotaMinima and <= LimitesDoChamado.NotaMaxima
            ? Nota.Value
            : throw FalhaDoSac.DadosInvalidos("nota", "O campo \"nota\" deve ser de 1 a 5.");
        var comentario = string.IsNullOrWhiteSpace(Comentario) ? null : Comentario.Trim();
        if (comentario?.Length > LimitesDoChamado.ComentarioMaximo)
        {
            throw FalhaDoSac.DadosInvalidos("comentario", "O campo \"comentario\" deve ter até 500 caracteres.");
        }
        return (nota, comentario);
    }
}

/// <summary>
/// Os filtros de GET /sac/chamados (RF-172, RF-174), como chegam na query.
/// Os do time (responsavel, atrasado, notaMaxima) pedidos pela conta são recusados.
/// </summary>
/// <param name="Situacao">EM_ANDAMENTO ou ENCERRADOS; sem ela, todos.</param>
/// <param name="Status">Um status.</param>
/// <param name="Categoria">Uma categoria.</param>
/// <param name="Busca">Número, sigla, nome da conta ou assunto.</param>
/// <param name="Pagina">A página, a partir de 1.</param>
/// <param name="Responsavel">nenhum ou eu (time).</param>
/// <param name="Atrasado">true (time).</param>
/// <param name="NotaMaxima">De 1 a 5 (time).</param>
public sealed record FiltroDeChamadosDto(
    string? Situacao, string? Status, string? Categoria, string? Busca, int? Pagina, string? Responsavel, string? Atrasado, string? NotaMaxima);

/// <summary>Conferências de campo comuns aos pedidos do SAC; cada uma lança DADOS_INVALIDOS com o campo.</summary>
static class Validacao
{
    /// <summary>O texto sem os espaços das pontas, com o tamanho entre os limites.</summary>
    public static string Texto(string? valor, string campo, int minimo, int maximo, string mensagem)
    {
        var texto = valor?.Trim() ?? "";
        return texto.Length >= minimo && texto.Length <= maximo ? texto : throw FalhaDoSac.DadosInvalidos(campo, mensagem);
    }

    /// <summary>Os ids dos anexos: até 5, sem repetir nem vazio (RN-292).</summary>
    public static IReadOnlyList<string> Anexos(IReadOnlyList<string?>? anexos)
    {
        var lista = anexos ?? [];
        if (lista.Count > LimitesDoChamado.Anexos) throw FalhaDoSac.LimiteAnexos();
        if (lista.Any(string.IsNullOrWhiteSpace) || lista.Distinct().Count() != lista.Count) throw FalhaDoSac.DadosInvalidos("anexos");
        return [.. lista.Select(a => a!)];
    }
}
