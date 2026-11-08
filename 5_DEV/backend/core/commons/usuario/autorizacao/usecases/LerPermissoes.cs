using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

/// <summary>
/// Abre as permissões de alguém (RF-160): os módulos que ele tem e a conta tem
/// agora (RN-238), a versão para o If-Match e o que quem pede pode conceder.
/// </summary>
public sealed class LerPermissoes : ILerPermissoes
{
    readonly Leitor leitor;
    readonly Concessao concessao;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public LerPermissoes(IUsuarios usuarios, IPermissoesGuardadas guardadas, IModulos modulos, IContratacoes contratacoes, TimeProvider relogio)
    {
        leitor = new Leitor(usuarios, guardadas, modulos, contratacoes, relogio);
        concessao = new Concessao(leitor);
    }

    /// <inheritdoc />
    public async Task<PermissoesLidasDto> Executar(PedidoDePermissoesDto pedido, CancellationToken ct = default)
    {
        var (quem, modsDeQuem, alvo) = await concessao.Conferir(pedido.QuemPede, pedido.UsuarioId, ct);
        var modulos = await leitor.ModulosDe(alvo, ct);
        var permissoes = await leitor.Guardadas(alvo.Id, ct);
        var concediveis = Concediveis(quem, modsDeQuem, alvo, modulos);
        return new PermissoesLidasDto(alvo.Id, permissoes.Versao, Textos.Mods(permissoes.Visiveis(modulos.Disponiveis)), concediveis);
    }

    /// <summary>O que quem pede pode conceder ao alvo, com o nome de cada módulo (RN-241).</summary>
    static List<ConcedivelDto> Concediveis(UsuarioAutorizado quem, IReadOnlyDictionary<string, Abas> modsDeQuem, UsuarioAutorizado alvo,
        ModulosDaConta modulos) =>
        CalculoDePermissoes.Concediveis(quem, modsDeQuem, alvo, modulos)
            .Select(c => new ConcedivelDto(c.Key, modulos.Registrado(c.Key)!.Nome, c.Value.ToString()))
            .ToList();
}
