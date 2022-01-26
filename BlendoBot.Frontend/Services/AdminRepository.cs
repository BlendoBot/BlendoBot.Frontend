using BlendoBot.Core.Services;
using BlendoBot.Frontend.Database;
using DSharpPlus.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.Services;

internal class AdminRepository : IAdminRepository {
	private readonly DiscordInteractor discordInteractor;

	public AdminRepository(DiscordInteractor discordInteractor) {
		this.discordInteractor = discordInteractor;
	}

	public Task<bool> IsUserBlendoBotAdmin(object o, DiscordGuild guild, DiscordChannel channel, DiscordUser user) {
		return IsUserBlendoBotAdmin(guild.Id, user.Id);
	}

	public Task<bool> IsUserAdmin(object o, DiscordGuild guild, DiscordChannel channel, DiscordUser user) {
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		return Task.FromResult(IsUserBlendoBotAdmin(guild.Id, user.Id).Result || IsUserAdmin(o, guild, channel, user).Result);
	}

	internal Task<bool> IsUserBlendoBotAdmin(ulong guildId, ulong userId) {
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		return Task.FromResult(dbContext.Admins.Any(a => a.GuildId == guildId && a.UserId == userId));
	}

	public Task<bool> IsUserBlendoBotOrDiscordAdmin(object o, DiscordGuild guild, DiscordChannel channel, DiscordUser user) {
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		return Task.FromResult(dbContext.Admins.Any(a => a.GuildId == guild.Id && a.UserId == user.Id) || IsUserAdmin(o, guild, channel, user).Result);
	}

	internal async Task<IReadOnlyList<DiscordUser>> GetBlendoBotAdmins(ulong guildId) {
		using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
		return await Task.WhenAll(dbContext.Admins.Where(u => u.GuildId == guildId).Select(u => discordInteractor.GetUserAsync(u.UserId)));
	}

	internal async Task<bool> AddBlendoBotAdmin(ulong guildId, ulong userId) {
		if (await IsUserBlendoBotAdmin(guildId, userId)) {
			return false;
		} else {
			using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
			dbContext.Admins.Add(new Admin { GuildId = guildId, UserId = userId });
			dbContext.SaveChanges();
			return true;
		}
	}

	internal async Task<bool> RemoveBlendoBotAdmin(ulong guildId, ulong userId) {
		if (!await IsUserBlendoBotAdmin(guildId, userId)) {
			return false;
		} else {
			using BlendoBotDbContext dbContext = BlendoBotDbContext.Get();
			dbContext.Admins.Remove(new Admin { GuildId = guildId, UserId = userId });
			dbContext.SaveChanges();
			return true;
		}
	}
}
