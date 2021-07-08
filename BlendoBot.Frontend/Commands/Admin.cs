using BlendoBot.Commands;
using BlendoBot.Core.Command;
using BlendoBot.Core.Entities;
using BlendoBot.Core.Interfaces;
using BlendoBot.Core.Utility;
using BlendoBot.Frontend.Services;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.Commands {
	[Command(Guid = "blendobot.frontend.commands.admin", Name = "Admin", Author = "Biendeo", DefaultTerm = "admin")]
	public class Admin : BaseCommand {
		public Admin(ulong guildId, BlendoBot blendobot) : base(guildId, blendobot) {
			discordInteractor = blendobot.DiscordInteractor;
			commandManager = blendobot.CommandManager;
		}

		public override string Description => "Does admin stuff, but only if you are either an administrator of the server, or if you've been granted permission!";
		public override string Usage => $"Usage:\n" +
			$"({"All of these commands are only accessible if you are either an administrator role on this Discord guild, or if you have been added to this admin list!".Italics()})\n" +
			$"{$"{Term} user add @person".Code()} ({"Adds a new person to be a BlendoBot administrator".Italics()})\n" +
			$"{$"{Term} user remove @person".Code()} ({"Removes a person from being a BlendoBot administrator".Italics()})\n" +
			$"{$"{Term} user list".Code()} ({"Lists all current BlendoBot admins".Italics()})\n" +
			$"{$"{Term} command enable [command guid]".Code()} ({"Enables a command currently disabled by BlendoBot".Italics()})\n" +
			$"{$"{Term} command disable [command guid]".Code()} ({"Disables a command currently enabled by BlendoBot".Italics()})\n" +
			$"{$"{Term} command list".Code()} ({$"Lists all currently disabled commands from BlendoBot (all enabled commands are in {commandManager.GetHelpCommandTerm(this, GuildId).Code()})".Italics()})\n" +
			$"{$"{Term} command rename [command term] [new term]".Code()} ({"Renames a command to use the new term (must be unique!)".Italics()})\n" +
			$"{$"{Term} config [config name]".Code()} ({"Prints the value of a config item".Italics()})\n" +
			$"{$"{Term} config [config name] [config value]".Code()} ({"Sets the value of a config item".Italics()})\n" +
			"\n" +
			"Config terms and values are:\n" +
			$"{"unknownprefix".Code()} {"[string]".Code()} (The prefix used before commands)\n" +
			$"{"unknowntoggle".Code()} {"[bool]".Code()} (Whether a message is printed if the prefix is used )\n" +
			$"{$"{Term} command unknownprefix".Code()} ({"Lists the current prefix used for the unknown command message".Italics()})\n" +
			$"{$"{Term} command unknownprefix [prefix]".Code()} ({"Changes the prefix used for the unkown command message".Italics()})\n" +
			$"{$"{Term} command unknowntoggle".Code()} ({"Toggles whether the unknown command message appears".Italics()})";

		private readonly DiscordInteractor discordInteractor;
		private readonly CommandManager commandManager;

		public override Task<bool> Startup() {
			return Task.FromResult(true);
		}

		public override async Task OnMessage(MessageCreateEventArgs e) {
			if (!await discordInteractor.IsUserAdmin(this, e.Guild, e.Channel, e.Author)) {
				await discordInteractor.SendMessage(this, new SendMessageEventArgs {
					Message = $"Only administrators can use {$"{Term}".Code()}!",
					Channel = e.Channel,
					LogMessage = "AdminNotAuthorised"
				});
				return;
			}
			string[] tokenizedMessage = e.Message.Content.Split(' ')[1..];
			if (tokenizedMessage.Length > 0) {
				switch (tokenizedMessage[0].ToLower()) {
					case "user":
						await OnMessageUser(e, tokenizedMessage[1..]);
						break;
					case "command":
						await OnMessageCommand(e, tokenizedMessage[1..]);
						break;
					case "config":
						await OnMessageConfig(e, tokenizedMessage[1..]);
						break;
					default:
						await OnMessageUnknown(e.Channel);
						break;
				}
			} else {
				await OnMessageUnknown(e.Channel);
			}
		}

		private async Task OnMessageUser(MessageCreateEventArgs e, string[] tokenizedMessage) {
			if (tokenizedMessage.Length > 0) {
				switch (tokenizedMessage[0].ToLower()) {
					case "add":
						await OnMessageUserAdd(e);
						break;
					case "remove":
						await OnMessageUserRemove(e);
						break;
					case "list":
						await OnMessageUserList(e);
						break;
					default:
						await OnMessageUnknown(e.Channel);
						break;
				}
			} else {
				await OnMessageUnknown(e.Channel);
			}
		}

		private async Task OnMessageUserAdd(MessageCreateEventArgs e) {
			if (e.MentionedUsers.Count != 1) {
				await discordInteractor.SendMessage(this, new SendMessageEventArgs {
					Message = $"Please mention only one user when using {$"{Term} user add".Code()}.",
					Channel = e.Channel,
					LogMessage = "AdminUserRemoveIncorrectCount"
				});
			} else {
				if (discordInteractor.AddBlendoBotAdmin(e.Guild.Id, e.MentionedUsers[0].Id)) {
					await discordInteractor.SendMessage(this, new SendMessageEventArgs {
						Message = $"Successfully added {e.MentionedUsers[0].Mention} as a BlendoBot admin!",
						Channel = e.Channel,
						LogMessage = "AdminUserAddSuccess"
					});
				} else {
					await discordInteractor.SendMessage(this, new SendMessageEventArgs {
						Message = $"{e.MentionedUsers[0].Mention} is already an admin!",
						Channel = e.Channel,
						LogMessage = "AdminUserAddFailure"
					});
				}
			}
		}

		private async Task OnMessageUserRemove(MessageCreateEventArgs e) {
			if (e.MentionedUsers.Count != 1) {
				await discordInteractor.SendMessage(this, new SendMessageEventArgs {
					Message = $"Please mention only one user when using {$"{Term} user remove".Code()}.",
					Channel = e.Channel,
					LogMessage = "AdminUserRemoveIncorrectCount"
				});
			} else {
				if (discordInteractor.RemoveBlendoBotAdmin(e.Guild.Id, e.MentionedUsers[0].Id)) {
					await discordInteractor.SendMessage(this, new SendMessageEventArgs {
						Message = $"Successfully removed {e.MentionedUsers[0].Mention} as a BlendoBot admin!",
						Channel = e.Channel,
						LogMessage = "AdminUserRemoveSuccess"
					});
				} else {
					await discordInteractor.SendMessage(this, new SendMessageEventArgs {
						Message = $"{e.MentionedUsers[0].Mention} is already not an admin!",
						Channel = e.Channel,
						LogMessage = "AdminUserRemoveFailure"
					});
				}
			}
		}

		private async Task OnMessageUserList(MessageCreateEventArgs e) {
			var sb = new StringBuilder();
			var administrators = await discordInteractor.GetBlendoBotAdmins(GuildId);
			if (administrators.Count > 0) {
				sb.AppendLine("Current BlendoBot administrators:");
				sb.AppendLine($"{"All current guild administrators plus".Italics()}");
				foreach (var user in administrators) {
					sb.AppendLine($"{user.Username} #{user.Discriminator.ToString().PadLeft(4, '0')}");
				}
			} else {
				sb.AppendLine($"No BlendoBot administrators have been assigned. If you are a guild administrator and want someone else to administer BlendoBot, please use {$"{Term} user add".Code()}.");
			}

			await discordInteractor.SendMessage(this, new SendMessageEventArgs {
				Message = sb.ToString(),
				Channel = e.Channel,
				LogMessage = "AdminUserList"
			});
		}

		private async Task OnMessageCommand(MessageCreateEventArgs e, string[] tokenizedMessage) {
			if (tokenizedMessage.Length > 0) {
				switch (tokenizedMessage[0].ToLower()) {
					case "enable":
						await OnMessageCommandEnable(e, tokenizedMessage[1..]);
						break;
					case "disable":
						await OnMessageCommandDisable(e, tokenizedMessage[1..]);
						break;
					case "list":
						await OnMessageCommandList(e);
						break;
					case "rename":
						await OnMessageCommandRename(e, tokenizedMessage[1..]);
						break;
					default:
						await OnMessageUnknown(e.Channel);
						break;
				}
			} else {
				await OnMessageUnknown(e.Channel);
			}
		}

		private async Task OnMessageCommandEnable(MessageCreateEventArgs e, string[] tokenizedMessage) {
			if (tokenizedMessage.Length > 0) {
				string commandGuid = tokenizedMessage[0].ToLower();
				(bool result, string reason) = await commandManager.EnableCommandByGuid(GuildId, commandGuid);
				string logMessage = result ? "AdminCommandEnableSuccess" : "AdminCommandEnableFailure";
				await discordInteractor.SendMessage(this, new SendMessageEventArgs {
					Message = reason,
					Channel = e.Channel,
					LogMessage = logMessage
				});
			} else {
				await OnMessageUnknown(e.Channel);
			}
		}

		private async Task OnMessageCommandDisable(MessageCreateEventArgs e, string[] tokenizedMessage) {
			if (tokenizedMessage.Length > 0) {
				string commandGuid = tokenizedMessage[0].ToLower();
				(bool result, string reason) = commandManager.DisableCommandByGuid(GuildId, commandGuid);
				string logMessage = result ? "AdminCommandDisableSuccess" : "AdminCommandDisableFailure";
				await discordInteractor.SendMessage(this, new SendMessageEventArgs {
					Message = reason,
					Channel = e.Channel,
					LogMessage = logMessage
				});
			} else {
				await OnMessageUnknown(e.Channel);
			}
		}

		private async Task OnMessageCommandList(MessageCreateEventArgs e) {
			var sb = new StringBuilder();
			var disabledCommands = blendobot.CommandManager.GetDisabledLoadedComamnds(GuildId);
			if (disabledCommands.Count > 0) {
				sb.AppendLine("Current disabled commands:");
				foreach (var command in disabledCommands) {
					sb.AppendLine($"{command.Term.Code()} ({command.CommandGuid.Code()})");
				}
			} else {
				sb.AppendLine($"No BlendoBot commands have been disabled.");
			}
			await discordInteractor.SendMessage(this, new SendMessageEventArgs {
				Message = sb.ToString(),
				Channel = e.Channel,
				LogMessage = "AdminCommandList"
			});
		}

		private async Task OnMessageCommandRename(MessageCreateEventArgs e, string[] tokenizedMessage) {
			if (tokenizedMessage.Length >= 2) {
				string commandGuid = tokenizedMessage[0].ToLower();
				(bool result, string reason) = commandManager.DisableCommandByGuid(GuildId, commandGuid);
				string logMessage = result ? "AdminCommandDisableSuccess" : "AdminCommandDisableFailure";
				await discordInteractor.SendMessage(this, new SendMessageEventArgs {
					Message = reason,
					Channel = e.Channel,
					LogMessage = logMessage
				});
			} else {
				await OnMessageUnknown(e.Channel);
			}
		}

		private async Task OnMessageConfig(MessageCreateEventArgs e, string[] tokenizedMessage) {
			if (tokenizedMessage.Length > 0) {
				switch (tokenizedMessage[0].ToLower()) {
					case "unknownprefix":
						await OnMessageConfigUnknownPrefix(e, tokenizedMessage[1..]);
						break;
					case "unknowntoggle":
						await OnMessageConfigUnknownToggle(e, tokenizedMessage[1..]);
						break;
					default:
						await OnMessageUnknown(e.Channel);
						break;
				}
			} else {
				await OnMessageUnknown(e.Channel);
			}
		}

		private async Task OnMessageConfigUnknownPrefix(MessageCreateEventArgs e, string[] tokenizedMessage) {
			if (tokenizedMessage.Length > 0) {
				OtherSettings.UnknownCommandPrefix = splitString[3].ToLower();
				SaveData();
				await discordInteractor.SendMessage(this, new SendMessageEventArgs {
					Message = $"Unknown command prefix is now \"{OtherSettings.UnknownCommandPrefix.Code()}\"",
					Channel = e.Channel,
					LogMessage = "AdminCommandUnknownPrefixChange"
				});
			} else {
				await discordInteractor.SendMessage(this, new SendMessageEventArgs {
					Message = $"The current unknown command prefix is \"{blendobot.CommandManager.GetCommandPrefix(this, GuildId).Code()}\"",
					Channel = e.Channel,
					LogMessage = "AdminCommandUnknownPrefixDisplay"
				});
			}
		}

		private async Task OnMessageConfigUnknownToggle(MessageCreateEventArgs e, string[] tokenizedMessage) {
			if (tokenizedMessage.Length > 0) {
				OtherSettings.IsUnknownCommandEnabled = !OtherSettings.IsUnknownCommandEnabled;
				SaveData();
				await discordInteractor.SendMessage(this, new SendMessageEventArgs {
					Message = $"Unknown command is now {(OtherSettings.IsUnknownCommandEnabled ? "enabled" : "disabled").Bold()}",
					Channel = e.Channel,
					LogMessage = "AdminCommandUnknownPrefixChange"
				});
			} else {
				await discordInteractor.SendMessage(this, new SendMessageEventArgs {
					Message = $"The current unknown command prefix is \"{blendobot.CommandManager.GetCommandPrefix(this, GuildId).Code()}\"",
					Channel = e.Channel,
					LogMessage = "AdminCommandUnknownPrefixDisplay"
				});
			}
		}

		private async Task OnMessageUnknown(DiscordChannel channel) {
			await discordInteractor.SendMessage(this, new SendMessageEventArgs {
				Message = $"I couldn't determine what you wanted. Make sure your command is handled by {$"{commandManager.GetHelpCommandTerm(this, GuildId)} admin".Code()}",
				Channel = channel,
				LogMessage = "AdminUnknownCommand"
			});
		}

		public void StoreRenamedCommand(BaseCommand command, string newTerm) {
			renamedCommands.Add(new RenamedCommand(newTerm, command.GetType().FullName));
			SaveData();
		}

		public string RenameCommandTermFromDatabase(BaseCommand command) {
			var renamedCommand = renamedCommands.Find(c => c.ClassName == command.GetType().FullName);
			if (renamedCommand == null) {
				string targetTerm = command.DefaultTerm.ToLower();
				int count = 1;
				while (renamedCommands.Exists(c => c.Term == targetTerm)) {
					targetTerm = $"{command.DefaultTerm.ToLower()}{++count}";
				}
				StoreRenamedCommand(command, targetTerm);
				return targetTerm;
			} else {
				command.Term = renamedCommand.Term.ToLower();
				return command.Term;
			}
		}
	}
}
