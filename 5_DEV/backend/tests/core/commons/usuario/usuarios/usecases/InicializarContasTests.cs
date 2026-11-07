using Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.UseCases;

/// <summary>Contas iniciais na partida (RN-190, RN-265 a RN-267, RNF-225, RNF-226).</summary>
public class InicializarContasTests
{
    readonly UsuariosEmMemoria mock = new();

    static readonly DadosDoTimeDto Time = new("Time deboaz", "", "", "");

    // o Key Vault de homologação completo: entradas do requisito e senhas da RNF-202
    static Dictionary<string, string?> KeyVault(bool comQa = true)
    {
        var kv = new Dictionary<string, string?>
        {
            ["USUARIO_ADMIN_ENTRADA"] = "Admin.Dboaz@deboaz.com",
            ["USUARIO_ADMIN_SENHA"] = "Ad#Senha123456",
        };
        if (!comQa) return kv;
        kv["USUARIO_QA_MEMBRO_ENTRADA"] = "qamembro.dboaz@deboaz.com";
        kv["USUARIO_QA_MEMBRO_SENHA"] = "Qa#Membro123456";
        kv["USUARIO_QA_MASTER_ENTRADA"] = "qa_instituicao@deboaz.com";
        kv["USUARIO_QA_MASTER_SENHA"] = "Qa#Master123456";
        kv["USUARIO_QA_AUTONOMO_ENTRADA"] = "qa_autonomo@deboaz.com";
        kv["USUARIO_QA_AUTONOMO_SENHA"] = "Qa#Autonomo123456";
        kv["USUARIO_QA_STAFF_ENTRADA"] = "qastaff.qains@deboaz.com";
        kv["USUARIO_QA_STAFF_SENHA"] = "Qa#Staff123456";
        kv["USUARIO_QA_PARTICIPANTE_CODIGO"] = "qains-qapart-0001";
        return kv;
    }

    Task<ContasGarantidasDto> Iniciar(string ambiente, Dictionary<string, string?> kv) =>
        mock.InicializarContas.Executar(new PartidaDto(ambiente, kv, Time));

    [Fact]
    public async Task RN_190_administrador_em_producao_com_a_entrada_e_a_senha_do_key_vault()
    {
        var garantidas = await Iniciar("prd", KeyVault(comQa: false));

        var admin = mock.Usuario("usr-dboaz-admin")!;
        Assert.Equal((TipoUsuario.Administrador, EstadoUsuario.Ativo, "admin.dboaz@deboaz.com", "conta-dboaz"),
            (admin.Tipo, admin.Estado, admin.Login, admin.ContaId));
        Assert.True(ResumoDeSenha.Confere("Ad#Senha123456", admin.ResumoSenha));
        Assert.Equal("Time deboaz", mock.Conta("conta-dboaz")!.Nome);
        Assert.Equal(["conta-dboaz"], garantidas.Contas);
        Assert.Equal(["usr-dboaz-admin"], garantidas.Usuarios);
        Assert.Empty(mock.Auditoria);
    }

    [Fact]
    public async Task RN_265_contas_qa_em_homologacao()
    {
        var garantidas = await Iniciar("hml", KeyVault());

        Assert.Equal(["conta-dboaz", "conta-qains", "conta-qaaut"], garantidas.Contas);
        Assert.Equal((TipoConta.Instituicao, "QAINS", "qa_instituicao"), (mock.Conta("conta-qains")!.Tipo, mock.Conta("conta-qains")!.Sigla, mock.Conta("conta-qains")!.Dominio));
        Assert.Equal((TipoConta.Autonomo, "QAAUT"), (mock.Conta("conta-qaaut")!.Tipo, mock.Conta("conta-qaaut")!.Sigla));
        Assert.Equal(TempoDaConta.Padrao, mock.Tempo("conta-qains"));
        Assert.Equal(TipoUsuario.MembroTime, mock.Usuario("usr-dboaz-qamembro")!.Tipo);
        Assert.Equal(("qa_instituicao", TipoUsuario.Master), (mock.Usuario("usr-qains-master")!.NomeCurto, mock.Usuario("usr-qains-master")!.Tipo));
        Assert.Equal(("conta-qaaut", TipoUsuario.Master), (mock.Usuario("usr-qaaut-master")!.ContaId, mock.Usuario("usr-qaaut-master")!.Tipo));
        Assert.Equal(("usr-qains-master", TipoUsuario.Staff), (mock.Usuario("usr-qains-qastaff")!.CriadoPor, mock.Usuario("usr-qains-qastaff")!.Tipo));
        var participante = mock.Usuario("usr-qains-qapart")!;
        Assert.Equal(("QAINS-QAPART-0001", null, EstadoUsuario.Ativo), (participante.Login, participante.ResumoSenha, participante.Estado));
        Assert.NotNull(participante.CodigoGeradoEm);
        Assert.Equal(9, garantidas.Criados.Count);
    }

