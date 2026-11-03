namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;

/// <summary>As contratações (16_modulo-catalogo, coleção contratacoes).</summary>
public interface IContratacoes
{
    /// <summary>Os ids dos módulos contratados pela conta e em vigor agora.</summary>
    Task<IReadOnlySet<string>> EmVigor(string contaId, DateTimeOffset agora, CancellationToken ct = default);
}
