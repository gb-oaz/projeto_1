using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.MsMono.Infra.Out.Commons.Usuario.Usuarios;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using IContas = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IContas;

namespace Boaz.MsMono.Tests.Integracao;

// RN-179 no banco: a sigla é buscada exata (gravada em maiúsculas) e, quando
// dois pedidos passam juntos pela conferência, o índice único recusa o segundo
// com o mesmo código da conferência, não com 500.
public class ContasNoBancoTests(Backend backend) : IClassFixture<Backend>
{
    IContas Contas => backend.Services.GetRequiredService<IContas>();

    async Task GravarDireto(string sigla, string dominio)
    {
        _ = backend.Server;
        await backend.Banco.GetCollection<BsonDocument>("contas").InsertOneAsync(new BsonDocument
        {
            ["_id"] = ContaAssinante.IdDa(sigla),
            ["sigla"] = sigla,
            ["dominio"] = dominio,
        });
    }

    Task Criar(string sigla, string dominio)
    {
        var agora = DateTimeOffset.UtcNow;
        var conta = new ContaAssinante(ContaAssinante.IdDa(sigla), sigla, dominio, "Escola Corrida", TipoConta.Instituicao,
            EstadoConta.Ativa, "12.345.678/0001-90", "Responsável Corrida", "(51) 99999-0000", agora);
        var master = new UsuarioCadastrado($"usr-{sigla.ToLowerInvariant()}-master", conta.Id, sigla, TipoUsuario.Master,
            conta.Nome, dominio, Logins.DaMaster(dominio), null, EstadoUsuario.Pendente, ResumoDeSenha.Gerar("Gu#Corrida123"), [], null, agora);
        var geracao = GeracaoDeSenhaPedida.MostradaNaCriacao(master, Credenciais.Resumo(CodigoDeLiberacao.Comprovante()), agora);
        return Contas.Criar(conta, master, geracao, TempoDaConta.Padrao);
    }

    [Fact]
    public async Task RN_179_sigla_buscada_exata_em_maiusculas()
    {
        await GravarDireto("BUSCA", "busca_rn179");

        Assert.True(await Contas.SiglaEmUso("BUSCA"));
        Assert.True(await Contas.SiglaEmUso("busca"));
        Assert.False(await Contas.SiglaEmUso("BUSCB"));
    }

    [Fact]
    public async Task RN_179_sigla_repetida_na_gravacao_e_SIGLA_EM_USO()
    {
        await GravarDireto("CORRI", "corri_rn179");

        var falha = await Assert.ThrowsAsync<FalhaDeUsuarios>(() => Criar("CORRI", "corri_rn179_outro"));
        Assert.Equal("SIGLA_EM_USO", falha.Codigo);
    }

    [Fact]
    public async Task RN_179_dominio_repetido_na_gravacao_e_DOMINIO_EM_USO()
    {
        await GravarDireto("DOMIN", "domin_rn179");

        var falha = await Assert.ThrowsAsync<FalhaDeUsuarios>(() => Criar("DOMIO", "domin_rn179"));
        Assert.Equal("DOMINIO_EM_USO", falha.Codigo);
    }

    [Theory]
    [InlineData("E11000 duplicate key error collection: boaz.contas index: sigla_unica dup key: { sigla: \"X\" }", "sigla_unica")]
    [InlineData("E11000 duplicate key error collection: boaz.contas index: _id_", "_id_")]
    [InlineData("outra mensagem", null)]
    public void Indice_da_chave_duplicada(string mensagem, string? indice) =>
        Assert.Equal(indice, ContasNoBanco.IndiceDuplicado(mensagem));
}
