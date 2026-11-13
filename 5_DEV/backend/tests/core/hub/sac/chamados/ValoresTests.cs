using Boaz.Core.Hub.Sac.Chamados.Domain;
using Boaz.Core.Hub.Sac.Chamados.Dtos;

namespace Boaz.Core.Hub.Tests.Sac.Chamados;

/// <summary>Os números, os textos do contrato e a forma dos pedidos (RN-291, RN-292).</summary>
public sealed class ValoresTests
{
    [Theory]
    [InlineData("SAC-000042", 42L)]
    [InlineData("SAC-1234567", 1234567L)]
    [InlineData("SAC-00004", null)]
    [InlineData("SAC-0000x2", null)]
    [InlineData("SAC-000000", null)]
    [InlineData("sac-000042", null)]
    [InlineData(null, null)]
    public void Numero_do_chamado(string? texto, long? numero) => Assert.Equal(numero, NumeroDoChamado.Ler(texto)?.Numero);

    [Fact]
    public void Numero_do_chamado_e_do_pedido()
    {
        var n = new NumeroDoChamado(87);
        Assert.Equal(("SAC-000087", "ajuda-000087", "SAC-000087"), (n.Valor, n.Pedido(), n.ToString()));
        Assert.Equal("msg-000318", ItemDaConversa.IdDe(318));
        Assert.Equal(318, ItemDaConversa.NumeroDe("msg-000318"));
        Assert.Null(ItemDaConversa.NumeroDe("anx-7f3a21"));
        Assert.Null(ItemDaConversa.NumeroDe(null));
    }

    [Fact]
    public void Textos_do_contrato_vao_e_voltam()
    {
        foreach (var v in Enum.GetValues<StatusDoChamado>()) Assert.Equal(v, Textos.StatusDe(v.Texto()));
        foreach (var v in Enum.GetValues<Categoria>()) Assert.Equal(v, Textos.CategoriaDe(v.Texto()));
        foreach (var v in Enum.GetValues<Fechamento>()) Assert.Equal(v, Textos.FechamentoDe(v.Texto()));
        foreach (var v in Enum.GetValues<TipoDeAutor>()) Assert.Equal(v, Textos.AutorDe(v.Texto()));
        foreach (var v in Enum.GetValues<TipoDeItem>()) Assert.Equal(v, Textos.ItemDe(v.Texto()));
        foreach (var v in Enum.GetValues<TipoDeOrigem>()) Assert.Equal(v, Textos.OrigemDe(v.Texto()));
        foreach (var v in Enum.GetValues<TipoDeEvento>()) Assert.Equal(v, Textos.EventoDe(v.Texto()));
        Assert.Equal("EM_ATENDIMENTO", StatusDoChamado.EmAtendimento.Texto());
        Assert.Equal("MODULO_COM_PROBLEMA", Categoria.ModuloComProblema.Texto());
        Assert.Null(Textos.StatusDe("ATENDENDO"));
        Assert.Null(Textos.CategoriaDe(null));
    }

    static string Campo(Action acao)
    {
        var falha = Assert.Throws<FalhaDoSac>(acao);
        return $"{falha.Codigo} {falha.Campo}";
    }

    static PedidoAbrirChamado Corpo(string? categoria = "MODULO_COM_PROBLEMA", string? assunto = "Presença", string? descricao = null,
        string? relacionadoA = null, params string?[] anexos) =>
        new(categoria, assunto, descricao ?? new string('d', 20), relacionadoA, anexos);

    [Theory]
    [InlineData(4, "DADOS_INVALIDOS assunto")]
    [InlineData(5, null)]
    [InlineData(120, null)]
    [InlineData(121, "DADOS_INVALIDOS assunto")]
    public void RN_291_assunto(int tamanho, string? recusa)
    {
        var pedido = Corpo(assunto: new string('a', tamanho));
        if (recusa is null) Assert.Equal(tamanho, pedido.Validar().Assunto.Length);
        else Assert.Equal(recusa, Campo(() => pedido.Validar()));
    }

    [Theory]
    [InlineData(19, "DADOS_INVALIDOS descricao")]
    [InlineData(5000, null)]
    [InlineData(5001, "DADOS_INVALIDOS descricao")]
    public void RN_291_descricao(int tamanho, string? recusa)
    {
        var pedido = Corpo(descricao: new string('a', tamanho));
        if (recusa is null) Assert.Equal(tamanho, pedido.Validar().Descricao.Length);
        else Assert.Equal(recusa, Campo(() => pedido.Validar()));
    }

    [Fact]
    public void RF_171_mensagem_do_assunto_do_contrato() =>
        Assert.Equal("O campo \"assunto\" deve ter de 5 a 120 caracteres.", Assert.Throws<FalhaDoSac>(() => Corpo(assunto: "Erro").Validar()).Message);

    [Theory]
    [InlineData(null, "DADOS_INVALIDOS categoria")]
    [InlineData("SENHA_CONTA_MASTER", "DADOS_INVALIDOS categoria")]
    [InlineData("OUTRA", "DADOS_INVALIDOS categoria")]
    public void RF_171_categoria(string? categoria, string recusa) => Assert.Equal(recusa, Campo(() => Corpo(categoria).Validar()));

