using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;
using Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;
using MongoDB.Bson;
using MongoDB.Driver;
using TempoDaConta = Boaz.Core.Commons.Usuario.Usuarios.Domain.TempoDaConta;
using TempoDoTime = Boaz.Core.Commons.Usuario.Usuarios.Domain.TempoDoTime;

namespace Boaz.MsMono.Infra.Out.Commons.Usuario.Autenticacao;

// O que a Autenticação lê do cadastro (usuarios, contas, geracoesSenha) e o
// pouco que grava nele: o último acesso e a confirmação da senha.
public sealed class CadastroDaEntrada(Armazenamento armazenamento, TimeDeboaz time) : IUsuarios, IContas, IGeracoesDeSenha
{
    IMongoCollection<BsonDocument> Usuarios => armazenamento.Colecao(Colecoes.Usuarios);

    IMongoCollection<BsonDocument> Contas => armazenamento.Colecao(Colecoes.Contas);

    IMongoCollection<BsonDocument> Geracoes => armazenamento.Colecao(Colecoes.GeracoesSenha);

    // ── IUsuarios ──────────────────────────────────────────────────────────────

    public async Task<UsuarioDaEntrada?> BuscarPorEntrada(string entrada, CancellationToken ct = default)
    {
        var filtro = Bson.Filtro.Eq("login", entrada) & Bson.Filtro.Ne("tipo", "PARTICIPANTE");
        var achado = await Usuarios.Find(filtro).FirstOrDefaultAsync(ct);
        if (achado is null && await time.GarantirSeFor(entrada: entrada, ct: ct)) achado = await Usuarios.Find(filtro).FirstOrDefaultAsync(ct);
        return achado is null ? null : Usuario(achado);
    }

    public async Task<UsuarioDaEntrada?> BuscarPorCodigoDeAcesso(string codigo, CancellationToken ct = default) =>
        await Usuarios.Find(Bson.Filtro.Eq("login", codigo) & Bson.Filtro.Eq("tipo", "PARTICIPANTE")).FirstOrDefaultAsync(ct) is { } d
            ? Usuario(d)
            : null;

    public async Task<UsuarioDaEntrada?> Buscar(string id, CancellationToken ct = default)
    {
        var achado = await Usuarios.Find(Bson.Filtro.Eq("_id", id)).FirstOrDefaultAsync(ct);
        if (achado is null && await time.GarantirSeFor(usuarioId: id, ct: ct)) achado = await Usuarios.Find(Bson.Filtro.Eq("_id", id)).FirstOrDefaultAsync(ct);
        return achado is null ? null : Usuario(achado);
    }

    public async Task RegistrarUltimoAcesso(string usuarioId, DateTimeOffset quando, string? quem, CancellationToken ct = default)
    {
        var usuario = await Usuarios.FindOneAndUpdateAsync(Bson.Filtro.Eq("_id", usuarioId),
            Bson.Mudar.Set("ultimoAcesso", Bson.Valor(quando)).Set("ultimoAcessoQuem", Bson.Valor(quem)), cancellationToken: ct);
        // a lista de contas do time mostra o último acesso da conta master
        if (usuario?.Texto("tipo") == "MASTER")
        {
            await Contas.UpdateOneAsync(Bson.Filtro.Eq("_id", usuario.Texto("contaId")),
                Bson.Mudar.Set("ultimoAcessoMaster", Bson.Valor(quando)), cancellationToken: ct);
        }
    }

    static UsuarioDaEntrada Usuario(BsonDocument d) =>
        new(d["_id"].AsString, d.Texto("contaId")!, d.Texto("sigla")!, Documentos.TipoDe(d), Documentos.EstadoDe(d), d.Texto("resumoSenha"));

    // ── IContas ────────────────────────────────────────────────────────────────

    async Task<ContaDaEntrada?> IContas.Buscar(string id, CancellationToken ct) =>
        await Contas.Find(Bson.Filtro.Eq("_id", id)).FirstOrDefaultAsync(ct) is { } conta
            ? Conta(conta, await DoTime(ct))
            : null;

