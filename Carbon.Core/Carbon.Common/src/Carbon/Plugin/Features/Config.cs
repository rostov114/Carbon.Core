using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Carbon.Plugins.Features
{
	public abstract class Config
	{
		[JsonIgnore]
		public string Name { get; private set; }

		protected Config(string name)
		{
			Name = name;
		}
	}
}
