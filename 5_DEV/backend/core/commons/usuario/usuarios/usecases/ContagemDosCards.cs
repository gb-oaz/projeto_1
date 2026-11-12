using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>O dono de um card: a conta, o perfil e, no STAFF, a staff.</summary>
/// <param name="Sigla">A sigla da conta (no DEBOAZ, a do time).</param>
/// <param name="Perfil">O perfil.</param>
/// <param name="ContaId">A conta.</param>
/// <param name="UsuarioId">A staff; null nos outros perfis.</param>
/// <param name="NomeCurto">O nome curto da staff, que entra no _id.</param>
sealed record DonoDeCard(string Sigla, PerfilCard Perfil, string ContaId, string? UsuarioId = null, string? NomeCurto = null)
{
    /// <summary>O _id do card.</summary>
    public string Id => CardUsuarios.IdDo(Sigla, Perfil, NomeCurto);

    /// <summary>O card DEBOAZ, um só, na conta do time.</summary>
    public static DonoDeCard Deboaz { get; } = new(Domain.Sigla.DoTime, PerfilCard.Deboaz, ContaAssinante.IdDa(Domain.Sigla.DoTime));

    /// <summary>O card de quem pede (RN-189).</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO para o participante.</exception>
    public static DonoDeCard De(UsuarioCadastrado quem, ContaAssinante conta) => DonoDoCard.Perfil(quem, conta) switch
    {
        PerfilCard.Staff => new(conta.Sigla, PerfilCard.Staff, conta.Id, quem.Id, quem.NomeCurto),
        var perfil => new(conta.Sigla, perfil, conta.Id),
    };
}

/// <summary>
/// Ler, contar e gravar os cards guardados (RN-278, RN-280): o que a leitura, a
/// recontagem na mesma execução e o fechamento repetem.
/// </summary>
sealed class ContagemDosCards(IRepositorioCardUsuarios cards, IContagemDoCard contagem)
{
    /// <summary>O card guardado; se ainda não existe, contado agora e gravado na versao 1.</summary>
    public async Task<CardUsuarios> Guardado(DonoDeCard dono, DateTimeOffset agora, CancellationToken ct) =>
        await cards.Obter(dono.Id, ct) ?? await Recontar(Novo(dono, agora), agora, ct);

    /// <summary>Reconta o card do dono (criando-o se faltar) e grava, se mudou.</summary>
    public async Task<CardUsuarios> Recontar(DonoDeCard dono, DateTimeOffset agora, CancellationToken ct) =>
        await Recontar(await cards.Obter(dono.Id, ct) ?? Novo(dono, agora), agora, ct);

    /// <summary>Os números do card com o que muda só com o tempo refeito agora, sem gravar.</summary>
    public async Task<NumerosDoCard> NoTempo(CardUsuarios card, DateTimeOffset agora, CancellationToken ct) =>
        card.Atual.ComTempo(await contagem.NoTempo(card.Perfil, card.ContaId, card.UsuarioId, agora, ct));

    /// <summary>Os números de agora do card.</summary>
    public Task<NumerosDoCard> Contar(CardUsuarios card, DateTimeOffset agora, CancellationToken ct) =>
        contagem.Contar(card.Perfil, card.ContaId, card.UsuarioId, agora, ct);

    async Task<CardUsuarios> Recontar(CardUsuarios card, DateTimeOffset agora, CancellationToken ct)
    {
        if (card.Aplicar(await Contar(card, agora, ct), agora)) await cards.Salvar(card, ct);
        return card;
    }

    static CardUsuarios Novo(DonoDeCard dono, DateTimeOffset agora) =>
        CardUsuarios.Novo(dono.Sigla, dono.Perfil, dono.ContaId, dono.UsuarioId, dono.NomeCurto, agora);
}
