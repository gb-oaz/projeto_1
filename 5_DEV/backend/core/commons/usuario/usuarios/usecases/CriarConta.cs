using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /contas (RF-104, RN-179, RN-242): a conta, a conta master e a senha dela,
/// gerada do nome do responsável e mostrada uma vez, com o comprovante que abre
/// a primeira sessão. Fora de homologação, a sigla iniciada por "QA" é recusada
/// (RN-179).
/// </summary>
public sealed class CriarConta(IUsuarios usuarios, IContas contas, IAuditoria auditoria, TimeProvider relogio, AmbienteDaAplicacao ambiente)
    : ICriarConta
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<ContaCriadaDto> Executar(NovaContaDto pedido, CancellationToken ct = default)
    {
        var (tipo, nome, documento, responsavel, telefone, dominio, sigla) = pedido.Validar();
        Sigla.ValidarNoAmbiente(sigla, ambiente);
        if (await contas.SiglaEmUso(sigla, ct)) throw FalhaDeUsuarios.SiglaEmUso();
        if (await contas.DominioEmUso(dominio, ct)) throw FalhaDeUsuarios.DominioEmUso();

        var agora = apoio.Agora;
        var conta = new ContaAssinante(
            ContaAssinante.IdDa(sigla), sigla, dominio, nome, tipo, EstadoConta.Ativa, documento, responsavel, telefone, agora);
        var senha = Senha.Gerar(responsavel);
        var comprovante = CodigoDeLiberacao.Comprovante();
        var master = new UsuarioCadastrado(
            $"usr-{sigla.ToLowerInvariant()}-master", conta.Id, sigla, TipoUsuario.Master, nome, dominio, Logins.DaMaster(dominio),
            null, EstadoUsuario.Pendente, ResumoDeSenha.Gerar(senha), [], null, agora);
        var geracao = GeracaoDeSenhaPedida.MostradaNaCriacao(master, Credenciais.Resumo(comprovante), agora);
        await contas.Criar(conta, master, geracao, TempoDaConta.Padrao, ct);
        await apoio.Auditar(conta.Id, master.Id, AcaoAuditada.CriarConta, conta.Id, ct);
        return new(conta.Id, master.Login, senha, comprovante);
    }
}
