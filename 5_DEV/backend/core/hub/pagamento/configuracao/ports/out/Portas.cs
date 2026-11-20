using Boaz.Core.Hub.Pagamento.Configuracao.Domain;

namespace Boaz.Core.Hub.Pagamento.Configuracao.Ports.Out;

/// <summary>A coleção configuracaoPagamento: uma versão por documento, nunca apagada (RF-193).</summary>
public interface IConfiguracoesDoPagamento
{
    /// <summary>A versão mais nova; null se nenhuma foi salva (vale a padrão).</summary>
    Task<ConfiguracaoDoPagamento?> Atual(CancellationToken ct = default);

    /// <summary>A versão vigente na hora dada; null se nenhuma salva valia ainda (RN-310).</summary>
    Task<ConfiguracaoDoPagamento?> VigenteEm(DateTimeOffset quando, CancellationToken ct = default);

    /// <summary>Grava a versão nova; false se ela já existe (outro salvou antes: 412).</summary>
    Task<bool> Incluir(ConfiguracaoDoPagamento configuracao, CancellationToken ct = default);
}
