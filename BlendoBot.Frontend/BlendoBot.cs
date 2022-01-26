using BlendoBot.Frontend.Services;
using System;
using System.Threading.Tasks;

namespace BlendoBot.Frontend;

public class BlendoBot {
	private ServiceManager ServiceManager { get; init; }
	private DateTime StartTime { get; set; }


	public BlendoBot(string configPath) {
		StartTime = DateTime.Now;
		ServiceManager = new ServiceManager();

		Config config = new(configPath);
		if (!config.Reload()) {
			Console.Error.WriteLine($"Could not find {config.ConfigPath}! A default one will be created. Please modify the appropriate fields!");
			config.CreateDefaultConfig();
			Environment.Exit(1);
		} else {
			Console.WriteLine($"Successfully read config file: bot name is {config.Name}");
			if (config.ActivityType.HasValue ^ config.ActivityName != null) {
				Console.WriteLine("The config's ActivityType and ActivityName must both be present to work. Defaulting to no current activity.");
			}
		}
		ServiceManager.RegisterService(config);

		Logger logger = new(StartTime);
		ServiceManager.RegisterService(logger);

		ModuleManager moduleManager = new(logger, ServiceManager);
		ServiceManager.RegisterService(moduleManager);

		FilePathProvider filePathProvider = new(moduleManager);
		ServiceManager.RegisterService(filePathProvider);

		DiscordInteractor discordInteractor = new(moduleManager, config, logger);
		ServiceManager.RegisterService(discordInteractor);

		AdminRepository adminRepository = new(discordInteractor);
		ServiceManager.RegisterService(adminRepository);
	}

	public async Task Start(string[] _) {
		ServiceManager.GetService<ModuleManager>().LoadModules();

		await ServiceManager.GetService<DiscordInteractor>().ConnectAsync();

		await Task.Delay(-1);
	}
}
