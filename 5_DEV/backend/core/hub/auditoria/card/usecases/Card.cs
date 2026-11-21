using System.Globalization;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Auditoria.Card.Domain;
using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Card.Ports.In;
using Boaz.Core.Hub.Auditoria.Card.Ports.Out;
using Boaz.Core.Hub.Auditoria.Execucoes.Domain;
using Boaz.Core.Hub.Auditoria.Metricas.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Domain;
using Boaz.Core.Hub.Auditoria.Registros.Ports.Out;
using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;

namespace Boaz.Core.Hub.Auditoria.Card.UseCases;

/// <summary>
/// GET /auditoria/card (RN-322, 15 RN-276): o card do time deboaz. Os serviços fora do ar vêm da última verificação desta
/// instância; os erros, os alertas e as interrompidas de hoje, do dia que o agendamento fecha a cada minuto; as séries somam os
/// dias na leitura (15 RN-277). Sem o banco, os números do dia ficam em zero e o selo segue.
/// </summary>
public sealed class VerCardDaAuditoria(ICardDaAuditoria card, IUltimaVerificacao ultima, TimeProvider relogio) : IVerCardDaAuditoria
{
    /// <inheritdoc />
    public async Task<CardDaAuditoriaDto> Executar(CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow();
        var hoje = CalendarioDoCard.Dia(agora);
        IReadOnlyList<DiaDoCard> dias = [];
        if (!ultima.BancoForaDoAr)
        {
            try
            {
                dias = await card.Dias(CardDaAuditoria.Desde(hoje), hoje, ct);
            }
            catch (FalhaDaAuditoria)
            {
                // o banco fora: os números do dia ficam em zero
            }
        }
        var foraDoAr = ultima.Servicos.Count(s => s.Estado == EstadoDoServico.ForaDoAr);
        return CardDaAuditoriaDto.De(new CardDaAuditoria(foraDoAr, dias, hoje, Janelas.Minuto(agora)));
    }
}

/// <summary>GET /auditoria/card/ao-vivo (RN-322, 15 RN-279): o card ao abrir e, a cada intervalo, só se a versão mudou.</summary>
public sealed class AcompanharCardDaAuditoriaAoVivo(IVerCardDaAuditoria ler) : IAcompanharCardDaAuditoriaAoVivo
{
    static readonly int[] Intervalos = [5, 15, 30, 60, 300];

    /// <inheritdoc />
    public async Task<(TimeSpan Intervalo, CardDaAuditoriaDto Card)> Abrir(string? intervalo, CancellationToken ct = default)
    {
        var segundos = intervalo is null ? 300
            : int.TryParse(intervalo, NumberStyles.None, CultureInfo.InvariantCulture, out var s) && Intervalos.Contains(s) ? s
            : throw FalhaDaAuditoria.DadosInvalidos("intervalo");
        return (TimeSpan.FromSeconds(segundos), await ler.Executar(ct));
    }

    /// <inheritdoc />
    public async Task<CardDaAuditoriaDto?> Proximo(long versao, CancellationToken ct = default)
    {
        var card = await ler.Executar(ct);
        return card.Versao == versao ? null : card;
    }
}

/// <summary>
/// O fechamento do minuto do card e o descarte (RN-322, RNF-248), pelo AgendamentoDaAuditoria: conta os erros, os alertas e as
/// execuções interrompidas do dia de Brasília até agora, guarda em cardAuditoria e, a cada hora, apaga o que passou do
/// guardarAte nas 6 coleções da Auditoria. As coleções auditoria e auditoriaErros nunca são apagadas.
/// </summary>
public sealed class FecharCardEDescartar(
    ICardDaAuditoria card, ILeituraDaAuditoria leitura, IDescarteDaAuditoria descarte, TimeProvider relogio) : IFecharCardEDescartar
{
    /// <inheritdoc />
    public async Task Executar(bool descartar, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow();
        var hoje = CalendarioDoCard.Dia(agora);
        var inicio = Janelas.InicioDoDia(hoje);
        var (erros, alertas) = await leitura.ContagemDeErros(inicio, agora, ct);
        var interrompidas = agora > inicio
            ? (await leitura.Execucoes(new Periodo(inicio, agora), ct)).Count(p => ResumoDaExecucao.De(p, agora).Resultado == ResultadoDaExecucao.Interrompida)
            : 0;
        await card.Fechar(new DiaDoCard(hoje, erros, alertas, interrompidas, 0), ct);
        if (descartar) await descarte.ApagarVencidos(agora, ct);
    }
}
