using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

namespace Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

/// <summary>O acesso anônimo vê só o módulo da sala, com Utilização (RF-151).</summary>
public sealed class CalcularPermissoesDoAnonimo : ICalcularPermissoesDoAnonimo
{
    /// <inheritdoc />
    public PermissoesNoTokenDto Executar(string moduloDaSala)
    {
        var token = CalculoDePermissoes.DoAnonimo(moduloDaSala);
        return new PermissoesNoTokenDto(token.Papel.Texto(), Textos.Mods(token.Mods), token.Pv);
    }
}
