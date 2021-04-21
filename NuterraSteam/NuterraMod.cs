using System;
using System.Reflection;
using UnityEngine;

namespace CustomModules
{
    public class NuterraMod : ModBase
    {
		public override void Init()
		{
			JSONBlockLoader.RegisterModuleLoader(new NuterraModuleLoader());
		}

		public override void DeInit()
		{
		}
	}
}