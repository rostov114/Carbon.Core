using System.Collections.Generic;
using ProtoBuf;

namespace Carbon.Features
{
	[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
	public class Group
	{
		public string Title { get; set; } = string.Empty;
		public string ParentGroup { get; set; } = string.Empty;

		public int Rank { get; set; }

		public HashSet<string> Perms { get; set; } = new HashSet<string>();
	}
}
