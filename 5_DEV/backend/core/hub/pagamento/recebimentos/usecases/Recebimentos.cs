using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Hub.Pagamento.Configuracao.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.UseCases;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;
using Boaz.Core.Hub.Pagamento.Pagamentos.UseCases;
using Boaz.Core.Hub.Pagamento.Recebimentos.Dtos;
using Boaz.Core.Hub.Pagamento.Recebimentos.Ports.In;

namespace Boaz.Core.Hub.Pagamento.Recebimentos.UseCases;

// dentro do namespace: "Pagamento" sozinho seria o namespace Boaz.Core.Hub.Pagamento
using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

/// <summary>
/// GET /pagamento/recebimentos/resumo e …/competencias (RF-190, RN-308), só do
/// ADMINISTRADOR (RN-298). Os números saem das faturas da competência:
/// faturado sem as canceladas; recebido = o pago com juros menos o estornado;
/// a receber = "A_PAGAR"; vencido = "VENCIDA", sem os juros; inadimplência =
/// vencido ÷ faturado, em %, com uma casa.
/// </summary>
public sealed class LerRecebimentos(IFaturas faturas, IPagamentos pagamentos, TimeProvider relogio) : ILerRecebimentos
{
    /// <summary>Quantas competências a série traz.</summary>
    public const int Meses = 12;

    /// <summary>Os tipos de conta, na ordem da tela.</summary>
    public static readonly IReadOnlyList<string> Tipos = ["INSTITUICAO", "AUTONOMO"];

