using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// GET /usuarios/card/ao-vivo (RN-281, 15 RN-279): abre com o card de quem pede
/// (até 3 conexões por usuário, a mais antiga sai) e, a cada intervalo, confere
/// a sessão, o MOD-03 e a versao do card guardado; o ": ping" sai a cada 30 s.
/// GET não é auditado.
/// </summary>
public sealed class AcompanharCardAoVivo(
    IUsuarios usuarios, IContas contas, IRepositorioCardUsuarios cards, IContagemDoCard contagem,
    IConexoesAoVivo conexoes, IAcessoAoVivo acesso, TimeProvider relogio) : IAcompanharCardAoVivo
{
    readonly LeituraDoCard leitura = new(usuarios, contas, cards, contagem, relogio);

    /// <inheritdoc />
    public async Task<(ConexaoAoVivo Conexao, CardDoModuloDto Card)> Abrir(AberturaAoVivoDto pedido, CancellationToken ct = default)
    {
        var intervalo = pedido.Validar();
        var (id, card) = await leitura.Ler(pedido.QuemPede, ct);
        var conexao = new ConexaoAoVivo(Guid.NewGuid().ToString("N"), pedido.QuemPede, pedido.Sid, id, intervalo, card.Versao, relogio.GetUtcNow());
        foreach (var antiga in await conexoes.Abrir(conexao.UsuarioId, conexao.Id, ct))
        {
            await conexoes.Encerrar(antiga, MotivoEncerramento.LimiteConexoes, ct);
        }
        return (conexao, card);
    }

    /// <inheritdoc />
    public async Task<PassoAoVivoDto> Proximo(ConexaoAoVivo conexao, CancellationToken ct = default)
    {
        while (true)
        {
            var agora = relogio.GetUtcNow();
            if (conexao.HoraDoPing(agora)) return new PassoAoVivoDto(Ping: true);
            if (conexao.HoraDeConferir(agora) && await Conferir(conexao, ct) is { } passo) return passo;
            var espera = conexao.ProximaAcao - relogio.GetUtcNow();
            if (await conexoes.Esperar(conexao.Id, espera > TimeSpan.Zero ? espera : TimeSpan.Zero, ct) is { } motivo)
            {
                return new PassoAoVivoDto(Encerrado: motivo);
            }
        }
    }

    /// <inheritdoc />
    public Task Fechar(ConexaoAoVivo conexao, CancellationToken ct = default) => conexoes.Fechar(conexao.UsuarioId, conexao.Id, ct);

    /// <summary>A sessão, o MOD-03 e a versao: o encerrado, o card novo ou nada.</summary>
    async Task<PassoAoVivoDto?> Conferir(ConexaoAoVivo conexao, CancellationToken ct)
    {
        await conexoes.Renovar(conexao.UsuarioId, conexao.Id, ct);
        if (!await acesso.SessaoAberta(conexao.Sid, ct)) return new PassoAoVivoDto(Encerrado: MotivoEncerramento.SessaoTerminada);
        if (!await acesso.PodeVerOCard(conexao.UsuarioId, ct)) return new PassoAoVivoDto(Encerrado: MotivoEncerramento.AcessoNegado);
        if (await cards.Versao(conexao.CardId, ct) is not { } versao || versao == conexao.Versao) return null;
        var (_, card) = await leitura.Ler(conexao.UsuarioId, ct);
        conexao.Mandou(card.Versao);
        return new PassoAoVivoDto(Card: card);
    }
}
