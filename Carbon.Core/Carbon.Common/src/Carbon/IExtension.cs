using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carbon
{
	public interface IExtension
	{
		void Install();
		void Uninstall();
	}
}
