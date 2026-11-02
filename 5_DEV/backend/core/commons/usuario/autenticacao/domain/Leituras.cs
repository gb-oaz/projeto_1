namespace Boaz.Core.Commons.Usuario.Autenticacao.Domain;

// O que a autenticação lê de Usuários, do catálogo e da autorização pelas
// portas de saída (autenticacao-documentos): só os campos de que precisa.

/// <summary>Quem entrou no quest: o participante ou um responsável com o parentesco (RN-228).</summary>
/// <param name="Tipo">Participante ou responsável.</param>
/// <param name="Parentesco">Como o responsável se apresenta ("Mãe"); só no responsável.</param>
public sealed record QuemEntrou(TipoQuemEntrou Tipo, string? Parentesco = null);

/// <summary>O usuário que tenta entrar, lido da coleção usuarios.</summary>
/// <param name="Id">Id do usuário (usr-ebfga-maria).</param>
/// <param name="ContaId">Conta a que pertence; no time deboaz, a conta do time.</param>
/// <param name="Sigla">Sigla da conta (EBFGA).</param>
/// <param name="Tipo">Tipo do usuário.</param>
/// <param name="Estado">Estado no cadastro.</param>
/// <param name="ResumoSenha">Resumo irreversível da senha; null no participante (RNF-201).</param>
public sealed record UsuarioDaEntrada(
    string Id, string ContaId, string Sigla, TipoUsuario Tipo, EstadoUsuario Estado, string? ResumoSenha);

/// <summary>A conta do usuário, lida da coleção contas.</summary>
/// <param name="Id">Id da conta.</param>
/// <param name="Sigla">Sigla da conta.</param>
/// <param name="Estado">Estado da conta.</param>
/// <param name="Tempo">Tempo sem uso da conta; no time deboaz, o dos membros e das contas master.</param>
public sealed record ContaDaEntrada(string Id, string Sigla, EstadoConta Estado, TempoDeSessao Tempo);

/// <summary>A geração de senha que o comprovante confirma (geracoesSenha, RF-143).</summary>
/// <param name="Id">Id da geração.</param>
/// <param name="UsuarioId">Usuário cuja senha foi gerada.</param>
/// <param name="PedidaEm">Quando a geração foi pedida: vale 72 h (RN-182).</param>
/// <param name="ConfirmadaEm">Quando o comprovante foi usado; null se ainda não foi.</param>
public sealed record GeracaoDeSenha(string Id, string UsuarioId, DateTimeOffset PedidaEm, DateTimeOffset? ConfirmadaEm);

/// <summary>Uma sala ao vivo aberta, para o acesso anônimo (RF-136).</summary>
/// <param name="Codigo">Código da sala (482107).</param>
/// <param name="ContaId">Conta dona da sala.</param>
/// <param name="Sigla">Sigla da conta.</param>
/// <param name="ModuloId">Módulo da sala, o único que o token anônimo libera (MOD-05).</param>
public sealed record SalaAberta(string Codigo, string ContaId, string Sigla, string ModuloId);

/// <summary>O acesso anônimo registrado para uma pessoa numa sala (acessosAnonimos).</summary>
/// <param name="Id">Id do acesso, que vai no sub do token.</param>
/// <param name="Apelido">Apelido mostrado na sala (PUPIL-EBFGA-13465).</param>
public sealed record AcessoAnonimo(string Id, string Apelido);

/// <summary>Papel, módulos e versão das permissões que vão no token (17-b Autorização, RF-145).</summary>
/// <param name="Papel">Papel da pessoa (STAFF, PARTICIPANTE…).</param>
/// <param name="Mods">Módulo → abas ("MOD-03" → "CU").</param>
/// <param name="Pv">Versão das permissões.</param>
public sealed record PermissoesNoToken(string Papel, IReadOnlyDictionary<string, string> Mods, int Pv);
