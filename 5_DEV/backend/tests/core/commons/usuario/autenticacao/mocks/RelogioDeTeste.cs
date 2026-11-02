namespace Boaz.Core.Commons.Tests.Usuario.Autenticacao.Mocks;

/// <summary>Relógio parado que o teste avança à mão.</summary>
public sealed class RelogioDeTeste(DateTimeOffset inicio) : TimeProvider
{
    /// <summary>Começa em 2026-11-01 09:00 UTC, a manhã dos exemplos do requisito.</summary>
    public RelogioDeTeste() : this(new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero)) { }

    /// <summary>A hora atual do teste.</summary>
    public DateTimeOffset Agora { get; set; } = inicio;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => Agora;

    /// <summary>Passa o tempo.</summary>
    public void Avancar(TimeSpan tempo) => Agora += tempo;
}
