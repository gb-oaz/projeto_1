namespace Boaz.Core.Hub.Sac.Chamados.Domain;

/// <summary>O status do chamado (18 RN-290).</summary>
public enum StatusDoChamado
{
    /// <summary>Aberto, à espera da primeira resposta do time.</summary>
    Aberto,
    /// <summary>Com o time.</summary>
    EmAtendimento,
    /// <summary>O time pediu informação à conta.</summary>
    AguardandoCliente,
    /// <summary>O time resolveu; a conta confirma ou reabre em 7 dias.</summary>
    Resolvido,
    /// <summary>Fechado: confirmado, encerrado pela conta ou pelo prazo.</summary>
    Fechado,
}

/// <summary>A categoria do chamado, que dá o prazo de primeira resposta (18 RN-289).</summary>
public enum Categoria
{
    /// <summary>Acesso e senha.</summary>
    AcessoESenha,
    /// <summary>Cobrança.</summary>
    Cobranca,
    /// <summary>Módulo com problema.</summary>
    ModuloComProblema,
    /// <summary>Dúvida de uso.</summary>
    DuvidaDeUso,
    /// <summary>Sugestão ao deboaz.</summary>
    SugestaoAoDeboaz,
    /// <summary>Senha da conta master: só o pedido sem login da tela Entrar.</summary>
    SenhaContaMaster,
}

/// <summary>Como o chamado fechou (18 RN-290, RN-296).</summary>
public enum Fechamento
{
    /// <summary>A conta confirmou a solução, com a avaliação.</summary>
    Confirmado,
    /// <summary>A conta encerrou antes de resolvido.</summary>
    PelaConta,
    /// <summary>Os 7 dias depois de resolvido passaram.</summary>
    Prazo,
}

/// <summary>Quem escreveu na conversa.</summary>
public enum TipoDeAutor
{
    /// <summary>A conta master ou o autônomo.</summary>
    Conta,
    /// <summary>O time deboaz.</summary>
    Time,
    /// <summary>O agendamento (prazos): "sistema" no lugar de quem (18 RN-297).</summary>
    Sistema,
}

/// <summary>O que é um item da conversa.</summary>
public enum TipoDeItem
{
    /// <summary>Mensagem que a conta vê.</summary>
    Mensagem,
    /// <summary>Nota interna, só do time.</summary>
    NotaInterna,
    /// <summary>Evento da linha do tempo.</summary>
    Evento,
}

/// <summary>De onde veio o chamado.</summary>
public enum TipoDeOrigem
{
    /// <summary>Aberto no hub pela conta.</summary>
    Hub,
    /// <summary>O pedido de senha sem login da tela Entrar (18 RF-179).</summary>
    EntrarSemLogin,
}

/// <summary>Os eventos da linha do tempo (sac-documentos v01).</summary>
public enum TipoDeEvento
{
    /// <summary>O chamado foi aberto.</summary>
    Aberto,
    /// <summary>O time assumiu.</summary>
    Assumiu,
    /// <summary>Outro do time assumiu no lugar.</summary>
    TrocouResponsavel,
    /// <summary>O status mudou.</summary>
    Status,
    /// <summary>A conta avaliou.</summary>
    Avaliado,
}

/// <summary>Os textos do contrato de API (maiúsculas) para os valores do domínio.</summary>
public static class Textos
{
    static readonly Dictionary<StatusDoChamado, string> Status = new()
    {
        [StatusDoChamado.Aberto] = "ABERTO",
        [StatusDoChamado.EmAtendimento] = "EM_ATENDIMENTO",
        [StatusDoChamado.AguardandoCliente] = "AGUARDANDO_CLIENTE",
        [StatusDoChamado.Resolvido] = "RESOLVIDO",
        [StatusDoChamado.Fechado] = "FECHADO",
    };

    static readonly Dictionary<Categoria, string> Categorias = new()
    {
        [Categoria.AcessoESenha] = "ACESSO_E_SENHA",
        [Categoria.Cobranca] = "COBRANCA",
        [Categoria.ModuloComProblema] = "MODULO_COM_PROBLEMA",
        [Categoria.DuvidaDeUso] = "DUVIDA_DE_USO",
        [Categoria.SugestaoAoDeboaz] = "SUGESTAO_AO_DEBOAZ",
        [Categoria.SenhaContaMaster] = "SENHA_CONTA_MASTER",
    };