    /// <inheritdoc />
    public async Task<ResumoDosRecebimentosDto> Resumo(string? competencia, QuemPede quem, CancellationToken ct = default)
    {
        quem.SoRecebedor();
        var mes = Ler(competencia, "competencia");
        var daCompetencia = await faturas.Listar(new FiltroDeFaturas(null, [], mes), ct);
        var numeros = Separadas.De(daCompetencia);

        var tentativas = await pagamentos.DasFaturas(daCompetencia.Select(f => f.Id), ct);
        var pagaram = tentativas.ToDictionary(p => p.Id);
        var porForma = TextosDoPagamento.Formas.Select(forma =>
        {
            var pagas = numeros.Pagas.Where(f => f.PagaPor is { } id && pagaram.TryGetValue(id, out var p) && p.Forma == forma).ToList();
            return new PorFormaDto(forma.Texto(), Separadas.Recebido(pagas).Reais, pagas.Count);
        }).ToList();
        var porTipo = Tipos.Select(tipo =>
        {
            var doTipo = Separadas.De(daCompetencia.Where(f => f.Conta.Tipo == tipo).ToList());
            return new PorTipoDeContaDto(tipo, Separadas.Recebido(doTipo.Pagas).Reais, doTipo.Pagas.Count, Separadas.Valor(doTipo.Vencidas).Reais,
                doTipo.Vencidas.Count);
        }).ToList();

        return new ResumoDosRecebimentosDto(mes.ToString(),
            new NumeroDto(Separadas.Valor(numeros.Faturadas).Reais, numeros.Faturadas.Count),
            new NumeroDto(Separadas.Recebido(numeros.Pagas).Reais, numeros.Pagas.Count),
            new NumeroDto(Separadas.Valor(numeros.APagar).Reais, numeros.APagar.Count),
            new NumeroDto(Separadas.Valor(numeros.Vencidas).Reais, numeros.Vencidas.Count),
            numeros.Inadimplencia, tentativas.Count(p => p.Duplicidade && p.Confirmado), porForma, porTipo);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompetenciaDto>> Competencias(string? ate, QuemPede quem, CancellationToken ct = default)
    {
        quem.SoRecebedor();
        var mes = Ler(ate, "ate");
        var lista = new List<CompetenciaDto>(Meses);
        for (var i = 0; i < Meses; i++, mes = mes.Anterior)
        {
            var numeros = Separadas.De(await faturas.Listar(new FiltroDeFaturas(null, [], mes), ct));
            lista.Add(new CompetenciaDto(mes.ToString(), Separadas.Valor(numeros.Faturadas).Reais, Separadas.Recebido(numeros.Pagas).Reais,
                Separadas.Valor(numeros.Vencidas).Reais, numeros.Inadimplencia));
        }
        return lista;
    }

    // a competência pedida; sem ela, a anterior à de hoje (a última fechada, RN-300)
    Competencia Ler(string? texto, string campo) =>
        texto is null ? Competencia.Do(Calendario.Dia(relogio.GetUtcNow())).Anterior
            : Competencia.Ler(texto) ?? throw FalhaDoPagamento.DadosInvalidos(campo);

    /// <summary>As faturas de uma competência separadas pelo status (RN-308).</summary>
    sealed record Separadas(List<Fatura> Faturadas, List<Fatura> APagar, List<Fatura> Vencidas, List<Fatura> Pagas)
    {
        public static Separadas De(IReadOnlyList<Fatura> faturas) => new(
            [.. faturas.Where(f => f.Status != StatusDaFatura.Cancelada)],
            [.. faturas.Where(f => f.Status == StatusDaFatura.APagar)],
            [.. faturas.Where(f => f.Status == StatusDaFatura.Vencida)],
            [.. faturas.Where(f => f.Status == StatusDaFatura.Paga)]);

        public static Dinheiro Valor(IEnumerable<Fatura> faturas) => Dinheiro.Somar(faturas.Select(f => f.Valor));

        // o pago com juros, menos o já estornado (RN-308)
        public static Dinheiro Recebido(IEnumerable<Fatura> faturas) => Dinheiro.Somar(faturas.Select(f => f.ValorPago - f.Devolvido));

        public decimal Inadimplencia
        {
            get
            {
                var faturado = Valor(Faturadas);
                return faturado.Centavos == 0 ? 0m
                    : Math.Round((decimal)Valor(Vencidas).Centavos * 100m / faturado.Centavos, 1, MidpointRounding.AwayFromZero);
            }
        }
    }
}

/// <summary>
/// GET /pagamento/recebimentos/faturas (RF-191): as faturas de todas as contas,
/// só para o ADMINISTRADOR (RN-298), filtradas pela competência, pela situação
/// (A_PAGAR, VENCIDA, PAGAS ou TODAS, o padrão) e pela busca na sigla, no nome
/// da conta ou no número; do vencimento mais antigo e, no mesmo vencimento, do
/// menor número; 50 por página. A conta desativada segue com as faturas (RN-313).
/// </summary>
public sealed class ListarFaturasDoRecebedor(IFaturas faturas, TimeProvider relogio) : IListarFaturasDoRecebedor
{
    /// <inheritdoc />
    public async Task<FaturasDoRecebedorDto> Executar(string? competencia, string? situacao, string? busca, int? pagina, QuemPede quem,
        CancellationToken ct = default)
    {
        quem.SoRecebedor();
        Competencia? mes = competencia is null ? null : Competencia.Ler(competencia) ?? throw FalhaDoPagamento.DadosInvalidos("competencia");
        IReadOnlyList<StatusDaFatura> status = situacao switch
        {
            null or "TODAS" => [],
            "A_PAGAR" => [StatusDaFatura.APagar],
            "VENCIDA" => [StatusDaFatura.Vencida],
            "PAGAS" => [StatusDaFatura.Paga],
            _ => throw FalhaDoPagamento.DadosInvalidos("situacao"),
        };
        var numero = pagina ?? 1;
        if (numero < 1) throw FalhaDoPagamento.DadosInvalidos("pagina");

        var hoje = Calendario.Dia(relogio.GetUtcNow());
        var texto = string.IsNullOrWhiteSpace(busca) ? null : busca.Trim();
        var achadas = (await faturas.Listar(new FiltroDeFaturas(null, status, mes, texto), ct))
            .OrderBy(f => f.Vencimento).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
        return new FaturasDoRecebedorDto(
            [.. achadas.Skip((numero - 1) * ListarFaturas.PorPagina).Take(ListarFaturas.PorPagina).Select(f => FaturaDoRecebedorDto.De(f, hoje))],
            achadas.Count);
    }
}

/// <summary>
/// POST /pagamento/recebimentos/faturas/{id}/cancelar (RF-191): o
/// ADMINISTRADOR cancela a fatura em aberto com o motivo; a paga se estorna
/// (409 FATURA_PAGA). Os pagamentos em aberto da fatura são cancelados e o
/// aviso de atraso, encerrado. Auditado como FATURA_CANCELADA (RN-309).
/// </summary>
public sealed class CancelarFatura(
    IFaturas faturas, IPagamentos pagamentos, ICadastroDoPagamento cadastro, IAvisosDeAtraso avisos, IConfiguracoesDoPagamento configuracoes,
    ICanalDoPagamento canal, IIntermediador intermediador, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : ICancelarFatura
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task<FaturaDto> Executar(string id, PedidoDeCancelamento pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.FaturaCancelada, quem.ContaId, quem.UsuarioId, id), async andamento =>
        {
            quem.SoRecebedor();
            if (string.IsNullOrWhiteSpace(pedido.Motivo)) throw FalhaDoPagamento.DadosInvalidos("motivo");
            var fatura = await faturas.Obter(id, ct) ?? throw FalhaDoPagamento.FaturaInexistente();
            var agora = relogio.GetUtcNow();
            fatura.Cancelar(pedido.Motivo.Trim(), quem.UsuarioId, agora, traceId.Valor);
            if (!await faturas.Salvar(fatura, ct)) throw Baixas.Concorrencia(fatura.Id);

            foreach (var pagamento in (await pagamentos.DasFaturas([fatura.Id], ct)).Where(p => p.EmAberto))
            {
                if (!pagamento.Cancelar(agora, traceId.Valor, quem.UsuarioId) || !await pagamentos.Salvar(pagamento, ct)) continue;
                await faturas.Liberar(fatura.Id, pagamento.Id, ct);
                if (pagamento.IdExterno is { } externo) await intermediador.Cancelar(externo, ct);
                await canal.Publicar(pagamento.Id, ct);
            }
            await avisos.Encerrar(fatura.Id, agora, ct);
            await auditada.Concluir(andamento.Atual, ct);
            return await LerFatura.Detalhe(fatura, pagamentos, cadastro, new TaxaDoVencimento(configuracoes), Calendario.Dia(agora), ct);
        }, ct);
}

