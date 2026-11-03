using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// Entrar no hub com a entrada e a senha (RF-134). Ordem: bloqueio (RF-158) →
/// credencial, com a mesma resposta para entrada inexistente (RF-139) →
/// disponibilidade do usuário e da conta (RF-140) → sessão.
/// </summary>
public sealed class EntrarNoHub : IEntrarNoHub
{
    // Resumo de uma senha qualquer, conferido quando a entrada não existe, para o
    // tempo da resposta não revelar se ela existe.
    static readonly Lazy<string> ResumoDeEngano = new(() => ResumoDeSenha.Gerar(Guid.NewGuid().ToString()));

    readonly IUsuarios usuarios;
    readonly ContadorDeTentativas tentativas;
    readonly AberturaDeSessao abertura;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public EntrarNoHub(IUsuarios usuarios, IContas contas, ISessoes sessoes, ITentativas tentativas,
        IPermissoes permissoes, IAssinador assinador, TimeProvider relogio)
    {
        this.usuarios = usuarios;
        this.tentativas = new ContadorDeTentativas(tentativas, relogio);
        abertura = new AberturaDeSessao(usuarios, contas, sessoes, permissoes, new EmissorDeTokens(sessoes, assinador), relogio);
    }

    /// <inheritdoc />
    public async Task<SessaoAbertaDto> Executar(EntradaNoHubDto pedido, CancellationToken ct = default)
    {
        var (entrada, senha, aparelho) = pedido.Validar();
        await tentativas.ConferirBloqueio(AlvoTentativa.Entrada, entrada, ct);

        var usuario = await usuarios.BuscarPorEntrada(entrada, ct);
        var confere = ResumoDeSenha.Confere(senha, usuario?.ResumoSenha ?? ResumoDeEngano.Value);
        if (usuario is null || usuario.Tipo == TipoUsuario.Participante || !confere)
        {
            throw await tentativas.Errar(AlvoTentativa.Entrada, entrada, FalhaDeAutenticacao.EntradaInvalida(), ct);
        }
        await tentativas.Acertar(AlvoTentativa.Entrada, entrada, ct);

        var conta = await abertura.ContaDisponivel(usuario, primeiroAcesso: false, ct);
        return await abertura.Abrir(usuario, conta, Frente.Hub, aparelho, null, ct);
    }
}
