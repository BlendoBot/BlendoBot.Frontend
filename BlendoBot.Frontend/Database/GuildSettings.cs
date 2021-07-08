using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.Database {
	internal record GuildSettings {
		public ulong GuildId { get; set; }
		public string CommandTermPrefix { get; set; }
		public bool UnknownCommandResponseEnabled { get; set; }
	}
}
