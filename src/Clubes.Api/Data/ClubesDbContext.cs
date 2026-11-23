using Clubes.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Clubes.Api.Data;

public sealed class ClubesDbContext(DbContextOptions<ClubesDbContext> options) : DbContext(options)
{
    public DbSet<Clube> Clubes => Set<Clube>();
    public DbSet<Pessoa> Pessoas => Set<Pessoa>();
    public DbSet<AssociacaoClube> Associacoes => Set<AssociacaoClube>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<VinculoUsuarioClube> VinculosUsuario => Set<VinculoUsuarioClube>();
    public DbSet<SessaoUsuario> Sessoes => Set<SessaoUsuario>();
    public DbSet<SolicitacaoIngresso> SolicitacoesIngresso => Set<SolicitacaoIngresso>();
    public DbSet<Dependente> Dependentes => Set<Dependente>();
    public DbSet<Interesse> Interesses => Set<Interesse>();
    public DbSet<PessoaInteresse> PessoasInteresses => Set<PessoaInteresse>();
    public DbSet<DiaVencimentoClube> DiasVencimento => Set<DiaVencimentoClube>();
    public DbSet<ReservaParticipante> ParticipantesReserva => Set<ReservaParticipante>();
    public DbSet<EventoClube> Eventos => Set<EventoClube>();
    public DbSet<EspacoClube> Espacos => Set<EspacoClube>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<ValorMensalidade> ValoresMensalidade => Set<ValorMensalidade>();
    public DbSet<Mensalidade> Mensalidades => Set<Mensalidade>();
    public DbSet<PagamentoMensalidade> Pagamentos => Set<PagamentoMensalidade>();
    public DbSet<Manifestacao> Manifestacoes => Set<Manifestacao>();
    public DbSet<RespostaManifestacao> RespostasManifestacao => Set<RespostaManifestacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Clube>(entity =>
        {
            entity.HasKey(clube => clube.Id);
            entity.HasIndex(clube => clube.Sigla).IsUnique();
            entity.Property(clube => clube.Nome).HasMaxLength(160).IsRequired();
            entity.Property(clube => clube.Sigla).HasMaxLength(20).IsRequired();
            entity.Property(clube => clube.Uf).HasMaxLength(2).IsRequired();
            entity.Property(clube => clube.Email).HasMaxLength(200);
        });

