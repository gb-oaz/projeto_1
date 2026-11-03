using Boaz.Core.Commons.Usuario.Autorizacao.Dtos;

namespace Boaz.Core.Commons.Usuario.Autorizacao.Ports.In;

/// <summary>As permissões do acesso anônimo: papel ANONIMO e só o módulo da sala (RF-151).</summary>
public interface ICalcularPermissoesDoAnonimo
{
    /// <summary>As permissões para a sala do módulo dado.</summary>
    PermissoesNoTokenDto Executar(string moduloDaSala);
}
