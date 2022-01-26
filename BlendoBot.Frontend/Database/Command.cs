namespace BlendoBot.Frontend.Database;

internal class Command {
	public ulong GuildId { get; set; }
	public Guild Guild { get; set; }

	public string ModuleGuid { get; set; }
	public Module Module { get; set; }

	public string Guid { get; set; }

	public string Term { get; set; }
}
