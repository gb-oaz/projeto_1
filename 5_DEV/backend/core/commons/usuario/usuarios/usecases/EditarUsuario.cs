using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// PATCH /usuarios/{id} (RF-153, RN-244): nome curto novo muda a entrada do staff
/// ou o código do participante, e as sessões abertas terminam. Auditado com o
/// traceId recebido: "INICIADO" com quem, a conta e o alvo ao chegar,
/// "CONCLUIDO" antes de responder e, na recusa ou na falha, o registro de erro
/// antes de a exceção sair (RN-178, RN-270, RN-271).
/// </summary>
public sealed class EditarUsuario(
    IUsuarios usuarios, IContas contas, ISessoesDosUsuarios sessoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio)
    : IEditarUsuario
{
    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public async Task<UsuarioEditadoDto> Executar(EdicaoDeUsuarioDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = await apoio.ExecucaoDe(traceId, AcaoAuditada.EditarUsuario, pedido.QuemPede, pedido.UsuarioId, ct);
        return await auditada.Auditar(execucao, () => Editar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do PATCH, do formulário ao "CONCLUIDO".</summary>
    async Task<UsuarioEditadoDto> Editar(EdicaoDeUsuarioDto pedido, Execucao execucao, CancellationToken ct)
    {
        var (nome, nomeCurto, matricula, responsaveis) = pedido.Validar();
        var (quem, alvo) = await AlvoAdministravel(pedido, ct);
        await GarantirDadosDoAlvo(quem, alvo, nomeCurto, matricula, responsaveis is not null, ct);

        var codigo = await CodigoDoNomeCurtoNovo(alvo, nomeCurto, ct);
        var mudouEntrada = alvo.Editar(nome, nomeCurto, matricula, responsaveis, apoio.Agora, () => codigo);
        await Gravar(alvo, mudouEntrada, execucao, ct);
        await auditada.Concluir(execucao, ct);
        return new(alvo.Id, alvo.Login);
    }

    /// <summary>Quem pede e o alvo, se quem pede está acima dele (RN-169).</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO ou USUARIO_INEXISTENTE.</exception>
    async Task<(UsuarioCadastrado Quem, UsuarioCadastrado Alvo)> AlvoAdministravel(EdicaoDeUsuarioDto pedido, CancellationToken ct)
    {
        var quem = await apoio.Quem(pedido.QuemPede, ct);
        return (quem, await apoio.Administravel(quem, pedido.UsuarioId, ct));
    }

    /// <summary>O código de acesso novo do participante cujo nome curto muda (RN-244); fora disso, nenhum.</summary>
    async Task<string> CodigoDoNomeCurtoNovo(UsuarioCadastrado alvo, string? nomeCurto, CancellationToken ct) =>
        nomeCurto is not null && nomeCurto != alvo.NomeCurto && alvo.Tipo == TipoUsuario.Participante
            ? await apoio.NovoCodigo(alvo.Sigla, nomeCurto, ct)
            : "";

    /// <summary>Grava o alvo e, se a entrada mudou, encerra as sessões dele (RN-244).</summary>
    async Task Gravar(UsuarioCadastrado alvo, bool mudouEntrada, Execucao execucao, CancellationToken ct)
    {
        await usuarios.Atualizar(alvo, ct);
        if (!mudouEntrada) return;
        var motivo = alvo.Tipo == TipoUsuario.Participante ? MotivoFim.NovoCodigo : MotivoFim.NovoNomeCurto;
        await sessoes.EncerrarDoUsuario(alvo.Id, motivo, execucao.Chamada, ct);
    }

    /// <summary>
    /// Responsáveis só no participante, matrícula fora do autônomo, e o nome curto
    /// e a matrícula novos livres na conta (RN-188, RN-244).
    /// </summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS, NOME_CURTO_EM_USO ou MATRICULA_EM_USO.</exception>
    async Task GarantirDadosDoAlvo(
        UsuarioCadastrado quem, UsuarioCadastrado alvo, string? nomeCurto, string? matricula, bool trazResponsaveis, CancellationToken ct)
    {
        if (trazResponsaveis && alvo.Tipo != TipoUsuario.Participante) throw FalhaDeUsuarios.DadosInvalidos("responsaveis");
        if (matricula is not null && (await apoio.ContaDe(quem, ct)).DeAutonomo) throw FalhaDeUsuarios.DadosInvalidos("matricula");
        if (nomeCurto is not null && nomeCurto != alvo.NomeCurto && await usuarios.NomeCurtoEmUso(alvo.ContaId, nomeCurto, alvo.Id, ct))
        {
            throw FalhaDeUsuarios.NomeCurtoEmUso();
        }
        if (matricula is not null && await usuarios.MatriculaEmUso(alvo.ContaId, matricula, alvo.Id, ct)) throw FalhaDeUsuarios.MatriculaEmUso();
    }
}
