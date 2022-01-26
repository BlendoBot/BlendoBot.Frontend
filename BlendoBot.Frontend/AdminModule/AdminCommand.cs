using BlendoBot.Core.Command;
using BlendoBot.Core.Entities;
using BlendoBot.Core.Module;
using BlendoBot.Core.Utility;
using BlendoBot.Frontend.AdminModule.Subcommands;
using DSharpPlus.EventArgs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.AdminModule;

internal class AdminCommand : ICommand {
	private readonly CommandSubcommand commandSubcommand;
	private readonly ConfigSubcommand configSubcommand;
	private readonly ModuleSubcommand moduleSubcommand;
	private readonly UserSubcommand userSubcommand;

	public AdminCommand(Admin module) {
		this.module = module;

		commandSubcommand = new(module, this);
		configSubcommand = new(module, this);
		moduleSubcommand = new(module, this);
		userSubcommand = new(module, this);
	}

	private readonly Admin module;
	public IModule Module => module;

	public string Guid => "admin.admin";
	public string DesiredTerm => "admin";

	public string Description => "Does admin stuff, but only if you are either an administrator of the server, or if you've been granted permission!";
	public Dictionary<string, string> Usage => new() {
		{ "General notes", "All of these commands are only accessible if you are either an administrator role on this Discord guild, or if you have been added to this admin list!" },
		{ "user add @person", "Adds a new person to be a BlendoBot administrator." },
		{ "user remove @person", "Removes a person from being a BlendoBot administrator." },
		{ "user list", "Lists all current BlendoBot admins." },
		{ "module enable [module guid]", "Enables a module currently disabled by BlendoBot." },
		{ "module disable [module guid]", "Disablees a module currently enabled by BlendoBot." },
		{ "module list", "Lists all modules loaded by BlendoBot, and states whether they're enabled or disabled." },
		{ "command rename [command term] [new term]", "Renames a command to a new term." },
		{ "config [config name] [config value]", "Sets the value of a config item. Leave the value blank to print the current value." },
		{ "Config items available", $"{"commandprefix".Code()} - The prefix all commands have before their term.\n{"unknowntoggle".Code()} - Whether BlendoBot responds to messages using the prefix but not handled with an error message."}
	};

	public async Task OnMessage(MessageCreateEventArgs e, string[] tokenizedMessage) {
		if (!await module.DiscordInteractor.IsUserAdmin(this, e.Guild, e.Channel, e.Author)) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"Only administrators can use {$"{module.ModuleManager.GetCommandTermWithPrefix(this)}".Code()}!",
				Channel = e.Channel,
				Tag = "AdminNotAuthorised"
			});
			return;
		}
		if (tokenizedMessage.Length > 0) {
			switch (tokenizedMessage[0].ToLower()) {
				case "user":
					await userSubcommand.OnMessage(e, tokenizedMessage[1..]);
					break;
				case "command":
					await commandSubcommand.OnMessage(e, tokenizedMessage[1..]);
					break;
				case "config":
					await configSubcommand.OnMessage(e, tokenizedMessage[1..]);
					break;
				case "module":
					await moduleSubcommand.OnMessage(e, tokenizedMessage[1..]);
					break;
				default:
					await module.DiscordInteractor.SendUnknownArgumentsMessage(this, e.Channel, this);
					break;
			}
		} else {
			await module.DiscordInteractor.SendUnknownArgumentsMessage(this, e.Channel, this);
		}
	}
}
