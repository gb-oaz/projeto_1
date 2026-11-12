using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.MsMono.Secundario.Out.Commons.Usuario.Usuarios;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using IContas = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IContas;
using IUsuarios = Boaz.Core.Commons.Usuario.Usuarios.Ports.Out.IUsuarios;

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

    // RN-188: sem matrícula, o campo fica ausente; dois sem matrícula na mesma
    // conta não colidem no índice único contaId+matricula
    [Fact]
    public async Task RN_188_dois_sem_matricula_na_mesma_conta()
    {
        _ = backend.Server;
        var usuarios = backend.Services.GetRequiredService<IUsuarios>();
        var agora = DateTimeOffset.UtcNow;
        foreach (var nome in new[] { "semmatra", "semmatrb" })
        {
            await usuarios.Inserir(new UsuarioCadastrado($"usr-rn188-{nome}", "conta-rn188", "RNOIT", TipoUsuario.Participante,
                $"Pessoa {nome}", nome, $"RNOIT-{nome.ToUpperInvariant()}-0001", null, EstadoUsuario.Ativo, null, [], null, agora));
        }

        var gravados = await backend.Banco.GetCollection<BsonDocument>("usuarios")
            .Find(Builders<BsonDocument>.Filter.Eq("contaId", "conta-rn188")).ToListAsync();
        Assert.Equal(2, gravados.Count);
        Assert.All(gravados, d => Assert.False(d.Contains("matricula")));
    }
}
