using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;

namespace Boaz.Core.Hub.Sac.Chamados.Ports.In;

/// <summary>POST /sac/chamados (RF-171, RN-291, RN-294).</summary>
public interface IAbrirChamado
{
    /// <summary>A conta abre o chamado.</summary>
    Task<ChamadoAbertoDto> Executar(PedidoAbrirChamado pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default);
}

/// <summary>GET /sac/chamados (RF-172, RF-174).</summary>
public interface IListarChamados
{
    /// <summary>A lista do lado de quem pede.</summary>
    Task<ListaDeChamadosDto> Executar(FiltroDeChamadosDto filtro, QuemPede quem, CancellationToken ct = default);
}

/// <summary>GET /sac/chamados/{id} (RF-173, RN-288): marca como lido.</summary>
public interface ILerChamado
{
    /// <summary>O chamado, as mensagens e a linha do tempo.</summary>
    Task<ChamadoDto> Executar(string id, QuemPede quem, CancellationToken ct = default);
}

/// <summary>POST /sac/chamados/{id}/mensagens (RF-173, RF-175, RF-179).</summary>
public interface IEnviarMensagem
{
    /// <summary>A mensagem ou nota interna.</summary>
    Task<MensagemEnviadaDto> Executar(string id, PedidoMensagem pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default);
}

/// <summary>POST /sac/chamados/{id}/assumir (RF-175).</summary>
public interface IAssumirChamado
{
    /// <summary>O time assume.</summary>
    Task<ResponsavelDto> Executar(string id, PedidoAssumir pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default);
}

/// <summary>POST /sac/chamados/{id}/status (RF-176, RF-179, RN-290).</summary>
public interface IMudarStatus
{
    /// <summary>O time pede informação ou resolve.</summary>
    Task<StatusDto> Executar(string id, PedidoStatus pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default);
}

/// <summary>POST /sac/chamados/{id}/reabrir (RF-177, RN-290, RN-294).</summary>
public interface IReabrirChamado
{
    /// <summary>A conta reabre em até 7 dias.</summary>
    Task<ResponsavelDto> Executar(string id, PedidoReabrir pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default);
}

/// <summary>POST /sac/chamados/{id}/encerrar (RF-177, RN-290).</summary>
public interface IEncerrarChamado
{
    /// <summary>A conta encerra.</summary>
    Task<StatusDto> Executar(string id, QuemPede quem, TraceId traceId, CancellationToken ct = default);
}

/// <summary>POST /sac/chamados/{id}/avaliacao (RF-178, RN-296).</summary>
public interface IAvaliarChamado
{
    /// <summary>A conta avalia uma vez.</summary>
    Task<AvaliadoDto> Executar(string id, PedidoAvaliacao pedido, QuemPede quem, TraceId traceId, CancellationToken ct = default);
}

/// <summary>GET /sac/chamados/{id}/ao-vivo (RNF-230): as mensagens novas, na ordem, sem nota interna para a conta.</summary>
public interface IAcompanharChamado
{
    /// <summary>Confere o acesso como o GET do chamado e marca de onde seguir (a última mensagem entregue, ou a última agora).</summary>
    Task<ConversaAoVivo> Abrir(string id, string? ultimoId, QuemPede quem, CancellationToken ct = default);

    /// <summary>As mensagens depois da última entregue, ou nenhuma depois de esperar o aviso até o tempo dado.</summary>
    Task<(IReadOnlyList<MensagemDto> Mensagens, ConversaAoVivo Conversa)> Proximas(ConversaAoVivo conversa, TimeSpan espera, CancellationToken ct = default);
}

/// <summary>O pedido de senha sem login (17 RF-111, 18 RF-179): vira o chamado na criação.</summary>
public interface IRegistrarPedidoDeSenha
{
    /// <summary>Abre o chamado SENHA_CONTA_MASTER e devolve o número dele.</summary>
    Task<NumeroDoChamado> Executar(string aparelho, string instituicao, string contato, DateTimeOffset quando, CancellationToken ct = default);
}

/// <summary>Os 7 dias de Aguardando cliente e de Resolvido (RN-290), pelo AgendamentoDoSac.</summary>
public interface IAplicarPrazos
{
    /// <summary>Muda os chamados vencidos; devolve quantos mudaram.</summary>
    Task<int> Executar(CancellationToken ct = default);
}
