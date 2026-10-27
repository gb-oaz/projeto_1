# backend

Um único serviço, `ms_mono`, com a estrutura hexagonal de
[`14_estrutura-projetos`](../../1_Requisitos/14_estrutura-projetos.html)
(RN-143 a RN-149, RNF-175 a RNF-178).

```
5_DEV/backend/
├── Boaz.slnx
├── core/                         domínio, casos de uso e portas, sem I/O
│   ├── commons/                  Boaz.Core.Commons (o que os contextos compartilham)
│   ├── landing_page/             Boaz.Core.LandingPage ─┐
│   ├── hub/                      Boaz.Core.Hub          ├─ importam só o Commons
│   └── quest/                    Boaz.Core.Quest       ─┘
│       └── <feature>/            domain/ · usecases/ · ports/in/ · ports/out/
├── ms_mono/                      Boaz.MsMono: ASP.NET Core, imagem backend-mono
│   ├── Program.cs                ponto de composição (liga portas e adaptadores)
│   ├── sistema/                  endpoints do runtime: /health e a documentação em /doc/api
│   ├── wwwroot/doc/api/          swagger.css e design/ (cópia gerada do design system, não edite)
│   └── infra/
│       ├── in/<contexto>/<feature>/    endpoints HTTP (chamam ports/in)
│       └── out/<contexto>/<feature>/   MongoDB e Redis (implementam ports/out)
├── tests/                        espelha core/ e ms_mono/, um xUnit por projeto
└── Dockerfile                    imagem backend-mono, porta 8080, /health
```

As features entram com o módulo que as cria, em `snake_case`, com o mesmo nome
nas três frentes.

```bash
dotnet build              # na pasta 5_DEV/backend
dotnet test
dotnet run --project ms_mono   # http://localhost:8080/health
```

A documentação da API (Swagger UI com a identidade deboaz) fica em
`http://localhost:8080/doc/api`: abre na capa do catálogo, e o seletor do
topo, à direita, abre cada API (`ms_mono · v1`); a raiz `/` leva a ela (302).
Ela existe quando `Swagger:Habilitado` é `true`: no `dotnet run`
(appsettings.Development.json), no CI local e em homologação. Em produção fica
desligada (a raiz responde 404) e o WAF bloqueia `/doc/api`. O banner (animado
e parado), o logo, o favicon e as cores de `wwwroot/doc/api/design/` vêm de
`pnpm run gerar` em `0_Assets/design/`.
