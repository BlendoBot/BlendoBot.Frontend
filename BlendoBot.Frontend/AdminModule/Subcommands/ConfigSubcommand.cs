using BlendoBot.Core.Entities;
using BlendoBot.Core.Utility;
using BlendoBot.Frontend.Database;
using DSharpPlus.EventArgs;
using System.Linq;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.AdminModule.Subcommands;

internal class ConfigSubcommand {
	private readonly Admin module;
	private readonly AdminCommand command;

	public ConfigSubcommand(Admin module, AdminCommand command) {
		this.module = module;
		this.command = command;
	}

	public async Task OnMessage(MessageCreateEventArgs e, string[] tokenizedMessage) {
		if (tokenizedMessage.Length > 0) {
			switch (tokenizedMessage[0].ToLower()) {
				case "commandprefix":
					await OnMessageCommandPrefix(e, tokenizedMessage[1..]);
					break;
				case "unknowntoggle":
					await OnMessageUnknownToggle(e, tokenizedMessage[1..]);
					break;
				default:
					await module.DiscordInteractor.SendUnknownArgumentsMessage(this, e.Channel, command);
					break;
			}
		} else {
			await module.DiscordInteractor.SendUnknownArgumentsMessage(this, e.Channel, command);
		}
	}

	public async Task OnMessageCommandPrefix(MessageCreateEventArgs e, string[] tokenizedMessage) {
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		Guild guildSettings = dbContext.Guilds.Single(g => g.GuildId == module.GuildId);
		string existingPrefix = guildSettings.CommandTermPrefix;
		if (tokenizedMessage.Length == 0) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"Current command prefix is {existingPrefix.Code()}",
				Channel = e.Channel,
				Tag = "AdminConfigCommandPrefixGet"
			});
		} else {
			if (existingPrefix == tokenizedMessage[0].ToLower()) {
				await module.DiscordInteractor.Send(this, new SendEventArgs {
					Message = $"Command prefix is already set to {tokenizedMessage[0].Code()}",
					Channel = e.Channel,
					Tag = "AdminConfigCommandPrefixSetRedundant"
				});
			} else {
				module.ModuleManager.ChangeCommandPrefix(module.GuildId, tokenizedMessage[0].ToLower());
				await module.DiscordInteractor.Send(this, new SendEventArgs {
					Message = $"Command prefix is now changed from {existingPrefix.Code()} to {tokenizedMessage[0].ToLower().Code()}",
					Channel = e.Channel,
					Tag = "AdminConfigCommandPrefixSet"
				});
			}
		}
	}

	public async Task OnMessageUnknownToggle(MessageCreateEventArgs e, string[] tokenizedMessage) {
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		Guild guildSettings = dbContext.Guilds.Single(g => g.GuildId == module.GuildId);
		bool existingSetting = guildSettings.UnknownCommandResponseEnabled;
		if (tokenizedMessage.Length == 0) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"Unknown command response is currently {existingSetting.ToString().Code()}",
				Channel = e.Channel,
				Tag = "AdminConfigUnknownCommandResponseGet"
			});
		} else if (bool.TryParse(tokenizedMessage[0], out bool isEnabled)) {
			if (existingSetting == isEnabled) {
				await module.DiscordInteractor.Send(this, new SendEventArgs {
					Message = $"Unknown command response is already set to {isEnabled.ToString().Code()}",
					Channel = e.Channel,
					Tag = "AdminConfigUnknownCommandResponseSetRedundant"
				});
			} else {
				module.ModuleManager.SetUnknownCommandResponse(module.GuildId, isEnabled);
				await module.DiscordInteractor.Send(this, new SendEventArgs {
					Message = $"Unknown command response is now set to {isEnabled.ToString().Code()}",
					Channel = e.Channel,
					Tag = "AdminConfigUnknownCommandResponseSet"
				});
			}
		} else {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"You must say either {"true".Code()} or {"false".Code()}",
				Channel = e.Channel,
				Tag = "AdminConfigUnknownToggleInvalidArgument"
			});
		}
	}
}
