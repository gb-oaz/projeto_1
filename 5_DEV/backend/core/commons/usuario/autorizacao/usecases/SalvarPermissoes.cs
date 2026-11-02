using Boaz.Core.Commons.Usuario.Autorizacao.Domain;
using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autorizacao.UseCases;

/// <summary>
/// Salva o conjunto inteiro das permissões, tudo ou nada (RF-161, RN-254). Ordem
/// das conferências: alvo visível (RN-235) → quem pode conceder (RN-252) →
/// If-Match (RNF-222) → campos → versão (RF-162) → módulos contratados
/// (RN-166) → só o que quem concede tem (RN-241). Com mudança, grava as
/// permissões, o registro e a versão juntos (RNF-223) e avisa as sessões.
/// </summary>
public sealed class SalvarPermissoes : ISalvarPermissoes
{
    readonly Leitor leitor;
    readonly Concessao concessao;
    readonly IPermissoesGuardadas guardadas;
    readonly ISessoesDoUsuario sessoes;
    readonly TimeProvider relogio;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public SalvarPermissoes(IUsuarios usuarios, IPermissoesGuardadas guardadas, IModulos modulos, IContratacoes contratacoes,
        ISessoesDoUsuario sessoes, TimeProvider relogio)
    {
        leitor = new Leitor(usuarios, guardadas, modulos, contratacoes, relogio);
        concessao = new Concessao(leitor);
        this.guardadas = guardadas;
        this.sessoes = sessoes;
        this.relogio = relogio;
    }

    /// <inheritdoc />
    public async Task<PermissoesSalvasDto> Executar(SalvamentoDePermissoesDto pedido, CancellationToken ct = default)
    {
        var (quem, modsDeQuem, alvo) = await concessao.Conferir(pedido.QuemPede, pedido.UsuarioId, ct);
        var versaoLida = pedido.VersaoLida();
        var novos = pedido.ModsLidos();

        var permissoes = await leitor.Guardadas(alvo.Id, ct);
        if (permissoes.Versao != versaoLida) throw FalhaDeAutorizacao.VersaoDesatualizada(permissoes.Versao, alvo.Nome);

        var modulos = await leitor.ModulosDe(alvo, ct);
        var antes = permissoes.Visiveis(modulos.Disponiveis);
        var concediveis = CalculoDePermissoes.Concediveis(quem, modsDeQuem, alvo, modulos);
        foreach (var mod in antes.Keys.Union(novos.Keys).Order(StringComparer.Ordinal))
        {
            var depois = novos.TryGetValue(mod, out var d) ? d : (Abas?)null;
            var anterior = antes.TryGetValue(mod, out var a) ? a : (Abas?)null;
            if (depois == anterior) continue;
            var modulo = modulos.Registrado(mod);
            if (modulo is null) throw FalhaDeAutorizacao.ModuloInexistente(mod);
            if (!modulos.Disponiveis.Contains(mod)) throw FalhaDeAutorizacao.ModuloNaoContratado(mod, modulo.Nome);
            if (!CalculoDePermissoes.AlvoUsa(alvo, modulo)) throw FalhaDeAutorizacao.ModuloNaoUsado(mod, modulo.Nome, alvo.Nome);
            var mexe = (depois ?? anterior!.Value).Com(anterior ?? depois!.Value);
            if (!concediveis.TryGetValue(mod, out var pode) || !pode.Contem(mexe))
            {
                throw FalhaDeAutorizacao.PermissaoAcima(mod, modulo.Nome, depois ?? anterior!.Value);
            }
        }

        var mudanca = permissoes.Salvar(novos, modulos.Disponiveis, quem.Id, relogio.GetUtcNow());
        if (mudanca is not null)
        {
            if (!await guardadas.Salvar(permissoes, mudanca, versaoLida, ct))
            {
                var atual = await leitor.Guardadas(alvo.Id, ct);
                throw FalhaDeAutorizacao.VersaoDesatualizada(atual.Versao, alvo.Nome);
            }
            await sessoes.AvisarPv(alvo.Id, permissoes.Pv, ct);
        }
        return new PermissoesSalvasDto(permissoes.Versao, Textos.Mods(permissoes.Visiveis(modulos.Disponiveis)));
    }
}
