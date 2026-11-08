using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using MongoDB.Bson;
using Documento = Boaz.Core.Commons.Usuario.Usuarios.Domain.Documento;

namespace Boaz.MsMono.Infra.Out.Commons.Usuario;

// Conversão entre os documentos do banco (usuario-documentos) e o domínio do
// core. O documento de usuarios é um só para os três módulos: Usuários grava
// os campos do cadastro, a Autenticação o último acesso e a confirmação, a
// Autorização o subdocumento "permissoes".
public static class Documentos
{
    // Campos do cadastro, os que o módulo Usuários grava. Sem matrícula (o
    // autônomo e o time, RN-188), o campo fica ausente, não null: o índice
    // único contaId+matricula só vale com o campo presente, e o Cosmos DB conta
    // dois null como repetidos (identidade-acesso-documentos v12).
    public static BsonDocument DoCadastro(UsuarioCadastrado u)
    {
        var d = Cadastro(u);
        if (u.Matricula is not null) d.InsertAt(d.IndexOfName("login") + 1, new BsonElement("matricula", u.Matricula));
        return d;
    }

    static BsonDocument Cadastro(UsuarioCadastrado u) => new()
    {
        ["contaId"] = u.ContaId,
        ["sigla"] = u.Sigla,
        ["tipo"] = u.Tipo.Texto(),
        ["nome"] = u.Nome,
        ["nomeCurto"] = u.NomeCurto,
        ["login"] = u.Login,
        ["estado"] = u.Estado.Texto(),
        ["resumoSenha"] = Bson.Valor(u.ResumoSenha),
        ["responsaveis"] = new BsonArray(u.Responsaveis.Select(r => new BsonDocument { ["nome"] = r.Nome, ["parentesco"] = r.Parentesco })),
        ["criadoPor"] = Bson.Valor(u.CriadoPor),
        ["criadoEm"] = Bson.Valor(u.CriadoEm),
        ["codigoGeradoEm"] = Bson.Valor(u.CodigoGeradoEm),
    };

    // O usuário como o módulo Usuários o vê.
    public static UsuarioCadastrado Usuario(BsonDocument d) => new(
        d["_id"].AsString,
        d.Texto("contaId")!,
        d.Texto("sigla")!,
        TipoDe(d),
        d.Texto("nome") ?? "",
        d.Texto("nomeCurto") ?? "",
        d.Texto("login") ?? "",
        d.Texto("matricula"),
        EstadoDe(d),
        d.Texto("resumoSenha"),
        d.TryGetValue("responsaveis", out var r) && r.IsBsonArray
            ? r.AsBsonArray.OfType<BsonDocument>().Select(x => new Responsavel(x.Texto("nome") ?? "", x.Texto("parentesco") ?? "")).ToList()
            : [],
        d.Texto("criadoPor"),
        d.Data("criadoEm") ?? DateTimeOffset.UnixEpoch,
        d.Data("codigoGeradoEm"),
        d.Data("ultimoAcesso"),
        d.Texto("ultimoAcessoQuem"));

    public static TipoUsuario TipoDe(BsonDocument d) => Textos.TipoDe(d.Texto("tipo")) ?? TipoUsuario.Staff;

    public static EstadoUsuario EstadoDe(BsonDocument d) => Textos.EstadoDe(d.Texto("estado")) ?? EstadoUsuario.Pendente;

    // A conta assinante; o tempo de sessão fica no mesmo documento (tempoSessao).
    public static BsonDocument DaConta(ContaAssinante c) => new()
    {
        ["_id"] = c.Id,
        ["sigla"] = c.Sigla,
        ["dominio"] = c.Dominio,
        ["nome"] = c.Nome,
        ["tipo"] = c.Tipo.Texto(),
        ["estado"] = c.Estado.Texto(),
        ["documento"] = c.Documento,
        // só os números, para a busca por CNPJ ou CPF com ou sem pontuação
        ["documentoNumeros"] = Documento.Numeros(c.Documento),
        ["responsavel"] = c.Responsavel,
        ["telefone"] = c.Telefone,
        ["criadaEm"] = Bson.Valor(c.CriadaEm),
        ["ultimoAcessoMaster"] = Bson.Valor(c.UltimoAcessoMaster),
    };

