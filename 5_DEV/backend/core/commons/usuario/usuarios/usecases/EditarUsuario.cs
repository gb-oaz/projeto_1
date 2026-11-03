using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// PATCH /usuarios/{id} (RF-153, RN-244): nome curto novo muda a entrada do staff
/// ou o código do participante, e as sessões abertas terminam.
/// </summary>
public sealed class EditarUsuario(
    IUsuarios usuarios, IContas contas, ISessoesDosUsuarios sessoes, IAuditoria auditoria, TimeProvider relogio) : IEditarUsuario
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<UsuarioEditadoDto> Executar(EdicaoDeUsuarioDto pedido, CancellationToken ct = default)
    {
        var (nome, nomeCurto, matricula, responsaveis) = pedido.Validar();
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var alvo = await apoio.Administravel(quem, pedido.UsuarioId, ct);
        var participante = alvo.Tipo == TipoUsuario.Participante;
        if (responsaveis is not null && !participante) throw FalhaDeUsuarios.DadosInvalidos("responsaveis");
        if (matricula is not null && (await apoio.ContaDe(quem, ct)).DeAutonomo) throw FalhaDeUsuarios.DadosInvalidos("matricula");
        var trocaNomeCurto = nomeCurto is not null && nomeCurto != alvo.NomeCurto;
        if (trocaNomeCurto && await usuarios.NomeCurtoEmUso(alvo.ContaId, nomeCurto!, alvo.Id, ct)) throw FalhaDeUsuarios.NomeCurtoEmUso();
        if (matricula is not null && await usuarios.MatriculaEmUso(alvo.ContaId, matricula, alvo.Id, ct)) throw FalhaDeUsuarios.MatriculaEmUso();

        var codigo = trocaNomeCurto && participante ? await apoio.NovoCodigo(alvo.Sigla, nomeCurto!, ct) : "";
        var mudouEntrada = alvo.Editar(nome, nomeCurto, matricula, responsaveis, apoio.Agora, () => codigo);
        await usuarios.Atualizar(alvo, ct);
        if (mudouEntrada) await sessoes.EncerrarDoUsuario(alvo.Id, participante ? MotivoFim.NovoCodigo : MotivoFim.NovoNomeCurto, ct);
        await apoio.Auditar(alvo.ContaId, quem.Id, AcaoAuditada.EditarUsuario, alvo.Id, ct);
        return new(alvo.Id, alvo.Login);
    }
}
