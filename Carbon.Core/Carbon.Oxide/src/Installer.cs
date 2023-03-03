using Carbon;
using Oxide.Core;

public class Installer : IExtension
{
	public void Install()
	{
		Interface.Initialize();
	}

	public void Uninstall()
	{
		Interface.ShutDown();
	}
}
