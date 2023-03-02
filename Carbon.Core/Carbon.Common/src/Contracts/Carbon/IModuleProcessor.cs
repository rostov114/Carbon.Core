using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Carbon.Base;
using Carbon.Base.Interfaces;
using Carbon.Hooks;

namespace Carbon.Contracts
{
	public interface IModuleProcessor : IDisposable
	{
		void Init();
		List<IModule> Modules { get; }
	}
}
