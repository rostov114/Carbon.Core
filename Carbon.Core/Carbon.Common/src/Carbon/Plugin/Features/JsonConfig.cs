using System;
using System.Collections.Generic;
using System.IO;
using Epic.OnlineServices.UI;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Steamworks;
using Carbon.Features.Common;
using System.Collections;
using Carbon.Core;

namespace Carbon.Features
{
	public class JsonConfig : IFileSerializer
	{
		public string FileName { get; set; }

		public JsonSerializerSettings Settings { get; } = new();

		protected Dictionary<string, object> _keyvalues;
		protected readonly JsonSerializerSettings _settings;

		public JsonConfig(string fileName)
		{
			FileName = fileName;

			_keyvalues = new Dictionary<string, object>();
			_settings = new JsonSerializerSettings();
			_settings.Converters.Add(new KeyValuesConverter());
		}

		public void Load(string filename = null)
		{
			filename = ValidatePath(filename ?? FileName);

			var value = File.ReadAllText(filename);
			_keyvalues = JsonConvert.DeserializeObject<Dictionary<string, object>>(value, _settings);
		}
		public void Save(string filename = null)
		{
			filename = ValidatePath(filename ?? FileName);

			var directoryName = Path.GetDirectoryName(filename);
			if (directoryName != null && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.WriteAllText(filename, JsonConvert.SerializeObject(_keyvalues, Formatting.Indented, _settings));
		}

		public T ReadObject<T>(string filename = null)
		{
			filename = ValidatePath(filename ?? FileName);

			T t;
			if (Exists(filename))
			{
				t = JsonConvert.DeserializeObject<T>(File.ReadAllText(filename), Settings);
			}
			else
			{
				t = Activator.CreateInstance<T>();
				WriteObject(t, false, filename);
			}
			return t;
		}
		public void WriteObject<T>(T config, bool sync = false, string fileName = null)
		{
			if (config == null) config = Activator.CreateInstance<T>();

			fileName = ValidatePath(fileName ?? FileName);
			var directoryName = Path.GetDirectoryName(fileName);
			if (directoryName != null && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}

			var data = JsonConvert.SerializeObject(config, Formatting.Indented, Settings);
			File.WriteAllText(fileName, data);
			if (sync)
			{
				_keyvalues = JsonConvert.DeserializeObject<Dictionary<string, object>>(data, _settings);
			}
		}

		public bool Exists(string fileName = null)
		{
			fileName = ValidatePath(fileName ?? FileName);

			var directoryName = Path.GetDirectoryName(fileName);
			return (directoryName == null || Directory.Exists(directoryName)) && File.Exists(fileName);
		}

		private string ValidatePath(string fileName)
		{
			fileName = SanitizeName(fileName);

			var fullPath = Path.GetFullPath(fileName);
			if (!fullPath.StartsWith(Defines.GetRootFolder(), StringComparison.Ordinal))
			{
				Logger.Log($"{fileName} ||| {fullPath}");
				throw new Exception("Only access to Carbon directory!\nPath: " + fullPath);
			}

			return fullPath;
		}

		public static string SanitizeName(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return string.Empty;
			}

			name = name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
			name = Regex.Replace(name, "[" + Regex.Escape(new string(Path.GetInvalidPathChars())) + "]", "_");
			name = Regex.Replace(name, "\\.+", ".");

			return name.TrimStart('.');
		}
		public static string SanitiseName(string name)
		{
			return SanitizeName(name);
		}

		public void Clear()
		{
			_keyvalues.Clear();
		}

		public void Remove(string key)
		{
			_keyvalues.Remove(key);
		}

		public object this[string key]
		{
			get => _keyvalues.TryGetValue(key, out var result) ? result : null;
			set => _keyvalues[key] = value;
		}
		public object this[string keyLevel1, string keyLevel2]
		{
			get => Get(keyLevel1, keyLevel2);		
			set => Set(keyLevel1, keyLevel2, value);
		}
		public object this[string keyLevel1, string keyLevel2, string keyLevel3]
		{
			get => Get( keyLevel1,keyLevel2, keyLevel3);		
			set => Set(keyLevel1, keyLevel2, keyLevel3, value );
		}

		public object ConvertValue(object value, Type destinationType)
		{
			if (!destinationType.IsGenericType)
			{
				return Convert.ChangeType(value, destinationType);
			}

			if (destinationType.GetGenericTypeDefinition() == typeof(List<>))
			{
				var conversionType = destinationType.GetGenericArguments()[0];
				var list = (IList)Activator.CreateInstance(destinationType);

				foreach (var value2 in ((IList)value))
				{
					list.Add(Convert.ChangeType(value2, conversionType));
				}
				return list;
			}

			if (destinationType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
			{
				var conversionType2 = destinationType.GetGenericArguments()[0];
				var conversionType3 = destinationType.GetGenericArguments()[1];
				var dictionary = (IDictionary)Activator.CreateInstance(destinationType);

				foreach (object obj in ((IDictionary)value).Keys)
				{
					dictionary.Add(Convert.ChangeType(obj, conversionType2), Convert.ChangeType(((IDictionary)value)[obj], conversionType3));
				}
				return dictionary;
			}
			throw new InvalidCastException("Generic types other than List<> and Dictionary<,> are not supported");
		}
		public T ConvertValue<T>(object value)
		{
			return (T)ConvertValue(value, typeof(T));
		}

		public object Get(params string[] path)
		{
			if (path.Length < 1)
			{
				throw new ArgumentException("path must not be empty");
			}

			if (!_keyvalues.TryGetValue(path[0], out var obj))
			{
				return null;
			}


			for (int i = 1; i < path.Length; i++)
			{
				if (obj is not Dictionary<string, object> dictionary || !dictionary.TryGetValue(path[i], out obj))
				{
					return null;
				}
			}

			return obj;
		}
		public T Get<T>(params string[] path)
		{
			return ConvertValue<T>(Get(path));
		}
		public void Set(params object[] pathAndTrailingValue)
		{
			if (pathAndTrailingValue.Length < 2)
			{
				throw new ArgumentException("path must not be empty");
			}

			var array = new string[pathAndTrailingValue.Length - 1];
			for (int i = 0; i < pathAndTrailingValue.Length - 1; i++)
			{
				array[i] = (string)pathAndTrailingValue[i];
			}

			var value = pathAndTrailingValue[pathAndTrailingValue.Length - 1];
			if (array.Length == 1)
			{
				_keyvalues[array[0]] = value;
				return;
			}

			if (!_keyvalues.TryGetValue(array[0], out var obj))
			{
				obj = (_keyvalues[array[0]] = new Dictionary<string, object>());
			}

			for (int j = 1; j < array.Length - 1; j++)
			{
				if (obj is not Dictionary<string, object> dictionary)
				{
					throw new ArgumentException("path is not a dictionary");
				}

				if (!dictionary.TryGetValue(array[j], out obj))
				{
					obj = (dictionary[array[j]] = new Dictionary<string, object>());
				}
			}

			((Dictionary<string, object>)obj)[array[array.Length - 1]] = value;
		}
	}
}
