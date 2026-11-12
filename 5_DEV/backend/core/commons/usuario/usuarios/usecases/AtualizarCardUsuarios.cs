using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// Os cards guardados na mesma execução da mudança (RN-278, RN-280): reconta o
/// card da conta, o da staff que criou o usuário e, quando a conta mudou, o
/// DEBOAZ; grava cada um com a versao + 1, mesmo com os números iguais. Sem transação: o fechamento da
/// 00:00 reconta tudo e corrige um desvio.
/// </summary>
public sealed class AtualizarCardUsuarios(
    IUsuarios usuarios, IContas contas, IRepositorioCardUsuarios cards, IContagemDoCard contagem, TimeProvider relogio)
    : IAtualizarCardUsuarios
{
    readonly ContagemDosCards guardados = new(cards, contagem);

    /// <inheritdoc />
    public async Task Executar(MudancaNoCadastroDto mudanca, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow();
        if (await contas.Buscar(mudanca.ContaId, ct) is not { } conta) return;
        if (conta.Sigla != Sigla.DoTime) await guardados.Recontar(new DonoDeCard(conta.Sigla, DonoDoCard.DaConta(conta), conta.Id), agora, ct);
        if (mudanca.UsuarioId is null)
        {
            await guardados.Recontar(DonoDeCard.Deboaz, agora, ct);
            return;
        }
        if (await StaffQueCriou(mudanca.UsuarioId, ct) is { } staff)
        {
            await guardados.Recontar(new DonoDeCard(conta.Sigla, PerfilCard.Staff, conta.Id, staff.Id, staff.NomeCurto), agora, ct);
        }
    }

    /// <summary>A staff que criou o usuário (o card dela conta só os que ela criou); null se não foi uma staff.</summary>
    async Task<UsuarioCadastrado?> StaffQueCriou(string usuarioId, CancellationToken ct) =>
        await usuarios.Buscar(usuarioId, ct) is { CriadoPor: { } criador }
        && await usuarios.Buscar(criador, ct) is { Tipo: TipoUsuario.Staff } staff
            ? staff
            : null;
}
