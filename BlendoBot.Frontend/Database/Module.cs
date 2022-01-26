using System.Collections.Generic;

namespace BlendoBot.Frontend.Database;

internal class Module {
	public ulong GuildId { get; set; }
	public Guild Guild { get; set; }

	public string Guid { get; set; }

	public bool Enabled { get; set; }

	public ICollection<Command> Commands { get; set; }
}
