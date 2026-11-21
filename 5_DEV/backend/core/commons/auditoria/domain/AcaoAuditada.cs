namespace Boaz.Core.Commons.Auditoria.Domain;

// O catálogo das ações auditadas, de todos os módulos: as do 17 (Usuários,
// Autenticação e Autorização), as do SAC (18 RN-297) e as do Pagamento (19 RN-309).

/// <summary>Ação registrada na auditoria (RN-178).</summary>
public enum AcaoAuditada
{
    /// <summary>POST /contas.</summary>
    CriarConta,
    /// <summary>POST /usuarios.</summary>
    CriarUsuario,
    /// <summary>PATCH /usuarios/{id}.</summary>
    EditarUsuario,
    /// <summary>POST /usuarios/{id}/nova-geracao-senha.</summary>
    NovaGeracaoSenha,
    /// <summary>POST /usuarios/gerar-senha: a senha foi mostrada.</summary>
    GerarSenha,
    /// <summary>POST /usuarios/{id}/novo-codigo-acesso.</summary>
    NovoCodigoAcesso,
    /// <summary>POST /usuarios/{id}/desativar.</summary>
    DesativarUsuario,
    /// <summary>POST /usuarios/{id}/reativar.</summary>
    ReativarUsuario,
    /// <summary>POST /contas/{id}/estado.</summary>
    MudarEstadoConta,
    /// <summary>POST /contas/{id}/codigo-master.</summary>
    CodigoMaster,
    /// <summary>PUT /contas/{id}/tempo-sessao ou PUT /time/tempo-sessao.</summary>
    SalvarTempoSessao,
    /// <summary>PUT /usuarios/{id}/permissoes.</summary>
    EditarPermissoes,
    /// <summary>POST /auth/hub/entrar.</summary>
    EntrarHub,
    /// <summary>POST /auth/quest/entrar.</summary>
    EntrarQuest,
    /// <summary>POST /auth/sala/entrar.</summary>
    EntrarSala,
    /// <summary>POST /auth/hub/confirmar-senha.</summary>
    ConfirmarSenha,
    /// <summary>POST /auth/renovar.</summary>
    RenovarSessao,
    /// <summary>POST /auth/sair.</summary>
    Sair,
    /// <summary>DELETE /auth/sessoes/{sid}.</summary>
    EncerrarSessao,
    /// <summary>DELETE /auth/sessoes.</summary>
    EncerrarSessoes,
    /// <summary>POST /contas/esqueci-senha-master, sem login.</summary>
    EsqueciSenhaMaster,
    /// <summary>Interna: encerrar as sessões de um usuário, de dentro de outra execução (RN-275).</summary>
    EncerrarSessoesDoUsuario,
    /// <summary>Interna: o pv novo nas sessões de um usuário, de dentro de outra execução (RN-275).</summary>
    AvisarPermissoesNovas,
    /// <summary>Interna: a contratação de uma conta mudou, avisada pelo Catálogo (RN-275).</summary>
    AvisarContratacaoMudou,
    /// <summary>POST /sac/chamados (18 RN-297).</summary>
    AbrirChamado,
    /// <summary>POST /sac/chamados/{id}/mensagens (18 RN-297).</summary>
    EnviarMensagem,
    /// <summary>POST /sac/chamados/{id}/assumir (18 RN-297).</summary>
    AssumirChamado,
    /// <summary>POST /sac/chamados/{id}/status (18 RN-297).</summary>
    MudarStatus,
    /// <summary>POST /sac/chamados/{id}/reabrir (18 RN-297).</summary>
    ReabrirChamado,
    /// <summary>POST /sac/chamados/{id}/encerrar (18 RN-297).</summary>
    EncerrarChamado,
    /// <summary>POST /sac/chamados/{id}/avaliacao (18 RN-297).</summary>
    AvaliarChamado,
    /// <summary>PUT /sac/configuracao (18 RN-297).</summary>
    SalvarPrazosSac,
    /// <summary>Sem quem: o agendamento do SAC muda o status pelos 7 dias (18 RN-290, RN-297).</summary>
    FecharPorPrazo,
    /// <summary>POST /pagamento/faturas/{id}/pagamentos (19 RN-309).</summary>
    PagamentoCriado,
    /// <summary>POST /pagamento/pagamentos/{id}/cancelar (19 RN-309).</summary>
    PagamentoCancelado,
    /// <summary>POST /pagamento/recebimentos/faturas/{id}/cancelar (19 RN-309).</summary>
    FaturaCancelada,
    /// <summary>POST /pagamento/recebimentos/pagamentos/{id}/estornos (19 RN-309).</summary>
    EstornoPedido,
    /// <summary>PUT /pagamento/recebimentos/configuracao (19 RN-309).</summary>
    ConfiguracaoSalva,
    /// <summary>POST /pagamento/notificacoes: o aviso do banco, sem login (19 RN-305, RN-309).</summary>
    AvisoDoBanco,
    /// <summary>Sem quem: o agendamento do Pagamento vira a fatura vencida e abre o aviso de atraso (19 RN-301, RN-309, RN-311).</summary>
    FaturaVencida,
    /// <summary>GET /auditoria/...: a consulta à Auditoria, lida e registrada (20 RN-325).</summary>
    ConsultarAuditoria,
}

