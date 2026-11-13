using System.Globalization;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.Ports.In;
using Boaz.Core.Hub.Sac.Chamados.Ports.Out;

namespace Boaz.Core.Hub.Sac.Chamados.UseCases;

/// <summary>
/// GET /sac/chamados (RF-172, RF-174): a conta vê os dela, do atualizado por
/// último ao mais antigo, com a resposta nova; o time vê todos, quem espera o
/// time há mais tempo primeiro, com a conta, o responsável, o prazo e o
/// atrasado. Os filtros do time pedidos pela conta são recusados. Até 50 por página.
/// </summary>
public sealed class ListarChamados(IChamados chamados, TimeProvider relogio) : IListarChamados
{
    /// <summary>Até 50 por página.</summary>
    public const int PorPagina = 50;

    static readonly StatusDoChamado[] EmAndamento = [StatusDoChamado.Aberto, StatusDoChamado.EmAtendimento, StatusDoChamado.AguardandoCliente];
    static readonly StatusDoChamado[] Encerrados = [StatusDoChamado.Resolvido, StatusDoChamado.Fechado];

    /// <inheritdoc />
    public async Task<ListaDeChamadosDto> Executar(FiltroDeChamadosDto filtro, QuemPede quem, CancellationToken ct = default)
    {
        var visao = quem.Visao;
        if (visao == Visao.Conta)
        {
            if (filtro.Responsavel is not null) throw FalhaDoSac.DadosInvalidos("responsavel");
            if (filtro.Atrasado is not null) throw FalhaDoSac.DadosInvalidos("atrasado");
            if (filtro.NotaMaxima is not null) throw FalhaDoSac.DadosInvalidos("notaMaxima");
        }
        IReadOnlyList<StatusDoChamado>? status = filtro.Situacao switch
        {
            null => null,
            "EM_ANDAMENTO" => EmAndamento,
            "ENCERRADOS" => Encerrados,
            _ => throw FalhaDoSac.DadosInvalidos("situacao"),
        };
        if (filtro.Status is not null)
        {
            var um = Domain.Textos.StatusDe(filtro.Status) ?? throw FalhaDoSac.DadosInvalidos("status");
            status = [.. (status ?? [um]).Where(s => s == um)];
        }
        Categoria? categoria = filtro.Categoria is null ? null : Domain.Textos.CategoriaDe(filtro.Categoria) ?? throw FalhaDoSac.DadosInvalidos("categoria");
        var pagina = filtro.Pagina ?? 1;
        if (pagina < 1) throw FalhaDoSac.DadosInvalidos("pagina");
        var (semResponsavel, responsavelId) = filtro.Responsavel switch
        {
            null => (false, null),
            "nenhum" => (true, null),
            "eu" => (false, quem.UsuarioId),
            _ => throw FalhaDoSac.DadosInvalidos("responsavel"),
        };
        var atrasados = filtro.Atrasado switch
        {
            null or "false" => false,
            "true" => true,
            _ => throw FalhaDoSac.DadosInvalidos("atrasado"),
        };
        int? notaMaxima = filtro.NotaMaxima is null ? null
            : int.TryParse(filtro.NotaMaxima, NumberStyles.None, CultureInfo.InvariantCulture, out var nota) && nota is >= 1 and <= 5
                ? nota
                : throw FalhaDoSac.DadosInvalidos("notaMaxima");
        var agora = relogio.GetUtcNow();
        var busca = string.IsNullOrWhiteSpace(filtro.Busca) ? null : filtro.Busca.Trim();
        var resultado = await chamados.Listar(new FiltroDeChamados(visao == Visao.Conta ? quem.ContaId : null, status, categoria, busca,
            semResponsavel, responsavelId, atrasados, notaMaxima, agora, pagina, PorPagina), ct);
        IReadOnlyList<object> itens = visao == Visao.Conta
            ? [.. resultado.Itens.Select(ResumoDaContaDto.De)]
            : [.. resultado.Itens.Select(d => ResumoDoTimeDto.De(d, agora))];
        return new ListaDeChamadosDto(itens, resultado.Total, pagina);
    }
}
