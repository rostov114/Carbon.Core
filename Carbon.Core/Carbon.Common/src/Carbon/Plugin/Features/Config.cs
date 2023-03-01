using System;
using System.Collections.Generic;
using System.IO;
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

		public static T Load<T>(string filename) where T : ConfigFile
		{
			var t = (T)Activator.CreateInstance(typeof(T), filename);
			t.Load(null);
			return t;
		}

		public virtual void Load(string filename = null)
		{
			JsonConvert.PopulateObject(File.ReadAllText(filename ?? this.Filename), this);
		}

		public virtual void Save(string filename = null)
		{
			string contents = JsonConvert.SerializeObject(this, Formatting.Indented);
			File.WriteAllText(filename ?? Filename, contents);
		}
	}
}
