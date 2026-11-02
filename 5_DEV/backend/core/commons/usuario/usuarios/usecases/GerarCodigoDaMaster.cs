using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /contas/{id}/codigo-master (RN-169, RN-172): o SAC confere o documento e
/// mais 2 de 3 dados; batendo, a conta master recebe uma nova geração de senha.
/// </summary>
public sealed class GerarCodigoDaMaster(
    IUsuarios usuarios, IContas contas, IGeracoesDeSenha geracoes, IPagamentos pagamentos, ISessoesDosUsuarios sessoes,
    IAuditoria auditoria, TimeProvider relogio) : IGerarCodigoDaMaster
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<CodigoDaMasterDto> Executar(ConferenciaDaMasterDto pedido, CancellationToken ct = default)
    {
        var quem = await apoio.DoTime(pedido.QuemPede, ct);
        var conferencia = pedido.Validar();
        var conta = await apoio.Assinante(pedido.ContaId, ct);
        var agora = apoio.Agora;
        var confere = conferencia.Confere(conta, await pagamentos.UltimoPagamento(conta.Id, ct));
        await contas.RegistrarConferencia(conta.Id, quem.Id, confere, agora, ct);
        if (!confere) throw FalhaDeUsuarios.ConferenciaIncompleta();

        var master = await usuarios.MasterDaConta(conta.Id, ct) ?? throw FalhaDeUsuarios.ContaInexistente();
        var (geracao, codigo) = GeracaoDeSenhaPedida.Pedir(master, quem.Id, agora);
        master.EsquecerSenha();
        await usuarios.Atualizar(master, ct);
        await geracoes.Inserir(geracao, ct);
        await sessoes.EncerrarDoUsuario(master.Id, MotivoFim.NovaSenha, ct);
        await apoio.Auditar(conta.Id, quem.Id, AcaoAuditada.CodigoMaster, master.Id, ct);
        return new(codigo, conta.Telefone, geracao.VenceEm);
    }
}
