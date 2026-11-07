using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Dtos;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Microsoft.Extensions.DependencyInjection;

namespace Boaz.MsMono.Tests.Integracao;

// Uma pessoa pronta para os testes: id, entrada, senha e um token do hub.
public sealed record Pessoa(string Id, string Entrada, string Senha, string Token, string Renovacao);

// Preparo do cenário pelos casos de uso do core, resolvidos do próprio
// backend: o que não é o endereço testado nasce direto pelas portas de
// entrada, com os mesmos adaptadores de banco e cache da API.
public sealed class Preparo(Backend backend)
{
    T Caso<T>() where T : notnull => backend.Services.GetRequiredService<T>();

    // Uma sigla de 5 letras ao acaso para a conta do teste, nunca uma reservada:
    // "QA…" fora de homologação (RN-179, RN-266) nem DBOAZ. Sem isso, cerca de
    // 1 sorteio em 676 começava por QA e o preparo falhava ao acaso.
    public static string SiglaSorteada()
    {
        while (true)
        {
            var sigla = string.Concat(Guid.NewGuid().ToByteArray().Take(5).Select(b => (char)('A' + b % 26)));
            if (!sigla.StartsWith(Core.Commons.Usuario.Usuarios.Domain.Sigla.PrefixoQa, StringComparison.Ordinal)
                && sigla != Core.Commons.Usuario.Usuarios.Domain.Sigla.DoTime)
            {
                return sigla;
            }
        }
    }

    // Conta criada e confirmada; devolve a conta master já com o token.
    public async Task<(string ContaId, Pessoa Master)> Conta(string sigla, string tipo = "INSTITUICAO")
    {
        var documento = tipo == "INSTITUICAO" ? "12.345.678/0001-90" : "123.456.789-09";
        var criada = await Caso<ICriarConta>().Executar(new NovaContaDto(tipo, $"Escola {sigla}", documento,
            "Gustavo Boaz de Moura Souza", "(51) 99999-0000", sigla.ToLowerInvariant(), sigla));
        await Confirmar(criada.Comprovante);
        return (criada.ContaId, await Entrar($"usr-{sigla.ToLowerInvariant()}-master", criada.Login, criada.Senha));
    }

    // O administrador do deboaz, da configuração.
    public Task<Pessoa> Admin() => Entrar("usr-dboaz-admin", Backend.AdminEntrada, Backend.AdminSenha);

    // Staff (ou membro do time) criado por quem pede, com a senha gerada e confirmada.
    public async Task<Pessoa> Usuario(Pessoa criador, string nomeCurto, string tipo = "STAFF")
    {
        var criado = await Caso<ICriarUsuario>().Executar(new NovoUsuarioDto(criador.Id, tipo, $"Pessoa {nomeCurto}", nomeCurto));
        var senha = await Caso<IGerarSenha>().Executar(new GeracaoDeSenhaDto(criado.Login, criado.CodigoLiberacao));
        await Confirmar(senha.Comprovante);
        return await Entrar(criado.Id, criado.Login, senha.Senha);
    }

    // Participante com responsáveis; devolve o id e o código de acesso.
    public async Task<(string Id, string Codigo)> Participante(Pessoa criador, string nomeCurto)
    {
        var criado = await Caso<ICriarUsuario>().Executar(new NovoUsuarioDto(criador.Id, "PARTICIPANTE", $"Participante {nomeCurto}", nomeCurto,
            Responsaveis: [new ResponsavelDto("Carla Andrade", "MAE")]));
        return (criado.Id, criado.Login);
    }

    // Confirma a senha anotada e sai, para a sessão do primeiro acesso não contar.
    async Task Confirmar(string comprovante)
    {
        var sessao = await Caso<IConfirmarSenha>().Executar(new ConfirmacaoDeSenhaDto(comprovante));
        await Caso<ISairDoAparelho>().Executar(new ConferenciaDeAcessoDto(sessao.TokenAcesso, Frente.Hub));
    }

    // Uma sessão nova no app: o token de acesso e o de renovação no corpo.
    public async Task<Pessoa> Entrar(string id, string entrada, string senha)
    {
        var sessao = await Caso<IEntrarNoHub>().Executar(new EntradaNoHubDto(entrada, senha, "APP"));
        return new Pessoa(id, entrada, senha, sessao.TokenAcesso, sessao.TokenRenovacao);
    }
}
