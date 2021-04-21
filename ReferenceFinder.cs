using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Nuterra.BlockInjector
{
	// TODO: Make this part of official codebase
	public static class ReferenceFinder
	{
		public static bool GetReferenceFromBlockResource(string blockPath, out object reference)
		{
			reference = null;
			int separator = blockPath.IndexOfAny(new char[] { '.', '/' });
			if (separator == -1)
			{
				Console.WriteLine("Reference path is invalid! Expected block name and path to GameObject (" + blockPath + ")");
				return false;
			}
			string sRefBlock = blockPath.Substring(0, separator);

			GameObject refBlock;
			if (int.TryParse(sRefBlock, out int ID))
				refBlock = GetBlockFromAssetTable(ID);
			else
				refBlock = GetBlockFromAssetTable(sRefBlock);

			if (refBlock == null)
			{
				Console.WriteLine("Reference block is nonexistent! (" + sRefBlock + ")");
				return false;
			}
			string sRefPath = blockPath.Substring(separator + 1);
			reference = refBlock.transform.RecursiveFindWithProperties(sRefPath);
			if (reference == null)
			{
				Console.WriteLine("Reference result is null! (block" + sRefBlock + ", path " + sRefPath + ")");
				return false;
			}
			return true;
		}

		public static GameObject GetBlockFromAssetTable(string BlockName)
		{
			GameBlocksByName(BlockName, out GameObject Block);
			return Block;
		}
		public static GameObject GetBlockFromAssetTable(int BlockID)
		{
			GameBlocksByID(BlockID, out GameObject Block);
			return Block;
		}

		private static string TrimForSafeSearch(string Value)
		{
			return Value.Replace("(", "").Replace(")", "").Replace("_", "").Replace(" ", "").ToLower();
		}


		private static Dictionary<string, GameObject> _gameBlocksNameDict;
		private static Dictionary<int, GameObject> _gameBlocksIDDict;
		public static bool GameBlocksByName(string ReferenceName, out GameObject Block)
		{
			if (_gameBlocksNameDict == null)
			{
				PopulateRefDictionaries();
			}
			return _gameBlocksNameDict.TryGetValue(TrimForSafeSearch(ReferenceName), out Block);
		}

		public static bool GameBlocksByID(int ReferenceID, out GameObject Block)
		{
			if (_gameBlocksIDDict == null)
			{
				PopulateRefDictionaries();
			}
			return _gameBlocksIDDict.TryGetValue(ReferenceID, out Block);
		}

		private static void PopulateRefDictionaries()
		{
			_gameBlocksIDDict = new Dictionary<int, GameObject>();
			_gameBlocksNameDict = new Dictionary<string, GameObject>();
			var gos = Resources.FindObjectsOfTypeAll<GameObject>();
			foreach (var go in gos)
			{
				try
				{
					if (go.GetComponent<TankBlock>())
					{
						_gameBlocksNameDict.Add(TrimForSafeSearch(go.name), go);
						Visible v = go.GetComponent<Visible>();
						if (v != null)
						{
							_gameBlocksIDDict.Add(v.ItemType, go);
						}
					}
				}
				catch { /*fail silently*/ }
			}
		}

		static Dictionary<string, Type> stringtypecache = new Dictionary<string, Type>();
		public static Type GetType(string Name)
		{
			if (stringtypecache.TryGetValue(Name, out Type type)) return type;
			type = Type.GetType(Name, new Func<AssemblyName, Assembly>(AssemblyResolver), new Func<Assembly, string, bool, Type>(TypeResolver), false, true);
			if (type == null)
			{
				type = Type.GetType("UnityEngine." + Name, new Func<AssemblyName, Assembly>(AssemblyResolver), new Func<Assembly, string, bool, Type>(TypeResolver), false, true);
				if (type != null)
				{
					Console.WriteLine("GetType(string): Warning! \"UnityEngine.\" should be added before search term \"" + Name + "\" to avoid searching twice!");
				}
				else
				{
					Console.WriteLine("GetType(string): " + Name + " is not a known type! It may need the proper namespace defined before it (ex: \"UnityEngine.LineRenderer\"), or it needs the class's Assembly's `FullName` (ex: \"" + typeof(ModuleFirstPerson).Assembly.FullName + "\", in which it'd be used as \"" + typeof(ModuleFirstPerson).AssemblyQualifiedName + "\"");
					stringtypecache.Add(Name, null);
					return null;
				}
			}
			stringtypecache.Add(Name, type);
			return type;
		}

		private static Type TypeResolver(Assembly arg1, string arg2, bool arg3)
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				var type = assembly.GetType(arg2, false, arg3);
				if (type != null) return type;
			}
			return null;
		}

		private static Assembly AssemblyResolver(AssemblyName arg)
		{
			return typeof(GameObject).Assembly;
		}
	}
}
