using Boaz.Core.Commons.Usuario.Usuarios.Dtos;

namespace Boaz.MsMono.Primario.In.Commons.Usuario.Usuarios;

// Corpos dos pedidos, como chegam no JSON (usuarios-contrato-api v03). Quem
// pede não vem no corpo: sai do token.

/// <summary>O cadastro de um staff, participante ou membro do time.</summary>
/// <param name="Tipo">O tipo: staff, participante ou membro do time.</param>
/// <param name="Nome">O nome completo.</param>
/// <param name="NomeCurto">O nome curto, que forma a entrada.</param>
/// <param name="Matricula">A matrícula, se a conta usa.</param>
/// <param name="Responsaveis">Os responsáveis do participante.</param>
public sealed record NovoUsuarioCorpo(string? Tipo, string? Nome, string? NomeCurto, string? Matricula, IReadOnlyList<ResponsavelDto?>? Responsaveis);

/// <summary>A edição de um usuário; "responsaveis" vai inteiro.</summary>
/// <param name="Nome">O nome completo.</param>
/// <param name="NomeCurto">O nome curto, que forma a entrada.</param>
/// <param name="Matricula">A matrícula, se a conta usa.</param>
/// <param name="Responsaveis">Os responsáveis do participante, a lista inteira.</param>
public sealed record EdicaoDeUsuarioCorpo(string? Nome, string? NomeCurto, string? Matricula, IReadOnlyList<ResponsavelDto?>? Responsaveis);

/// <summary>O pedido de ajuda ao SAC quando a conta master esqueceu a senha.</summary>
/// <param name="Instituicao">A instituição da conta.</param>
/// <param name="Contato">Como o SAC encontra quem pede.</param>
public sealed record AjudaDaMasterCorpo(string? Instituicao, string? Contato);

/// <summary>O estado novo da conta.</summary>
/// <param name="Estado">O estado pedido para a conta.</param>
public sealed record EstadoDaContaCorpo(string? Estado);

/// <summary>A conferência do cadastro antes de gerar o código da conta master.</summary>
/// <param name="Documento">O CNPJ da conta.</param>
/// <param name="Responsavel">Quem cadastrou a conta.</param>
/// <param name="Telefone">O telefone do cadastro.</param>
/// <param name="UltimoPagamento">A data do último pagamento.</param>
public sealed record ConferenciaDaMasterCorpo(string? Documento, string? Responsavel, string? Telefone, string? UltimoPagamento);

/// <summary>O tempo de sessão do staff e dos participantes, em duração ISO 8601.</summary>
/// <param name="StaffNavegador">O staff no navegador, em horas (PT8H).</param>
/// <param name="StaffApp">O staff no app, em dias (P30D).</param>
/// <param name="Participante">Os participantes, em dias (P30D).</param>
public sealed record TempoDaContaCorpo(string? StaffNavegador, string? StaffApp, string? Participante);

/// <summary>O tempo de sessão dos membros do time e das contas master, em horas (ISO 8601).</summary>
/// <param name="MembrosNavegador">Os membros do time no navegador.</param>
/// <param name="MembrosApp">Os membros do time no app.</param>
/// <param name="MastersNavegador">As contas master no navegador.</param>
/// <param name="MastersApp">As contas master no app.</param>
public sealed record TempoDoTimeCorpo(string? MembrosNavegador, string? MembrosApp, string? MastersNavegador, string? MastersApp);

/// <summary>A escolha de atualização dos cards, inteira (RN-283).</summary>
/// <param name="AtualizacaoCards">NA_ABERTURA ou AO_VIVO.</param>
/// <param name="IntervaloAoVivo">5, 15, 30, 60 ou 300.</param>
public sealed record PreferenciasCorpo(string? AtualizacaoCards, int? IntervaloAoVivo);
