using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Hub.Pagamento.Configuracao.Dtos;
using Boaz.Core.Hub.Pagamento.Faturas.Dtos;

namespace Boaz.Core.Hub.Pagamento.Configuracao.Ports.In;

/// <summary>GET /pagamento/recebimentos/configuracao (RF-193): só o ADMINISTRADOR.</summary>
public interface ILerConfiguracao
{
    /// <summary>A versão atual; sem PUT ainda, a padrão (versão 1).</summary>
    Task<ConfiguracaoDto> Executar(QuemPede quem, CancellationToken ct = default);
}

/// <summary>PUT /pagamento/recebimentos/configuracao (RF-193): grava uma versão nova.</summary>
public interface ISalvarConfiguracao
{
    /// <summary>Confere o corpo e o If-Match e grava a versão seguinte; auditado como CONFIGURACAO_SALVA.</summary>
    Task<ConfiguracaoDto> Executar(PedidoDeConfiguracao pedido, int? versaoLida, QuemPede quem, TraceId traceId, CancellationToken ct = default);
}

/// <summary>GET /pagamento/formas (RF-184): as formas ligadas, para quem paga.</summary>
public interface ILerFormas
{
    /// <summary>As formas ligadas agora, na ordem da tela.</summary>
    Task<FormasDto> Executar(QuemPede quem, CancellationToken ct = default);
}
