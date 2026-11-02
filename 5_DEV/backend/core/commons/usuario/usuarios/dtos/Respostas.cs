using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;

namespace Boaz.Core.Commons.Usuario.Usuarios.Dtos;

// Respostas dos endereços de usuarios-contrato-api, com os nomes do JSON.
// Campo null não vai na resposta (o adaptador omite).

/// <summary>Uma página: itens, pagina e total.</summary>
/// <param name="Itens">Os itens.</param>
/// <param name="Pagina">A página.</param>
/// <param name="Total">O total do filtro.</param>
public sealed record PaginaDto<T>(IReadOnlyList<T> Itens, int Pagina, int Total);

/// <summary>Um item de GET /usuarios.</summary>
/// <param name="Id">Id.</param>
/// <param name="Nome">Nome.</param>
/// <param name="Login">Entrada ou código de acesso.</param>
/// <param name="Matricula">Matrícula.</param>
/// <param name="Estado">PENDENTE, ATIVO ou DESATIVADO.</param>
/// <param name="UltimoAcesso">Último acesso.</param>
/// <param name="UltimoAcessoQuem">No participante, quem entrou.</param>
public sealed record UsuarioNaListaDto(
    string Id, string Nome, string Login, string? Matricula, string Estado, DateTimeOffset? UltimoAcesso, string? UltimoAcessoQuem)
{
    /// <summary>O item do usuário.</summary>
    public static UsuarioNaListaDto De(UsuarioCadastrado u) =>
        new(u.Id, u.Nome, u.Login, u.Matricula, u.Estado.Texto(), u.UltimoAcesso, u.UltimoAcessoQuem);
}

/// <summary>GET /usuarios/{id}: sem senha, resumo nem código de liberação (RN-170, RN-181).</summary>
/// <param name="Id">Id.</param>
/// <param name="Tipo">MASTER, STAFF, PARTICIPANTE, MEMBRO_TIME ou ADMINISTRADOR.</param>
/// <param name="Nome">Nome.</param>
/// <param name="NomeCurto">Nome curto.</param>
/// <param name="Login">Entrada ou código de acesso.</param>
/// <param name="Matricula">Matrícula.</param>
/// <param name="Estado">Estado.</param>
/// <param name="Responsaveis">Só no participante.</param>
/// <param name="UltimoAcesso">Último acesso.</param>
/// <param name="UltimoAcessoQuem">No participante, quem entrou.</param>
public sealed record UsuarioLidoDto(
    string Id, string Tipo, string Nome, string NomeCurto, string Login, string? Matricula, string Estado,
    IReadOnlyList<ResponsavelDto>? Responsaveis, DateTimeOffset? UltimoAcesso, string? UltimoAcessoQuem)
{
    /// <summary>A leitura do usuário.</summary>
    public static UsuarioLidoDto De(UsuarioCadastrado u) => new(
        u.Id, u.Tipo.Texto(), u.Nome, u.NomeCurto, u.Login, u.Matricula, u.Estado.Texto(),
        u.Tipo == TipoUsuario.Participante ? u.Responsaveis.Select(r => new ResponsavelDto(r.Nome, r.Parentesco)).ToList() : null,
        u.UltimoAcesso, u.UltimoAcessoQuem);
}

/// <summary>POST /usuarios: 201.</summary>
/// <param name="Id">Id do novo.</param>
/// <param name="Login">Entrada, ou o código de acesso do participante.</param>
/// <param name="Estado">PENDENTE no staff e no membro; ATIVO no participante.</param>
/// <param name="CodigoLiberacao">Mostrado uma vez; null no participante.</param>
/// <param name="VenceEm">Fim da validade do código; null no participante.</param>
public sealed record UsuarioCriadoDto(string Id, string Login, string Estado, string? CodigoLiberacao, DateTimeOffset? VenceEm);

/// <summary>PATCH /usuarios/{id}: 200.</summary>
/// <param name="Id">Id.</param>
/// <param name="Login">Entrada ou código, novos se o nome curto mudou.</param>
public sealed record UsuarioEditadoDto(string Id, string Login);

/// <summary>POST /usuarios/gerar-senha: 200, mostrada uma vez.</summary>
/// <param name="Senha">A senha gerada (RN-171).</param>
/// <param name="Comprovante">Confirma em POST /auth/hub/confirmar-senha.</param>
public sealed record SenhaGeradaDto(string Senha, string Comprovante);

/// <summary>POST /usuarios/{id}/nova-geracao-senha: 201, mostrado uma vez.</summary>
/// <param name="CodigoLiberacao">6 números.</param>
/// <param name="VenceEm">72 h depois do pedido.</param>
public sealed record CodigoDeLiberacaoDto(string CodigoLiberacao, DateTimeOffset VenceEm);

/// <summary>POST /usuarios/{id}/novo-codigo-acesso: 200.</summary>
/// <param name="Login">O código novo.</param>
/// <param name="CodigoGeradoEm">Quando foi gerado.</param>
public sealed record CodigoDeAcessoDto(string Login, DateTimeOffset CodigoGeradoEm);

