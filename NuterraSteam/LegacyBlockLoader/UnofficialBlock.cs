using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace CustomModules.NuterraSteam.LegacyBlockLoader
{
    public class UnofficialBlock
    {
        public ModdedBlockDefinition blockDefinition;
        public JObject jObject;
        public int ID;

        private interface EnumParser {
            object ParseEnum(int val, object defaultValue);
        }
        private class EnumParser<T> : EnumParser
        {
            Dictionary<int, T> mappingDict;
            public object ParseEnum(int val, object defaultValue)
            {
                if (mappingDict is null)
                {
                    Array values = Enum.GetValues(typeof(T));
                    mappingDict = new Dictionary<int, T>();
                    foreach (T value in values)
                    {
                        mappingDict[Convert.ToInt32(value)] = value;
                    }
                }
                if (mappingDict.TryGetValue(val, out T result))
                {
                    return result;
                }
                return defaultValue;
            }
        }
        private static Dictionary<Type, EnumParser> EnumDict = new Dictionary<Type, UnofficialBlock.EnumParser>();

        private static string StripComments(string input)
        {
            // JavaScriptSerializer doesn't accept commented-out JSON,
            // so we'll strip them out ourselves;
            // NOTE: for safety and simplicity, we only support comments on their own lines,
            // not sharing lines with real JSON
            input = Regex.Replace(input, @"^\s*//.*$", "", RegexOptions.Multiline);  // removes comments like this
            input = Regex.Replace(input, @"^\s*/\*(\s|\S)*?\*/\s*$", "", RegexOptions.Multiline); /* comments like this */
            return input;
        }

        private static T TryParseEnum<T>(int val, T defaultValue) where T : Enum
        {
            if (EnumDict.TryGetValue(typeof(T), out EnumParser parser))
            {
                return (T) parser.ParseEnum(val, defaultValue);
            }
            else
            {
                parser = new EnumParser<T>();
                EnumDict.Add(typeof(T), parser);
                return (T)parser.ParseEnum(val, defaultValue);
            }
        }

        public UnofficialBlock(string path)
        {
            string text = StripComments(File.ReadAllText(path));

            jObject = JObject.Parse(DirectoryBlockLoader.ResolveFiles(text, path));
            UnofficialBlockDefinition unofficialDef = jObject.ToObject<UnofficialBlockDefinition>(new JsonSerializer() { MissingMemberHandling = MissingMemberHandling.Ignore });
            ID = unofficialDef.ID;
            if (unofficialDef.Name is null || unofficialDef.Name.Length == 0)
            {
                unofficialDef.Name = ID.ToString();
            }
            unofficialDef.Grade++;  // Add 1 to Grade, b/c Legacy grade is 0-indexed, official blocks are 1-indexed

            JProperty Grade = jObject.Property("Grade");
            Grade.Value = unofficialDef.Grade;

            JProperty Category = jObject.Property("Category");
            BlockCategories blockCategory = TryParseEnum<BlockCategories>(unofficialDef.Category, BlockCategories.Base);
            Category.Value = blockCategory.ToString();

            JProperty Rarity = jObject.Property("Rarity");
            BlockRarity blockRarity = TryParseEnum<BlockRarity>(unofficialDef.Rarity, BlockRarity.Common);
            Rarity.Value = blockRarity.ToString();

            blockDefinition = new ModdedBlockDefinition();
            blockDefinition.m_BlockIdentifier = ID.ToString();
            blockDefinition.m_BlockDisplayName = unofficialDef.Name;
            blockDefinition.m_BlockDescription = unofficialDef.Description;
            blockDefinition.m_Corporation = TryParseEnum<FactionSubTypes>(unofficialDef.Faction, FactionSubTypes.GSO).ToString();
            blockDefinition.m_Category = blockCategory;
            blockDefinition.m_Rarity = blockRarity;
            blockDefinition.m_Grade = unofficialDef.Grade;
            blockDefinition.m_Price = unofficialDef.Price;
            blockDefinition.m_UnlockWithLicense = true;
            blockDefinition.m_DamageableType = TryParseEnum<ManDamage.DamageableType>(unofficialDef.DamageableType, ManDamage.DamageableType.Standard);
            blockDefinition.m_Mass = unofficialDef.Mass;

            WrapJSON();
        }
        public UnofficialBlock(FileInfo file) => new UnofficialBlock(file.FullName);

        public void WrapJSON()
        {
            JObject wrappedJSON = new JObject();
            wrappedJSON.Add("NuterraBlock", jObject);
            blockDefinition.m_Json = new UnityEngine.TextAsset(wrappedJSON.ToString());
        }
    }
}
