using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// Primeiro acesso (RF-143): o comprovante recebido com a senha gerada prova que
/// ela foi anotada e abre a sessão do hub. Vale uma vez e por 72 h (RN-182).
/// </summary>
public sealed class ConfirmarSenha : IConfirmarSenha
{
    /// <summary>Validade da geração de senha.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(72);

    readonly IUsuarios usuarios;
    readonly IGeracoesDeSenha geracoes;
    readonly AberturaDeSessao abertura;
    readonly TimeProvider relogio;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public ConfirmarSenha(IUsuarios usuarios, IContas contas, IGeracoesDeSenha geracoes, ISessoes sessoes,
        IPermissoes permissoes, IAssinador assinador, TimeProvider relogio)
    {
        this.usuarios = usuarios;
        this.geracoes = geracoes;
        this.relogio = relogio;
        abertura = new AberturaDeSessao(usuarios, contas, sessoes, permissoes, new EmissorDeTokens(sessoes, assinador), relogio);
    }

    /// <inheritdoc />
    public async Task<SessaoAbertaDto> Executar(ConfirmacaoDeSenhaDto pedido, CancellationToken ct = default)
    {
        var (comprovante, aparelho) = pedido.Validar();
        var agora = relogio.GetUtcNow();
        var geracao = await geracoes.BuscarPorComprovante(Credenciais.Resumo(comprovante), ct);
        if (geracao is null || geracao.ConfirmadaEm is not null || agora >= geracao.PedidaEm + Validade)
        {
            throw FalhaDeAutenticacao.ComprovanteInvalido();
        }
        var usuario = await usuarios.Buscar(geracao.UsuarioId, ct) ?? throw FalhaDeAutenticacao.ComprovanteInvalido();
        var conta = await abertura.ContaDisponivel(usuario, primeiroAcesso: true, ct);

        await geracoes.MarcarConfirmada(geracao.Id, agora, ct);
        return await abertura.Abrir(usuario, conta, Frente.Hub, aparelho, null, ct);
    }
}
