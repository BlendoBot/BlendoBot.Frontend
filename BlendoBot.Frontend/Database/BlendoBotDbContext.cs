using BlendoBot.Frontend.Database;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.Database {
	internal class BlendoBotDbContext : DbContext {
		private BlendoBotDbContext(DbContextOptions<BlendoBotDbContext> options) : base(options) { }
		public DbSet<CommandSettings> CommandSettings { get; set; }
		public DbSet<GuildSettings> GuildSettings { get; set; }
		public DbSet<AdminUser> AdminUsers { get; set; }

		public static string DatabasePath => Path.Combine("data", "common", "BlendoBot", "blendobot.db");

		public static BlendoBotDbContext Get() {
			Directory.CreateDirectory(Path.Combine("data", "common", "BlendoBot"));
			var optionsBuilder = new DbContextOptionsBuilder<BlendoBotDbContext>();
			optionsBuilder.UseSqlite($"Data Source={DatabasePath}");
			return new BlendoBotDbContext(optionsBuilder.Options);
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			modelBuilder.Entity<AdminUser>().HasKey(a => new { a.GuildId, a.UserId });
			modelBuilder.Entity<CommandSettings>().HasKey(a => new { a.GuildId, a.CommandGuid });
			modelBuilder.Entity<GuildSettings>().HasKey(a => a.GuildId);
		}
	}
}
