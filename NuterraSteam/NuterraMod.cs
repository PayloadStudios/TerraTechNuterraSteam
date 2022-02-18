﻿using System;
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
        internal static FieldInfo m_CurrentSession = typeof(ManMods).GetField("m_CurrentSession", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal static Dictionary<string, int> blockIDToLegacyIDs = new Dictionary<string, int>();
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
                if (blockIDToLegacyIDs.TryGetValue(blockName, out int legacyID))
                {
                    try
                    {
                        legacyToSessionIds.Add(legacyID, sessionID);
                        LoggingWrapper.Info("Registering block {Block} with legacy ID {LegacyID} to session ID {SessionID}", blockName, legacyID, sessionID);
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
                    catch (Exception e)
                    {
                        LoggingWrapper.Fatal(e);
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
            ModSessionInfo sessionInfo = (ModSessionInfo)m_CurrentSession.GetValue(Singleton.Manager<ManMods>.inst);

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