    public async Task<ContaDaEntrada> BuscarTimeDeboaz(CancellationToken ct = default)
    {
        var doTime = await DoTime(ct);
        if (doTime is null)
        {
            await time.Garantir(ct);
            doTime = await DoTime(ct);
        }
        return Conta(doTime!, doTime);
    }

    Task<BsonDocument?> DoTime(CancellationToken ct) =>
        Contas.Find(Bson.Filtro.Eq("_id", TimeDeboaz.ContaId)).FirstOrDefaultAsync(ct)!;

    // os tempos da conta valem para o staff e os participantes; os do time, para
    // os membros e as contas master (RN-183, RN-243)
    static ContaDaEntrada Conta(BsonDocument conta, BsonDocument? doTime)
    {
        var daConta = Documentos.TempoDaConta(conta) ?? TempoDaConta.Padrao;
        var doTimeTempo = Documentos.TempoDoTime(doTime) ?? TempoDoTime.Padrao;
        return new ContaDaEntrada(conta["_id"].AsString, conta.Texto("sigla")!, Documentos.EstadoDaConta(conta), new TempoDeSessao(
            daConta.StaffNavegador, daConta.StaffApp, daConta.Participante,
            doTimeTempo.MembrosNavegador, doTimeTempo.MembrosApp, doTimeTempo.MastersNavegador, doTimeTempo.MastersApp));
    }

    // ── IGeracoesDeSenha ───────────────────────────────────────────────────────

    public async Task<GeracaoDeSenha?> BuscarPorComprovante(string resumoDoComprovante, CancellationToken ct = default) =>
        await Geracoes.Find(Bson.Filtro.Eq("resumoComprovante", resumoDoComprovante)).FirstOrDefaultAsync(ct) is { } g
            ? new GeracaoDeSenha(g["_id"].AsString, g.Texto("usuarioId")!, g.Data("pedidaEm") ?? DateTimeOffset.UnixEpoch, g.Data("confirmadaEm"))
            : null;

    // a senha foi anotada: a geração não vale mais e quem estava pendente fica ativo
    public async Task MarcarConfirmada(string geracaoId, DateTimeOffset quando, CancellationToken ct = default)
    {
        var geracao = await Geracoes.FindOneAndUpdateAsync(Bson.Filtro.Eq("_id", geracaoId),
            Bson.Mudar.Set("confirmadaEm", Bson.Valor(quando)), cancellationToken: ct);
        if (geracao?.Texto("usuarioId") is { } usuarioId)
        {
            await Usuarios.UpdateOneAsync(Bson.Filtro.Eq("_id", usuarioId) & Bson.Filtro.Eq("estado", "PENDENTE"),
                Bson.Mudar.Set("estado", "ATIVO"), cancellationToken: ct);
        }
    }
}

// Permissões que vão no token, calculadas pela Autorização (RF-145).
public sealed class PermissoesDoToken(ICalcularPermissoes calcular, ICalcularPermissoesDoAnonimo anonimo) : IPermissoes
{
    public async Task<PermissoesNoToken> DoUsuario(UsuarioDaEntrada usuario, CancellationToken ct = default)
    {
        var p = await calcular.Executar(usuario.Id, ct);
        return new PermissoesNoToken(p.Papel, p.Mods, p.Pv);
    }

    public Task<PermissoesNoToken> DoAnonimo(SalaAberta sala, CancellationToken ct = default)
    {
        var p = anonimo.Executar(sala.ModuloId);
        return Task.FromResult(new PermissoesNoToken(p.Papel, p.Mods, p.Pv));
    }
}

// As salas ao vivo ainda não têm módulo nem contrato: nenhuma está aberta, e
// POST /auth/sala/entrar responde SALA_FECHADA (404).
public sealed class SalasAindaSemModulo : ISalas
{
    public Task<SalaAberta?> BuscarAberta(string codigo, CancellationToken ct = default) => Task.FromResult<SalaAberta?>(null);

    public Task<AcessoAnonimo> NovoAcessoAnonimo(SalaAberta sala, CancellationToken ct = default) =>
        throw new NotSupportedException("As salas ao vivo ainda não existem.");
}
