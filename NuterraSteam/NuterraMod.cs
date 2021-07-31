using System.Collections.Generic;

namespace CustomModules
{
    public class NuterraMod : ModBase
    {
        internal static Dictionary<int, int> legacyToSessionIds = new Dictionary<int, int>();

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
		}
	}
}