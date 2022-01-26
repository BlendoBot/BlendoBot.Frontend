using System.Collections.Generic;

namespace BlendoBot.Frontend.Database;

internal class Guild {
	public ulong GuildId { get; set; }

	public string CommandTermPrefix { get; set; }

	public bool UnknownCommandResponseEnabled { get; set; }

	public ICollection<Admin> Admins { get; set; }
	public ICollection<Command> Commands { get; set; }
	public ICollection<Module> Modules { get; set; }
}