    [Fact]
    public void RF_171_relacionado_e_anexos()
    {
        Assert.Equal(42, Corpo(relacionadoA: "SAC-000042").Validar().RelacionadoA?.Numero);
        Assert.Equal("DADOS_INVALIDOS relacionadoA", Campo(() => Corpo(relacionadoA: "42").Validar()));
        Assert.Equal("LIMITE_ANEXOS anexos", Campo(() => Corpo(anexos: ["a1", "a2", "a3", "a4", "a5", "a6"]).Validar()));
        Assert.Equal("DADOS_INVALIDOS anexos", Campo(() => Corpo(anexos: ["a1", "a1"]).Validar()));
        Assert.Equal("DADOS_INVALIDOS anexos", Campo(() => Corpo(anexos: [" "]).Validar()));
        Assert.Equal(5, Corpo(anexos: ["a1", "a2", "a3", "a4", "a5"]).Validar().Anexos.Count);
    }

    [Theory]
    [InlineData(0, "DADOS_INVALIDOS texto")]
    [InlineData(5000, null)]
    [InlineData(5001, "DADOS_INVALIDOS texto")]
    public void RN_291_mensagem(int tamanho, string? recusa)
    {
        var pedido = new PedidoMensagem(new string('m', tamanho), null, null);
        if (recusa is null) Assert.Equal((tamanho, false), (pedido.Validar().Texto.Length, pedido.Validar().Interna));
        else Assert.Equal(recusa, Campo(() => pedido.Validar()));
    }

    [Theory]
    [InlineData(null, "Qual?", "DADOS_INVALIDOS status")]
    [InlineData("RESOLVIDO", null, "DADOS_INVALIDOS mensagem")]
    [InlineData("RESOLVIDO", " ", "DADOS_INVALIDOS mensagem")]
    public void RF_176_status_e_mensagem(string? status, string? mensagem, string recusa) =>
        Assert.Equal(recusa, Campo(() => new PedidoStatus(status, mensagem).Validar()));

    [Theory]
    [InlineData(null, null, "DADOS_INVALIDOS nota")]
    [InlineData(0, null, "DADOS_INVALIDOS nota")]
    [InlineData(6, null, "DADOS_INVALIDOS nota")]
    [InlineData(4, 501, "DADOS_INVALIDOS comentario")]
    public void RF_178_avaliacao(int? nota, int? comentario, string recusa) =>
        Assert.Equal(recusa, Campo(() => new PedidoAvaliacao(nota, comentario is { } n ? new string('c', n) : null).Validar()));

    [Fact]
    public void RF_178_comentario_de_500_e_vazio()
    {
        Assert.Equal(500, new PedidoAvaliacao(5, new string('c', 500)).Validar().Comentario?.Length);
        Assert.Null(new PedidoAvaliacao(5, "  ").Validar().Comentario);
        Assert.Equal("DADOS_INVALIDOS mensagem", Campo(() => new PedidoReabrir(null).Validar()));
    }

    [Theory]
    [InlineData("MASTER", Visao.Conta)]
    [InlineData("AUTONOMO", Visao.Conta)]
    [InlineData("MEMBRO_TIME", Visao.Time)]
    [InlineData("ADMINISTRADOR", Visao.Time)]
    public void RN_288_lado_de_quem_pede(string papel, Visao visao) => Assert.Equal(visao, new QuemPede("u", papel, "c").Visao);

    [Theory]
    [InlineData("STAFF")]
    [InlineData("PARTICIPANTE")]
    public void RN_288_quem_nao_usa_o_sac(string papel) =>
        Assert.Equal("ACESSO_NEGADO", Assert.Throws<FalhaDoSac>(() => new QuemPede("u", papel, "c").Visao).Codigo);

    [Fact]
    public void Falhas_com_o_status_do_contrato()
    {
        Assert.Equal(GrupoDaFalha.MuitoGrande, FalhaDoSac.AnexoGrande().Grupo);
        Assert.Equal(GrupoDaFalha.TipoNaoAceito, FalhaDoSac.TipoNaoAceito().Grupo);
        Assert.Equal(GrupoDaFalha.Indisponivel, FalhaDoSac.VerificacaoIndisponivel().Grupo);
        Assert.Equal(("ANEXO_RECUSADO", GrupoDaFalha.DadosInvalidos), (FalhaDoSac.AnexoRecusado().Codigo, FalhaDoSac.AnexoRecusado().Grupo));
        var fora = FalhaDoSac.ForaDosLimites("horasUteis", 1, 80);
        Assert.Equal(("FORA_DOS_LIMITES", "horasUteis", "1", "80"), (fora.Codigo, fora.Campo, fora.Minimo, fora.Maximo));
        Assert.Equal(GrupoDaFalha.NaoEncontrado, FalhaDoSac.AnexoInexistente().Grupo);
        Assert.Equal("PRAZO_AVALIACAO_VENCIDO", FalhaDoSac.PrazoAvaliacaoVencido().Codigo);
    }
}
