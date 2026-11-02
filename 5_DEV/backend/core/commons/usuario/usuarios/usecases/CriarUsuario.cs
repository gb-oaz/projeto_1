using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /usuarios (RF-106, RF-108, RN-181, RN-184, RN-188): o staff e o membro
/// nascem PENDENTE com um código de liberação; o participante, ATIVO com o
/// código de acesso.
/// </summary>
public sealed class CriarUsuario(
    IUsuarios usuarios, IContas contas, IGeracoesDeSenha geracoes, IAuditoria auditoria, TimeProvider relogio) : ICriarUsuario
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<UsuarioCriadoDto> Executar(NovoUsuarioDto pedido, CancellationToken ct = default)
    {
        var (tipo, nome, nomeCurto, matricula, responsaveis) = pedido.Validar();
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        var conta = await apoio.ContaDe(quem, ct);
        if (!Hierarquia.PodeCriar(quem, conta.DeAutonomo, tipo)) throw FalhaDeUsuarios.AcessoNegado();
        if (conta.DeAutonomo && matricula is not null) throw FalhaDeUsuarios.DadosInvalidos("matricula");
        if (await usuarios.NomeCurtoEmUso(conta.Id, nomeCurto, null, ct)) throw FalhaDeUsuarios.NomeCurtoEmUso();
        if (matricula is not null && await usuarios.MatriculaEmUso(conta.Id, matricula, null, ct)) throw FalhaDeUsuarios.MatriculaEmUso();

        var agora = apoio.Agora;
        var participante = tipo == TipoUsuario.Participante;
        matricula ??= conta.DeAutonomo || quem.Tipo.DoTime() ? null : await apoio.NovaMatricula(conta.Id, ct);
        var login = participante ? await apoio.NovoCodigo(conta.Sigla, nomeCurto, ct) : Logins.DoNomeCurto(nomeCurto, conta.Sigla);
        var novo = new UsuarioCadastrado(
            await apoio.NovoId(conta.Sigla, nomeCurto, ct), conta.Id, conta.Sigla, tipo, nome, nomeCurto, login, matricula,
            participante ? EstadoUsuario.Ativo : EstadoUsuario.Pendente, null, responsaveis, quem.Id, agora,
            codigoGeradoEm: participante ? agora : null);
        await usuarios.Inserir(novo, ct);

        string? codigo = null;
        DateTimeOffset? venceEm = null;
        if (!participante)
        {
            var (geracao, codigoLiberacao) = GeracaoDeSenhaPedida.Pedir(novo, quem.Id, agora);
            await geracoes.Inserir(geracao, ct);
            (codigo, venceEm) = (codigoLiberacao, geracao.VenceEm);
        }
        await apoio.Auditar(conta.Id, quem.Id, AcaoAuditada.CriarUsuario, novo.Id, ct);
        return new(novo.Id, novo.Login, novo.Estado.Texto(), codigo, venceEm);
    }
}
