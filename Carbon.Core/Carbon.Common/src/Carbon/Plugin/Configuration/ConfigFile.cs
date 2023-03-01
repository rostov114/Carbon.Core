using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Carbon.Plugins.Configuration
{
	public abstract class ConfigFile
	{
		[JsonIgnore]
		public string Name { get; private set; }

		protected ConfigFile(string name) { Name = name; }
	}
}
