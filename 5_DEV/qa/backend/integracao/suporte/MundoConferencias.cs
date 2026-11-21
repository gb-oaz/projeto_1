using System.Text.Json.Nodes;

namespace Boaz.Qa.Integracao.Suporte;

// Conferência extra de "a resposta é 200 com:" (Respostas.cs): quando o corpo do
// exemplo tem listas que o ambiente não reproduz por inteiro, o Quando tira esses
// campos de CamposSemConferir e diz aqui o que conferir deles (20 Auditoria).
public sealed partial class Mundo
{
    public Func<JsonNode?, Task>? ConferirDoExemplo { get; set; }
}
