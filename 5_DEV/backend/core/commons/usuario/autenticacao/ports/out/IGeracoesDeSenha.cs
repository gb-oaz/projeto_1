using Boaz.Core.Commons.Usuario.Autenticacao.Domain;

namespace Boaz.Core.Commons.Usuario.Autenticacao.Ports.Out;

/// <summary>As gerações de senha (geracoesSenha) que o comprovante confirma (RF-143).</summary>
public interface IGeracoesDeSenha
{
    /// <summary>A geração cujo comprovante tem esse resumo SHA-256; null se nenhuma.</summary>
    Task<GeracaoDeSenha?> BuscarPorComprovante(string resumoDoComprovante, CancellationToken ct = default);

    /// <summary>
    /// Marca a geração como confirmada, para o comprovante não valer de novo. No
    /// primeiro acesso, o adaptador também passa o usuário de PENDENTE a ATIVO
    /// (regra de Usuários).
    /// </summary>
    Task MarcarConfirmada(string geracaoId, DateTimeOffset quando, CancellationToken ct = default);
}
