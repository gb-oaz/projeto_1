using Boaz.Core.Hub.Auditoria.Metricas.Domain;
using Boaz.Core.Hub.Auditoria.Metricas.Ports.Out;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Domain;
using Boaz.Core.Hub.Auditoria.Ocorrencias.Ports.Out;
using Boaz.Core.Hub.Auditoria.Saude.Domain;
using Boaz.Core.Hub.Auditoria.Saude.Ports.In;
using Boaz.Core.Hub.Auditoria.Saude.Ports.Out;

namespace Boaz.Core.Hub.Auditoria.Saude.UseCases;

/// <summary>
/// A verificação a cada 30 s (RNF-246, RN-315, RN-316, RN-324): cada serviço pela sonda dele, com até 3 s para responder, e cada
/// módulo pelos pedidos dos últimos 5 min. Cada instância se verifica por conta própria e guarda o resultado na memória; só a
/// que pega a trava guarda o histórico, as quedas e as ocorrências no banco, para a mudança de estado ter uma só ocorrência.
/// </summary>
public sealed class VerificarServicos(
    IEnumerable<ISondaDeServico> sondas, IUltimaVerificacao ultima, IHistoricoDaSaude historico, IInstancias instancias, ITravaDaAuditoria trava,
    IMedicoes medicoes, IOcorrencias ocorrencias, IOcorrenciasAbertas abertas, IAvisosDeOcorrencias avisos, IdentidadeDaInstancia eu, TimeProvider relogio)
    : IVerificarServicos
{
    /// <summary>Até quanto cada serviço tem para responder.</summary>
    public static readonly TimeSpan Limite = TimeSpan.FromSeconds(3);

    /// <summary>A trava da verificação, um pouco menos que o intervalo de 30 s.</summary>
    public static readonly TimeSpan PrazoDaTrava = TimeSpan.FromSeconds(25);

    /// <summary>O nome da trava da verificação.</summary>
    public const string Tarefa = "verificacao";

    readonly SemaphoreSlim umaPorVez = new(1, 1);

    /// <inheritdoc />
    public async Task<SaudeDoSistema> Executar(bool forcar = false, CancellationToken ct = default)
    {
        // duas verificações juntas não se misturam: a memória e as ocorrências andam em ordem
        await umaPorVez.WaitAsync(ct);
        try
        {
            return await Verificar(forcar, ct);
        }
        finally
        {
            umaPorVez.Release();
        }
    }

    async Task<SaudeDoSistema> Verificar(bool forcar, CancellationToken ct)
    {
        var resultados = await Task.WhenAll(Servico.Todos.Select(s => Sondar(s, ct)));
        var agora = relogio.GetUtcNow();
        var emAberto = await LerAbertas(ct);

        var servicos = Servico.Todos.Zip(resultados, (servico, sonda) =>
        {
            var anterior = Coerente(ultima.De(servico), emAberto, servico.Id, v => v.Estado.Ocorre());
            return Verificacao.Seguinte(anterior, servico, eu.Id, sonda, agora);
        }).ToList();
        var modulos = await Modulos(agora, emAberto, ct);

        ultima.Guardar(agora, servicos, modulos);
        await Anunciar(agora, ct);
        if (forcar || await TentarATrava(ct)) await Guardar(servicos, modulos, emAberto, agora, ct);
        return new SaudeDoSistema(eu.Ambiente, agora, servicos, modulos, await instancias.Listar(ct).ContinuaSeFalhar(eu, agora));
    }

    async Task<ResultadoDaSonda> Sondar(Servico servico, CancellationToken ct)
    {
        var sonda = sondas.FirstOrDefault(s => s.Servico == servico);
        if (sonda is null) return ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta);
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(Limite);
        try
        {
            var tarefa = sonda.Verificar(limite.Token);
            var fim = await Task.WhenAny(tarefa, Task.Delay(Limite, ct));
            if (fim != tarefa)
            {
                await limite.CancelAsync();
                _ = tarefa.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                return ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta);
            }
            return await tarefa;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ResultadoDaSonda.Falhou(FalhaDaVerificacao.SemResposta);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return ResultadoDaSonda.Falhou(FalhaDaVerificacao.RespostaComErro);
        }
    }

    // os pedidos dos últimos 5 min de cada módulo; sem o banco, os módulos ficam como na verificação anterior
    async Task<IReadOnlyList<SaudeDoModulo>> Modulos(DateTimeOffset agora, IReadOnlyDictionary<string, Ocorrencia>? emAberto, CancellationToken ct)
    {
        IReadOnlyDictionary<string, ResumoDeMedicoes> pedidos;
        try
        {
            var de = Janelas.Minuto(agora).AddMinutes(-SaudeDoModulo.Minutos);
            pedidos = await medicoes.PorModulo(de, agora, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return ultima.Modulos;
        }
        return [.. ModuloDoEndereco.Modulos.Keys.Select(modulo =>
        {
            var anterior = Coerente(ultima.DoModulo(modulo), emAberto, modulo, m => m.Estado.Ocorre());
            return SaudeDoModulo.Seguinte(anterior, modulo, pedidos.GetValueOrDefault(modulo, ResumoDeMedicoes.Vazio));
        })];
    }

    // o estado de antes só vale se o Redis ainda o tem aberto: com o cache limpo, o alvo parte de No ar
    static T? Coerente<T, TEstado>(T? anterior, IReadOnlyDictionary<string, Ocorrencia>? emAberto, string alvo, Func<T, TEstado> estado)
        where T : class where TEstado : struct, Enum
    {
        if (anterior is null || emAberto is null) return anterior;
        return !emAberto.ContainsKey(alvo) && !estado(anterior).Equals(default(TEstado)) ? null : anterior;
    }

    async Task<IReadOnlyDictionary<string, Ocorrencia>?> LerAbertas(CancellationToken ct)
    {
        try
        {
            return (await abertas.Abertas(ct)).ToDictionary(o => o.Alvo);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    async Task Anunciar(DateTimeOffset agora, CancellationToken ct)
    {
        try
        {
            await instancias.Anunciar(new Instancia(eu.Id, eu.Versao, eu.Inicio, agora), ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // o sinal desta instância volta na próxima verificação
        }
    }

    async Task<bool> TentarATrava(CancellationToken ct)
    {
        try
        {
            return await trava.Tentar(Tarefa, PrazoDaTrava, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    // o histórico, as quedas e as ocorrências: cada uma pode falhar sem derrubar as outras (o banco fora é o caso)
    async Task Guardar(
        IReadOnlyList<Verificacao> servicos, IReadOnlyList<SaudeDoModulo> modulos, IReadOnlyDictionary<string, Ocorrencia>? emAberto, DateTimeOffset agora,
        CancellationToken ct)
    {
        await Tentar(() => historico.Gravar(servicos, ct), ct);
        // sem o cache não se sabe o que já está aberto: nada de quedas nem ocorrências repetidas
        if (emAberto is null) return;
        foreach (var v in servicos)
        {
            var antes = emAberto?.GetValueOrDefault(v.Servico.Id)?.Para ?? EstadoDeOcorrencia.NoAr;
            var agoraEstado = v.Estado.Ocorre();
            if (agoraEstado == EstadoDeOcorrencia.ForaDoAr && antes != EstadoDeOcorrencia.ForaDoAr)
            {
                await Tentar(() => historico.AbrirQueda(new Queda(Id(), v.Servico, v.Desde, null, v.Falha ?? FalhaDaVerificacao.SemResposta), ct), ct);
            }
            else if (agoraEstado != EstadoDeOcorrencia.ForaDoAr && antes == EstadoDeOcorrencia.ForaDoAr)
            {
                await Tentar(() => historico.FecharQueda(v.Servico, agora, ct), ct);
            }
            await Mudanca(TipoDoAlvo.Servico, v.Servico.Id, v.Servico.Nome, emAberto, agoraEstado, v.Falha, agora, ct);
        }
        foreach (var m in modulos) await Mudanca(TipoDoAlvo.Modulo, m.Modulo, m.Nome, emAberto, m.Estado.Ocorre(), null, agora, ct);
    }

    async Task Mudanca(
        TipoDoAlvo tipo, string alvo, string nome, IReadOnlyDictionary<string, Ocorrencia>? emAberto, EstadoDeOcorrencia atual, FalhaDaVerificacao? falha,
        DateTimeOffset agora, CancellationToken ct)
    {
        var ocorrencia = Ocorrencia.Detectar(emAberto?.GetValueOrDefault(alvo), tipo, alvo, nome, atual, falha, agora, Id());
        if (ocorrencia is null) return;
        await Tentar(() => abertas.Mudar(ocorrencia, ct), ct);
        await Tentar(() => ocorrencias.Gravar(ocorrencia, ct), ct);
        await Tentar(() => avisos.Publicar(ocorrencia, ct), ct);
    }

    static string Id() => Guid.NewGuid().ToString("N");

    static async Task Tentar(Func<Task> acao, CancellationToken ct)
    {
        try
        {
            await acao();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // a falha de uma parte (banco, cache) não derruba a verificação nem as outras partes
        }
    }
}

static class TarefasDaSaude
{
    // a lista de instâncias, ou só esta, se o cache não responde
    public static async Task<IReadOnlyList<Instancia>> ContinuaSeFalhar(this Task<IReadOnlyList<Instancia>> lida, IdentidadeDaInstancia eu, DateTimeOffset agora)
    {
        IReadOnlyList<Instancia> outras;
        try
        {
            outras = await lida;
        }
        catch (Exception)
        {
            outras = [];
        }
        return Juntar(outras, eu, agora);
    }

    // esta instância sempre consta, com o sinal de agora
    public static IReadOnlyList<Instancia> Juntar(IReadOnlyList<Instancia> outras, IdentidadeDaInstancia eu, DateTimeOffset agora) =>
        [.. outras.Where(i => i.Id != eu.Id).Append(new Instancia(eu.Id, eu.Versao, eu.Inicio, agora)).OrderBy(i => i.Inicio).ThenBy(i => i.Id, StringComparer.Ordinal)];
}
