using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Carbon.Features
{
	public interface IFileSerializer
	{
		string FileName { get; set; }

		void Load(string fileName);
		void Save(string fileName);

		T ReadObject<T>(string fileName);
		void WriteObject<T>(T config, bool async = false, string fileName = null);

		bool Exists(string fileName);
		void Clear();
	}
}
