using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

/// <summary>As salas ao vivo, para o acesso anônimo (RF-136, RN-231).</summary>
public interface ISalas
{
    /// <summary>A sala aberta com esse código; null se não existir ou estiver fechada.</summary>
    Task<SalaAberta?> BuscarAberta(string codigo, CancellationToken ct = default);

    /// <summary>Registra um acesso anônimo novo na sala, com o apelido dele.</summary>
    Task<AcessoAnonimo> NovoAcessoAnonimo(SalaAberta sala, CancellationToken ct = default);
}
