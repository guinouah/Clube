# Projeto Clubes

Aplicação acadêmica para administrar clubes, associados, pedidos de ingresso, eventos, mensalidades, espaços, reservas, solicitações e reclamações. A primeira entrega móvel tem foco em Android.

## Aplicações

- `src/Clubes.Api`: Web API ASP.NET Core 10; única aplicação com acesso direto ao banco central.
- `src/Clubes.Web`: painel administrativo ASP.NET Core MVC; autentica pela API e faz todas as operações via HTTP.
- `src/Clubes.Mobile`: app .NET MAUI para Android, com telas para associado e armazenamento SQLite local.
- `src/Clubes.Mobile.Core`: serviços, cache/fila SQLite e componentes MVVM sem dependência de MAUI.
- `tests/Clubes.UnitTests`: validação de CPF e catálogo das 27 UFs.

## Funcionalidades

- CPF validado e único globalmente; senhas armazenadas com hash e tokens opacos com validade de 30 minutos, revogáveis no logout.
- A pessoa aprovada em um clube fica associada a todos os clubes ativos; os perfis de presidente e tesoureiro seguem limitados ao clube correspondente.
- Cadastro de clube e pedido de ingresso com seleção de UF e município. As 27 UFs são listadas localmente; municípios vêm do serviço oficial de localidades do IBGE e ficam em cache por 24 horas na API e no SQLite do app.
- API para eventos vinculados a espaços, associados, dependentes, aprovação de ingresso, reservas, mensalidades/pagamentos e solicitações/reclamações. O CPF só pode ter vínculo com um clube por vez; interesses são individuais. O tesoureiro configura dias de vencimento e o associado escolhe entre as opções. Quadras e espaços guardam tipo, capacidade, funcionamento e regra para dependentes; reservas validam conflito e aceitam participantes.
- O app consulta eventos e as próprias mensalidades com a última sincronização visível. As telas de eventos e mensalidades usam ViewModels. Cache financeiro e fila são isolados pelo ID da pessoa autenticada. Solicitações, reclamações e reservas ficam em fila SQLite quando não há conexão; na retomada, o app tenta enviar automaticamente e a API revalida conflitos. Reserva offline não é confirmação nem bloqueia horário.
- O painel mostra listas oficiais e ações conforme perfil. Ele requer conexão com a API. Para a demonstração local, a API usa SQLite sem exigir instalação de servidor; MySQL continua disponível como opção.

## Requisitos

- .NET SDK 10.0.401 ou versão compatível da linha 10.0.
- Nenhum servidor de banco é necessário para a demonstração local (SQLite).
- Opcional: MySQL 8 para configurar outro banco central.
- Para compilar Android: workload `maui-android`, Java JDK e Android SDK configurados. iOS e MacCatalyst não fazem parte do alvo desta versão.
- O Android Emulator ou aparelho físico só é necessário para executar e avaliar a interface; a compilação pode ser feita sem um aparelho conectado.

## Configurar e executar

### Iniciar com SQLite (recomendado para testar)

Execute `iniciar-local.cmd` na pasta do projeto. Ele abrirá a API e o painel em duas janelas. A API cria o arquivo `src/Clubes.Api/App_Data/clubes-v2.db` automaticamente. O arquivo `clubes.db` de versões anteriores é preservado sem alterações; esta versão não migra dados antigos, então cadastre novamente os dados de demonstração no banco v2.

Na primeira execução, abra `http://localhost:5229/conta/criar-superadmin`, preencha o formulário e crie a conta inicial. Depois entre pelo site com o CPF e a senha informados. O cadastro inicial só pode ser concluído uma vez.

### Usar MySQL (opcional)

Crie um banco vazio, por exemplo:

```sql
CREATE DATABASE clubes CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
```

Configure a conexão e inicie a API:

```powershell
$env:Database__Provider = "MySql"
$env:ConnectionStrings__MySql = "Server=localhost;Port=3306;Database=clubes;User ID=SEU_USUARIO;Password=SUA_SENHA;"
$env:Bootstrap__SetupKey = "ESCOLHA-UMA-CHAVE-TEMPORARIA-LONGA"
dotnet run --project src/Clubes.Api --launch-profile http
```

Em outro terminal, inicie o painel com a mesma chave de bootstrap:

```powershell
$env:Bootstrap__SetupKey = "ESCOLHA-UMA-CHAVE-TEMPORARIA-LONGA"
dotnet run --project src/Clubes.Web --launch-profile http
```

Abra `http://localhost:5229/conta/criar-superadmin` e faça o cadastro inicial pelo formulário. Depois entre no painel e configure os clubes e seus perfis. Um presidente pode analisar pedidos de ingresso; o app oferece **Criar conta e pedir ingresso**.

Para Android Emulator, a API usa `http://10.0.2.2:5140/`. Inicie a API também sem limitar o host ao loopback se a configuração do seu Android SDK exigir acesso pela rede local. Para aparelho físico, ajuste a URL em `src/Clubes.Mobile/MauiProgram.cs` para o IP local do computador; API e aparelho precisam estar na mesma rede. O HTTP sem TLS é somente para desenvolvimento local.

## Compilar e testar

```powershell
dotnet build src/Clubes.Api/Clubes.Api.csproj
dotnet build src/Clubes.Web/Clubes.Web.csproj
dotnet test tests/Clubes.UnitTests/Clubes.UnitTests.csproj
dotnet build src/Clubes.Mobile/Clubes.Mobile.csproj -f net10.0-android
```

No modo local, SQLite é o banco central oficial da API; o app mantém outro SQLite privado apenas para cache e fila offline. Com MySQL configurado, ele passa a ser o banco central oficial. Nenhum cliente conecta diretamente ao banco central.

## Decisões e escopo

As categorias iniciais são fundador/associado, convidado e simpatizante. São pressupostos acadêmicos e podem ser ajustados após validação com o professor. Consulte [docs/DECISOES.md](docs/DECISOES.md) e [docs/ARQUITETURA.md](docs/ARQUITETURA.md).

Não coloque senhas de banco, chaves de bootstrap ou credenciais pessoais nos arquivos versionados. O repositório GitHub permanece sem publicação até a revisão solicitada pelo usuário.
