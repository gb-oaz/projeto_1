using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// Entrar no quest com o código de acesso do participante, como ele ou como um
/// responsável (RF-135, RN-228). O quest não tem limite de sessões (RN-247) e o
/// código errado conta para o bloqueio como a senha (RNF-204).
/// </summary>
public sealed class EntrarNoQuest : IEntrarNoQuest
{
    readonly IUsuarios usuarios;
    readonly ContadorDeTentativas tentativas;
    readonly AberturaDeSessao abertura;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public EntrarNoQuest(IUsuarios usuarios, IContas contas, ISessoes sessoes, ITentativas tentativas,
        IPermissoes permissoes, IAssinador assinador, TimeProvider relogio)
    {
        this.usuarios = usuarios;
        this.tentativas = new ContadorDeTentativas(tentativas, relogio);
        abertura = new AberturaDeSessao(usuarios, contas, sessoes, permissoes, new EmissorDeTokens(sessoes, assinador), relogio);
    }

    /// <inheritdoc />
    public async Task<SessaoAbertaDto> Executar(EntradaNoQuestDto pedido, CancellationToken ct = default)
    {
        var (codigo, aparelho, quem) = pedido.Validar();
        await tentativas.ConferirBloqueio(AlvoTentativa.CodigoAcesso, codigo, ct);

        var usuario = await usuarios.BuscarPorCodigoDeAcesso(codigo, ct);
        if (usuario is not { Tipo: TipoUsuario.Participante })
        {
            throw await tentativas.Errar(AlvoTentativa.CodigoAcesso, codigo, FalhaDeAutenticacao.CodigoInvalido(), ct);
        }
        await tentativas.Acertar(AlvoTentativa.CodigoAcesso, codigo, ct);

        var conta = await abertura.ContaDisponivel(usuario, primeiroAcesso: false, ct);
        return await abertura.Abrir(usuario, conta, Frente.Quest, aparelho, quem, ct);
    }
}
