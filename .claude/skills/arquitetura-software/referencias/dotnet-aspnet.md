# C#, .NET e ASP.NET Core no boaz

Versões: `1_Requisitos/01_stack-desenvolvimento.md` (RNF-01, versão LTS fixada). Estrutura: `14_estrutura-projetos.md`.

## C# idiomático

- `sealed` por padrão; `record`/`readonly record struct` para objetos de valor e DTOs; construtor primário para injeção.
- `nullable` ligado e avisos como erro (RNF-22): trate `null` na borda, não espalhe `!`.
- `async`/`await` até a borda, com `CancellationToken` em toda porta de I/O; nunca `.Result`/`.Wait()`.
- Exceção para o inesperado; resultado explícito (`Resultado`, código de erro) para recusa de negócio prevista no Gherkin.
- `dotnet format` sem diferença (RNF-15).

## Minimal APIs (padrão do projeto)

```csharp
// infra/in/hub/salas/SalasEndpoints.cs — adaptador primário
public static class SalasEndpoints
{
    public static RouteGroupBuilder MapSalas(this RouteGroupBuilder g)
    {
        g.MapPost("/", async (CriarSalaDto dto, ICriarSala caso, CancellationToken ct) =>
            (await caso.ExecutarAsync(dto, ct)).ParaHttp());   // mapeia resultado → status do contrato
        return g;
    }
}
// Program.cs — único ponto de composição
builder.Services.AddScoped<ICriarSala, CriarSala>();
builder.Services.AddScoped<IRepositorioSalas, RepositorioSalasMongo>();
app.MapGroup("/hub/salas").RequireAuthorization().MapSalas();
```

- Endpoint fino: ligação, autorização, chamada do caso de uso e mapeamento para HTTP. Nenhuma regra de negócio.
- Agrupe por feature com `MapGroup`; filtros de endpoint para preocupações transversais.
- **Controllers** (`ControllerBase`) só com ADR — o projeto não os usa hoje.

## DTOs

- Em `core/<contexto>/<feature>/dtos/`: entram e saem dos casos de uso, com a validação de campos (RNF-175).
- Nome dos campos = nome do JSON do contrato (glossário); nunca exponha entidade de domínio nem documento do banco.
- Erro de validação segue o formato e o código definidos no contrato/requisito do projeto (ex.: 422 com o campo), não um formato padrão do framework.

## Pipeline HTTP

- Ordem importa: tratamento de erro → traceId/log → autenticação → autorização → endpoint.
- Corpo com content-type errado, JSON malformado e campo inválido têm respostas definidas em requisito (ex.: `RN-271`): consulte antes de mudar middleware.

## Injeção de dependência

- `Scoped` para casos de uso e repositórios; `Singleton` para clientes thread-safe (`IMongoClient`, `IConnectionMultiplexer`); `Transient` raro.
- Dependência de tempo por porta (`IRelogio`) para testar.

## Testes do backend

xUnit (RNF-14), `Microsoft.AspNetCore.Mvc.Testing` para integração entre poucos componentes, banco real nos de integração (RNF-37), cobertura ≥ 80 % (RNF-25).
