# Decisões e pressupostos

## Confirmadas pelo usuário

- O repositório existente `guinouah/Clube` deve ser usado; não criar outro.
- Antes de publicar no GitHub, mostrar o resultado e conferir com o usuário.
- API ASP.NET Core central, painel administrativo MVC e app .NET MAUI com MVVM. Por decisão posterior do usuário, SQLite é aceito como banco central para facilitar a demonstração local; MySQL permanece opcional. O app também tem SQLite próprio para cache e fila offline.
- CPF identifica uma pessoa no sistema; cada pessoa pode ter vínculo de associado em apenas um clube por vez. Um pedido de ingresso fica ligado ao clube escolhido, e sua aprovação cria vínculo somente naquele clube.
- .NET 10 é a versão prevista; Android é o primeiro alvo móvel.

## Pressupostos registrados

- Categorias iniciais: FundadorAssociado, Convidado e Simpatizante. As regras acadêmicas de voto e contribuição precisam de confirmação do professor.
- A aprovação de ingresso ativa o vínculo somente no clube solicitado. O presidente e o tesoureiro têm perfil de administração limitado ao clube selecionado.
- A primeira estrutura é criada com EF Core `EnsureCreated`; uma etapa futura deve introduzir migrations antes de manter dados reais e fazer alterações de esquema.
- Esta versão amplia o esquema sem migrations retroativas. O perfil Development usa `App_Data/clubes-v2.db` para criar a estrutura nova e manter intacto o banco anterior `clubes.db`; dados de demonstração precisam ser cadastrados novamente no banco v2.
- A associação opcional entre evento e espaço adiciona a coluna `Eventos.EspacoId`. Como o bootstrap usa `EnsureCreated`, bancos MySQL criados por uma versão anterior não serão alterados automaticamente; faça backup e aplique uma migração controlada ou recrie apenas o banco local de demonstração antes de usar esta versão.
- Mensalidades são geradas por competência, com valor vigente por categoria. Pagamento é registrado pelo tesoureiro; não há integração bancária.
- Cada clube começa com vencimentos nos dias 5, 10, 15 e 20, que o tesoureiro pode alterar. Cada associado escolhe uma das opções configuradas; a geração mensal usa o dia escolhido. O padrão do cadastro é dia 10.
- Interesses são guardados individualmente por associado; os nomes podem ser adicionados no catálogo pelo próprio envio do perfil.
- Espaços incluem tipo, capacidade, horário local de funcionamento e permissão para dependentes. Reserva permite selecionar dependente vinculado, associados participantes e convidados nominais. Como a duração não foi especificada, a primeira versão oferece intervalos de 30, 60 ou 90 minutos. O responsável associado envia a reserva em nome do dependente, pois dependentes ainda não têm login próprio.
- Horários de funcionamento são validados no fuso de Brasília, adequado ao cenário de demonstração no Paraná. Uma implantação com clubes em outros fusos deve armazenar o fuso por clube.
- Reserva pendente não bloqueia após recusa/cancelamento; pedidos pendentes ou aprovados e eventos coincidentes impedem conflito no espaço.
- Eventos que ocupam espaço devem vincular o espaço no cadastro; evento sem espaço registrado não bloqueia a agenda de reservas.
- A proteção contra colisões concorrentes da agenda é local à instância atual da API e atende à demonstração acadêmica. Implantação horizontal precisa de coordenação persistente.
- Lista de municípios depende do serviço oficial do IBGE, com cache temporário. Primeiro login/cadastro exigem conexão.

## Segurança de demonstração

- O setup key existe apenas em variável de ambiente e deve ser retirado depois da criação do superadmin.
- HTTP e tráfego claro Android são para desenvolvimento local; publicação deve usar HTTPS e gestão segura das chaves de Data Protection.
- Dados de teste e credenciais reais não entram no Git.

## Fora do alvo inicial

- iOS e MacCatalyst.
- Aprovação financeira automática, integração de pagamento, armazenamento offline de alterações financeiras e funcionamento offline no primeiro acesso.
- Publicação no GitHub nesta etapa; a revisão do usuário vem antes do push.
