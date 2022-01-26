namespace BlendoBot.Frontend.Database;

internal class Admin {
	public ulong GuildId { get; set; }
	public Guild Guild { get; set; }

	public ulong UserId { get; set; }
}
