# POO e SOLID

## POO

- **Encapsulamento**: estado privado, mudança só por método com nome do domínio (`sala.Encerrar()`, não `sala.Status = 3`). Invariantes checadas no construtor/fábrica — objeto inválido não nasce.
- **Abstração**: interfaces descrevem capacidades do ponto de vista de quem usa (`IRepositorioSalas`, `IRelogio`), não da tecnologia (`IMongoSalas`).
- **Composição > herança**: herde só quando "é um" for verdade no domínio e o contrato do pai for respeitado.
- **Polimorfismo** troca `switch` por tipo/estado repetido em vários lugares.

## SOLID

| Princípio | No projeto | Sinal de violação |
|---|---|---|
| **S** — responsabilidade única | um caso de uso por classe; DTO só transporta e valida | classe "Service"/"Manager" com 10 métodos sem relação |
| **O** — aberto/fechado | nova regra = nova estratégia/handler, sem editar os existentes | `if (tipo == ...)` crescendo a cada requisito |
| **L** — substituição de Liskov | adaptador falso de teste e real se comportam igual para a porta | implementação lança `NotImplementedException` |
| **I** — segregação de interfaces | portas pequenas por necessidade do caso de uso | caso de uso recebe repositório com 15 métodos e usa 1 |
| **D** — inversão de dependência | caso de uso depende de `ports/out/`, ligação no `Program.cs`/`main.ts` | `new MongoClient()` dentro de `usecases/` |

## Exemplo (C#)

```csharp
// core/hub/salas/ports/out/IRepositorioSalas.cs
public interface IRepositorioSalas
{
    Task<Sala?> ObterAsync(SalaId id, CancellationToken ct);
    Task SalvarAsync(Sala sala, CancellationToken ct);
}

// core/hub/salas/usecases/EncerrarSala.cs
public sealed class EncerrarSala(IRepositorioSalas salas, IRelogio relogio) : IEncerrarSala
{
    public async Task<Resultado> ExecutarAsync(EncerrarSalaDto dto, CancellationToken ct)
    {
        var sala = await salas.ObterAsync(new SalaId(dto.SalaId), ct);
        if (sala is null) return Resultado.NaoEncontrado("SALA_NAO_ENCONTRADA");
        sala.Encerrar(relogio.Agora());          // regra no domínio
        await salas.SalvarAsync(sala, ct);
        return Resultado.Ok();
    }
}
```

Os nomes de erro e o tipo de retorno reais estão no código e no contrato do `deboaz-arc`; o exemplo mostra só a forma.

## Exemplo (TypeScript, web)

```ts
// core/hub/salas/ports/out/repositorio-salas.ts
export interface RepositorioSalas { obter(id: string): Promise<Sala | null> }

// core/hub/salas/usecases/encerrar-sala.ts — sem Vue, sem fetch
export function criarEncerrarSala(repo: RepositorioSalas) {
  return async (id: string) => { /* regra */ }
}
```
