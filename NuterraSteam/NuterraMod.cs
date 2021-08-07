using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


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

        internal static string Format(string input)
        {
            // JavaScriptSerializer doesn't accept commented-out JSON,
            // so we'll strip them out ourselves;
            input = Regex.Replace(input, @"^\s*//.*$", "", RegexOptions.Multiline);  // removes line comments like this
            input = Regex.Replace(input, @"/\*(\s|\S)*?\*/", "", RegexOptions.Multiline); /* comments like this */
            // Console.WriteLine(input);
            input = Regex.Replace(input, @"([,\[\{\]\}\." + Regex.Escape("\"") + @"0-9]|null)\s*//[^\n]*\n", "$1\n", RegexOptions.Multiline);    // Removes mixed JSON comments
            // Console.WriteLine(input);
            input = Regex.Replace(input, @",\s*([\}\]])", "\n$1", RegexOptions.Multiline);  // remove trailing ,
            // Console.WriteLine(input);
            return input.Replace("JSONBLOCK", "Deserializer");
        }

        public static void TryRegisterIDMapping(int legacyID, int sessionID)
        {
            legacyToSessionIds.Add(legacyID, sessionID);
        }

        public static void TryRegisterUnofficialBlock(int blockID, ModdedBlockDefinition blockDef)
        {
            string json = blockDef.m_Json.text;
            string text = Format(json);
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
            legacyToSessionIds.Clear();
		}
	}
}