/// <summary>GET /usuarios/card (RN-189).</summary>
/// <param name="Selo">Códigos de liberação que vencem hoje.</param>
/// <param name="Dado">Usuários, participantes ou contas ativas.</param>
/// <param name="Rotulo">O que o dado conta.</param>
/// <param name="NoMes">Diferença desde o fim do mês anterior.</param>
/// <param name="Meses">Os 5 últimos meses.</param>
/// <param name="Staff">Staff ativo; só na instituição.</param>
/// <param name="Participantes">Participantes ativos; não vem no time.</param>
public sealed record CardDeUsuariosDto(int Selo, int Dado, string Rotulo, int NoMes, IReadOnlyList<int> Meses, int? Staff, int? Participantes);

/// <summary>POST /contas: 201, com a senha mostrada uma vez.</summary>
/// <param name="ContaId">Id da conta.</param>
/// <param name="Login">Entrada da conta master.</param>
/// <param name="Senha">A senha gerada (RN-171).</param>
/// <param name="Comprovante">Confirma em POST /auth/hub/confirmar-senha.</param>
public sealed record ContaCriadaDto(string ContaId, string Login, string Senha, string Comprovante);

/// <summary>GET /contas/sigla-sugerida: 200.</summary>
/// <param name="Sigla">A sugestão; null se o nome não der 5 letras.</param>
/// <param name="Livre">Nenhuma conta usa a sugestão.</param>
public sealed record SiglaSugeridaDto(string? Sigla, bool Livre);

/// <summary>Um item de GET /contas.</summary>
/// <param name="Id">Id.</param>
/// <param name="Sigla">Sigla.</param>
/// <param name="Nome">Nome.</param>
/// <param name="Tipo">INSTITUICAO ou AUTONOMO.</param>
/// <param name="Estado">Estado.</param>
/// <param name="UltimoAcessoMaster">Último acesso da conta master.</param>
public sealed record ContaNaListaDto(string Id, string Sigla, string Nome, string Tipo, string Estado, DateTimeOffset? UltimoAcessoMaster)
{
    /// <summary>O item da conta.</summary>
    public static ContaNaListaDto De(ContaAssinante c) => new(c.Id, c.Sigla, c.Nome, c.Tipo.Texto(), c.Estado.Texto(), c.UltimoAcessoMaster);
}

/// <summary>GET /contas/{id}: só o cadastro, sem usuários, turmas nem conteúdos (RN-187).</summary>
/// <param name="Id">Id.</param>
/// <param name="Tipo">INSTITUICAO ou AUTONOMO.</param>
/// <param name="Nome">Nome.</param>
/// <param name="Documento">CNPJ ou CPF.</param>
/// <param name="Sigla">Sigla.</param>
/// <param name="LoginMaster">Entrada da conta master.</param>
/// <param name="Responsavel">Quem cadastrou.</param>
/// <param name="Telefone">Telefone.</param>
/// <param name="Estado">Estado.</param>
/// <param name="CriadaEm">Criação.</param>
/// <param name="UltimoPagamento">AAAA-MM-DD; null se nunca pagou.</param>
/// <param name="UltimoAcessoMaster">Último acesso da conta master.</param>
public sealed record ContaLidaDto(
    string Id, string Tipo, string Nome, string Documento, string Sigla, string LoginMaster, string Responsavel, string Telefone,
    string Estado, DateTimeOffset CriadaEm, string? UltimoPagamento, DateTimeOffset? UltimoAcessoMaster);

/// <summary>POST /contas/{id}/estado: 200.</summary>
/// <param name="Id">Id.</param>
/// <param name="Estado">O estado novo.</param>
public sealed record ContaComEstadoDto(string Id, string Estado);

/// <summary>POST /contas/{id}/codigo-master: 201.</summary>
/// <param name="CodigoLiberacao">6 números, mostrado uma vez.</param>
/// <param name="Contato">O telefone do cadastro, para onde o código vai.</param>
/// <param name="VenceEm">72 h depois.</param>
public sealed record CodigoDaMasterDto(string CodigoLiberacao, string Contato, DateTimeOffset VenceEm);

/// <summary>GET e PUT /contas/{id}/tempo-sessao: 200, em ISO 8601.</summary>
/// <param name="StaffNavegador">Como PT8H.</param>
/// <param name="StaffApp">Como P7D.</param>
/// <param name="Participante">Como P30D.</param>
public sealed record TempoDaContaLidoDto(string StaffNavegador, string StaffApp, string Participante)
{
    /// <summary>O tempo em texto.</summary>
    public static TempoDaContaLidoDto De(TempoDaConta t) => new(Textos.IsoEmHoras(t.StaffNavegador), Textos.Iso(t.StaffApp), Textos.Iso(t.Participante));
}

/// <summary>GET e PUT /time/tempo-sessao: 200, em ISO 8601.</summary>
/// <param name="MembrosNavegador">Como PT8H.</param>
/// <param name="MembrosApp">Como PT8H.</param>
/// <param name="MastersNavegador">Como PT4H.</param>
/// <param name="MastersApp">Como PT8H.</param>
public sealed record TempoDoTimeLidoDto(string MembrosNavegador, string MembrosApp, string MastersNavegador, string MastersApp)
{
    /// <summary>O tempo em texto.</summary>
    public static TempoDoTimeLidoDto De(TempoDoTime t) =>
        new(Textos.IsoEmHoras(t.MembrosNavegador), Textos.IsoEmHoras(t.MembrosApp), Textos.IsoEmHoras(t.MastersNavegador), Textos.IsoEmHoras(t.MastersApp));
}
