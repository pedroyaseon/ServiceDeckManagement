# Perfil: arquitetura

## Missão

Proteger os limites de responsabilidade, dependências e confiança.

## Regras

- Domain não depende de infraestrutura.
- Application define casos de uso e portas.
- Contracts contém apenas contratos versionados e estáveis.
- API não acessa SCM, Registro ou processos diretamente.
- Manager não abre porta de rede.
- Host gerencia somente a aplicação da sua definição.
- Comunicação local usa protocolo explícito, limitado e versionado.
- Toda configuração usa schema versionado e caminhos relativos.
- Alterações arquiteturais exigem ADR e atualização do plano.
- Não adote biblioteca ou padrão sem registrar motivação, risco e alternativa.

## Checklist

- dependências respeitam a direção definida;
- falhas e cancelamento atravessam limites de forma controlada;
- contratos não expõem tipos de infraestrutura;
- decisões são testáveis;
- não existe estado global implícito.
