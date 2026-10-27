using Boaz.MsMono.Sistema;

// Ponto de composição do backend (RN-149): aqui, e só aqui, as portas do
// core/ são ligadas aos adaptadores de infra/in/ e infra/out/.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDocumentacaoApi();

var app = builder.Build();

// Documentação da API em /doc/api, só com Swagger:Habilitado (homologação e
// máquina; nunca em produção, 05_cd-implantacao RNF-73)
if (DocumentacaoApi.Habilitada(app.Configuration))
{
    app.UseDocumentacaoApi();
}

app.MapSaude();

app.Run();
