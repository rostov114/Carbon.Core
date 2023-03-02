using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Carbon.Core;
using Epic.OnlineServices.UI;
using ProtoBuf;
using Steamworks;

namespace Carbon.Extensions
{
	public static class ProtoEx
	{
		public static IEnumerable<string> GetFiles(string subDirectory)
		{
			var fileDataPath = GetFileDataPath(subDirectory.Replace("..", ""));

			if (!Directory.Exists(fileDataPath))
			{
				yield break;
			}

			foreach (string value in Directory.GetFiles(fileDataPath, "*.data"))
			{
				yield return Path.GetFileNameWithoutExtension(value);
			}

			yield break;
		}

		public static T Load<T>(params string[] subPaths)
		{
			var fileName = GetFileName(subPaths);
			var fileDataPath = GetFileDataPath(fileName);

			try
			{
				if (File.Exists(fileDataPath))
				{
					T result;
					using (FileStream fileStream = File.OpenRead(fileDataPath))
					{
						result = Serializer.Deserialize<T>(fileStream);
					}
					return result;
				}
			}
			catch (Exception ex)
			{
				Logger.Error("Failed to load protobuf data from " + fileName, ex);
			}

			return default(T);
		}

		public static void Save<T>(T data, params string[] subPaths)
		{
			var fileName = GetFileName(subPaths);
			var fileDataPath = GetFileDataPath(fileName);
			var directoryName = Path.GetDirectoryName(fileDataPath);

			try
			{
				if (directoryName != null && !Directory.Exists(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}

				var mode = File.Exists(fileDataPath) ? FileMode.Truncate : FileMode.Create;
				using (FileStream fileStream = File.Open(fileDataPath, mode))
				{
					Serializer.Serialize<T>(fileStream, data);
				}
			}
			catch (Exception ex)
			{
				Logger.Error("Failed to save protobuf data to " + fileName, ex);
			}
		}

		public static bool Exists(params string[] subPaths)
		{
			return File.Exists(GetFileDataPath(GetFileName(subPaths)));
		}

		public static string GetFileName(params string[] subPaths)
		{
			return string.Join(Path.DirectorySeparatorChar.ToString(), subPaths).Replace("..", "") + ".data";
		}

		public static string GetFileDataPath(string name)
		{
			return Path.Combine(Defines.GetDataFolder(), name);
		}
	}
}