    static readonly Dictionary<Fechamento, string> Fechamentos = new()
    {
        [Fechamento.Confirmado] = "CONFIRMADO",
        [Fechamento.PelaConta] = "PELA_CONTA",
        [Fechamento.Prazo] = "PRAZO",
    };

    static readonly Dictionary<TipoDeAutor, string> Autores = new()
    {
        [TipoDeAutor.Conta] = "CONTA",
        [TipoDeAutor.Time] = "TIME",
        [TipoDeAutor.Sistema] = "SISTEMA",
    };

    static readonly Dictionary<TipoDeItem, string> Itens = new()
    {
        [TipoDeItem.Mensagem] = "MENSAGEM",
        [TipoDeItem.NotaInterna] = "NOTA_INTERNA",
        [TipoDeItem.Evento] = "EVENTO",
    };

    static readonly Dictionary<TipoDeOrigem, string> Origens = new()
    {
        [TipoDeOrigem.Hub] = "HUB",
        [TipoDeOrigem.EntrarSemLogin] = "ENTRAR_SEM_LOGIN",
    };

    static readonly Dictionary<TipoDeEvento, string> Eventos = new()
    {
        [TipoDeEvento.Aberto] = "ABERTO",
        [TipoDeEvento.Assumiu] = "ASSUMIU",
        [TipoDeEvento.TrocouResponsavel] = "TROCOU_RESPONSAVEL",
        [TipoDeEvento.Status] = "STATUS",
        [TipoDeEvento.Avaliado] = "AVALIADO",
    };

    /// <summary>ABERTO, EM_ATENDIMENTO, AGUARDANDO_CLIENTE, RESOLVIDO ou FECHADO.</summary>
    public static string Texto(this StatusDoChamado valor) => Status[valor];

    /// <summary>ACESSO_E_SENHA, COBRANCA, MODULO_COM_PROBLEMA, DUVIDA_DE_USO, SUGESTAO_AO_DEBOAZ ou SENHA_CONTA_MASTER.</summary>
    public static string Texto(this Categoria valor) => Categorias[valor];

    /// <summary>CONFIRMADO, PELA_CONTA ou PRAZO.</summary>
    public static string Texto(this Fechamento valor) => Fechamentos[valor];

    /// <summary>CONTA, TIME ou SISTEMA.</summary>
    public static string Texto(this TipoDeAutor valor) => Autores[valor];

    /// <summary>MENSAGEM, NOTA_INTERNA ou EVENTO.</summary>
    public static string Texto(this TipoDeItem valor) => Itens[valor];

    /// <summary>HUB ou ENTRAR_SEM_LOGIN.</summary>
    public static string Texto(this TipoDeOrigem valor) => Origens[valor];

    /// <summary>ABERTO, ASSUMIU, TROCOU_RESPONSAVEL, STATUS ou AVALIADO.</summary>
    public static string Texto(this TipoDeEvento valor) => Eventos[valor];

    /// <summary>O status pelo texto; null se não for um status.</summary>
    public static StatusDoChamado? StatusDe(string? texto) => De(Status, texto);

    /// <summary>A categoria pelo texto; null se não for uma categoria.</summary>
    public static Categoria? CategoriaDe(string? texto) => De(Categorias, texto);

    /// <summary>O fechamento pelo texto; null se não for um.</summary>
    public static Fechamento? FechamentoDe(string? texto) => De(Fechamentos, texto);

    /// <summary>O tipo de autor pelo texto; null se não for um.</summary>
    public static TipoDeAutor? AutorDe(string? texto) => De(Autores, texto);

    /// <summary>O tipo de item pelo texto; null se não for um.</summary>
    public static TipoDeItem? ItemDe(string? texto) => De(Itens, texto);

    /// <summary>A origem pelo texto; null se não for uma.</summary>
    public static TipoDeOrigem? OrigemDe(string? texto) => De(Origens, texto);

    /// <summary>O evento pelo texto; null se não for um.</summary>
    public static TipoDeEvento? EventoDe(string? texto) => De(Eventos, texto);

    static T? De<T>(Dictionary<T, string> textos, string? texto) where T : struct, Enum =>
        textos.Where(p => p.Value == texto).Select(p => (T?)p.Key).FirstOrDefault();
}
