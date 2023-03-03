using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Carbon.Base;
using Carbon.Contracts;

namespace Carbon.Components
{
	public class Command
	{
		public static bool FromRcon { get; set; }

		public string Name { get; set; }
		public IMetadata Plugin { get; set; }
		public Action<BasePlayer, string, string[]> Callback { get; set; }
		public string[] Permissions { get; set; }
		public string[] Groups { get; set; }
		public int AuthLevel { get; set; } = 0;
		public int Cooldown { get; set; } = 0;
		public bool SkipOriginal { get; set; }
		public string Help { get; set; }
		public object Reference { get; set; }

		public Command() { }
		public Command(string name, Action<BasePlayer, string, string[]> callback, bool skipOriginal, string[] permissions = null, string[] groups = null)
		{
			Name = name;
			Plugin = Community.Runtime.CorePlugin;
			Callback = callback;
			SkipOriginal = skipOriginal;
			Permissions = permissions;
			Groups = groups;
		}
	}
}
