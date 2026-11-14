using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Hub.Sac.Anexos.UseCases;
using Boaz.Core.Hub.Sac.Card.UseCases;
using Boaz.Core.Hub.Sac.Chamados.Dtos;
using Boaz.Core.Hub.Sac.Chamados.UseCases;
using Boaz.Core.Hub.Sac.Configuracao.UseCases;

namespace Boaz.Core.Hub.Tests.Sac.Mocks;

/// <summary>
/// O mundo dos Cenários de API do 18: a conta master da EBFGA, o autônomo
/// Pedro Reis, o Caio e a Ana Lima do time, a staff Maria; os casos de uso do
/// SAC ligados às portas em memória e ao relógio do teste.
/// </summary>
public sealed class Cenario
{
    /// <summary>A conta master da EBFGA.</summary>
    public static readonly QuemPede Master = new("usr-ebfga-master", "MASTER", "conta-ebfga");

    /// <summary>O autônomo Pedro Reis.</summary>
    public static readonly QuemPede Pedro = new("usr-preis-master", "AUTONOMO", "conta-preis");

    /// <summary>O Caio, do time.</summary>
    public static readonly QuemPede Caio = new("usr-dboaz-caio", "MEMBRO_TIME", "conta-dboaz");

    /// <summary>A Ana Lima, do time.</summary>
    public static readonly QuemPede Ana = new("usr-dboaz-ana", "MEMBRO_TIME", "conta-dboaz");

    /// <summary>A staff Maria.</summary>
    public static readonly QuemPede Maria = new("usr-ebfga-maria", "STAFF", "conta-ebfga");

    /// <summary>Um traceId qualquer.</summary>
    public static TraceId Trace => TraceId.Novo();

    /// <summary>As portas.</summary>
    public SacEmMemoria Mock { get; } = new();

    /// <summary>O relógio.</summary>
    public Relogio Relogio { get; } = new();

    /// <summary>Os casos de uso.</summary>
    public Cenario()
    {
        var m = Mock;
        Abrir = new(m, m, m, m, m, m, m, m, Relogio);
        Listar = new(m, Relogio);
        Ler = new(m, m, m, m, Relogio);
        Enviar = new(m, m, m, m, m, m, m, Relogio);
        Assumir = new(m, m, m, m, m, m, Relogio);
        Status = new(m, m, m, m, m, m, Relogio);
        Reabrir = new(m, m, m, m, m, m, m, Relogio);
        Encerrar = new(m, m, m, m, m, m, m, Relogio);
        Avaliar = new(m, m, m, m, m, m, m, Relogio);
        AoVivo = new(m, m, m, m);
        PedidoDeSenha = new(m, m, m, m, m);
        Prazos = new(m, m, m, m, m, m, m, Relogio);
        EnviarAnexo = new(m, m, m, Relogio);
        LerAnexo = new(m, m, Relogio);
        Descartar = new(m, m, Relogio);
        LerPrazos = new(m);
        SalvarPrazos = new(m, m, m, m, Relogio);
        Card = new(m, m, Relogio);
        CardAoVivo = new(Card);
        FecharDia = new(m, m, Relogio);
    }

    public AbrirChamado Abrir { get; }
    public ListarChamados Listar { get; }
    public LerChamado Ler { get; }
    public EnviarMensagem Enviar { get; }
    public AssumirChamado Assumir { get; }
    public MudarStatus Status { get; }
    public ReabrirChamado Reabrir { get; }
    public EncerrarChamado Encerrar { get; }
    public AvaliarChamado Avaliar { get; }
    public AcompanharChamado AoVivo { get; }
    public RegistrarPedidoDeSenha PedidoDeSenha { get; }
    public AplicarPrazos Prazos { get; }
    public EnviarAnexo EnviarAnexo { get; }
    public LerAnexo LerAnexo { get; }
    public DescartarAnexosVencidos Descartar { get; }
    public LerPrazos LerPrazos { get; }
    public SalvarPrazos SalvarPrazos { get; }
    public LerCardDoSac Card { get; }
    public AcompanharCardDoSac CardAoVivo { get; }
    public FecharCardDoSacDoDia FecharDia { get; }

    /// <summary>O corpo do exemplo do RF-171.</summary>
    public static PedidoAbrirChamado Corpo(string assunto = "Presença não salva a chamada", string categoria = "MODULO_COM_PROBLEMA",
        params string[] anexos) =>
        new(categoria, assunto, "Desde ontem, ao tocar em Salvar na chamada da turma, aparece erro.", null, anexos);

    /// <summary>Abre um chamado da conta dada (a EBFGA, sem dizer).</summary>
    public async Task<string> Novo(QuemPede? dono = null, string assunto = "Presença não salva a chamada") =>
        (await Abrir.Executar(Corpo(assunto), dono ?? Master, Trace)).Id;

    /// <summary>Leva o chamado até o status, pelo Caio (ou o membro dado).</summary>
    public async Task<string> No(string status, QuemPede? dono = null, QuemPede? membro = null)
    {
        var id = await Novo(dono);
        membro ??= Caio;
        switch (status)
        {
            case "ABERTO":
                break;
            case "EM_ATENDIMENTO":
                await Assumir.Executar(id, new(true), membro, Trace);
                break;
            case "AGUARDANDO_CLIENTE":
            case "RESOLVIDO":
                await Assumir.Executar(id, new(true), membro, Trace);
                await Status.Executar(id, new(status, status == "RESOLVIDO" ? "Corrigido. Pode salvar de novo." : "Qual a turma?"), membro, Trace);
                break;
            case "FECHADO":
                await Assumir.Executar(id, new(true), membro, Trace);
                await Status.Executar(id, new("RESOLVIDO", "Corrigido."), membro, Trace);
                await Avaliar.Executar(id, new(5, "Obrigado"), dono ?? Master, Trace);
                break;
            case "ENCERRADO":
                await Encerrar.Executar(id, dono ?? Master, Trace);
                break;
        }
        return id;
    }

    /// <summary>Espera a recusa do SAC com o código dado.</summary>
    public static async Task<Boaz.Core.Hub.Sac.Chamados.Domain.FalhaDoSac> Recusa(string codigo, Func<Task> pedido)
    {
        var falha = await Assert.ThrowsAsync<Boaz.Core.Hub.Sac.Chamados.Domain.FalhaDoSac>(pedido);
        Assert.Equal(codigo, falha.Codigo);
        return falha;
    }
}
