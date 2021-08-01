using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;


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

        public bool TryGetSessionID(int legacyId, out int newId)
        {
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