using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Boaz.Core.Commons.Usuario.Usuarios.Domain;
using Boaz.Core.Commons.Usuario.Usuarios.Ports.Out;
using Boaz.Core.Hub.Auditoria.Card.Dtos;
using Boaz.Core.Hub.Auditoria.Card.Ports.Out;

namespace Boaz.Core.Hub.Auditoria.Card.UseCases;

/// <summary>
/// O que as conexões ao vivo da Auditoria repetem (RNF-246, RNF-249, 15 RN-279): até 3 conexões por usuário (a 4ª fecha a
/// mais antiga com "encerrado" LIMITE_CONEXOES), o ": ping" a cada 30 s, a sessão e o acesso conferidos a cada 5 s
/// ("encerrado" SESSAO_TERMINADA ou ACESSO_NEGADO) e os eventos de cada fonte, entregues na ordem em que chegam.
/// </summary>
public sealed class AoVivoDaAuditoria
{
    readonly IConexoesAoVivo conexoes;
    readonly IAcessoDoAoVivo acesso;
    readonly TimeProvider relogio;

    /// <summary>As conexões ao vivo dos usuários, a conferência de acesso e o relógio da aplicação.</summary>
    public AoVivoDaAuditoria(IConexoesAoVivo conexoes, IAcessoDoAoVivo acesso, TimeProvider relogio)
    {
        this.conexoes = conexoes;
        this.acesso = acesso;
        this.relogio = relogio;
    }

    /// <summary>De quanto em quanto tempo sai o ": ping".</summary>
    public static readonly TimeSpan Ping = TimeSpan.FromSeconds(30);

    /// <summary>De quanto em quanto tempo a sessão e o acesso são conferidos.</summary>
    public static readonly TimeSpan Conferencia = TimeSpan.FromSeconds(5);

    /// <summary>Uma fonte de eventos: o que a conexão recebe depois dos primeiros.</summary>
    public delegate IAsyncEnumerable<EventoAoVivo> Fonte(CancellationToken ct);

    /// <summary>
    /// Abre a conexão do usuário: a mais antiga além de 3 é encerrada. Os <paramref name="recursos"/> (as assinaturas dos
    /// canais) são descartados quando a conexão termina, ou aqui mesmo se a abertura falhar.
    /// </summary>
    public async Task<AoVivoAberto> Abrir(
        QuemPede quem, IReadOnlyList<EventoAoVivo> iniciais, IReadOnlyList<Fonte> fontes, IReadOnlyList<IAsyncDisposable> recursos, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        try
        {
            foreach (var antiga in await conexoes.Abrir(quem.UsuarioId, id, ct))
            {
                await conexoes.Encerrar(antiga, MotivoEncerramento.LimiteConexoes, ct);
            }
        }
        catch
        {
            foreach (var recurso in recursos) await recurso.DisposeAsync();
            throw;
        }
        return new Conexao(this, quem, id, iniciais, fontes, recursos);
    }

    sealed class Conexao(
        AoVivoDaAuditoria dono, QuemPede quem, string id, IReadOnlyList<EventoAoVivo> iniciais, IReadOnlyList<Fonte> fontes,
        IReadOnlyList<IAsyncDisposable> recursos) : AoVivoAberto
    {
        public override async IAsyncEnumerable<EventoAoVivo> Eventos([EnumeratorCancellation] CancellationToken ct)
        {
            using var parar = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var canal = Channel.CreateUnbounded<EventoAoVivo>();
            var tarefas = new List<Task>();
            try
            {
                foreach (var evento in iniciais) yield return evento;
                foreach (var fonte in fontes) tarefas.Add(Task.Run(() => Escoar(fonte(parar.Token), canal.Writer, parar.Token), CancellationToken.None));
                tarefas.Add(Task.Run(() => Pingar(canal.Writer, parar.Token), CancellationToken.None));
                tarefas.Add(Task.Run(() => Guardar(canal.Writer, parar.Token), CancellationToken.None));
                while (await canal.Reader.WaitToReadAsync(ct))
                {
                    while (canal.Reader.TryRead(out var evento))
                    {
                        yield return evento;
                        if (evento.Tipo == "encerrado") yield break;
                    }
                }
            }
            finally
            {
                await parar.CancelAsync();
                foreach (var recurso in recursos) await recurso.DisposeAsync();
                await dono.conexoes.Fechar(quem.UsuarioId, id, CancellationToken.None);
                try
                {
                    await Task.WhenAll(tarefas);
                }
                catch (Exception)
                {
                    // as tarefas só terminam por cancelamento; o que falhou nelas já foi tratado
                }
            }
        }

        static async Task Escoar(IAsyncEnumerable<EventoAoVivo> fonte, ChannelWriter<EventoAoVivo> escrita, CancellationToken ct)
        {
            try
            {
                await foreach (var evento in fonte.WithCancellation(ct)) escrita.TryWrite(evento);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                // a conexão terminou
            }
            catch (Exception)
            {
                // uma fonte que falha deixa a conexão viva: o ping e a conferência seguem
            }
        }

        async Task Pingar(ChannelWriter<EventoAoVivo> escrita, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(Ping, dono.relogio, ct);
                    escrita.TryWrite(EventoAoVivo.Ping);
                }
            }
            catch (OperationCanceledException)
            {
                // a conexão terminou
            }
        }

        // o encerramento pedido por outra conexão (o limite), a sessão e o acesso, a cada 5 s
        async Task Guardar(ChannelWriter<EventoAoVivo> escrita, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (await dono.conexoes.Esperar(id, Conferencia, ct) is { } motivo)
                    {
                        escrita.TryWrite(EventoAoVivo.Encerrado(motivo.Texto()));
                        return;
                    }
                    if (await Conferir(ct) is { } fim)
                    {
                        escrita.TryWrite(EventoAoVivo.Encerrado(fim.Texto()));
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // a conexão terminou
            }
        }

        async Task<MotivoEncerramento?> Conferir(CancellationToken ct)
        {
            try
            {
                await dono.conexoes.Renovar(quem.UsuarioId, id, ct);
                if (!await dono.acesso.SessaoAberta(quem.Sid, ct)) return MotivoEncerramento.SessaoTerminada;
                return await dono.acesso.PodeUsar(quem.UsuarioId, ct) ? null : MotivoEncerramento.AcessoNegado;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // sem a conferência agora (cache ou banco indisponível): a conexão fica, e a próxima tenta de novo
                return null;
            }
        }
    }
}
