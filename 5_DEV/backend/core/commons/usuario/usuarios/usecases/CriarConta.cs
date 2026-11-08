using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using DadosDaConta = (
    Boaz.Core.Commons.Usuario.Usuarios.Domain.TipoConta Tipo, string Nome, string Documento, string Responsavel, string Telefone,
    string Dominio, string Sigla);

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /contas (RF-104, RN-179, RN-242): a conta, a conta master e a senha dela,
/// gerada do nome do responsável e mostrada uma vez, com o comprovante que abre
/// a primeira sessão. Fora de homologação, a sigla iniciada por "QA" é recusada
/// (RN-179). A execução é auditada com o traceId recebido: "INICIADO" ao chegar,
/// "CONCLUIDO" antes de responder e, na recusa ou na falha, o registro de erro
/// antes de a exceção sair daqui (RN-178, RN-270, RN-271). Sem log.
/// </summary>
public sealed class CriarConta(IContas contas, IAuditoria auditoria, IAuditoriaDeErros erros, TimeProvider relogio, AmbienteDaAplicacao ambiente)
    : ICriarConta
{
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task<ContaCriadaDto> Executar(NovaContaDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.CriarConta, null, null, null);
        return auditada.Auditar(execucao, () => Criar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do POST /contas, do formulário ao "CONCLUIDO".</summary>
    async Task<ContaCriadaDto> Criar(NovaContaDto pedido, Execucao execucao, CancellationToken ct)
    {
        var dados = ValidarPedido(pedido);
        await GarantirSiglaLivre(dados.Sigla, ct);
        await GarantirDominioLivre(dados.Dominio, ct);

        var agora = relogio.GetUtcNow();
        var conta = MontarConta(dados, agora);
        var senha = Senha.Gerar(dados.Responsavel);
        var comprovante = CodigoDeLiberacao.Comprovante();
        var master = MontarMaster(conta, senha, agora);

        await Gravar(conta, master, comprovante, agora, ct);
        await RegistrarConclusao(execucao, conta, ct);
        return new(conta.Id, master.Login, senha, comprovante);
    }

    /// <summary>Os campos do formulário e a sigla no ambiente (RN-179, RN-242).</summary>
    /// <exception cref="FalhaDeUsuarios">DADOS_INVALIDOS ou SIGLA_INVALIDA.</exception>
    DadosDaConta ValidarPedido(NovaContaDto pedido)
    {
        var dados = pedido.Validar();
        Sigla.ValidarNoAmbiente(dados.Sigla, ambiente);
        return dados;
    }

    /// <summary>A sigla não pode ser de outra conta (RN-179).</summary>
    /// <exception cref="FalhaDeUsuarios">SIGLA_EM_USO.</exception>
    async Task GarantirSiglaLivre(string sigla, CancellationToken ct)
    {
        if (await contas.SiglaEmUso(sigla, ct)) throw FalhaDeUsuarios.SiglaEmUso();
    }

    /// <summary>O domínio não pode ser de outra conta (RF-104).</summary>
    /// <exception cref="FalhaDeUsuarios">DOMINIO_EM_USO.</exception>
    async Task GarantirDominioLivre(string dominio, CancellationToken ct)
    {
        if (await contas.DominioEmUso(dominio, ct)) throw FalhaDeUsuarios.DominioEmUso();
    }

    /// <summary>A conta assinante, ativa, com o id tirado da sigla.</summary>
    static ContaAssinante MontarConta(DadosDaConta dados, DateTimeOffset agora) =>
        new(ContaAssinante.IdDa(dados.Sigla), dados.Sigla, dados.Dominio, dados.Nome, dados.Tipo, EstadoConta.Ativa, dados.Documento,
            dados.Responsavel, dados.Telefone, agora);

    /// <summary>A conta master, pendente até a primeira sessão, com o resumo da senha gerada.</summary>
    static UsuarioCadastrado MontarMaster(ContaAssinante conta, string senha, DateTimeOffset agora) =>
        new($"usr-{conta.Sigla.ToLowerInvariant()}-master", conta.Id, conta.Sigla, TipoUsuario.Master, conta.Nome, conta.Dominio,
            Logins.DaMaster(conta.Dominio), null, EstadoUsuario.Pendente, ResumoDeSenha.Gerar(senha), [], null, agora);

    /// <summary>Grava a conta, a master, a geração de senha mostrada e o tempo de sessão padrão.</summary>
    Task Gravar(ContaAssinante conta, UsuarioCadastrado master, string comprovante, DateTimeOffset agora, CancellationToken ct)
    {
        var geracao = GeracaoDeSenhaPedida.MostradaNaCriacao(master, Credenciais.Resumo(comprovante), agora);
        return contas.Criar(conta, master, geracao, TempoDaConta.Padrao, ct);
    }

    /// <summary>
    /// O "CONCLUIDO" antes de responder, com a conta criada como conta e alvo, sem quem; o "INICIADO"
    /// foi sem login, sem conta e sem alvo, que ainda não existiam (RN-178).
    /// </summary>
    Task RegistrarConclusao(Execucao execucao, ContaAssinante conta, CancellationToken ct) =>
        auditada.Concluir(execucao with { ContaId = conta.Id, Alvo = conta.Id }, ct);
}