        modelBuilder.Entity<Pessoa>(entity =>
        {
            entity.HasKey(pessoa => pessoa.Id);
            entity.HasIndex(pessoa => pessoa.Cpf).IsUnique();
            entity.Property(pessoa => pessoa.Cpf).HasMaxLength(11).IsRequired();
            entity.Property(pessoa => pessoa.NomeCompleto).HasMaxLength(160).IsRequired();
            entity.Property(pessoa => pessoa.Email).HasMaxLength(200);
            entity.Property(pessoa => pessoa.Uf).HasMaxLength(2);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(usuario => usuario.Id);
            entity.HasIndex(usuario => usuario.PessoaId).IsUnique();
            entity.HasOne(usuario => usuario.Pessoa).WithOne(pessoa => pessoa.Usuario).HasForeignKey<Usuario>(usuario => usuario.PessoaId);
            entity.Property(usuario => usuario.SenhaHash).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<AssociacaoClube>(entity =>
        {
            entity.HasKey(associacao => new { associacao.PessoaId, associacao.ClubeId });
            entity.HasIndex(associacao => associacao.PessoaId).IsUnique();
            entity.Property(associacao => associacao.DiaVencimento).HasDefaultValue(10);
            entity.Property(associacao => associacao.Categoria).HasConversion<string>().HasMaxLength(24);
            entity.Property(associacao => associacao.Situacao).HasConversion<string>().HasMaxLength(24);
            entity.HasOne(associacao => associacao.Pessoa).WithMany(pessoa => pessoa.Associacoes).HasForeignKey(associacao => associacao.PessoaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(associacao => associacao.Clube).WithMany(clube => clube.Associacoes).HasForeignKey(associacao => associacao.ClubeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VinculoUsuarioClube>(entity =>
        {
            entity.HasKey(vinculo => new { vinculo.UsuarioId, vinculo.ClubeId });
            entity.Property(vinculo => vinculo.Perfil).HasConversion<string>().HasMaxLength(24);
            entity.HasOne(vinculo => vinculo.Usuario).WithMany(usuario => usuario.Vinculos).HasForeignKey(vinculo => vinculo.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(vinculo => vinculo.Clube).WithMany().HasForeignKey(vinculo => vinculo.ClubeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SessaoUsuario>(entity =>
        {
            entity.HasKey(sessao => sessao.Id);
            entity.HasIndex(sessao => sessao.TokenHash).IsUnique();
            entity.HasIndex(sessao => new { sessao.UsuarioId, sessao.ExpiraEmUtc });
            entity.Property(sessao => sessao.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(sessao => sessao.Perfil).HasMaxLength(24).IsRequired();
            entity.HasOne(sessao => sessao.Usuario).WithMany().HasForeignKey(sessao => sessao.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SolicitacaoIngresso>(entity =>
        {
            entity.HasKey(pedido => pedido.Id);
            entity.HasIndex(pedido => new { pedido.PessoaId, pedido.ClubeId, pedido.Situacao });
            entity.Property(pedido => pedido.Categoria).HasConversion<string>().HasMaxLength(24);
            entity.Property(pedido => pedido.Situacao).HasConversion<string>().HasMaxLength(24);
            entity.Property(pedido => pedido.Justificativa).HasMaxLength(500);
            entity.HasOne(pedido => pedido.Pessoa).WithMany().HasForeignKey(pedido => pedido.PessoaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(pedido => pedido.Clube).WithMany().HasForeignKey(pedido => pedido.ClubeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Dependente>(entity =>
        {
            entity.HasKey(dependente => dependente.Id);
            entity.Property(dependente => dependente.NomeCompleto).HasMaxLength(160).IsRequired();
            entity.Property(dependente => dependente.Parentesco).HasMaxLength(40);
            entity.HasOne(dependente => dependente.Titular).WithMany(pessoa => pessoa.Dependentes).HasForeignKey(dependente => dependente.TitularPessoaId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Interesse>(entity =>
        {
            entity.HasKey(interesse => interesse.Id);
            entity.HasIndex(interesse => interesse.Nome).IsUnique();
            entity.Property(interesse => interesse.Nome).HasMaxLength(60).IsRequired();
        });

        modelBuilder.Entity<PessoaInteresse>(entity =>
        {
            entity.HasKey(item => new { item.PessoaId, item.InteresseId });
            entity.HasOne(item => item.Pessoa).WithMany(pessoa => pessoa.Interesses).HasForeignKey(item => item.PessoaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Interesse).WithMany(interesse => interesse.Pessoas).HasForeignKey(item => item.InteresseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DiaVencimentoClube>(entity =>
        {
            entity.HasKey(item => new { item.ClubeId, item.Dia });
            entity.HasOne<Clube>().WithMany().HasForeignKey(item => item.ClubeId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("CK_DiaVencimento_1_28", "Dia >= 1 AND Dia <= 28"));
        });

        modelBuilder.Entity<EventoClube>(entity =>
        {
            entity.HasKey(evento => evento.Id);
            entity.HasIndex(evento => new { evento.ClubeId, evento.InicioUtc });
            entity.Property(evento => evento.Nome).HasMaxLength(160).IsRequired();
            entity.Property(evento => evento.Local).HasMaxLength(160);
            entity.HasOne(evento => evento.Clube).WithMany().HasForeignKey(evento => evento.ClubeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(evento => evento.Espaco).WithMany().HasForeignKey(evento => evento.EspacoId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EspacoClube>(entity =>
        {
            entity.HasKey(espaco => espaco.Id);
            entity.HasIndex(espaco => new { espaco.ClubeId, espaco.Nome }).IsUnique();
            entity.Property(espaco => espaco.Nome).HasMaxLength(100).IsRequired();
            entity.Property(espaco => espaco.Tipo).HasMaxLength(60).IsRequired();
            entity.Property(espaco => espaco.AbreAs).HasConversion(value => value.ToString("HH:mm:ss"), value => TimeOnly.Parse(value));
            entity.Property(espaco => espaco.FechaAs).HasConversion(value => value.ToString("HH:mm:ss"), value => TimeOnly.Parse(value));
            entity.HasOne(espaco => espaco.Clube).WithMany().HasForeignKey(espaco => espaco.ClubeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Reserva>(entity =>
        {
            entity.HasKey(reserva => reserva.Id);
            entity.HasIndex(reserva => reserva.IdempotencyKey).IsUnique();
            entity.HasIndex(reserva => new { reserva.EspacoId, reserva.InicioUtc, reserva.FimUtc });
            entity.Property(reserva => reserva.Situacao).HasConversion<string>().HasMaxLength(24);
            entity.Property(reserva => reserva.Finalidade).HasMaxLength(300).IsRequired();
            entity.HasOne(reserva => reserva.Espaco).WithMany().HasForeignKey(reserva => reserva.EspacoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(reserva => reserva.DependenteId);
        });

        modelBuilder.Entity<ReservaParticipante>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.NomeConvidado).HasMaxLength(120);
            entity.HasOne(item => item.Reserva).WithMany(reserva => reserva.Participantes).HasForeignKey(item => item.ReservaId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ValorMensalidade>(entity =>
        {
            entity.HasKey(valor => valor.Id);
            entity.HasIndex(valor => new { valor.ClubeId, valor.Categoria, valor.VigenteDesde }).IsUnique();
            entity.Property(valor => valor.Categoria).HasConversion<string>().HasMaxLength(24);
            entity.Property(valor => valor.Valor).HasPrecision(12, 2);
            entity.Property(valor => valor.VigenteDesde).HasColumnType("date");
        });

        modelBuilder.Entity<Mensalidade>(entity =>
        {
            entity.HasKey(mensalidade => mensalidade.Id);
            entity.HasIndex(mensalidade => new { mensalidade.ClubeId, mensalidade.PessoaId, mensalidade.Ano, mensalidade.Mes }).IsUnique();
            entity.Property(mensalidade => mensalidade.Valor).HasPrecision(12, 2);
            entity.Property(mensalidade => mensalidade.Situacao).HasConversion<string>().HasMaxLength(24);
            entity.Property(mensalidade => mensalidade.Vencimento).HasColumnType("date");
            entity.HasOne(mensalidade => mensalidade.Pessoa).WithMany().HasForeignKey(mensalidade => mensalidade.PessoaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Clube>().WithMany().HasForeignKey(mensalidade => mensalidade.ClubeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PagamentoMensalidade>(entity =>
        {
            entity.HasKey(pagamento => pagamento.Id);
            entity.Property(pagamento => pagamento.Valor).HasPrecision(12, 2);
            entity.Property(pagamento => pagamento.Forma).HasMaxLength(40);
            entity.HasOne<Mensalidade>().WithMany().HasForeignKey(pagamento => pagamento.MensalidadeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Manifestacao>(entity =>
        {
            entity.HasKey(manifestacao => manifestacao.Id);
            entity.HasIndex(manifestacao => new { manifestacao.ClubeId, manifestacao.Situacao, manifestacao.CriadaEmUtc });
            entity.Property(manifestacao => manifestacao.Tipo).HasConversion<string>().HasMaxLength(24);
            entity.Property(manifestacao => manifestacao.Situacao).HasConversion<string>().HasMaxLength(24);
            entity.Property(manifestacao => manifestacao.Assunto).HasMaxLength(160).IsRequired();
            entity.Property(manifestacao => manifestacao.Descricao).HasMaxLength(2000).IsRequired();
        });

        modelBuilder.Entity<RespostaManifestacao>(entity =>
        {
            entity.HasKey(resposta => resposta.Id);
            entity.Property(resposta => resposta.Mensagem).HasMaxLength(2000).IsRequired();
            entity.HasOne<Manifestacao>().WithMany().HasForeignKey(resposta => resposta.ManifestacaoId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
