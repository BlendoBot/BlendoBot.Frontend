using BlendoBot.Frontend.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace BlendoBot.Frontend.Database;

internal class BlendoBotDbContext : DbContext {
	private BlendoBotDbContext(DbContextOptions<BlendoBotDbContext> options) : base(options) { }
	public DbSet<Admin> Admins { get; set; }
	public DbSet<Command> Commands { get; set; }
	public DbSet<Guild> Guilds { get; set; }
	public DbSet<Module> Modules { get; set; }

	public static BlendoBotDbContext Get() {
		DbContextOptionsBuilder<BlendoBotDbContext> optionsBuilder = new();
		optionsBuilder.UseSqlite($"Data Source={Path.Combine(FilePathProvider.GetAdminDatabasePath(), "blendobot.db")}");
		BlendoBotDbContext dbContext = new(optionsBuilder.Options);
		dbContext.Database.EnsureCreated();
		return dbContext;
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder) {
		modelBuilder.Entity<Admin>().HasKey(a => new { a.GuildId, a.UserId });
		modelBuilder.Entity<Admin>().HasOne(a => a.Guild).WithMany(g => g.Admins).HasForeignKey(a => a.GuildId);
		modelBuilder.Entity<Command>().HasKey(c => new { c.GuildId, c.ModuleGuid, c.Guid });
		modelBuilder.Entity<Command>().HasOne(c => c.Module).WithMany(m => m.Commands).HasForeignKey(c => new { c.GuildId, c.ModuleGuid });
		modelBuilder.Entity<Command>().HasOne(c => c.Guild).WithMany(g => g.Commands).HasForeignKey(c => c.GuildId);
		modelBuilder.Entity<Guild>().HasKey(g => g.GuildId);
		modelBuilder.Entity<Module>().HasKey(m => new { m.GuildId, m.Guid });
		modelBuilder.Entity<Module>().HasOne(m => m.Guild).WithMany(g => g.Modules).HasForeignKey(m => m.GuildId);
	}
}
