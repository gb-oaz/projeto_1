namespace Boaz.Core.Commons.Usuario.Autorizacao.Domain;

/// <summary>
/// Os módulos que uma conta tem agora (RN-203): os da conta sempre, os
/// contratáveis só contratados e os do time só na conta do time deboaz.
/// </summary>
public sealed class ModulosDaConta
{
    readonly Dictionary<string, ModuloRegistrado> registro;

    /// <summary>Monta a partir do registro e das contratações em vigor.</summary>
    /// <param name="registro">Todos os módulos registrados.</param>
    /// <param name="contratados">Os contratáveis em vigor na conta.</param>
    /// <param name="contaDoTime">A conta é a do time deboaz.</param>
    public ModulosDaConta(IEnumerable<ModuloRegistrado> registro, IReadOnlySet<string> contratados, bool contaDoTime)
    {
        this.registro = registro.ToDictionary(m => m.Id);
        Disponiveis = this.registro.Values
            .Where(m => m.Tipo switch
            {
                TipoDeModulo.DaConta => true,
                TipoDeModulo.Contratavel => contratados.Contains(m.Id),
                _ => contaDoTime,
            })
            .Select(m => m.Id)
            .ToHashSet();
    }

    /// <summary>Os ids dos módulos que a conta tem agora.</summary>
    public IReadOnlySet<string> Disponiveis { get; }

    /// <summary>O módulo registrado; null se o id não existe.</summary>
    public ModuloRegistrado? Registrado(string id) => registro.GetValueOrDefault(id);

    /// <summary>Os módulos que a conta tem e satisfazem o filtro.</summary>
    public IEnumerable<ModuloRegistrado> Onde(Func<ModuloRegistrado, bool> filtro) =>
        Disponiveis.Select(id => registro[id]).Where(filtro).OrderBy(m => m.Id, StringComparer.Ordinal);
}
