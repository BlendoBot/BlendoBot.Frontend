using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlendoBot.Frontend.Database {
	internal record CommandSettings {
		public ulong GuildId { get; set; }
		public string CommandGuid { get; set; }
		public string Term { get; set; }
		public bool Enabled { get; set; }
	}
}