    [Fact]
    public async Task RN_266_producao_ignora_os_segredos_qa()
    {
        var garantidas = await Iniciar("prd", KeyVault());

        Assert.Null(mock.Conta("conta-qains"));
        Assert.Null(mock.Usuario("usr-qains-qastaff"));
        Assert.Equal(["usr-dboaz-admin"], garantidas.Usuarios);
    }

    [Fact]
    public async Task RN_267_segunda_partida_nao_duplica_e_reaplica_senha_e_estado()
    {
        await Iniciar("hml", KeyVault());
        var staff = mock.Usuario("usr-qains-qastaff")!;
        staff.Desativar();
        var kv = KeyVault();
        kv["USUARIO_QA_MASTER_SENHA"] = "Qa#Nova987654";

        var segunda = await Iniciar("hml", kv);

        Assert.Empty(segunda.Criados);
        Assert.Equal(EstadoUsuario.Ativo, mock.Usuario("usr-qains-qastaff")!.Estado);
        Assert.Equal(staff.CriadoEm, mock.Usuario("usr-qains-qastaff")!.CriadoEm);
        var master = mock.Usuario("usr-qains-master")!;
        Assert.True(ResumoDeSenha.Confere("Qa#Nova987654", master.ResumoSenha));
        Assert.False(ResumoDeSenha.Confere("Qa#Master123456", master.ResumoSenha));
        Assert.Empty(mock.Auditoria);
    }

    [Theory]
    [InlineData("prd", "USUARIO_ADMIN_SENHA")]
    [InlineData("hml", "USUARIO_ADMIN_ENTRADA")]
    [InlineData("hml", "USUARIO_QA_STAFF_SENHA")]
    [InlineData("hml", "USUARIO_QA_PARTICIPANTE_CODIGO")]
    public async Task RNF_226_segredo_ausente_diz_so_o_nome(string ambiente, string variavel)
    {
        var kv = KeyVault();
        kv[variavel] = ambiente == "prd" ? null : " ";

        var falha = await Assert.ThrowsAsync<FalhaNaInicializacao>(() => Iniciar(ambiente, kv));

        Assert.Equal([variavel], falha.Variaveis);
        Assert.Contains(variavel, falha.Message);
        Assert.Null(mock.Usuario("usr-dboaz-admin"));
    }

    [Fact]
    public async Task RNF_226_senha_fraca_diz_so_o_nome()
    {
        var kv = KeyVault();
        kv["USUARIO_QA_STAFF_SENHA"] = "Gu#B5";

        var falha = await Assert.ThrowsAsync<FalhaNaInicializacao>(() => Iniciar("hml", kv));

        Assert.Equal(["USUARIO_QA_STAFF_SENHA"], falha.Variaveis);
        Assert.DoesNotContain("Gu#B5", falha.Message);
    }

    [Theory]
    [InlineData("hml", true)]
    [InlineData(" HML ", true)]
    [InlineData("prd", false)]
    [InlineData(null, false)]
    public void RN_266_so_hml_e_homologacao(string? ambiente, bool homologacao)
    {
        Assert.Equal(homologacao, ContasIniciais.EmHomologacao(ambiente));
        Assert.Equal(homologacao ? 11 : 2, ContasIniciais.VariaveisDo(ambiente).Count);
    }
}
