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
        internal static Dictionary<string, int> blockNameToLegacyIDs = new Dictionary<string, int>();
        internal static Dictionary<int, int> legacyToSessionIds = new Dictionary<int, int>();
        internal static List<int> nonLegacyBlocks = new List<int>();
        internal static List<BlockRotationTable.GroupIndexLookup> addedRotationGroups = new List<BlockRotationTable.GroupIndexLookup>();
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
            // LoggingWrapper.Log(input);
            input = Regex.Replace(input, @"([,\[\{\]\}\." + Regex.Escape("\"") + @"0-9]|null)\s*//[^\n]*\n", "$1\n", RegexOptions.Multiline);    // Removes mixed JSON comments
            // LoggingWrapper.Log(input);
            input = Regex.Replace(input, @",\s*([\}\]])", "\n$1", RegexOptions.Multiline);  // remove trailing ,
            // LoggingWrapper.Log(input);
            return input.Replace("JSONBLOCK", "Deserializer");
        }

        public static void RegisterUnofficialBlocks(ModSessionInfo newSessionInfo)
        {
            foreach (KeyValuePair<int, string> keyValuePair in newSessionInfo.BlockIDs)
            {
                int sessionID = keyValuePair.Key;
                string blockName = keyValuePair.Value;
                if (blockNameToLegacyIDs.TryGetValue(blockName, out int legacyID))
                {
                    try
                    {
                        legacyToSessionIds.Add(legacyID, sessionID);
                        LoggingWrapper.Info("Registering block {Block} with legacy ID {LegacyID} to session ID {SessionID}", blockName, legacyID, sessionID);
                    }
                    catch (ArgumentException e)
                    {
                        string currentBlockName = newSessionInfo.BlockIDs[legacyToSessionIds[legacyID]];
                        string currentModName = ModUtils.GetModFromCompoundId(currentBlockName);
                        string modName = ModUtils.GetModFromCompoundId(blockName);
                        LoggingWrapper.Error(e);
                        if (modName == "LegacyBlockLoader")
                        {
                            LoggingWrapper.Warn("Legacy Block {LegacyID} already has Official block {Block} assigned to it", legacyID, currentBlockName);
                        }
                        else if (currentModName == "LegacyBlockLoader")
                        {
                            legacyToSessionIds[legacyID] = sessionID;
                            LoggingWrapper.Warn("Reassigning Official block {Block} to replace Legacy Block {LegacyID}", blockName, legacyID);
                        }
                        else
                        {
                            LoggingWrapper.Error("Legacy Block {LegacyID} can be assigned to official blocks {Block1} or {Block2}. Resolving to {Block}", legacyID, blockName, currentBlockName, currentBlockName);
                        }
                    }
                }
                else
                {
                    LoggingWrapper.Debug("Block {block} does not have an associated legacy ID", blockName);
                }
            }
        }

        public static bool TryGetSessionID(int legacyId, out int newId)
        {
            /*
             * LoggingWrapper.Log($"Trying to get key {legacyId}");
            foreach (int key in legacyToSessionIds.Keys)
            {
                LoggingWrapper.Log(key);
            }
            */
            return legacyToSessionIds.TryGetValue(legacyId, out newId);
        }

        public override void EarlyInit()
        {
            LoggingWrapper.Init();
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
            // Reset Paired Block Unlock Table
            Globals.inst.m_BlockPairsList.m_BlockPairs =
                Globals.inst.m_BlockPairsList.m_BlockPairs
                .Where(pair => !nonLegacyBlocks.Contains((int) pair.m_Block) && !legacyToSessionIds.ContainsValue((int) pair.m_Block))
                .ToArray();

            nonLegacyBlocks.Clear();
            legacyToSessionIds.Clear();
            
            //Reset Block Rotation Table
            BlockRotationTable BlockRotationTable = (BlockRotationTable)NuterraModuleLoader.m_BlockRotationTable.GetValue(Singleton.Manager<ManTechBuilder>.inst);
            foreach (BlockRotationTable.GroupIndexLookup lookup in addedRotationGroups)
            {
                if (!BlockRotationTable.m_BlockRotationGroupIndex.Remove(lookup))
                {
                    LoggingWrapper.Error("[NuterraSteam] ERROR - FAILED TO REMOVE ADDED BlockRotationTable.GroupIndexLookup");
                }
            }
            addedRotationGroups.Clear();
        }
	}
}