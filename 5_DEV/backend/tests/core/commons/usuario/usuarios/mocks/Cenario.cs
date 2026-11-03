using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Tests.Usuario.Usuarios.Mocks;

/// <summary>
/// O mundo dos cenários de API do requisito 17: a Escola Bento Freitas (EBFGA) com
/// a conta master, a Maria, o João e o Rui (staff com Adm. usuários), a Ana
/// (criada pelo João), o Paulo e o Lucas; o educador autônomo Pedro (PREIS); e o
/// time deboaz (DBOAZ) com o administrador, o Caio e a Ana Lima.
/// </summary>
public sealed class Cenario
{
    /// <summary>Senha de exemplo da Maria, só para os testes.</summary>
    public const string SenhaDaMaria = "Ma!Silva204871";

    /// <summary>Monta o mundo.</summary>
    public Cenario()
    {
        var criada = new DateTimeOffset(2026, 3, 12, 13, 5, 0, TimeSpan.Zero);
        Mock.Guardar(new ContaAssinante("conta-ebfga", "EBFGA", "objetivo_unidade1", "Escola Bento Freitas", TipoConta.Instituicao,
            EstadoConta.Ativa, "12.345.678/0001-90", "Gustavo Boaz de Moura Souza", "(51) 99999-0000", criada,
            new DateTimeOffset(2026, 11, 1, 8, 15, 0, TimeSpan.Zero)));
        Mock.Guardar(new ContaAssinante("conta-preis", "PREIS", "pedro_reis", "Pedro Reis", TipoConta.Autonomo,
            EstadoConta.Ativa, "123.456.789-09", "Pedro Reis", "(51) 98888-0000", criada));
        Mock.Guardar(new ContaAssinante("conta-dboaz", "DBOAZ", "deboaz", "Time deboaz", TipoConta.Instituicao,
            EstadoConta.Ativa, "00.000.000/0001-00", "Time deboaz", "(51) 90000-0000", criada));
        Mock.Pagou("conta-ebfga", new DateOnly(2026, 10, 5));

        Usuario("usr-ebfga-master", "EBFGA", TipoUsuario.Master, "Escola Bento Freitas", "objetivo_unidade1", null, login: "objetivo_unidade1@deboaz.com");
        Usuario("usr-ebfga-maria", "EBFGA", TipoUsuario.Staff, "Maria Souza", "maria", "usr-ebfga-master", "20260087", SenhaDaMaria);
        Usuario("usr-ebfga-joao", "EBFGA", TipoUsuario.Staff, "João Pereira", "joao", "usr-ebfga-master", "20260011");
        Usuario("usr-ebfga-rui", "EBFGA", TipoUsuario.Staff, "Rui Alves", "rui", "usr-ebfga-master", "20260012");
        Usuario("usr-ebfga-ana", "EBFGA", TipoUsuario.Staff, "Ana Costa", "ana", "usr-ebfga-joao", "20260013");
        Usuario("usr-ebfga-paulo", "EBFGA", TipoUsuario.Staff, "Paulo Mendes", "paulo", "usr-ebfga-master", "20260014");
        Mock.Guardar(new UsuarioCadastrado("usr-ebfga-lucas", "conta-ebfga", "EBFGA", TipoUsuario.Participante, "Lucas Andrade", "lucas",
            "EBFGA-LUCAS-0412", "48201755", EstadoUsuario.Ativo, null, [new Responsavel("Carla Andrade", "MAE")], "usr-ebfga-master",
            new DateTimeOffset(2026, 3, 20, 15, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 20, 15, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 1, 7, 55, 0, TimeSpan.Zero), "MAE"));

        Usuario("usr-preis-pedro", "PREIS", TipoUsuario.Master, "Pedro Reis", "pedro_reis", null, login: "pedro_reis@deboaz.com");
        Usuario("usr-dboaz-admin", "DBOAZ", TipoUsuario.Administrador, "Administrador", "admin", null);
        Usuario("usr-dboaz-caio", "DBOAZ", TipoUsuario.MembroTime, "Caio Reis", "caio", "usr-dboaz-admin");
        Usuario("usr-dboaz-analima", "DBOAZ", TipoUsuario.MembroTime, "Ana Lima", "analima", "usr-dboaz-admin");
    }

    /// <summary>O mock, com os casos de uso.</summary>
    public UsuariosEmMemoria Mock { get; } = new();

    /// <summary>Um usuário ativo, com a senha dada (ou uma de exemplo) guardada em resumo.</summary>
    public UsuarioCadastrado Usuario(string id, string sigla, TipoUsuario tipo, string nome, string nomeCurto, string? criadoPor,
        string? matricula = null, string senha = "Xu@Xa182482", string? login = null) =>
        Mock.Guardar(new UsuarioCadastrado(id, ContaAssinante.IdDa(sigla), sigla, tipo, nome, nomeCurto,
            login ?? Logins.DoNomeCurto(nomeCurto, sigla), matricula, EstadoUsuario.Ativo, ResumoDeSenha.Gerar(senha, 1000), [], criadoPor,
            new DateTimeOffset(2026, 3, 14, 10, 20, 0, TimeSpan.Zero), ultimoAcesso: new DateTimeOffset(2026, 10, 29, 9, 42, 0, TimeSpan.Zero)));
}

/// <summary>Atalhos das asserções de falha.</summary>
public static class Esperado
{
    /// <summary>A ação falha com o código dado; devolve a falha para mais conferências.</summary>
    public static async Task<FalhaDeUsuarios> Falha(string codigo, Func<Task> acao)
    {
        var falha = await Assert.ThrowsAsync<FalhaDeUsuarios>(acao);
        Assert.Equal(codigo, falha.Codigo);
        return falha;
    }
}
