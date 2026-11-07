using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// A partida (diagrama usuario-inicializacao-sequencia): confere os segredos do
/// Key Vault (RNF-226) e garante, pelo _id e sem duplicar (RN-267), a conta do
/// time e o administrador em todo ambiente (RN-190) e, só em homologação, as
/// contas "QAINS" e "QAAUT" com as 6 contas qa (RN-265, RN-266). A cada partida,
/// a senha do Key Vault vale de novo e a conta volta ATIVO; em homologação, as
/// contratações da "QAINS" e da "QAAUT" e as permissões da staff voltam à parte
/// "staff" da lista USUARIO_QA_MODULOS, e as do membro qa à parte "time", que
/// não vira contratação (RN-267, RN-268). Em produção, nenhum
/// módulo vem da partida. Nada de auditoria: quem cria é a própria aplicação, e
/// a segunda partida não registra nada (RN-267). A senha só vira resumo; não
/// sai no retorno (RNF-225).
/// </summary>
public sealed class InicializarContas(IUsuarios usuarios, IContas contas, TimeProvider relogio) : IInicializarContas
{
    /// <inheritdoc />
    public async Task<ContasGarantidasDto> Executar(PartidaDto partida, CancellationToken ct = default)
    {
        ContasIniciais.Conferir(partida.Ambiente, partida.Ler);
        var lista = ContasIniciais.ModulosDo(partida.Ambiente, partida.Ler);
        var agora = relogio.GetUtcNow();
        var criados = new List<string>();
        var idsDasContas = new List<string>();

        var time = new ContaAssinante(ContaAssinante.IdDa(Sigla.DoTime), Sigla.DoTime, Sigla.DoTime.ToLowerInvariant(), partida.Time.Nome,
            TipoConta.Instituicao, EstadoConta.Ativa, partida.Time.Documento, partida.Time.Responsavel, partida.Time.Telefone, agora);
        await Conta(time, null);

        var usuariosDoAmbiente = ContasIniciais.UsuariosDo(partida.Ambiente);
        foreach (var inicial in ContasIniciais.ContasDo(partida.Ambiente))
        {
            // o domínio da conta é o da entrada da conta master dela
            var master = usuariosDoAmbiente.First(u => u.ContaId == inicial.Id && u.Tipo == TipoUsuario.Master);
            var dominio = ContasIniciais.DominioDa(ContasIniciais.Login(master, partida.Ler(master.VariavelEntrada)!));
            var conta = new ContaAssinante(inicial.Id, inicial.Sigla, dominio, inicial.Nome, inicial.Tipo, EstadoConta.Ativa,
                inicial.Documento, "Time deboaz", "", agora);
            await Conta(conta, TempoDaConta.Padrao);
            if (lista is not null) await contas.ReporContratacoes(conta, lista.Modulos, agora, ct);
        }

        foreach (var inicial in usuariosDoAmbiente)
        {
            var login = ContasIniciais.Login(inicial, partida.Ler(inicial.VariavelEntrada)!);
            var nomeCurto = inicial.Tipo == TipoUsuario.Master ? ContasIniciais.DominioDa(login) : inicial.NomeCurto;
            var participante = inicial.Tipo == TipoUsuario.Participante;
            var resumo = inicial.VariavelSenha is { } senha ? ResumoDeSenha.Gerar(partida.Ler(senha)!) : null;
            var usuario = new UsuarioCadastrado(inicial.Id, inicial.ContaId, inicial.Sigla, inicial.Tipo, inicial.Nome, nomeCurto, login,
                null, EstadoUsuario.Ativo, resumo, [], inicial.CriadoPor, agora, codigoGeradoEm: participante ? agora : null);
            if (await usuarios.Garantir(usuario, ct)) criados.Add(usuario.Id);
            if (lista is not null && ContasIniciais.ModsDaLista(lista, usuario.Id) is { } mods)
            {
                await usuarios.ReporModulos(usuario.Id, mods, ct);
            }
        }

        return new ContasGarantidasDto(idsDasContas, [.. usuariosDoAmbiente.Select(u => u.Id)], criados);

        async Task Conta(ContaAssinante conta, TempoDaConta? tempo)
        {
            idsDasContas.Add(conta.Id);
            if (await contas.Garantir(conta, tempo, ct)) criados.Add(conta.Id);
        }
    }
}
