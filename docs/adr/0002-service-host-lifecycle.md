# ADR 0002 — Ciclo de vida do Service Host

## Contexto

O Host precisa funcionar como Serviço do Windows, executar aplicações sem shell
e encerrar árvores de processos de forma previsível. Aplicações arbitrárias não
possuem uma semântica uniforme e segura para pause e continue.

## Decisão

- usar o Generic Host do .NET com
  `Microsoft.Extensions.Hosting.WindowsServices`;
- iniciar aplicações por `ProcessStartInfo` com `UseShellExecute = false` e
  `ArgumentList`;
- associar cada aplicação a um Windows Job Object com
  `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`;
- oferecer start, stop e restart na v1;
- não declarar suporte a pause e continue na v1;
- tratar fechamento da janela principal como tentativa cooperativa e aplicar
  encerramento da árvore após timeout.

## Consequências

- o SCM recebe os estados padrão de início, execução e parada pelo lifetime
  oficial do .NET;
- argumentos não passam por shell;
- a árvore é encerrada mesmo quando a aplicação principal falha;
- aplicações sem janela precisam encerrar por conta própria antes do timeout ou
  serão finalizadas;
- adicionar pause no futuro exigirá protocolo explícito, testes e novo ADR;
- Job Object é controle de ciclo de vida e não transforma código hostil em código
  seguro.
