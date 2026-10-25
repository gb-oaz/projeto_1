// Ponto de composição do backend (RN-149): aqui, e só aqui, as portas do
// core/ são ligadas aos adaptadores de infra/in/ e infra/out/.
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Prontidão: o CI local, a homologação e a produção esperam 200 em /health
app.MapGet("/health", () => Results.Ok());

app.Run();
