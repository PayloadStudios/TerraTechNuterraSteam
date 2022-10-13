using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq;
using CustomModules.Logging;


namespace CustomModules
{
    public class NuterraMod : ModBase
    {
        internal static FieldInfo m_CurrentSession = typeof(ManMods).GetField("m_CurrentSession", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal static Dictionary<string, int> blockIDToLegacyIDs = new Dictionary<string, int>();
        internal static Dictionary<int, int> legacyToSessionIds = new Dictionary<int, int>();
        internal static List<int> nonLegacyBlocks = new List<int>();
        internal static List<BlockRotationTable.GroupIndexLookup> addedRotationGroups = new List<BlockRotationTable.GroupIndexLookup>();

        internal static Logger logger;
        internal static Logger modLogger;
        internal static Logger.TargetConfig LoggerTarget = new Logger.TargetConfig {
            path = "NuterraBlocks",
            layout = "${longdate} ${level:uppercase=true:padding=-5:alignmentOnTruncation=left} ${message}  ${exception}",
            keepOldFiles = false
        };

        // This doesn't get cleaned up, since rotation groups will always be the same as long as NuterraMod is here
        internal static Dictionary<string, string> rotationGroupsMap = new Dictionary<string, string>();

        public static int LoadOrder = 2;
        internal static string TTSteamDir = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name == "Assembly-CSharp").First().Location
            .Replace("Assembly-CSharp.dll", ""), @"../../"
        ));
        internal static string NuterraLogsDir = Path.Combine(TTSteamDir, "Logs", "NuterraBlocks");

        internal static string Format(string input)
        {
            // JavaScriptSerializer doesn't accept commented-out JSON,
            // so we'll strip them out ourselves;
            input = Regex.Replace(input, @"^\s*//.*$", "", RegexOptions.Multiline);  // removes line comments like this
            input = Regex.Replace(input, @"/\*(\s|\S)*?\*/", "", RegexOptions.Multiline); /* comments like this */
            // NuterraMod.modLogger.Log(input);
            input = Regex.Replace(input, @"([,\[\{\]\}\." + Regex.Escape("\"") + @"0-9]|null)\s*//[^\n]*\n", "$1\n", RegexOptions.Multiline);    // Removes mixed JSON comments
            // NuterraMod.modLogger.Log(input);
            input = Regex.Replace(input, @",\s*([\}\]])", "\n$1", RegexOptions.Multiline);  // remove trailing ,
            // NuterraMod.modLogger.Log(input);
            return input.Replace("JSONBLOCK", "Deserializer");
        }

        public static void RegisterUnofficialBlocks(ModSessionInfo newSessionInfo)
        {
            foreach (KeyValuePair<int, string> keyValuePair in newSessionInfo.BlockIDs)
            {
                int sessionID = keyValuePair.Key;
                string blockName = keyValuePair.Value;
                if (blockIDToLegacyIDs.TryGetValue(blockName, out int legacyID))
                {
                    try
                    {
                        legacyToSessionIds.Add(legacyID, sessionID);
                        NuterraMod.modLogger.Info($"Registering block {blockName} with legacy ID {legacyID} to session ID {sessionID}");
                    }
                    catch (ArgumentException e)
                    {
                        int currentSessionID = legacyToSessionIds[legacyID];
                        string currentBlockName = newSessionInfo.BlockIDs[currentSessionID];
                        if (sessionID != currentSessionID)
                        {
                            string currentModName = ModUtils.GetModFromCompoundId(currentBlockName);
                            string modName = ModUtils.GetModFromCompoundId(blockName);
                            if (modName == "LegacyBlockLoader")
                            {
                                NuterraMod.modLogger.Warn($"Legacy Block {legacyID} already has Official block {currentBlockName} assigned to it");
                            }
                            else if (currentModName == "LegacyBlockLoader")
                            {
                                legacyToSessionIds[legacyID] = sessionID;
                                NuterraMod.modLogger.Warn($"Reassigning Official block {blockName} to replace Legacy Block {legacyID}");
                            }
                            else
                            {
                                NuterraMod.modLogger.Error($"Legacy Block {legacyID} can be assigned to official blocks {blockName} or {currentBlockName}. Resolving to {currentBlockName}");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        NuterraMod.modLogger.Fatal(e);
                    }
                }
                else
                {
                    NuterraMod.modLogger.Debug($"Block {blockName} does not have an associated legacy ID");
                }
            }
        }

        public static bool TryGetSessionID(int legacyId, out int newId)
        {
            return legacyToSessionIds.TryGetValue(legacyId, out newId);
        }

        public static bool TryGetLegacyID(string blockID, out int legacyID)
        {
            return blockIDToLegacyIDs.TryGetValue(blockID, out legacyID);
        }

        internal static bool Inited = false;

        public void ManagedEarlyInit()
        {
            if (!Inited)
            {
                Inited = true;
                DirectoryInfo info = new DirectoryInfo(NuterraLogsDir);
                if (info.Exists)
                {
                    foreach (FileInfo file in info.GetFiles())
                    {
                        if (file.Exists && file.Extension == ".log")
                        {
                            file.Delete();
                        }
                    }
                }

                modLogger = new Logger("NuterraSteam");
            }
        }

        public override void EarlyInit()
        {
            this.ManagedEarlyInit();
        }

        public override bool HasEarlyInit()
        {
            return true;
        }

        public static void SetupMetadata()
        {
            ManMods manMods = Singleton.Manager<ManMods>.inst;
            ModSessionInfo sessionInfo = (ModSessionInfo)m_CurrentSession.GetValue(manMods);

            // Clear this map. We never clear blockNametoLegacyIDs, since te keys guaranteed unique, and can stay indefinitely
            // As such, we will repopulate the legacyToSessionIds map based on historical data (only with stuff present in the current mod sesion)
            legacyToSessionIds.Clear();

            // Populate the reverse lookup, since it won't have been created yet
            Dictionary<string, int> blockIDToSessionID = new Dictionary<string, int>();
            if (blockIDToLegacyIDs.Count() > 0)
            {
                foreach (KeyValuePair<int, string> keyValuePair in sessionInfo.BlockIDs)
                {
                    blockIDToSessionID.Add(keyValuePair.Value, keyValuePair.Key);
                }
            }

            // Populate new session IDs based on the old block data
            // The remaining legacy to session IDs will be processed by the module loader when it runs on new blocks
            //  - We have the guarantee that Module loader will only run once, and the blocks will stay around indefinitely
            Dictionary<int, string> legacyIDToBlockName = new Dictionary<int, string>();
            Dictionary<int, string> legacyIDToModName = new Dictionary<int, string>();
            foreach (KeyValuePair<string, int> keyValuePair in blockIDToLegacyIDs)
            {
                string compoundName = keyValuePair.Key;
                // Only check if block ID is still present
                if (blockIDToSessionID.TryGetValue(compoundName, out int sessionID))
                {
                    // If there is conflict between mods for legacy IDs, choose the last block (by ASCII-betical ordering on block name),
                    // and choose the one from the last mod if the block names are tied
                    string mod = ModUtils.GetModFromCompoundId(compoundName);
                    string block = ModUtils.GetAssetFromCompoundId(compoundName);
                    int legacyID = keyValuePair.Value;

                    bool add = false;
                    if (legacyIDToBlockName.TryGetValue(legacyID, out string currentBlock))
                    {
                        int compareBlockNames = String.Compare(block, currentBlock);
                        if (compareBlockNames > 0)
                        {
                            // new is higher
                            legacyIDToBlockName[legacyID] = block;
                            legacyIDToModName[legacyID] = mod;
                            add = true;
                        }
                        else if (compareBlockNames == 0)
                        {
                            // block names are equal
                            int compareModNames = String.Compare(mod, legacyIDToModName[legacyID]);
                            if (compareModNames > 0)
                            {
                                // new is higher
                                legacyIDToModName[legacyID] = mod;
                                add = true;
                            }
                            // else - do nothing
                        }
                        // else - do nothing
                    }
                    else
                    {
                        legacyIDToBlockName.Add(legacyID, block);
                        legacyIDToModName.Add(legacyID, mod);
                        add = true;
                    }
                    if (add)
                    {
                        if (legacyToSessionIds.ContainsKey(legacyID))
                        {
                            legacyToSessionIds.Add(legacyID, sessionID);
                        }
                        else
                        {
                            legacyToSessionIds[legacyID] = sessionID;
                        }
                    }
                }
            }

            // block ID reverse lookup doesn't exist yet, so we have to make one ourselves
            Dictionary<string, int> temporaryReverseLookup = new Dictionary<string, int>();
            foreach (KeyValuePair<int, string> keyValuePair in sessionInfo.BlockIDs)
            {
                temporaryReverseLookup.Add(keyValuePair.Value, keyValuePair.Key);
            }

            // Repopulate rotation groups overrides
            foreach (KeyValuePair<string, string> keyValuePair in rotationGroupsMap)
            {
                string CompoundBlockID = keyValuePair.Key;
                string RotationGroup = keyValuePair.Value;

                if (temporaryReverseLookup.TryGetValue(CompoundBlockID, out int blockID))
                {
                    ModdedBlockDefinition blockDef = manMods.FindModdedAsset<ModdedBlockDefinition>(CompoundBlockID);
                    if (blockDef != null)
                    {
                        Visible visible = blockDef.m_PhysicalPrefab.GetComponent<Visible>();
                        if (visible != null)
                        {
                            // We only add rotation groups if block def is valid, and visible is null
                            // AKA we are not running JSONLoaders, and are reusing what's in the component pools
                            AddRotationGroupsOverride(blockID, RotationGroup);
                        }
                    }
                }
            }
        }

        public static void ClearMetadata()
        {
            // Reset Paired Block Unlock Table
            Globals.inst.m_BlockPairsList.m_BlockPairs =
                Globals.inst.m_BlockPairsList.m_BlockPairs
                .Where(pair => !nonLegacyBlocks.Contains((int)pair.m_Block) && !legacyToSessionIds.ContainsValue((int)pair.m_Block))
                .ToArray();

            nonLegacyBlocks.Clear();

            //Reset Block Rotation Table
            BlockRotationTable BlockRotationTable = (BlockRotationTable)m_BlockRotationTable.GetValue(Singleton.Manager<ManTechBuilder>.inst);
            foreach (BlockRotationTable.GroupIndexLookup lookup in addedRotationGroups)
            {
                if (!BlockRotationTable.m_BlockRotationGroupIndex.Remove(lookup))
                {
                    NuterraMod.modLogger.Error("FAILED TO REMOVE ADDED BlockRotationTable.GroupIndexLookup");
                }
            }
            addedRotationGroups.Clear();
        }

        public override void Init()
		{
            SetupMetadata();
			JSONBlockLoader.RegisterModuleLoader(new NuterraModuleLoader());
		}

		public override void DeInit()
		{
            ClearMetadata();
        }

        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
        internal static readonly FieldInfo m_BlockRotationTable = typeof(ManTechBuilder).GetField("m_BlockRotationTable", InstanceFlags);

        internal static BlockRotationTable.GroupIndexLookup AddRotationGroupsOverride(int blockID, string RotationGroupName)
        {
            BlockRotationTable BlockRotationTable = (BlockRotationTable)m_BlockRotationTable.GetValue(Singleton.Manager<ManTechBuilder>.inst);
            BlockRotationTable.GroupIndexLookup newLookup = new BlockRotationTable.GroupIndexLookup
            {
                blockType = blockID,
                groupName = RotationGroupName
            };
            BlockRotationTable.m_BlockRotationGroupIndex.Add(newLookup);
            return newLookup;
        }
	}
}