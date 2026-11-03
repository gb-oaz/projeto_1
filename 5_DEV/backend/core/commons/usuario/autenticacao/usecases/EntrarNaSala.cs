using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Autenticacao.UseCases;

/// <summary>
/// Acesso anônimo a uma sala aberta, sem código de acesso (RF-136). A sessão não
/// tem tempo sem uso: termina quando a sala fecha (RN-231).
/// </summary>
public sealed class EntrarNaSala : IEntrarNaSala
{
    /// <summary>Tipo que vai no token do acesso anônimo.</summary>
    public const string TipoAnonimo = "ANONIMO";

    readonly ISalas salas;
    readonly IPermissoes permissoes;
    readonly EmissorDeTokens emissor;
    readonly TimeProvider relogio;

    /// <summary>Monta o caso de uso com as portas de saída.</summary>
    public EntrarNaSala(ISalas salas, ISessoes sessoes, IPermissoes permissoes, IAssinador assinador, TimeProvider relogio)
    {
        this.salas = salas;
        this.permissoes = permissoes;
        this.relogio = relogio;
        emissor = new EmissorDeTokens(sessoes, assinador);
    }

    /// <inheritdoc />
    public async Task<SessaoAbertaDto> Executar(EntradaNaSalaDto pedido, CancellationToken ct = default)
    {
        var (codigo, aparelho) = pedido.Validar();
        var sala = await salas.BuscarAberta(codigo, ct) ?? throw FalhaDeAutenticacao.SalaFechada();
        var acesso = await salas.NovoAcessoAnonimo(sala, ct);
        var doAnonimo = await permissoes.DoAnonimo(sala, ct);
        var agora = relogio.GetUtcNow();
        var (sessao, renovacao) = Sessao.Abrir(acesso.Id, sala.ContaId, sala.Sigla, Frente.Sala, aparelho, null,
            sala.Codigo, doAnonimo.Pv, tempoSemUso: null, agora);
        return await emissor.Emitir(sessao, renovacao, TipoAnonimo, doAnonimo, agora, ct);
    }
}
