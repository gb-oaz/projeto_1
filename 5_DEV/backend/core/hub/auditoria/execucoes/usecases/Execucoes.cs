using System.Text.RegularExpressions;
using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Execucoes.Domain;
using Boaz.Core.Hub.Auditoria.Execucoes.Dtos;
using Boaz.Core.Hub.Auditoria.Execucoes.Ports.In;
using Boaz.Core.Hub.Auditoria.Metricas.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Dtos;
using Boaz.Core.Hub.Auditoria.Registros.Ports.In;
using Boaz.Core.Hub.Auditoria.Registros.Ports.Out;
using Boaz.Core.Hub.Auditoria.Registros.UseCases;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;

namespace Boaz.Core.Hub.Auditoria.Execucoes.UseCases;

/// <summary>
/// GET /auditoria/execucoes (RF-203, RN-318, RN-321): as execuções do período (até 7 dias), uma por traceId, com o resultado de
/// cada uma e a contagem de todos os resultados com os outros filtros. A execução que está sendo consultada não existe
/// ainda: a consulta só é gravada depois da leitura. Sem o banco, 503 (RNF-252).
/// </summary>
public sealed class ListarExecucoes(
    ILeituraDaAuditoria leitura, INomesDaAuditoria nomes, IUltimaVerificacao ultima, IRegistrarConsulta registrar, TimeProvider relogio) : IListarExecucoes
{
    /// <inheritdoc />
    public async Task<ExecucoesDto> Executar(PedidoDeExecucoes pedido, ConsultaPedida consulta, CancellationToken ct = default)
    {
        var resultados = LerResultados(pedido.Resultado);
        if (pedido.Modulo is not null && !ModuloDoEndereco.Valido(pedido.Modulo)) throw FalhaDaAuditoria.DadosInvalidos("modulo");
        if (pedido.Acao is not null && !CatalogoDeAcoes.Existe(pedido.Acao)) throw FalhaDaAuditoria.DadosInvalidos("acao");
        var de = Periodo.Ler(pedido.De, "de");
        var ate = Periodo.Ler(pedido.Ate, "ate");
        var depois = Cursor.Ler(pedido.Depois);
        var limite = Cursor.LerLimite(pedido.Limite);
        var agora = relogio.GetUtcNow();
        var periodo = Periodo.Criar(de, ate, Periodo.MaximoDeDiasDasExecucoes, agora);
        if (ultima.BancoForaDoAr) throw FalhaDaAuditoria.BancoDeDadosFora();

        var contaId = pedido.Conta is null ? null : await nomes.IdDaConta(pedido.Conta, ct);
        var base0 = pedido.Conta is not null && contaId is null
            ? []
            : (await leitura.Execucoes(periodo, ct)).Select(p => ResumoDaExecucao.De(p, agora))
                .Where(r => (pedido.Modulo is null || r.Modulo == pedido.Modulo) && (pedido.Acao is null || r.Pontas.Acao == pedido.Acao)
                    && (contaId is null || r.Pontas.ContaId == contaId))
                .ToList();
        var contagem = new ContagemDto(
            base0.Count(r => r.Resultado == ResultadoDaExecucao.Concluida), base0.Count(r => r.Resultado == ResultadoDaExecucao.Recusada),
            base0.Count(r => r.Resultado == ResultadoDaExecucao.Falhou), base0.Count(r => r.Resultado == ResultadoDaExecucao.Interrompida),
            base0.Count(r => r.Resultado == ResultadoDaExecucao.EmAndamento));

        var lista = base0.Where(r => resultados.Count == 0 || resultados.Contains(r.Resultado))
            .OrderByDescending(r => r.Pontas.Inicio).ThenByDescending(r => r.Pontas.TraceId, StringComparer.Ordinal)
            .Where(r => depois is not { } c || r.Pontas.Inicio < c.Quando
                || (r.Pontas.Inicio == c.Quando && string.CompareOrdinal(r.Pontas.TraceId, c.Id) < 0))
            .ToList();
        var pagina = lista.Take(limite).ToList();
        var proximo = lista.Count > limite ? new Cursor(pagina[^1].Pontas.Inicio, pagina[^1].Pontas.TraceId).Texto : null;

        var usuarios = await nomes.Usuarios(pagina.Select(r => r.Pontas.QuemId).OfType<string>().Distinct(), ct);
        var contas = await nomes.Contas(pagina.Select(r => r.Pontas.ContaId).OfType<string>().Distinct(), ct);
        var itens = pagina.Select(r =>
        {
            var p = r.Pontas;
            return new ExecucaoDaListaDto(p.TraceId, p.Acao, CatalogoDeAcoes.Nome(p.Acao), r.Modulo, r.Resultado.Texto(), Formato.ComMilissegundos(p.Inicio),
                r.DuracaoMs, p.CodigoDoErro, p.QuemId is { } q ? new QuemDto(q, usuarios.GetValueOrDefault(q, q)) : null,
                p.ContaId is { } c ? new ContaDto(c, contas.GetValueOrDefault(c, c)) : null, p.Alvo);
        }).ToList();

        await registrar.Executar(consulta, ct);
        return new ExecucoesDto(periodo.De, periodo.Ate, contagem, itens, proximo);
    }

    // "FALHOU,INTERROMPIDA": um ou mais resultados; qualquer outro texto é DADOS_INVALIDOS em "resultado"
    static HashSet<ResultadoDaExecucao> LerResultados(string? texto)
    {
        var lidos = new HashSet<ResultadoDaExecucao>();
        if (texto is null) return lidos;
        foreach (var parte in texto.Split(','))
        {
            lidos.Add(TextosDaExecucao.ResultadoDe(parte.Trim()) ?? throw FalhaDaAuditoria.DadosInvalidos("resultado"));
        }
        return lidos;
    }
}

/// <summary>
/// GET /auditoria/execucoes/{traceId} (RF-202, RN-318): a execução do começo ao fim, os registros do traceId em ordem de quando.
/// A ação principal é a do 1º INICIADO; as outras são internas (RN-275). Sem o banco, 503 (RNF-252).
/// </summary>
public sealed class VerExecucao(ILeituraDaAuditoria leitura, LeitorDeRegistros leitor, IUltimaVerificacao ultima, IRegistrarConsulta registrar, TimeProvider relogio)
    : IVerExecucao
{
    /// <inheritdoc />
    public async Task<ExecucaoDto> Executar(string traceId, ConsultaPedida consulta, CancellationToken ct = default)
    {
        if (!Regex.IsMatch(traceId, "^trc-[0-9a-f]{8}$")) throw FalhaDaAuditoria.DadosInvalidos("traceId");
        if (ultima.BancoForaDoAr) throw FalhaDaAuditoria.BancoDeDadosFora();

        var registros = await leitor.Ler(await leitura.DoTrace(traceId, ct), ct);
        var execucao = Execucao.Montar(registros, relogio.GetUtcNow());

        await registrar.Executar(consulta, ct);
        return ExecucaoDto.De(execucao);
    }
}