    public static ContaAssinante Conta(BsonDocument d) => new(
        d["_id"].AsString,
        d.Texto("sigla")!,
        d.Texto("dominio") ?? "",
        d.Texto("nome") ?? "",
        Textos.TipoDaContaDe(d.Texto("tipo")) ?? TipoConta.Instituicao,
        EstadoDaConta(d),
        d.Texto("documento") ?? "",
        d.Texto("responsavel") ?? "",
        d.Texto("telefone") ?? "",
        d.Data("criadaEm") ?? DateTimeOffset.UnixEpoch,
        d.Data("ultimoAcessoMaster"));

    public static EstadoConta EstadoDaConta(BsonDocument d) => Textos.EstadoDaContaDe(d.Texto("estado")) ?? EstadoConta.Ativa;

    // Tempo de sessão do staff e dos participantes da conta (tempoSessao).
    public static BsonDocument DoTempo(TempoDaConta t) => new()
    {
        ["staffNavegador"] = Textos.Iso(t.StaffNavegador),
        ["staffApp"] = Textos.Iso(t.StaffApp),
        ["participante"] = Textos.Iso(t.Participante),
    };

    public static TempoDaConta? TempoDaConta(BsonDocument conta) =>
        conta.Documento("tempoSessao") is { } t
        && Textos.IsoDe(t.Texto("staffNavegador")) is { } navegador
        && Textos.IsoDe(t.Texto("staffApp")) is { } app
        && Textos.IsoDe(t.Texto("participante")) is { } participante
            ? new TempoDaConta(navegador, app, participante)
            : null;

    // Tempo de sessão dos membros e das contas master, guardado na conta do time.
    public static BsonDocument DoTempo(TempoDoTime t) => new()
    {
        ["membrosNavegador"] = Textos.Iso(t.MembrosNavegador),
        ["membrosApp"] = Textos.Iso(t.MembrosApp),
        ["mastersNavegador"] = Textos.Iso(t.MastersNavegador),
        ["mastersApp"] = Textos.Iso(t.MastersApp),
    };

    public static TempoDoTime? TempoDoTime(BsonDocument? contaDoTime) =>
        contaDoTime?.Documento("tempoDoTime") is { } t
        && Textos.IsoDe(t.Texto("membrosNavegador")) is { } membrosNavegador
        && Textos.IsoDe(t.Texto("membrosApp")) is { } membrosApp
        && Textos.IsoDe(t.Texto("mastersNavegador")) is { } mastersNavegador
        && Textos.IsoDe(t.Texto("mastersApp")) is { } mastersApp
            ? new TempoDoTime(membrosNavegador, membrosApp, mastersNavegador, mastersApp)
            : null;

    // Uma geração de senha (geracoesSenha); a conta sai da sigla do id (ger-<sigla>-…).
    public static BsonDocument DaGeracao(GeracaoDeSenhaPedida g)
    {
        var sigla = g.Id.Split('-') is [_, var s, ..] ? s.ToUpperInvariant() : "";
        return new()
        {
            ["_id"] = g.Id,
            ["sigla"] = sigla,
            ["contaId"] = ContaAssinante.IdDa(sigla),
            ["usuarioId"] = g.UsuarioId,
            ["pedidaPor"] = g.PedidaPor,
            ["resumoCodigo"] = Bson.Valor(g.ResumoCodigo),
            ["resumoComprovante"] = Bson.Valor(g.ResumoComprovante),
            ["pedidaEm"] = Bson.Valor(g.PedidaEm),
            ["senhaMostradaEm"] = Bson.Valor(g.SenhaMostradaEm),
            ["confirmadaEm"] = Bson.Valor(g.ConfirmadaEm),
        };
    }

    public static GeracaoDeSenhaPedida Geracao(BsonDocument d) => new(
        d["_id"].AsString,
        d.Texto("usuarioId")!,
        d.Texto("pedidaPor") ?? "",
        d.Texto("resumoCodigo"),
        d.Data("pedidaEm") ?? DateTimeOffset.UnixEpoch,
        d.Data("senhaMostradaEm"),
        d.Texto("resumoComprovante"),
        d.Data("confirmadaEm"));
}
