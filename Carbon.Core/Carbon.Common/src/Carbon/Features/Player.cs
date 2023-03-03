using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace Carbon.Features
{
	[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
	public class Player
	{
		public string LastSeenNickname { get; set; } = "Unknown";
		public string Language { get; set; } = "en";

		public HashSet<string> Perms { get; set; } = new HashSet<string>();
		public HashSet<string> Groups { get; set; } = new HashSet<string>();
	}
}
