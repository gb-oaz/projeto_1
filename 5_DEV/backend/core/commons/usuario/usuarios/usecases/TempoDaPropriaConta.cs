using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>O tempo de uma conta é só da conta master e do staff dela; para os outros, a conta não existe.</summary>
static class TempoDaPropriaConta
{
    /// <summary>A conta, se quem pede é master ou staff dela.</summary>
    public static async Task<ContaAssinante> Conta(Apoio apoio, string quemPede, string contaId, CancellationToken ct)
    {
        var quem = await apoio.Quem(quemPede, ct);
        if (quem.ContaId != contaId || quem.Tipo is not (TipoUsuario.Master or TipoUsuario.Staff)) throw FalhaDeUsuarios.ContaInexistente();
        return await apoio.Assinante(contaId, ct);
    }
}
