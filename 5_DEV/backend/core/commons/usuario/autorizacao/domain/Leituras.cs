namespace Boaz.Core.Commons.Usuario.Autorizacao.Domain;

// O que a autorização lê de Usuários, do registro de módulos (15_modulos) e
// das contratações (16_modulo-catalogo) pelas portas de saída.

/// <summary>Uma pessoa, como a autorização a vê.</summary>
/// <param name="Id">Id do usuário.</param>
/// <param name="ContaId">Conta a que pertence.</param>
/// <param name="Nome">Nome completo, usado nas mensagens.</param>
/// <param name="Papel">Papel (RN-232).</param>
/// <param name="CriadoPor">Quem a criou, para a ordem de superior; null em quem criou a própria conta.</param>
public sealed record UsuarioAutorizado(string Id, string ContaId, string Nome, Papel Papel, string? CriadoPor);

/// <summary>Uma linha do registro de módulos.</summary>
/// <param name="Id">MOD-NN.</param>
/// <param name="Nome">Nome do módulo (Presença).</param>
/// <param name="Tipo">Da conta, contratável ou do time.</param>
/// <param name="QuemUsa">Perfis e frentes de "Quem usa".</param>
public sealed record ModuloRegistrado(string Id, string Nome, TipoDeModulo Tipo, IReadOnlySet<(Perfil Perfil, FrenteDeUso Frente)> QuemUsa)
{
    /// <summary>O perfil usa o módulo nessa frente.</summary>
    public bool Usa(Perfil perfil, FrenteDeUso frente) => QuemUsa.Contains((perfil, frente));

    /// <summary>Algum perfil usa o módulo nessa frente.</summary>
    public bool TemUsoNo(FrenteDeUso frente) => QuemUsa.Any(q => q.Frente == frente);
}
