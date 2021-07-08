using BlendoBot.Commands;
using BlendoBot.Core.Command;
using BlendoBot.Core.Entities;
using BlendoBot.Core.Interfaces;
using BlendoBot.Core.Utility;
using BlendoBot.Frontend.Commands;
using BlendoBot.Frontend.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;

namespace BlendoBot {
	public class BlendoBot : IBotServiceProvider {
		public CommandManager CommandManager { get; private set; }
		public Config Config { get; private set; }
		public DiscordInteractor DiscordInteractor { get; private set; }
		public Logger Logger { get; private set; }
		public DateTime StartTime { get; private set; }


		public BlendoBot(string configPath) {
			Config = new Config(configPath);
			StartTime = DateTime.Now;
			Logger = new Logger(StartTime);
			CommandManager = new CommandManager(Logger, this);
			DiscordInteractor = new DiscordInteractor(CommandManager, Config, Logger);
		}

		public async Task Start(string[] _) {
			if (!Config.Reload()) {
				Console.Error.WriteLine($"Could not find {Config.ConfigPath}! A default one will be created. Please modify the appropriate fields!");
				Config.CreateDefaultConfig();
				Environment.Exit(1);
			} else {
				Console.WriteLine($"Successfully read config file: bot name is {Config.Name}");
				if (Config.ActivityType.HasValue ^ Config.ActivityName != null) {
					Console.WriteLine("The config's ActivityType and ActivityName must both be present to work. Defaulting to no current activity.");
				}
			}

			CommandManager.LoadCommands();

			await DiscordInteractor.ConnectAsync();

			await Task.Delay(-1);
		}
		public T GetService<T>() where T : IBotService => (T)(typeof(T) switch {
			ICommandManager => (IBotService)CommandManager,
			IConfig => Config,
			IDiscordInteractor => DiscordInteractor,
			ILogger => Logger,
			_ => default
		});
	}
}
