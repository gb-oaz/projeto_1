using Boaz.Core.Commons.Usuario.Autenticacao.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Dtos;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.In;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;

namespace Boaz.Core.Commons.Usuario.Usuarios.UseCases;

/// <summary>
/// POST /usuarios/gerar-senha (RF-109, RN-171, RN-182): a entrada e o código de
/// liberação viram a senha, mostrada uma vez. Erro conta tentativa: a quinta
/// seguida bloqueia, como na entrada do hub.
/// </summary>
public sealed class GerarSenha(
    IUsuarios usuarios, IContas contas, IGeracoesDeSenha geracoes, ITentativasDeLiberacao tentativas, IAuditoria auditoria,
    TimeProvider relogio) : IGerarSenha
{
    readonly Apoio apoio = new(usuarios, contas, auditoria, relogio);

    /// <inheritdoc />
    public async Task<SenhaGeradaDto> Executar(GeracaoDeSenhaDto pedido, CancellationToken ct = default)
    {
        var (entrada, codigo) = pedido.Validar();
        var agora = apoio.Agora;
        var erros = await tentativas.Buscar(entrada, ct) ?? new TentativasDeEntrada(AlvoTentativa.CodigoLiberacao, entrada);
        if (erros.EstaBloqueada(agora)) throw FalhaDeUsuarios.EntradaBloqueada(erros.BloqueadoAte!.Value);

        var usuario = await usuarios.BuscarPorEntrada(entrada, ct);
        var geracao = usuario is { TemSenha: true, Estado: not EstadoUsuario.Desativado }
            ? await geracoes.UltimaDoUsuario(usuario.Id, ct) : null;
        if (usuario is null || geracao is not { Aberta: true } || !geracao.Confere(codigo))
        {
            var bloqueou = erros.RegistrarErro(agora);
            await tentativas.Gravar(erros, ct);
            throw bloqueou ? FalhaDeUsuarios.EntradaBloqueada(erros.BloqueadoAte!.Value) : FalhaDeUsuarios.CodigoInvalido();
        }
        if (geracao.Vencida(agora)) throw FalhaDeUsuarios.CodigoVencido();

        // A senha da conta master sai do nome de quem cadastrou a conta (RN-171).
        var nome = usuario.Tipo == TipoUsuario.Master ? (await apoio.ContaDe(usuario, ct)).Responsavel : usuario.Nome;
        var senha = Senha.Gerar(nome);
        var comprovante = CodigoDeLiberacao.Comprovante();
        usuario.DefinirSenha(ResumoDeSenha.Gerar(senha));
        geracao.MostrarSenha(Credenciais.Resumo(comprovante), agora);
        await usuarios.Atualizar(usuario, ct);
        await geracoes.Atualizar(geracao, ct);
        await tentativas.Apagar(entrada, ct);
        await apoio.Auditar(usuario.ContaId, usuario.Id, AcaoAuditada.GerarSenha, usuario.Id, ct);
        return new(senha, comprovante);
    }
}
