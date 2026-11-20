using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Hub.Pagamento.Configuracao.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.Domain;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.Ports.Out;
using Boaz.Core.Hub.Pagamento.Faturas.UseCases;
using Boaz.Core.Hub.Pagamento.Pagamentos.Domain;
using Boaz.Core.Hub.Pagamento.Pagamentos.Dtos;
using Boaz.Core.Hub.Pagamento.Pagamentos.Ports.Out;
using Boaz.Core.Hub.Pagamento.Pagamentos.UseCases;
using Boaz.Core.Hub.Pagamento.Recebimentos.Dtos;
using Boaz.Core.Hub.Pagamento.Recebimentos.Ports.In;

namespace Boaz.Core.Hub.Pagamento.Recebimentos.UseCases;

// dentro do namespace: "Pagamento" sozinho seria o namespace Boaz.Core.Hub.Pagamento
using Pagamento = Boaz.Core.Hub.Pagamento.Pagamentos.Domain.Pagamento;

/// <summary>
/// GET /pagamento/recebimentos/resumo e …/competencias (RF-190, RN-308), só do
/// ADMINISTRADOR (RN-298). Os números saem das somas das faturas da
/// competência, feitas no banco, sem trazer fatura por fatura (RNF-245):
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
        var numeros = new Somadas(await faturas.Somas([mes], ct));
        var porForma = TextosDoPagamento.Formas.Select(forma =>
        {
            var pagas = numeros.Com(StatusDaFatura.Paga).Where(s => s.FormaPaga == forma).ToList();
            return new PorFormaDto(forma.Texto(), Somadas.Recebido(pagas).Reais, Somadas.Quantas(pagas));
        }).ToList();
        var porTipo = Tipos.Select(tipo =>
        {
            var pagas = numeros.Com(StatusDaFatura.Paga).Where(s => s.TipoDaConta == tipo).ToList();
            var vencidas = numeros.Com(StatusDaFatura.Vencida).Where(s => s.TipoDaConta == tipo).ToList();
            return new PorTipoDeContaDto(tipo, Somadas.Recebido(pagas).Reais, Somadas.Quantas(pagas), Somadas.Valor(vencidas).Reais,
                Somadas.Quantas(vencidas));
        }).ToList();

        return new ResumoDosRecebimentosDto(mes.ToString(),
            numeros.Numero(numeros.Faturadas),
            new NumeroDto(Somadas.Recebido(numeros.Com(StatusDaFatura.Paga)).Reais, Somadas.Quantas(numeros.Com(StatusDaFatura.Paga))),
            numeros.Numero(numeros.Com(StatusDaFatura.APagar)),
            numeros.Numero(numeros.Com(StatusDaFatura.Vencida)),
            numeros.Inadimplencia, await pagamentos.Duplicidades(mes, ct), porForma, porTipo);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompetenciaDto>> Competencias(string? ate, QuemPede quem, CancellationToken ct = default)
    {
        quem.SoRecebedor();
        var mes = Ler(ate, "ate");
        var meses = new List<Competencia>(Meses);
        for (var i = 0; i < Meses; i++, mes = mes.Anterior) meses.Add(mes);
        var somas = await faturas.Somas(meses, ct);
        return [.. meses.Select(m =>
        {
            var numeros = new Somadas([.. somas.Where(s => s.Competencia == m)]);
            return new CompetenciaDto(m.ToString(), Somadas.Valor(numeros.Faturadas).Reais, Somadas.Recebido(numeros.Com(StatusDaFatura.Paga)).Reais,
                Somadas.Valor(numeros.Com(StatusDaFatura.Vencida)).Reais, numeros.Inadimplencia);
        })];
    }

    // a competência pedida; sem ela, a anterior à de hoje (a última fechada, RN-300)
    Competencia Ler(string? texto, string campo) =>
        texto is null ? Competencia.Do(Calendario.Dia(relogio.GetUtcNow())).Anterior
            : Competencia.Ler(texto) ?? throw FalhaDoPagamento.DadosInvalidos(campo);
}

/// <summary>As somas de uma competência separadas pelo status (RN-308), também para o card do deboaz (RN-307).</summary>
public sealed class Somadas(IReadOnlyList<SomaDeFaturas> somas)
{
    /// <summary>As de um status.</summary>
    public IReadOnlyList<SomaDeFaturas> Com(StatusDaFatura status) => [.. somas.Where(s => s.Status == status)];

    /// <summary>As faturadas: todas menos as canceladas.</summary>
    public IReadOnlyList<SomaDeFaturas> Faturadas => [.. somas.Where(s => s.Status != StatusDaFatura.Cancelada)];

