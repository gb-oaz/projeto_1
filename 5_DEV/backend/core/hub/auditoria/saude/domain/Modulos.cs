using Boaz.Core.Hub.Auditoria.Metricas.Domain;

namespace Boaz.Core.Hub.Auditoria.Saude.Domain;

/// <summary>O estado de um módulo, pelos pedidos dos últimos 5 min (RN-316).</summary>
public enum EstadoDoModulo
{
    /// <summary>Sem falha e com p95 até 1 s.</summary>
    NoAr,
    /// <summary>p95 de 1 s a 3 s, ou a 1ª e a 2ª falha seguidas.</summary>
    Lento,
    /// <summary>A 3ª falha seguida.</summary>
    ForaDoAr,
    /// <summary>Nenhum pedido nos últimos 5 min: sem o que avaliar.</summary>
    SemPedidos,
}

/// <summary>
/// A saúde de um módulo na verificação (RF-196, RN-316): os pedidos dos últimos 5 min. Há falha quando os erros 5xx
/// passam de 1 % ou o p95 passa de 3 s; a 1ª e a 2ª falha seguidas deixam o módulo Lento, e a 3ª, Fora do ar.
/// </summary>
/// <param name="Modulo">MOD-01…</param>
/// <param name="Nome">O nome do módulo.</param>
/// <param name="Estado">O estado.</param>
/// <param name="Pedidos">Quantos pedidos nos últimos 5 min.</param>
/// <param name="Erros5xx">Com 5xx.</param>
/// <param name="TaxaErro5xx">Os 5xx sobre os pedidos, em %; null sem pedido.</param>
/// <param name="P95Ms">O p95; null sem pedido ou acima de 10 s.</param>
/// <param name="FalhasSeguidas">Quantas verificações seguidas tiveram falha.</param>
public sealed record SaudeDoModulo(
    string Modulo, string Nome, EstadoDoModulo Estado, long Pedidos, long Erros5xx, decimal? TaxaErro5xx, int? P95Ms, int FalhasSeguidas)
{
    /// <summary>Quantos minutos a verificação olha para trás.</summary>
    public const int Minutos = 5;

    /// <summary>Acima disto, o p95 é falha: 3 s.</summary>
    public const int LimiteDaFalhaMs = 3000;

    /// <summary>Acima disto, o p95 deixa o módulo Lento: 1 s.</summary>
    public const int LimiteLentoMs = 1000;

    /// <summary>A saúde seguinte do módulo, pelos pedidos dos últimos 5 min e pela anterior (null na primeira).</summary>
    public static SaudeDoModulo Seguinte(SaudeDoModulo? anterior, string modulo, ResumoDeMedicoes pedidos)
    {
        var nome = ModuloDoEndereco.Modulos.GetValueOrDefault(modulo, modulo);
        if (pedidos.Pedidos == 0) return new SaudeDoModulo(modulo, nome, EstadoDoModulo.SemPedidos, 0, 0, null, null, 0);
        var p95 = pedidos.P95Ms;
        var falhou = pedidos.Erros5xx * 100 > pedidos.Pedidos || p95 is null or > LimiteDaFalhaMs;
        var falhas = falhou ? (anterior?.FalhasSeguidas ?? 0) + 1 : 0;
        var estado = falhou
            ? falhas >= Verificacao.FalhasParaForaDoAr ? EstadoDoModulo.ForaDoAr : EstadoDoModulo.Lento
            : p95 > LimiteLentoMs ? EstadoDoModulo.Lento : EstadoDoModulo.NoAr;
        return new SaudeDoModulo(modulo, nome, estado, pedidos.Pedidos, pedidos.Erros5xx, pedidos.TaxaErro5xx, p95, falhas);
    }

    /// <summary>A posição na lista de módulos: FORA_DO_AR, LENTO, NO_AR e SEM_PEDIDOS (RF-196).</summary>
    public int Ordem => Estado switch
    {
        EstadoDoModulo.ForaDoAr => 0,
        EstadoDoModulo.Lento => 1,
        EstadoDoModulo.NoAr => 2,
        _ => 3,
    };
}

/// <summary>Os textos do estado do módulo, como no contrato.</summary>
public static class TextosDoModulo
{
    /// <summary>NO_AR, LENTO, FORA_DO_AR ou SEM_PEDIDOS.</summary>
    public static string Texto(this EstadoDoModulo estado) => estado switch
    {
        EstadoDoModulo.NoAr => "NO_AR",
        EstadoDoModulo.Lento => "LENTO",
        EstadoDoModulo.ForaDoAr => "FORA_DO_AR",
        _ => "SEM_PEDIDOS",
    };
}

/// <summary>Uma instância da aplicação no ar (RF-194, auditoria:instancia:&lt;id&gt;).</summary>
/// <param name="Id">O nome da instância.</param>
/// <param name="Versao">A versão da aplicação.</param>
/// <param name="Inicio">Quando subiu.</param>
/// <param name="UltimoSinal">A última verificação dela.</param>
public sealed record Instancia(string Id, string Versao, DateTimeOffset Inicio, DateTimeOffset UltimoSinal);

/// <summary>A saúde de todos os serviços e módulos na última verificação desta instância (RF-194).</summary>
/// <param name="Ambiente">PRODUCAO ou HOMOLOGACAO.</param>
/// <param name="VerificadoEm">Quando a verificação terminou.</param>
/// <param name="Servicos">Os 8 serviços.</param>
/// <param name="Modulos">Os módulos.</param>
/// <param name="Instancias">As instâncias no ar.</param>
public sealed record SaudeDoSistema(
    string Ambiente, DateTimeOffset VerificadoEm, IReadOnlyList<Verificacao> Servicos, IReadOnlyList<SaudeDoModulo> Modulos,
    IReadOnlyList<Instancia> Instancias)
{
    /// <summary>Quantos serviços estão Fora do ar.</summary>
    public int ForaDoAr => Servicos.Count(s => s.Estado == EstadoDoServico.ForaDoAr);

    /// <summary>Quantos serviços estão Lentos.</summary>
    public int Lentos => Servicos.Count(s => s.Estado == EstadoDoServico.Lento);

    /// <summary>FORA_DO_AR se algum serviço está fora; senão LENTO se algum está lento; senão NO_AR.</summary>
    public EstadoDoServico Geral => ForaDoAr > 0 ? EstadoDoServico.ForaDoAr : Lentos > 0 ? EstadoDoServico.Lento : EstadoDoServico.NoAr;
}
