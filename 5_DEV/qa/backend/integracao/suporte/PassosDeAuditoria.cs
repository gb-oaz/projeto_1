using Reqnroll;

namespace Boaz.Qa.Integracao.Suporte;

// Passos de auditoria comuns às features: as frases que valem igual para os
// Cenários de API de 17_modulo-usuario.md (RN-178, RN-270, RN-271, RN-274) e
// de 17_modulo-usuario-a-autenticacao.md (RN-272, RN-273). Os pedidos
// auditados, que mudam de feature para feature, ficam nos AuditoriaPassos de
// cada pasta e passam por Execucoes (suporte/Auditoria.cs).
[Binding]
public sealed class PassosDeAuditoria(Mundo mundo, Execucoes execucoes)
{
    // ── respostas ──────────────────────────────────────────────────────────
    [When(@"^a resposta é de sucesso$")]
    public void Sucesso() =>
        Assert.True(mundo.Ultima.Status is >= 200 and < 300, $"esperava sucesso, veio {mundo.Ultima}");

    [When(@"^a resposta é (\d{3}),? com o cabeçalho X-Trace-Id$")]
    [Then(@"^a resposta é (\d{3}),? com o cabeçalho X-Trace-Id$")]
    public void ComTraceId(int status)
    {
        Respostas.Confere(mundo.Ultima, status);
        Auditoria.TraceIdDoCabecalho(mundo.Ultima);
    }

    [When(@"^a resposta é (\d{3}) ([A-Z_]+),? com o cabeçalho X-Trace-Id$")]
    [Then(@"^a resposta é (\d{3}) ([A-Z_]+),? com o cabeçalho X-Trace-Id$")]
    public void RecusaComTraceId(int status, string codigo)
    {
        Respostas.Confere(mundo.Ultima, status, codigo);
        Auditoria.TraceIdDoCabecalho(mundo.Ultima);
    }

    // a leitura sem o módulo (RN-270): recusada sem passar pelo caso de uso
    [Then(@"^a resposta é (\d{3}) ([A-Z_]+), sem o cabeçalho X-Trace-Id$")]
    public void RecusaSemTraceId(int status, string codigo)
    {
        Respostas.Confere(mundo.Ultima, status, codigo);
        SemTraceId();
    }

    [Then(@"^a resposta não traz o cabeçalho X-Trace-Id$")]
    public void SemTraceId() =>
        Assert.True(string.IsNullOrEmpty(mundo.Ultima.Cabecalho("X-Trace-Id")),
            $"a resposta não devia trazer X-Trace-Id, veio \"{mundo.Ultima.Cabecalho("X-Trace-Id")}\": {mundo.Ultima}");

    // ── auditoria ──────────────────────────────────────────────────────────
    // a execução do começo ao fim: INICIADO e CONCLUIDO, nessa ordem, mesma ação e traceId
    [Then(@"^""auditoria"" ganha ""INICIADO"" e ""CONCLUIDO"" da ação ""(\w+)""(?:, com o mesmo traceId)?$")]
    public async Task IniciadoEConcluido(string acao)
    {
        // só os da ação: o pedido e os casos de uso internos que ele chama (RN-275) gravam juntos
        var novos = (await execucoes.Novos(Auditoria.Registros)).Where(d => Auditoria.Texto(d, "acao") == acao).ToList();
        Assert.True(novos.Select(d => Auditoria.Texto(d, "etapa")).SequenceEqual(["INICIADO", "CONCLUIDO"]),
            $"esperava INICIADO e CONCLUIDO de {acao} em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        Assert.All(novos, d =>
        {
            Assert.Equal(acao, Auditoria.Texto(d, "acao"));
            execucoes.NaHoraDoPedido(d);
            Auditoria.GuardadoCincoAnos(d);
        });
        Auditoria.MesmoTraceId(novos);
        execucoes.Registro = novos[0];
    }

    // a leitura não auditada (RN-271 do 17) e o pedido recusado antes do caso de uso (RN-273)
    [Then(@"^as coleções ""auditoria"" e ""auditoriaErros"" não ganham registro$")]
    [Then(@"^""auditoria"" e ""auditoriaErros"" não ganham registro, como o 422 da RN-271 do 17$")]
    [Then(@"^as coleções ""auditoria"" e ""auditoriaErros"" não ganham registro, como a leitura da RN-178$")]
    public async Task NadaGravado()
    {
        var registros = await execucoes.Novos(Auditoria.Registros);
        var erros = await execucoes.Novos(Auditoria.Erros);
        Assert.True(registros.Count == 0 && erros.Count == 0,
            $"o pedido gravou {Auditoria.Resumo(registros)} em \"auditoria\" e {Auditoria.Resumo(erros)} em \"auditoriaErros\"");
    }

    // a execução recusada: só o INICIADO, sem CONCLUIDO
    [Then(@"^""auditoria"" fica só com o ""INICIADO"" da ação ""(\w+)""$")]
    public async Task SoOIniciado(string acao)
    {
        var novos = await execucoes.Novos(Auditoria.Registros);
        Assert.True(novos.Count == 1 && Auditoria.Texto(novos[0], "etapa") == "INICIADO" && Auditoria.Texto(novos[0], "acao") == acao,
            $"esperava só o INICIADO de {acao} em \"auditoria\", vieram {Auditoria.Resumo(novos)}");
        execucoes.NaHoraDoPedido(novos[0]);
        execucoes.Registro = novos[0];
    }

    [Then(@"^""auditoria"" fica só com o ""INICIADO"" da ação ""(\w+)"", com esse traceId$")]
    public async Task SoOIniciadoDoCabecalho(string acao)
    {
        await SoOIniciado(acao);
        Assert.Equal(Auditoria.TraceIdDoCabecalho(mundo.Ultima), Auditoria.TraceId(execucoes.Registro!));
    }

    [Then(@"^""auditoriaErros"" ganha o código ""(\w+)"" e o nivel ""(\w+)""$")]
    public async Task ErroComCodigoENivel(string codigo, string nivel)
    {
        var erro = execucoes.Erro = await execucoes.UmNovoErro();
        Assert.Equal(codigo, Auditoria.Texto(erro, "codigo"));
        Assert.Equal(nivel, Auditoria.Texto(erro, "nivel"));
        execucoes.NaHoraDoPedido(erro);
    }

    // o mesmo traceId do INICIADO da frase anterior
    [Then(@"^""auditoriaErros"" ganha o código ""(\w+)"" e o nivel ""(\w+)"", com o mesmo traceId$")]
    public async Task ErroComOMesmoTraceId(string codigo, string nivel)
    {
        Assert.NotNull(execucoes.Registro);
        await ErroComCodigoENivel(codigo, nivel);
        Assert.Equal(Auditoria.TraceId(execucoes.Registro), Auditoria.TraceId(execucoes.Erro!));
    }

    // o código da resposta e o traceId do cabeçalho X-Trace-Id
    [Then(@"^""auditoriaErros"" ganha o código da resposta e o nivel ""(\w+)"", com esse traceId$")]
    public async Task ErroDaResposta(string nivel)
    {
        var codigo = mundo.Ultima.Corpo?["codigo"]?.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(codigo), $"a resposta veio sem código: {mundo.Ultima}");
        await ErroComCodigoENivel(codigo!, nivel);
        Assert.Equal(Auditoria.TraceIdDoCabecalho(mundo.Ultima), Auditoria.TraceId(execucoes.Erro!));
    }
}
