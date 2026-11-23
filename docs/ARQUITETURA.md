# Arquitetura e fluxos

```text
MAUI Android ── HTTP/JSON ──┐
                            ├──> Clubes.Api ───> SQLite (demo) ou MySQL
Painel ASP.NET Core MVC ────┘
      │
      └── MVC não conecta ao MySQL

MAUI ─── SQLite local (cache recente + ações offline)
```

## API

`src/Clubes.Api` concentra autenticação, validação, regras de negócio e persistência. SQLite é o padrão para demonstração local sem instalação adicional; MySQL pode ser selecionado por configuração. As sessões usam token aleatório opaco; a API guarda o digest SHA-256 e verifica validade, revogação, usuário e clube ativo a cada autenticação. Senhas usam `PasswordHasher<T>`. O banco central mantém vínculos por clube para consultar e validar registros por ID.

Rotas principais:

- `POST /api/auth/bootstrap`, `POST /api/auth/login`, `POST /api/auth/selecionar-clube`, `POST /api/auth/logout`, `GET /api/auth/me`.
- `GET /api/localidades/estados` e `GET /api/localidades/estados/{uf}/municipios`.
- `GET/POST /api/clubes`, `GET /api/clubes/{id}/associados`, `POST /api/clubes/{id}/usuarios`.
- `POST /api/auth/registro`, `GET/PATCH /api/clubes/{id}/pedidos-ingresso`.
- Sob `/api/clubes/{id}`: `eventos`, `espacos`, `reservas`, `solicitacoes`, `dependentes`, `mensalidades` e `mensalidades/minhas`.
- Operações financeiras: `PUT mensalidades/valores`, `POST mensalidades/gerar`, `PATCH mensalidades/{id}/pagamento`.
- Aprovações: `PATCH reservas/{id}/avaliacao`, `PATCH pedidos-ingresso/{id}`, `PATCH solicitacoes/{id}/situacao`.

Listas de UF são estáticas, com as 27 unidades federativas. Municípios vêm do serviço de localidades do IBGE, com cache de 24 horas na API. O backend rejeita município/UF que não pertençam à lista oficial.

## Identidade e autorização por clube

O CPF normalizado tem índice único. Aprovar o primeiro ingresso ativa a associação da pessoa em todos os clubes ativos, seguindo decisão confirmada pelo usuário. Criar um novo clube também inclui as pessoas já associadas. Presidência, tesouraria e superadmin continuam sujeitos às permissões de perfil. O primeiro superadmin é criado apenas com a variável de ambiente `Bootstrap__SetupKey`, depois que o banco estiver configurado; a chave deve ser removida do ambiente após a configuração inicial.

## Painel MVC

`src/Clubes.Web` guarda a sessão do navegador em cookie protegido, mas envia o token à API para cada operação. Há telas de acesso, criação/seleção de clube, listas de associados/pedidos/eventos/reservas/mensalidades/espaços, revisão de pedidos e formulários para eventos, espaços, valores, competências e perfis administrativos.

## App MAUI e offline parcial

O alvo inicial é `net10.0-android`. O app tem login online, seleção de clube, cadastro/pedido de ingresso e consulta de eventos e mensalidades. O token fica no armazenamento seguro do aparelho. SQLite armazena cache de eventos, mensalidades, estados/municípios e fila de pedidos.

As telas de eventos e mensalidades usam ViewModels separados, coleções observáveis e estados explícitos de sincronização. A camada MAUI lê a sessão segura e repassa o contexto da tela; transporte HTTP, cache e fila ficam nos serviços. O cache financeiro e a fila são vinculados ao ID da pessoa autenticada, para que a troca de conta no mesmo aparelho não exponha mensalidades nem envie pedidos em nome de outra pessoa.

Na fila, solicitação, reclamação ou reserva mostra “Aguardando envio”. Ao voltar a conectividade, o app tenta sincronizar automaticamente e a tela Conta ainda oferece uma tentativa manual. O app envia a mesma chave de idempotência da reserva em novas tentativas. Respostas 4xx ficam marcadas como recusadas; falha de rede, timeout ou erro 5xx mantém o item para nova tentativa. Quando recebida, reserva continua pendente até presidente aprovar.

Eventos podem ser vinculados a um espaço. A API verifica conflito com eventos e reservas somente daquele espaço; eventos sem espaço vinculado não bloqueiam reservas. Criação e aprovação de evento/reserva usam uma trava por clube para evitar colisões entre requisições simultâneas atendidas pela mesma instância da API. Em implantação com múltiplas instâncias, será necessário substituir essa trava em memória por coordenação no banco ou serviço distribuído.

O app não permite baixa financeira offline. Dados pessoais guardados ficam limitados a cache de telas e filas do usuário. Primeiro acesso e primeiro login requerem conexão.

## Dados centrais

O modelo atual contém clube, pessoa, usuário, vínculo administrativo por clube, sessão, associação, dependente, pedido de ingresso, evento, espaço, reserva, valor de mensalidade, mensalidade, pagamento, solicitação/reclamação e resposta. O banco central configurado é a fonte oficial (SQLite na demonstração local ou MySQL). A criação inicial usa `EnsureCreated`; alterações futuras no esquema exigirão migração/recriação controlada para ambientes com dados.

## Referências de interface e código

- [PanificaB2B painel](https://github.com/guinouah/PanificaB2B/blob/main/src/PanificaB2B.Web/Views/Shared/_Layout.cshtml) e [visão geral](https://github.com/guinouah/PanificaB2B/blob/main/src/PanificaB2B.Web/Views/Home/Index.cshtml).
- [PanificaB2B app](https://github.com/guinouah/PanificaB2B/blob/main/src/PanificaB2B.App/MainPage.xaml).
- [LimpaFacil MVVM](https://github.com/guinouah/LimpaFacil/blob/main/docs/ARQUITETURA_MVVM.md), [composição MAUI](https://github.com/guinouah/LimpaFacil/blob/main/src/LimpaFacil.Mobile/MauiProgram.cs) e [tela de catálogo](https://github.com/guinouah/LimpaFacil/blob/main/src/LimpaFacil.Mobile/Pages/CatalogPage.cs).
