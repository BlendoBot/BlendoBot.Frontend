using BlendoBot.Core.Entities;
using BlendoBot.Core.Module;
using BlendoBot.Core.Utility;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.AdminModule.Subcommands;

internal class ModuleSubcommand {
	private readonly Admin module;
	private readonly AdminCommand command;

	public ModuleSubcommand(Admin module, AdminCommand command) {
		this.module = module;
		this.command = command;
	}

	public async Task OnMessage(MessageCreateEventArgs e, string[] tokenizedMessage) {
		if (tokenizedMessage.Length > 0) {
			switch (tokenizedMessage[0].ToLower()) {
				case "enable":
					await OnMessageEnable(e, tokenizedMessage[1..]);
					break;
				case "disable":
					await OnMessageDisable(e, tokenizedMessage[1..]);
					break;
				case "list":
					await OnMessageList(e);
					break;
				default:
					await module.DiscordInteractor.SendUnknownArgumentsMessage(this, e.Channel, command);
					break;
			}
		} else {
			await module.DiscordInteractor.SendUnknownArgumentsMessage(this, e.Channel, command);
		}
	}

	public async Task OnMessageEnable(MessageCreateEventArgs e, string[] tokenizedMessage) {
		if (tokenizedMessage.Length == 0) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"Please try again with the GUID of a module after {"enable".Code()}",
				Channel = e.Channel,
				Tag = "AdminModuleEnableNoArgument"
			});
			return;
		}
		Type moduleType = module.ModuleManager.GetModuleTypeByGuid(tokenizedMessage[0].ToLower());
		if (moduleType == null) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"{tokenizedMessage[0].Code()} is not a loaded module!",
				Channel = e.Channel,
				Tag = "AdminModuleEnableNotFound"
			});
		} else if (module.ModuleManager.IsModuleEnabled(module.GuildId, moduleType)) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"{tokenizedMessage[0].Code()} is already enabled!",
				Channel = e.Channel,
				Tag = "AdminModuleEnableFailureRedundant"
			});
		} else if (module.ModuleManager.DoesModuleHaveUninstantiatedDependencies(module.GuildId, moduleType, out List<string> missingDependencyGuids)) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"{tokenizedMessage[0].Code()} was missing these dependencies. Please enable them first:\n{string.Join("\n", missingDependencyGuids.Select(s => s.Code()))}",
				Channel = e.Channel,
				Tag = "AdminModuleEnableFailureDependency"
			});
		} else if (await module.ModuleManager.InstantiateModuleForGuild(module.GuildId, moduleType)) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"{tokenizedMessage[0].Code()} is now enabled for this guild!",
				Channel = e.Channel,
				Tag = "AdminModuleEnableSuccess"
			});
		} else {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"{tokenizedMessage[0].Code()} could not be enabled, the bot owner should fix this!",
				Channel = e.Channel,
				Tag = "AdminModuleEnableFailure"
			});
		}
	}

	public async Task OnMessageDisable(MessageCreateEventArgs e, string[] tokenizedMessage) {
		if (tokenizedMessage.Length == 0) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"Please try again with the GUID of a module after {"disable".Code()}",
				Channel = e.Channel,
				Tag = "AdminModuleDisableNoArgument"
			});
			return;
		}
		Type moduleType = module.ModuleManager.GetModuleTypeByGuid(tokenizedMessage[0].ToLower());
		if (moduleType == null) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"{tokenizedMessage[0].Code()} is not a loaded module!",
				Channel = e.Channel,
				Tag = "AdminModuleDisableNotFound"
			});
		} else if (tokenizedMessage[0].ToLower() == typeof(Admin).GetCustomAttribute<ModuleAttribute>().Guid) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"You can't disable the admin module!",
				Channel = e.Channel,
				Tag = "AdminModuleDisableFailureAdmin"
			});
		} else if (!module.ModuleManager.IsModuleEnabled(module.GuildId, moduleType)) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"{tokenizedMessage[0].Code()} is already disabled!",
				Channel = e.Channel,
				Tag = "AdminModuleDisableFailureRedundant"
			});
		} else if (module.ModuleManager.DoesModuleHaveInstantiatedDependants(module.GuildId, moduleType, out List<string> liveDependantGuids)) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"{tokenizedMessage[0].Code()} has these dependencies. Please disable them first:\n{string.Join("\n", liveDependantGuids.Select(s => s.Code()))}",
				Channel = e.Channel,
				Tag = "AdminModuleDisableFailureDependency"
			});
		} else if (await module.ModuleManager.UninstantiateModuleForGuild(module.GuildId, moduleType, true)) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"{tokenizedMessage[0].Code()} is now disabled for this guild!",
				Channel = e.Channel,
				Tag = "AdminModuleDisableSuccess"
			});
		} else {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"{tokenizedMessage[0].Code()} could not be disabled, the bot owner should fix this!",
				Channel = e.Channel,
				Tag = "AdminModuleDisableFailure"
			});
		}
	}

	public async Task OnMessageList(MessageCreateEventArgs e) {
		List<(string Guid, Type Type, bool Enabled)> modules = module.ModuleManager.GetAllModules(e.Guild.Id);
		DiscordEmbedBuilder embedBuilder = new();
		embedBuilder.Title = "BlendoBot Modules";
		embedBuilder.AddField("Enabled modules", string.Join(' ', modules.Where(m => m.Enabled).OrderBy(m => m.Guid).Select(m => m.Guid.Code())));
		if (modules.Any(m => !m.Enabled)) {
			embedBuilder.AddField("Disabled modules", string.Join(' ', modules.Where(m => !m.Enabled).OrderBy(m => m.Guid).Select(m => m.Guid.Code())));
		}

		await module.DiscordInteractor.Send(this, new SendEventArgs {
			Embed = embedBuilder.Build(),
			Channel = e.Channel,
			Tag = "AdminModuleList"
		});
	}
}
