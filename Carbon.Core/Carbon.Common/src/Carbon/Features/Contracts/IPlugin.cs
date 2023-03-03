using System;
using Carbon.Core;
using Carbon.Plugins;
using Oxide.Core;

namespace Carbon.Contracts
{
	public interface IPlugin : IMetadata, IHookable, IDisposable
	{
		bool IsCorePlugin { get; set; }

		string Author { get; set; }
		VersionNumber Version { get; set; }

		string FilePath { get; set; }
		string FileName { get; set; }

		float CompileTime { get; set; }
		double TotalHookTime { get; set; }

		IPlugin[] Requires { get; set; }

		IBaseProcessor Processor { get; set; }
		IBaseProcessor.IInstance ProcessorInstance { get; set; }

		Persistence Persistence { get; set; }
		bool HasConditionals { get; set; }
		bool HasInitialized { get; set; }

		void SetProcessor(IBaseProcessor processor);
		void ApplyPluginReferences();

		void SetupMod(Loader.CarbonMod mod, string name, string author, VersionNumber version, string description);
		void Setup(string name, string author, VersionNumber version, string description);

		void ILoadConfig();
		void ILoadDefaultMessages();
		void IInit();
		void IUnload();

		void Load();
		void Unload();

		#region Logging

		void Log(object message);
		void LogError(object message, Exception exception = null);
		void LogWarning(object message);
		void LogError(object message);

		#endregion

		#region Calls

		public T Call<T>(string hook);
		public T Call<T>(string hook, object arg1);
		public T Call<T>(string hook, object arg1, object arg2);
		public T Call<T>(string hook, object arg1, object arg2, object arg3);
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4);
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5);
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6);
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7);
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8);
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9);

		public object Call(string hook);
		public object Call(string hook, object arg1);
		public object Call(string hook, object arg1, object arg2);
		public object Call(string hook, object arg1, object arg2, object arg3);
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4);
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5);
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6);
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7);
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8);
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9);

		public T CallHook<T>(string hook);
		public T CallHook<T>(string hook, object arg1);
		public T CallHook<T>(string hook, object arg1, object arg2);
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3);
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4);
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5);
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6);
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7);
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8);
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9);

		public object CallHook(string hook);
		public object CallHook(string hook, object arg1);
		public object CallHook(string hook, object arg1, object arg2);
		public object CallHook(string hook, object arg1, object arg2, object arg3);
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4);
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5);
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6);
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7);
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8);
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9);

		#endregion
	}
}