    /// <summary>A soma do valor das faturas.</summary>
    public static Dinheiro Valor(IEnumerable<SomaDeFaturas> somas) => Dinheiro.Somar(somas.Select(s => s.Valor));

    /// <summary>O pago com juros, menos o já estornado (RN-308).</summary>
    public static Dinheiro Recebido(IEnumerable<SomaDeFaturas> somas) => Dinheiro.Somar(somas.Select(s => s.Recebido));

    /// <summary>Quantas faturas.</summary>
    public static int Quantas(IEnumerable<SomaDeFaturas> somas) => somas.Sum(s => s.Quantas);

    /// <summary>O valor e quantas.</summary>
    public NumeroDto Numero(IEnumerable<SomaDeFaturas> somas)
    {
        var lista = somas.ToList();
        return new NumeroDto(Valor(lista).Reais, Quantas(lista));
    }

    /// <summary>Vencido ÷ faturado, em %, com uma casa.</summary>
    public decimal Inadimplencia
    {
        get
        {
            var faturado = Valor(Faturadas);
            return faturado.Centavos == 0 ? 0m
                : Math.Round((decimal)Valor(Com(StatusDaFatura.Vencida)).Centavos * 100m / faturado.Centavos, 1, MidpointRounding.AwayFromZero);
        }
    }
}

/// <summary>
/// GET /pagamento/recebimentos/faturas/{id} (RF-191-api): a fatura de qualquer
/// conta, com o mesmo corpo do GET da conta, só para o ADMINISTRADOR (RN-298).
/// </summary>
public sealed class LerFaturaDoRecebedor(IFaturas faturas, IPagamentos pagamentos, ICadastroDoPagamento cadastro,
    IConfiguracoesDoPagamento configuracoes, TimeProvider relogio) : ILerFaturaDoRecebedor
{
    /// <inheritdoc />
    public async Task<FaturaDto> Executar(string id, QuemPede quem, CancellationToken ct = default)
    {
        quem.SoRecebedor();
        var fatura = await faturas.Obter(id, ct) ?? throw FalhaDoPagamento.FaturaInexistente();
        return await LerFatura.Detalhe(fatura, pagamentos, cadastro, new TaxaDoVencimento(configuracoes), Calendario.Dia(relogio.GetUtcNow()), ct);
    }
}

/// <summary>
/// GET /pagamento/recebimentos/pagamentos/{id} (RF-192-api): o pagamento de
/// qualquer conta, com a conta, quem pagou, o estornado, o saldo e os
/// estornos, só para o ADMINISTRADOR (RN-298).
/// </summary>
public sealed class LerPagamentoDoRecebedor(IPagamentos pagamentos, IEstornos estornos, IFaturas faturas, ICadastroDoPagamento cadastro)
    : ILerPagamentoDoRecebedor
{
    /// <inheritdoc />
    public async Task<PagamentoDoRecebedorDto> Executar(string id, QuemPede quem, CancellationToken ct = default)
    {
        quem.SoRecebedor();
        var pagamento = await pagamentos.Obter(id, ct) ?? throw FalhaDoPagamento.PagamentoInexistente();
        var conta = (await faturas.Obter(pagamento.FaturaId, ct))?.Conta ?? await cadastro.Conta(pagamento.ContaId, ct);
        var nomes = await cadastro.Nomes([pagamento.CriadoPor], ct);
        return PagamentoDoRecebedorDto.De(pagamento,
            new ContaDoPagamentoDto(pagamento.ContaId, conta?.Sigla ?? "", conta?.Nome ?? ""),
            nomes.GetValueOrDefault(pagamento.CriadoPor), await estornos.DoPagamento(pagamento.Id, ct));
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
    ICanalDoPagamento canal, IIntermediadores intermediadores, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : ICancelarFatura
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
                if (pagamento.IdExterno is { } externo) await intermediadores.Do(pagamento.Intermediador).Cancelar(externo, ct);
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
    IIntermediadores intermediadores, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio) : IPedirEstorno
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
            return await intermediadores.Do(pagamento.Intermediador).Estornar(
                new PedidoDeEstorno(estorno.Id, pagamento.Id, pagamento.IdExterno ?? string.Empty, estorno.Valor, estorno.TraceId), ct);
        }
        catch (Exception falha) when (falha is not OperationCanceledException)
        {
            return new ResultadoDoEstorno(SituacaoDoEstorno.Indisponivel);
        }
    }
}