/// <summary>
/// POST /pagamento/recebimentos/pagamentos/{id}/estornos (RF-192, RN-306): o
/// ADMINISTRADOR pede ao banco o estorno total ou parcial de um pagamento por
/// cartão ou PIX confirmado, até o saldo; o boleto se estorna pelo SAC. O
/// pagamento fica "ESTORNO_PENDENTE" até o aviso do banco; sem resposta do
/// banco, nada muda (503). Auditado como ESTORNO_PEDIDO, com o pagamento como
/// alvo (RN-309).
/// </summary>
public sealed class PedirEstorno(
    IPagamentos pagamentos, IEstornos estornos, IFaturas faturas, INumeracaoDoPagamento numeracao, ICanalDoPagamento canal,
    IIntermediador intermediador, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IPedirEstorno
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task<EstornoDto> Executar(string pagamentoId, PedidoDeEstornoDto pedido, QuemPede quem, TraceId traceId,
        CancellationToken ct = default) =>
        auditada.Auditar(new Execucao(traceId, AcaoAuditada.EstornoPedido, quem.ContaId, quem.UsuarioId, pagamentoId), async andamento =>
        {
            quem.SoRecebedor();
            var (valor, motivo) = pedido.Validar();
            var pagamento = await pagamentos.Obter(pagamentoId, ct) ?? throw FalhaDoPagamento.PagamentoInexistente();
            var agora = relogio.GetUtcNow();
            pagamento.PedirEstorno(valor, agora, traceId.Valor, quem.UsuarioId);
            if (!await pagamentos.Salvar(pagamento, ct)) throw FalhaDoPagamento.EstornoEmAndamento();

            var id = Numeros.De(Numeros.Estornos, await numeracao.Proximo(Numeros.Estornos, ct));
            var estorno = Estorno.Pedir(id, pagamento, valor, motivo, agora, quem.UsuarioId, traceId.Valor);
            await estornos.Incluir(estorno, ct);

            var resultado = await Estornar(estorno, pagamento, ct);
            if (resultado.Situacao == SituacaoDoEstorno.Indisponivel)
            {
                // o banco não respondeu: nada foi devolvido, o pagamento volta como estava
                await estornos.Remover(estorno.Id, ct);
                pagamento.DesfazerEstorno(relogio.GetUtcNow(), traceId.Valor, quem.UsuarioId);
                if (!await pagamentos.Salvar(pagamento, ct)) throw Baixas.Concorrencia(pagamento.Id);
                throw FalhaDoPagamento.BancoIndisponivel();
            }

            estorno.Enviado(resultado.IdExterno);
            var concluido = resultado.Situacao == SituacaoDoEstorno.Concluido;
            if (resultado.Situacao != SituacaoDoEstorno.Pendente && (concluido ? estorno.Concluir(agora, traceId.Valor) : estorno.Recusar(agora, traceId.Valor)))
            {
                await ReceberAvisoDoBanco.AplicarNoPagamento(estorno, pagamento, concluido, agora, traceId.Valor, pagamentos, faturas, canal, ct);
            }
            else await canal.Publicar(pagamento.Id, ct);
            if (!await estornos.Salvar(estorno, ct)) throw Baixas.Concorrencia(estorno.Id);

            await auditada.Concluir(andamento.Atual, ct);
            return new EstornoDto(estorno.Id, pagamento.Id, estorno.Valor.Reais, estorno.Motivo, estorno.Status.Texto(), estorno.PedidoEm);
        }, ct);

    async Task<ResultadoDoEstorno> Estornar(Estorno estorno, Pagamento pagamento, CancellationToken ct)
    {
        try
        {
            return await intermediador.Estornar(
                new PedidoDeEstorno(estorno.Id, pagamento.Id, pagamento.IdExterno ?? string.Empty, estorno.Valor, estorno.TraceId), ct);
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            return new ResultadoDoEstorno(SituacaoDoEstorno.Indisponivel);
        }
    }
}
