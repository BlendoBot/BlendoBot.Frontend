using BlendoBot.Core.Module;
using BlendoBot.Frontend.Services;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.AdminModule;

[Module(Name = "Admin", Guid = "com.biendeo.blendobot.module.admin", Author = "Biendeo", Version = "1.0.0", Url = "https://github.com/BlendoBot/BlendoBot.Frontend")]
internal class Admin : IModule {
	internal readonly AdminRepository AdminRepository;
	internal readonly Config Config;
	internal readonly DiscordInteractor DiscordInteractor;
	internal readonly Logger Logger;
	internal readonly ModuleManager ModuleManager;

	internal readonly AdminCommand AdminCommand;
	internal readonly HelpCommand HelpCommand;
	internal readonly AboutCommand AboutCommand;

	internal ulong GuildId { get; private set; }

	public Admin(AdminRepository adminRepository, Config config, DiscordInteractor discordInteractor, Logger logger, ModuleManager moduleManager) {
		AdminRepository = adminRepository;
		Config = config;
		DiscordInteractor = discordInteractor;
		Logger = logger;
		ModuleManager = moduleManager;

		AdminCommand = new AdminCommand(this);
		HelpCommand = new HelpCommand(this);
		AboutCommand = new AboutCommand(this);
	}

	public Task<bool> Startup(ulong guildId) {
		GuildId = guildId;
		return Task.FromResult(
			ModuleManager.RegisterCommand(this, AdminCommand, out _) &&
			ModuleManager.RegisterCommand(this, HelpCommand, out _) &&
			ModuleManager.RegisterCommand(this, AboutCommand, out _));
	}
}
