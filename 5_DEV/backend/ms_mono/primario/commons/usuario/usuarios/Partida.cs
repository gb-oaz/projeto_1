using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.MsMono.Infra.Out.Commons;
using Boaz.MsMono.Infra.Out.Commons.Usuario.Autorizacao;
using Microsoft.Extensions.Options;

namespace Boaz.MsMono.Primario.Commons.Usuario.Usuarios;

// O adaptador primário da partida (diagrama usuario-inicializacao-sequencia):
// roda no Program.cs antes de app.Run, ou seja, antes de /health e do primeiro
// pedido. Lê AMBIENTE e as variáveis USUARIO_* que o Container Apps entregou do
// Key Vault (RNF-225) — em hml também as USUARIO_QA_*; fora de hml elas são
// ignoradas (RN-266) — e chama IInicializarContas. Antes disso grava o
// registro de módulos e cria os índices do banco.
//
// Segredo ausente ou senha fora da RNF-202: o log diz só o nome da variável e
// a partida devolve false, para o processo terminar com código ≠ 0 (RNF-226).
// Nenhum valor vai para o log (RNF-225).
public sealed class Partida(
    IConfiguration configuracao, IInicializarContas inicializar, RegistroDeModulos modulos, Indices indices,
    IOptions<OpcoesDoTime> time, ILogger<Partida> log)
{
    public async Task<bool> Executar(CancellationToken ct = default)
    {
        var ambiente = configuracao[ContasIniciais.VariavelDoAmbiente];
        var variaveis = ContasIniciais.VariaveisDo(ambiente).ToDictionary(v => v, v => configuracao[v]);
        var dadosDoTime = time.Value;
        var pedido = new PartidaDto(ambiente, variaveis,
            new DadosDoTimeDto(dadosDoTime.Nome, dadosDoTime.Documento, dadosDoTime.Responsavel, dadosDoTime.Telefone));
        try
        {
            // confere os segredos antes de tocar no banco
            ContasIniciais.Conferir(ambiente, pedido.Ler);
            await modulos.Gravar();
            await indices.Criar(ct);
            var garantidas = await inicializar.Executar(pedido, ct);
            log.LogInformation("Contas iniciais garantidas em {Ambiente}: {Contas} contas e {Usuarios} usuários, {Criados} criados agora",
                ContasIniciais.EmHomologacao(ambiente) ? "homologação" : "produção",
                garantidas.Contas.Count, garantidas.Usuarios.Count, garantidas.Criados.Count);
            return true;
        }
        catch (FalhaNaInicializacao falha)
        {
            log.LogCritical("A aplicação não inicia: {Motivo}", falha.Message);
            return false;
        }
    }
}
