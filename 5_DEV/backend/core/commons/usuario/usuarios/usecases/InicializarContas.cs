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
        var garantidas = new Garantidas();
        var usuariosDoAmbiente = ContasIniciais.UsuariosDo(partida.Ambiente);

        await GarantirContaDoTime(partida, agora, garantidas, ct);
        await GarantirContasDoAmbiente(partida, usuariosDoAmbiente, lista, agora, garantidas, ct);
        await GarantirUsuarios(partida, usuariosDoAmbiente, lista, agora, garantidas, ct);
        return new ContasGarantidasDto(garantidas.Contas, [.. usuariosDoAmbiente.Select(u => u.Id)], garantidas.Criados);
    }

    /// <summary>A conta do time deboaz, em todo ambiente, com o cadastro da partida (RN-190).</summary>
    Task GarantirContaDoTime(PartidaDto partida, DateTimeOffset agora, Garantidas garantidas, CancellationToken ct)
    {
        var time = new ContaAssinante(ContaAssinante.IdDa(Sigla.DoTime), Sigla.DoTime, Sigla.DoTime.ToLowerInvariant(), partida.Time.Nome,
            TipoConta.Instituicao, EstadoConta.Ativa, partida.Time.Documento, partida.Time.Responsavel, partida.Time.Telefone, agora);
        return GarantirConta(time, null, garantidas, ct);
    }

    /// <summary>
    /// As contas iniciais do ambiente, com o domínio da entrada da conta master
    /// de cada uma e, em homologação, as contratações da parte "staff" (RN-265, RN-268).
    /// </summary>
    async Task GarantirContasDoAmbiente(PartidaDto partida, IReadOnlyList<UsuarioInicial> usuariosDoAmbiente, ModulosIniciais? lista,
        DateTimeOffset agora, Garantidas garantidas, CancellationToken ct)
    {
        foreach (var inicial in ContasIniciais.ContasDo(partida.Ambiente))
        {
            var conta = new ContaAssinante(inicial.Id, inicial.Sigla, DominioDaConta(partida, usuariosDoAmbiente, inicial), inicial.Nome,
                inicial.Tipo, EstadoConta.Ativa, inicial.Documento, "Time deboaz", "", agora);
            await GarantirConta(conta, TempoDaConta.Padrao, garantidas, ct);
            if (lista is not null) await contas.ReporContratacoes(conta, lista.Modulos, agora, ct);
        }
    }

    /// <summary>O domínio da conta é o da entrada da conta master dela.</summary>
    static string DominioDaConta(PartidaDto partida, IReadOnlyList<UsuarioInicial> usuariosDoAmbiente, ContaInicial inicial)
    {
        var master = usuariosDoAmbiente.First(u => u.ContaId == inicial.Id && u.Tipo == TipoUsuario.Master);
        return ContasIniciais.DominioDa(ContasIniciais.Login(master, partida.Ler(master.VariavelEntrada)!));
    }

    /// <summary>
    /// Cada usuário inicial, ATIVO e com a senha do Key Vault em resumo (RN-267,
    /// RNF-225); em homologação, os módulos da lista voltam a ele (RN-268).
    /// </summary>
    async Task GarantirUsuarios(PartidaDto partida, IReadOnlyList<UsuarioInicial> usuariosDoAmbiente, ModulosIniciais? lista,
        DateTimeOffset agora, Garantidas garantidas, CancellationToken ct)
    {
        foreach (var inicial in usuariosDoAmbiente)
        {
            var usuario = MontarUsuario(partida, inicial, agora);
            if (await usuarios.Garantir(usuario, ct)) garantidas.Criados.Add(usuario.Id);
            if (lista is not null && ContasIniciais.ModsDaLista(lista, usuario.Id) is { } mods)
            {
                await usuarios.ReporModulos(usuario.Id, mods, ct);
            }
        }
    }

    /// <summary>O usuário inicial com a entrada e a senha das variáveis da partida.</summary>
    static UsuarioCadastrado MontarUsuario(PartidaDto partida, UsuarioInicial inicial, DateTimeOffset agora)
    {
        var login = ContasIniciais.Login(inicial, partida.Ler(inicial.VariavelEntrada)!);
        var nomeCurto = inicial.Tipo == TipoUsuario.Master ? ContasIniciais.DominioDa(login) : inicial.NomeCurto;
        var participante = inicial.Tipo == TipoUsuario.Participante;
        var resumo = inicial.VariavelSenha is { } senha ? ResumoDeSenha.Gerar(partida.Ler(senha)!) : null;
        return new UsuarioCadastrado(inicial.Id, inicial.ContaId, inicial.Sigla, inicial.Tipo, inicial.Nome, nomeCurto, login,
            null, EstadoUsuario.Ativo, resumo, [], inicial.CriadoPor, agora, codigoGeradoEm: participante ? agora : null);
    }

    /// <summary>Garante a conta pelo _id, sem duplicar (RN-267), e anota se ela nasceu agora.</summary>
    async Task GarantirConta(ContaAssinante conta, TempoDaConta? tempo, Garantidas garantidas, CancellationToken ct)
    {
        garantidas.Contas.Add(conta.Id);
        if (await contas.Garantir(conta, tempo, ct)) garantidas.Criados.Add(conta.Id);
    }

    /// <summary>O que a partida garantiu: as contas, na ordem, e o que nasceu nela.</summary>
    sealed class Garantidas
    {
        /// <summary>Ids das contas garantidas.</summary>
        public List<string> Contas { get; } = [];

        /// <summary>Ids das contas e dos usuários criados nesta partida.</summary>
        public List<string> Criados { get; } = [];
    }
}
