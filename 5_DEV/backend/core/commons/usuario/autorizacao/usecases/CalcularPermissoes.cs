using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

/// <summary>Papel, módulos e pv do token, com as permissões e contratações atuais (RF-144, RF-145, RN-233).</summary>
public sealed class CalcularPermissoes : ICalcularPermissoes
{
    readonly Leitor leitor;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public CalcularPermissoes(IUsuarios usuarios, IPermissoesGuardadas guardadas, IModulos modulos, IContratacoes contratacoes, TimeProvider relogio) =>
        leitor = new Leitor(usuarios, guardadas, modulos, contratacoes, relogio);

    /// <inheritdoc />
    public async Task<PermissoesNoTokenDto> Executar(string usuarioId, CancellationToken ct = default)
    {
        var usuario = await leitor.Usuario(usuarioId, ct) ?? throw FalhaDeAutorizacao.UsuarioInexistente();
        var token = CalculoDePermissoes.Calcular(usuario, await leitor.Guardadas(usuarioId, ct), await leitor.ModulosDe(usuario, ct));
        return new PermissoesNoTokenDto(token.Papel.Texto(), Textos.Mods(token.Mods), token.Pv);
    }
}
