using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// GET /usuarios/card (RN-189, RN-280, 15 RN-276): o card guardado de quem pede,
/// lido pelo _id, com o selo, "Sem acesso há 30 dias" e "Códigos vencidos"
/// refeitos na hora, sem gravar nem subir a versao. Card que ainda não existe é
/// contado e gravado. GET não é auditado.
/// </summary>
public sealed class ObterCardUsuarios(
    IUsuarios usuarios, IContas contas, IRepositorioCardUsuarios cards, IContagemDoCard contagem, TimeProvider relogio) : IObterCardUsuarios
{
    readonly LeituraDoCard leitura = new(usuarios, contas, cards, contagem, relogio);

    /// <inheritdoc />
    public async Task<CardDoModuloDto> Executar(QuemPedeDto pedido, CancellationToken ct = default) =>
        (await leitura.Ler(pedido.QuemPede, ct)).Card;
}

/// <summary>A leitura do card de quem pede, também usada pelo ao vivo.</summary>
sealed class LeituraDoCard(IUsuarios usuarios, IContas contas, IRepositorioCardUsuarios cards, IContagemDoCard contagem, TimeProvider relogio)
{
    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly ContagemDosCards guardados = new(cards, contagem);

    /// <summary>O _id e o card de quem pede.</summary>
    /// <exception cref="FalhaDeUsuarios">ACESSO_NEGADO para o participante.</exception>
    public async Task<(string Id, CardDoModuloDto Card)> Ler(string quemPede, CancellationToken ct)
    {
        var quem = await apoio.Quem(quemPede, ct);
        var dono = DonoDeCard.De(quem, await apoio.ContaDe(quem, ct));
        var agora = apoio.Agora;
        var card = await guardados.Guardado(dono, agora, ct);
        return (card.Id, CardDoModuloDto.De(card, await guardados.NoTempo(card, agora, ct)));
    }
}
