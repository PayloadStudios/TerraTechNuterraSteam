using System;
using System.Reflection;
using UnityEngine;

namespace CustomModules
{
    public class MyMod : ModBase
    {
		public override void Init()
		{
			JSONBlockLoader.RegisterModuleLoader(new MyModuleLoader());
		}

		public override void DeInit()
		{
		}
	}
}