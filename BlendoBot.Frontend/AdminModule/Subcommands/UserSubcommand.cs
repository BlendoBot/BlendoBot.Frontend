using BlendoBot.Core.Entities;
using BlendoBot.Core.Utility;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.AdminModule.Subcommands;

internal class UserSubcommand {
	private readonly Admin module;
	private readonly AdminCommand command;

	public UserSubcommand(Admin module, AdminCommand command) {
		this.module = module;
		this.command = command;
	}

	public async Task OnMessage(MessageCreateEventArgs e, string[] tokenizedMessage) {
		if (tokenizedMessage.Length > 0) {
			switch (tokenizedMessage[0].ToLower()) {
				case "add":
					await OnMessageAdd(e);
					break;
				case "remove":
					await OnMessageRemove(e);
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

	private async Task OnMessageAdd(MessageCreateEventArgs e) {
		if (e.MentionedUsers.Count != 1) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"Please mention only one user when using {$"{module.ModuleManager.GetCommandTermWithPrefix(command)} user add".Code()}.",
				Channel = e.Channel,
				Tag = "AdminUserAddIncorrectCount"
			});
		} else {
			if (await module.AdminRepository.AddBlendoBotAdmin(e.Guild.Id, e.MentionedUsers[0].Id)) {
				await module.DiscordInteractor.Send(this, new SendEventArgs {
					Message = $"Successfully added {e.MentionedUsers[0].Mention} as a BlendoBot admin!",
					Channel = e.Channel,
					Tag = "AdminUserAddSuccess"
				});
			} else {
				await module.DiscordInteractor.Send(this, new SendEventArgs {
					Message = $"{e.MentionedUsers[0].Mention} is already an admin!",
					Channel = e.Channel,
					Tag = "AdminUserAddFailure"
				});
			}
		}
	}

	private async Task OnMessageRemove(MessageCreateEventArgs e) {
		if (e.MentionedUsers.Count != 1) {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"Please mention only one user when using {$"{module.ModuleManager.GetCommandTermWithPrefix(module.AdminCommand)} user remove".Code()}.",
				Channel = e.Channel,
				Tag = "AdminUserRemoveIncorrectCount"
			});
		} else {
			if (await module.AdminRepository.RemoveBlendoBotAdmin(e.Guild.Id, e.MentionedUsers[0].Id)) {
				await module.DiscordInteractor.Send(this, new SendEventArgs {
					Message = $"Successfully removed {e.MentionedUsers[0].Mention} as a BlendoBot admin!",
					Channel = e.Channel,
					Tag = "AdminUserRemoveSuccess"
				});
			} else {
				await module.DiscordInteractor.Send(this, new SendEventArgs {
					Message = $"{e.MentionedUsers[0].Mention} is already not an admin!",
					Channel = e.Channel,
					Tag = "AdminUserRemoveFailure"
				});
			}
		}
	}
	private async Task OnMessageList(MessageCreateEventArgs e) {
		IReadOnlyList<DiscordUser> administrators = await module.AdminRepository.GetBlendoBotAdmins(module.ModuleManager.GetGuildIdForModule(module));
		if (administrators.Count > 0) {
			DiscordMessage discordMessage = await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = "Getting admins...",
				Channel = e.Channel,
				Tag = "AdminUserList"
			});
			StringBuilder sb = new();

			sb.AppendLine("Current BlendoBot administrators:");
			sb.AppendLine(string.Join(' ', administrators.Select(a => a.Mention)));

			await discordMessage.ModifyAsync(content: sb.ToString());
		} else {
			await module.DiscordInteractor.Send(this, new SendEventArgs {
				Message = $"No BlendoBot administrators have been assigned. If you are a guild administrator and want someone else to administer BlendoBot, please use {$"{module.ModuleManager.GetCommandTermWithPrefix(command)} user add".Code()}.",
				Channel = e.Channel,
				Tag = "AdminUserList"
			});
		}
	}
}
