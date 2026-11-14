namespace Boaz.Core.Commons.Auditoria.Domain;

/// <summary>
/// Uma recusa prevista pela regra (4xx), como as falhas de Usuários, da
/// Autenticação, da Autorização e do SAC: a auditoria a grava como ALERTA com o
/// código dela; o que não é recusa prevista é ERRO_INESPERADO, nivel ERRO (17 RN-270).
/// </summary>
public interface IRecusaPrevista
{
    /// <summary>O código do contrato, como SIGLA_EM_USO.</summary>
    string Codigo { get; }
}
