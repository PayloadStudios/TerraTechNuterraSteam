using Newtonsoft.Json.Linq;
using System;
using System.Reflection;
using UnityEngine;

namespace CustomModules
{
    public class MyModuleLoader : JSONModuleLoader
	{
		// This method should add a module to the TankBlock prefab
		public override bool CreateModuleForBlock(int blockID, ModdedBlockDefinition def, TankBlock block, JToken jToken)
		{
			if (jToken.Type == JTokenType.Object)
			{
				JObject jData = (JObject)jToken;

				// Add a module
				ModuleAltimeter module = GetOrAddComponent<ModuleAltimeter>(block);

				// Optionally, we can get our default values from a template
				ModuleAltimeter template = ManSpawn.inst.GetBlockPrefab(BlockTypes.GSOAltimeter_111).GetComponent<ModuleAltimeter>();
				//module.m_Priority = template.m_Priority;

				// Now we try and get values from the JSON and apply them to the added module
				//module.m_Priority = TryParse(jData, "Priority", 1);

				return true;
			}
			return false;
		}

		// This is the JSON key that we check for in custom blocks
		public override string GetModuleKey()
		{
			return "MyModule";
		}
	}
}