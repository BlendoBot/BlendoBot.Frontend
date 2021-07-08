using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.Database {
	internal record AdminUser {
		public ulong GuildId { get; set; }
		public ulong UserId { get; set; }
	}
}
