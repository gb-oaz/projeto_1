using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using DadosDoUsuario = (
    Boaz.Core.Commons.Usuario.Autenticacao.Domain.TipoUsuario Tipo, string Nome, string NomeCurto, string? Matricula,
    System.Collections.Generic.IReadOnlyList<Boaz.Core.Commons.Usuario.Usuarios.Domain.Responsavel> Responsaveis);

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /usuarios (RF-106, RF-108, RN-181, RN-184, RN-188): o staff e o membro
/// nascem PENDENTE com um código de liberação; o participante, ATIVO com o
/// código de acesso. Auditado com o traceId recebido: "INICIADO" ao chegar com
/// quem e a conta, sem alvo (o usuário ainda não existe); "CONCLUIDO" antes de
/// responder, já com o id criado; na recusa ou na falha, o registro de erro sem
/// alvo antes de a exceção sair (RN-178, RN-270, RN-271).
/// </summary>
public sealed class CriarUsuario(
    IUsuarios usuarios, IContas contas, IGeracoesDeSenha geracoes, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio,
    IAtualizarCardUsuarios cardsDoModulo) : ICriarUsuario
{
    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public async Task<UsuarioCriadoDto> Executar(NovoUsuarioDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = await apoio.ExecucaoDe(traceId, AcaoAuditada.CriarUsuario, pedido.QuemPede, null, ct);
        return await auditada.Auditar(execucao, () => Criar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do POST /usuarios, do formulário ao "CONCLUIDO".</summary>
    async Task<UsuarioCriadoDto> Criar(NovoUsuarioDto pedido, Execucao execucao, CancellationToken ct)
    {
        var dados = pedido.Validar();
        var (quem, conta) = await QuemPodeCriar(pedido.QuemPede, dados, ct);
        await GarantirNomeCurtoEMatriculaLivres(conta, dados, ct);

        var novo = await Montar(quem, conta, dados, ct);
        var (codigo, venceEm) = await Gravar(novo, quem, ct);
        await cardsDoModulo.Executar(new MudancaNoCadastroDto(novo.ContaId, novo.Id), ct);
        await RegistrarConclusao(execucao, novo, ct);
        return new(novo.Id, novo.Login, novo.Estado.Texto(), codigo, venceEm);
    }

    /// <summary>Quem pede e a conta dele, se ele pode criar esse tipo ali (RN-164, RN-188).</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO, CONTA_INEXISTENTE ou DADOS_INVALIDOS.</exception>
    async Task<(UsuarioCadastrado Quem, ContaAssinante Conta)> QuemPodeCriar(string quemPede, DadosDoUsuario dados, CancellationToken ct)
    {
        var quem = await apoio.Quem(quemPede, ct);
        var conta = await apoio.ContaDe(quem, ct);
        GarantirQuePodeCriar(quem, conta, dados);
        return (quem, conta);
    }

    /// <summary>Quem pede pode criar esse tipo, e o autônomo não manda matrícula (RN-164, RN-188).</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO ou DADOS_INVALIDOS.</exception>
    static void GarantirQuePodeCriar(UsuarioCadastrado quem, ContaAssinante conta, DadosDoUsuario dados)
    {
        if (!Hierarquia.PodeCriar(quem, conta.DeAutonomo, dados.Tipo)) throw FalhaDeUsuarios.AcessoNegado();
        if (conta.DeAutonomo && dados.Matricula is not null) throw FalhaDeUsuarios.DadosInvalidos("matricula");
    }

    /// <summary>O nome curto e a matrícula não podem ser de outro usuário da conta (RN-188).</summary>
    /// <exception cref="FalhaDeUsuarios">NOME_CURTO_EM_USO ou MATRICULA_EM_USO.</exception>
    async Task GarantirNomeCurtoEMatriculaLivres(ContaAssinante conta, DadosDoUsuario dados, CancellationToken ct)
    {
        if (await usuarios.NomeCurtoEmUso(conta.Id, dados.NomeCurto, null, ct)) throw FalhaDeUsuarios.NomeCurtoEmUso();
        if (dados.Matricula is not null && await usuarios.MatriculaEmUso(conta.Id, dados.Matricula, null, ct))
        {
            throw FalhaDeUsuarios.MatriculaEmUso();
        }
    }

    /// <summary>
    /// O usuário novo: o participante ATIVO com o código de acesso; o staff e o
    /// membro PENDENTE com a entrada do nome curto.
    /// </summary>
    async Task<UsuarioCadastrado> Montar(UsuarioCadastrado quem, ContaAssinante conta, DadosDoUsuario dados, CancellationToken ct)
    {
        var agora = apoio.Agora;
        var participante = dados.Tipo == TipoUsuario.Participante;
        var matricula = dados.Matricula ?? (conta.DeAutonomo || quem.Tipo.DoTime() ? null : await apoio.NovaMatricula(conta.Id, ct));
        var login = participante ? await apoio.NovoCodigo(conta.Sigla, dados.NomeCurto, ct) : Logins.DoNomeCurto(dados.NomeCurto, conta.Sigla);
        var id = await apoio.NovoId(conta.Sigla, dados.NomeCurto, ct);
        return new UsuarioCadastrado(
            id, conta.Id, conta.Sigla, dados.Tipo, dados.Nome, dados.NomeCurto, login, matricula,
            participante ? EstadoUsuario.Ativo : EstadoUsuario.Pendente, null, dados.Responsaveis, quem.Id, agora,
            codigoGeradoEm: participante ? agora : null);
    }

    /// <summary>Grava o usuário novo e, no staff e no membro, a geração de senha com o código de liberação.</summary>
    async Task<(string? Codigo, DateTimeOffset? VenceEm)> Gravar(UsuarioCadastrado novo, UsuarioCadastrado quem, CancellationToken ct)
    {
        await usuarios.Inserir(novo, ct);
        return await PedirGeracao(novo, quem, ct);
    }

    /// <summary>O staff e o membro recebem o código de liberação, mostrado uma vez (RN-181); o participante, nada.</summary>
    async Task<(string? Codigo, DateTimeOffset? VenceEm)> PedirGeracao(UsuarioCadastrado novo, UsuarioCadastrado quem, CancellationToken ct)
    {
        if (novo.Tipo == TipoUsuario.Participante) return (null, null);
        var (geracao, codigo) = GeracaoDeSenhaPedida.Pedir(novo, quem.Id, apoio.Agora);
        await geracoes.Inserir(geracao, ct);
        return (codigo, geracao.VenceEm);
    }

    /// <summary>O "CONCLUIDO" antes de responder, já com o usuário criado como alvo (RN-178).</summary>
    Task RegistrarConclusao(Execucao execucao, UsuarioCadastrado novo, CancellationToken ct) =>
        auditada.Concluir(execucao with { Alvo = novo.Id }, ct);
}
