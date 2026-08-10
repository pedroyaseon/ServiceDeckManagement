# Launcher v1

## Objetivo

O Launcher é o cliente desktop local do Service Deck Management. Ele controla os
serviços da própria máquina sem exigir login, endereço de API ou conexão de
rede. A comunicação normal segue este caminho:

```text
Launcher -> Named Pipe local autenticado -> Manager -> SCM do Windows
```

O Launcher não acessa o SCM nem executa shell. O Manager continua sendo o único
componente privilegiado. A API é um gateway opcional, destinado ao Dashboard em
outra máquina, e não participa do uso local do Launcher.

Versão: `1.0.0-beta.3`.

## Execução em desenvolvimento

Com o Manager instalado na mesma raiz portátil:

```powershell
& '.\scripts\dotnet.ps1' run --project '.\src\ServiceDeckManagement.Launcher'
```

O manifesto usa `asInvoker`. Abrir o Launcher não solicita elevação. O Manager
valida a identidade do usuário do Windows no Named Pipe e executa as operações
autorizadas.

## Autorização local

O SID da conta autorizada a usar o Launcher deve estar no campo
`launcherClientSid` de `config/manager-security.json`. Esse SID recebe apenas
acesso ao transporte local. Papéis enviados no payload são ignorados: a função
efetiva é derivada pelo Manager da identidade autenticada do Windows.

O SID do Launcher é separado do SID da API. Ausência, SID privilegiado genérico
ou reutilização do mesmo SID nos dois campos faz a configuração falhar de modo
fechado.

## Sincronização automática

O inventário e os logs são consultados diretamente no Manager a cada dois
segundos. Não existe botão Atualizar, recarga da janela ou dependência do
SignalR. O item selecionado é preservado durante a sincronização quando ainda
existe.

A interface distingue claramente:

- `Manager local`: disponibilidade do canal necessário ao Launcher;
- `Acesso remoto`: recurso opcional, configurado em outra etapa pela API.

## Lista de serviços

A lista usa linhas próprias em vez do estilo padrão do `DataGrid`. Cada linha
apresenta nome, executável, estado, modo de inicialização e PID. Hover e seleção
usam contraste discreto, faixa lateral e tipografia hierárquica; nenhuma seleção
produz fundo branco ou texto ilegível.

Os comandos `Iniciar`, `Parar`, `Reiniciar`, `Editar`, `Reparar registro` e
`Remover` são habilitados de acordo com a seleção e o estado atual.

## Adição e edição

`Adicionar` oferece duas escolhas:

- `Adicionar nova`: cria uma definição com identificador ainda não existente;
- `Adicionar existente`: registra no produto um executável já presente na raiz
  portátil.

O editor aceita somente executáveis e diretórios dentro da pasta do produto.
Argumentos são transmitidos como uma lista, sem concatenação ou interpretação
por shell.

`Remover` exige confirmação explícita. O Manager para o processo, remove o
registro identificado do SCM e só então exclui a definição. Falhas ficam
visíveis na faixa inferior da janela.

## Logs

O painel mostra stdout e stderr sanitizados pelo Host, sempre em UTF-8 e sem
sequências ANSI. `Limpar` afeta apenas a visualização atual. `Copiar` envia o
texto exibido para a área de transferência e `Exportar` cria um arquivo UTF-8
sem BOM escolhido pelo usuário.

## API opcional e Dashboard

Ativar acesso remoto será uma ação separada. Nessa etapa, a API receberá HTTPS,
porta e política de origem explícitas. O Dashboard pedirá o endereço IP e a
porta desse endpoint. Nenhuma configuração de API é solicitada ao abrir o
Launcher e a indisponibilidade da API não bloqueia o gerenciamento local.

## Testes iniciais

Os testes automatizados cobrem chamadas locais tipadas, identidade não confiada
no payload, falhas estáveis sem vazamento de detalhes e rejeição de respostas
incompatíveis. A compilação WPF valida recursos, bindings e handlers declarados
em XAML. O teste administrativo do SCM deve ocorrer em uma VM Windows
descartável e permanece separado da suíte normal.
