using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carbon.Features
{
	public abstract class BaseFile : IFileSerializer
	{
		public BaseFile(string fileName)
		{

		}

		public string FileName { get; set; }

		public abstract void Clear();

		public abstract bool Exists(string fileName);

		public abstract void Load(string fileName);

		public abstract T ReadObject<T>(string fileName);

		public abstract void Save(string fileName);

		public abstract void WriteObject<T>(T value, bool async = false, string fileName = null);
	}
}
