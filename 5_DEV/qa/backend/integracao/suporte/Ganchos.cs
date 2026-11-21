using Reqnroll;

namespace Boaz.Qa.Integracao.Suporte;

[Binding]
public sealed class Ganchos(Api api)
{
    // cada cenário começa com o banco e o cache vazios e com a memória da instância do backend também: as medições que esperavam
    // para gravar, a última verificação da saúde e as sondas trocadas de um cenário não passam para o seguinte (20 RN-315, RNF-247)
    [BeforeScenario]
    public async Task Limpar()
    {
        await Estado.Limpar();
        var r = await api.Enviar("POST", "/_teste/auditoria/reiniciar");
        Mundo.Exigir(r, 204, "POST /_teste/auditoria/reiniciar (memória da Auditoria entre cenários, Teste__Relogio=sim)");
    }
}
