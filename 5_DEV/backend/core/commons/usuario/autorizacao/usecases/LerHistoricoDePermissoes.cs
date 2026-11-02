using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

/// <summary>
/// O registro de mudanças (RN-256): quem pode abrir as permissões da pessoa vê;
/// a própria pessoa não.
/// </summary>
public sealed class LerHistoricoDePermissoes : ILerHistoricoDePermissoes
{
    /// <summary>Tamanho da página.</summary>
    public const int Pagina = 50;

    readonly Concessao concessao;
    readonly IPermissoesGuardadas guardadas;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public LerHistoricoDePermissoes(IUsuarios usuarios, IPermissoesGuardadas guardadas, IModulos modulos, IContratacoes contratacoes, TimeProvider relogio)
    {
        concessao = new Concessao(new Leitor(usuarios, guardadas, modulos, contratacoes, relogio));
        this.guardadas = guardadas;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MudancaDto>> Executar(PedidoDePermissoesDto pedido, CancellationToken ct = default)
    {
        var (_, _, alvo) = await concessao.Conferir(pedido.QuemPede, pedido.UsuarioId, ct);
        return (await guardadas.Historico(alvo.Id, pedido.Ate, Pagina, ct))
            .Select(m => new MudancaDto(m.Em, m.Por, m.Versao, Textos.Mods(m.Antes), Textos.Mods(m.Depois)))
            .ToList();
    }
}
