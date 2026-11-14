using Boaz.Core.Commons.Auditoria.Domain;
using Boaz.Core.Commons.Auditoria.Ports.Out;
using Boaz.Core.Commons.Auditoria.UseCases;
using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /usuarios/gerar-senha (RF-109, RN-171, RN-182): a entrada e o código de
/// liberação viram a senha, mostrada uma vez. Erro conta tentativa: a quinta
/// seguida bloqueia, como na entrada do hub. Sem login, auditado com o traceId
/// recebido: "INICIADO" sem quem, sem conta e sem alvo; "CONCLUIDO" com a conta
/// e o alvo, quem teve a senha gerada; na recusa ou na falha, o registro de erro
/// antes de a exceção sair (RN-178, RN-270, RN-271). Os cards do Usuários são
/// recontados na mesma execução (RN-280).
/// </summary>
public sealed class GerarSenha(
    IUsuarios usuarios, IContas contas, IGeracoesDeSenha geracoes, ITentativasDeLiberacao tentativas, IAuditoria auditoria,
    IAuditoriaDeErros erros, TimeProvider relogio, IAtualizarCardUsuarios cardsDoModulo) : IGerarSenha
{
    readonly Apoio apoio = new(usuarios, contas, relogio);
    readonly AuditoriaDaExecucao auditada = new(auditoria, erros, relogio);

    /// <inheritdoc />
    public Task<SenhaGeradaDto> Executar(GeracaoDeSenhaDto pedido, TraceId traceId, CancellationToken ct = default)
    {
        var execucao = new Execucao(traceId, AcaoAuditada.GerarSenha, null, null, null);
        return auditada.Auditar(execucao, () => Gerar(pedido, execucao, ct), ct);
    }

    /// <summary>A regra do gerar-senha, da entrada ao "CONCLUIDO".</summary>
    async Task<SenhaGeradaDto> Gerar(GeracaoDeSenhaDto pedido, Execucao execucao, CancellationToken ct)
    {
        var (entrada, codigo) = pedido.Validar();
        var (usuario, geracao) = await GeracaoConferida(entrada, codigo, ct);

        var senha = Senha.Gerar(await NomeDaSenha(usuario, ct));
        var comprovante = CodigoDeLiberacao.Comprovante();
        await Gravar(usuario, geracao, senha, comprovante, ct);
        // o card da conta é recontado na mesma execução e a versao sobe (RN-280)
        await cardsDoModulo.Executar(new MudancaNoCadastroDto(usuario.ContaId, usuario.Id), ct);
        await tentativas.Apagar(entrada, ct);
        await RegistrarConclusao(execucao, usuario, ct);
        return new(senha, comprovante);
    }

    /// <summary>O nome de onde a senha sai: o da conta master é o de quem cadastrou a conta (RN-171).</summary>
    async Task<string> NomeDaSenha(UsuarioCadastrado usuario, CancellationToken ct) =>
        usuario.Tipo == TipoUsuario.Master ? (await apoio.ContaDe(usuario, ct)).Responsavel : usuario.Nome;

    /// <summary>Grava o resumo da senha no usuário e a geração como mostrada, com o resumo do comprovante.</summary>
    async Task Gravar(UsuarioCadastrado usuario, GeracaoDeSenhaPedida geracao, string senha, string comprovante, CancellationToken ct)
    {
        usuario.DefinirSenha(ResumoDeSenha.Gerar(senha));
        geracao.MostrarSenha(Credenciais.Resumo(comprovante), apoio.Agora);
        await usuarios.Atualizar(usuario, ct);
        await geracoes.Atualizar(geracao, ct);
    }

    /// <summary>
    /// O "CONCLUIDO" antes de responder, com a conta e o alvo de quem teve a senha
    /// gerada, sem quem; o "INICIADO" foi sem nenhum dos três (RN-178).
    /// </summary>
    Task RegistrarConclusao(Execucao execucao, UsuarioCadastrado usuario, CancellationToken ct) =>
        auditada.Concluir(execucao with { ContaId = usuario.ContaId, Alvo = usuario.Id }, ct);

    /// <summary>
    /// O usuário e a geração aberta que o código confere; o erro conta tentativa e
    /// a quinta seguida bloqueia (RN-182, RNF-204).
    /// </summary>
    /// <exception cref="FalhaDeUsuarios">ENTRADA_BLOQUEADA, CODIGO_INVALIDO ou CODIGO_VENCIDO.</exception>
    async Task<(UsuarioCadastrado Usuario, GeracaoDeSenhaPedida Geracao)> GeracaoConferida(string entrada, string codigo, CancellationToken ct)
    {
        var agora = apoio.Agora;
        var erradas = await tentativas.Buscar(entrada, ct) ?? new TentativasDeEntrada(AlvoTentativa.CodigoLiberacao, entrada);
        if (erradas.EstaBloqueada(agora)) throw FalhaDeUsuarios.EntradaBloqueada(erradas.BloqueadoAte!.Value);

        var usuario = await usuarios.BuscarPorEntrada(entrada, ct);
        var geracao = usuario is { TemSenha: true, Estado: not EstadoUsuario.Desativado }
            ? await geracoes.UltimaDoUsuario(usuario.Id, ct) : null;
        if (usuario is null || geracao is not { Aberta: true } || !geracao.Confere(codigo))
        {
            var bloqueou = erradas.RegistrarErro(agora);
            await tentativas.Gravar(erradas, ct);
            throw bloqueou ? FalhaDeUsuarios.EntradaBloqueada(erradas.BloqueadoAte!.Value) : FalhaDeUsuarios.CodigoInvalido();
        }
        return geracao.Vencida(agora) ? throw FalhaDeUsuarios.CodigoVencido() : (usuario, geracao);
    }
}
