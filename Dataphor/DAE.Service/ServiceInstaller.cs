/*
	Alphora Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/

using System;
using System.Reflection;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;
using Microsoft.Win32;

namespace Alphora.Dataphor.DAE.Service
{
	/// <summary>Installs the Dataphor DAE as a service on the target machine</summary>
	[RunInstaller(true)]
	public class ProjectInstaller : System.Configuration.Install.Installer
	{
		private ServiceProcessInstaller FServiceProcessInstaller;
		private ServiceInstaller FServiceInstaller;
		
		public ProjectInstaller()
		{
			FServiceProcessInstaller = new ServiceProcessInstaller();
			FServiceProcessInstaller.Account = ServiceAccount.LocalSystem;

			FServiceInstaller = new ServiceInstaller();
			FServiceInstaller.StartType = ServiceStartMode.Automatic;

			//Add installers to the collection.
			Installers.AddRange(new Installer[] { FServiceInstaller, FServiceProcessInstaller });
		}

		private void Prepare()
		{
			string LServiceName = Context.Parameters["ServiceName"];
			if (LServiceName == null)
				LServiceName = Alphora.Dataphor.DAE.Server.ServerService.GetServiceName();

			FServiceInstaller.DisplayName = LServiceName;
			FServiceInstaller.ServiceName = LServiceName;
			FServiceInstaller.Description = "Provides platform services for Dataphor applications.";
		}

		public override void Install(IDictionary AStateSaver)
		{
			Prepare();
			base.Install(AStateSaver);
			
			string LServiceKey = String.Format("HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\{0}", FServiceInstaller.ServiceName);
			string LImagePath = Registry.GetValue(LServiceKey, "ImagePath", null) as String;
			if (LImagePath != null)
				Registry.SetValue(LServiceKey, "ImagePath", String.Format("{0} -name \"{1}\"", LImagePath, FServiceInstaller.ServiceName));
			else
				throw new InvalidOperationException("Could not retrieve service ImagePath from registry.");
		}

		public override void Uninstall(IDictionary ASavedState)
		{
			Prepare();
			base.Uninstall(ASavedState);
		}
	}
}