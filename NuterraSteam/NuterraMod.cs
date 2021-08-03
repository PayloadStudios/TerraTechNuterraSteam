using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CustomModules.NuterraSteam.LegacyBlockLoader;


namespace CustomModules
{
    public class NuterraMod : ModBase
    {
        internal static Dictionary<int, int> legacyToSessionIds = new Dictionary<int, int>();
        public static int LoadOrder = 2;
        internal static string TTSteamDir = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name == "Assembly-CSharp").First().Location
            .Replace("Assembly-CSharp.dll", ""), @"../../"
        ));

        public static void TryRegisterUnofficialBlock(int blockID, ModdedBlockDefinition blockDef)
        {
            string json = blockDef.m_Json.text;
            string text = UnofficialBlock.Format(json);
            try
            {
                JObject jObj = JObject.Parse(text);
                if (jObj.TryGetValue(NuterraModuleLoader.ModuleID, out JToken nuterra) && nuterra.Type == JTokenType.Object)
                {
                    JObject UnofficialJson = (JObject) nuterra;
                    if (UnofficialJson.TryGetValue("ID", out JToken value))
                    {
                        if (value.Type == JTokenType.Integer)
                        {
                            legacyToSessionIds.Add(value.ToObject<int>(), blockID);
                        }
                        else if (value.Type == JTokenType.String && int.TryParse(value.ToString(), out int ID))
                        {
                            legacyToSessionIds.Add(ID, blockID);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to read Block {blockDef.m_BlockDisplayName} ({blockDef.m_BlockIdentifier}) json");
                Console.WriteLine(e);
            }
        }

        public static bool TryGetSessionID(int legacyId, out int newId)
        {
            /*
             * Console.WriteLine($"Trying to get key {legacyId}");
            foreach (int key in legacyToSessionIds.Keys)
            {
                Console.WriteLine(key);
            }
            */
            return legacyToSessionIds.TryGetValue(legacyId, out newId);
        }

        public override void EarlyInit()
        {
            TTReferences.TryInit();
        }

        public override bool HasEarlyInit()
        {
            return true;
        }

        public override void Init()
		{
			JSONBlockLoader.RegisterModuleLoader(new NuterraModuleLoader());
		}

		public override void DeInit()
		{
            Console.WriteLine("DE-INITED NUTERRASTEAM");
            legacyToSessionIds.Clear();
		}
	}
}