namespace Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

/// <summary>O que o ao vivo confere a cada intervalo (RN-281): a sessão e o MOD-03.</summary>
public interface IAcessoAoVivo
{
    /// <summary>A sessão ainda está aberta (sessao:&lt;sid&gt;).</summary>
    Task<bool> SessaoAberta(string sid, CancellationToken ct = default);

    /// <summary>O usuário ainda tem o Usuários (MOD-03 · U) e o perfil na ficha do card.</summary>
    Task<bool> PodeVerOCard(string usuarioId, CancellationToken ct = default);
}
