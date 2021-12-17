using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomModules
{
	public static class TTReferences
	{
		private static bool sInited = false;

		private static Dictionary<string, GameObject> sBlocksByName;
		private static Dictionary<int, GameObject> sBlocksByID;

		private static Dictionary<string, GameObject> originalBlocksByName;
		private static Dictionary<int, GameObject> originalBlocksByID;

		private static Dictionary<string, Material> sMaterials;
		private static Dictionary<Type, Dictionary<string, UnityEngine.Object>> sObjectsByType;

		private static readonly Dictionary<string, string> materialRenames = new Dictionary<string, string> {
			{ "Sparks", "Mat_FX_Sparks" }
		};

		public static Material kMissingTextureTankBlock;

		private static string TrimForSafeSearch(string Value) 
			=> Value.Replace("(", "").Replace(")", "").Replace("_", "").Replace(" ", "").ToLower();


		public static void TryInit()
		{
			if (!sInited)
			{
				sInited = true;
				originalBlocksByName = new Dictionary<string, GameObject>();
				originalBlocksByID = new Dictionary<int, GameObject>();
				sBlocksByName = new Dictionary<string, GameObject>();
				sBlocksByID = new Dictionary<int, GameObject>();
				sMaterials = new Dictionary<string, Material>();
				sObjectsByType = new Dictionary<Type, Dictionary<string, UnityEngine.Object>>();

				foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
				{
					try
					{
						if (go.GetComponent<TankBlock>() is TankBlock block)
						{
							GameObject copy = GameObject.Instantiate(go);
							copy.SetActive(false);

							String blockName = TrimForSafeSearch(go.name);
							sBlocksByName[blockName] = copy;
							originalBlocksByName[blockName] = go;

							BlockTypes blockType = block.BlockType;
							if (Enum.IsDefined(typeof(BlockTypes), blockType))
                            {
								string enumName = TrimForSafeSearch(blockType.ToString());
								sBlocksByName[enumName] = copy;
								originalBlocksByName[enumName] = go;
							}

							Visible v = go.GetComponent<Visible>();
							if (v != null)
							{
								sBlocksByID[v.ItemType] = copy;
								originalBlocksByID[v.ItemType] = go;
							}
						}
					}
					catch { /*fail silently*/ }
				}

				foreach (Material mat in Resources.FindObjectsOfTypeAll<Material>())
				{
					sMaterials[mat.name] = mat;
					LoggingWrapper.Debug("[Nuterra] Registering MATERIAL " + mat.name);
				}

				foreach(Shader shader in Resources.FindObjectsOfTypeAll<Shader>())
				{
					if(shader.name == "StandardTankBlock")
					{
						kMissingTextureTankBlock = new Material(shader);
						kMissingTextureTankBlock.SetColor("_EmissionColor", Color.magenta);
					}
				}
				
			}
		}

		public static bool GetReferenceFromBlockResource(string blockPath, out object reference)
		{
			reference = null;
			int separator = blockPath.IndexOfAny(new char[] { '.', '/' });
			if (separator == -1)
			{
				LoggingWrapper.Warn("Reference path is invalid! Expected block name and path to GameObject (" + blockPath + ")");
				return false;
			}
			string blockReferenceString = blockPath.Substring(0, separator);

			GameObject refBlock;
			if (int.TryParse(blockReferenceString, out int ID))
				refBlock = FindBlockReferenceByID(ID);
			else
				refBlock = FindBlockReferenceByName(blockReferenceString);

			if (refBlock == null)
			{
				LoggingWrapper.Warn("Reference block is nonexistent! (" + blockReferenceString + ")");
				return false;
			}
			string sRefPath = blockPath.Substring(separator + 1);

			reference = refBlock.transform.RecursiveFindWithProperties(sRefPath);

			// Copy if is reference to Unity obj
			/*
			if (reference is GameObject obj)
            {
				GameObject copy = GameObject.Instantiate(obj);
				copy.SetActive(false);
				reference = copy;
			}
			else if (reference is Component c)
            {
				GameObject copy = GameObject.Instantiate(c.gameObject);
				copy.SetActive(false);
				reference = copy;
			}
			*/
			if (reference == null)
			{
				LoggingWrapper.Warn("Reference result is null! (block" + blockReferenceString + ", path " + sRefPath + ")");
				return false;
			}
			return true;
		}

		// Get original block for modification
		public static GameObject FindOriginalBlockFromString(string input)
        {
			if (int.TryParse(input, out int id))
				return FindOriginalBlockByID(id);
			else
				return FindOriginalBlockByName(input);
		}
		public static GameObject FindOriginalBlockByName(string name)
        {
			TryInit();
			string safeName = TrimForSafeSearch(name);
			if (originalBlocksByName.TryGetValue(safeName, out GameObject result)) {
				return result;
			}
			return null;
		}
		public static GameObject FindOriginalBlockByID(int id)
        {
			TryInit();
			if (originalBlocksByID.TryGetValue(id, out GameObject result))
				return result;
			return null;
		}

		// Get copy of the original block to modify - we don't want to modify the original block itself
		// Will check block IDs first
		public static GameObject FindBlockReferenceFromString(string input)
		{
			if (int.TryParse(input, out int id))
				return FindBlockReferenceByID(id);
			else
				return FindBlockReferenceByName(input);
		}

		public static GameObject FindBlockReferenceByName(string name)
		{
			TryInit();
			string safeName = TrimForSafeSearch(name);
			if (sBlocksByName.TryGetValue(safeName, out GameObject result))
			{
				return result;
			}
			return null;
		}

		public static GameObject FindBlockReferenceByID(int id)
		{
			TryInit();
			if (sBlocksByID.TryGetValue(id, out GameObject result))
				return result;
			return null;
		}

		public static Material FindMaterial(string name)
		{
			TryInit();
			if (materialRenames.TryGetValue(name, out string currentName))
            {
				name = currentName;
            }
			if (sMaterials.TryGetValue(name, out Material result))
			{
				return result;
			}

			LoggingWrapper.Error($"[Nuterra] FAILED to find material with name " + name);
			return null;
		}

		public static bool TryFind(string name, ModContents mod, Type type, out object result)
		{
			TryInit();
			UnityEngine.Object obj;
			if (mod != null)
			{
				// Try our mod resources first
				obj = mod.FindAsset(name);
				if (obj != null && type.IsAssignableFrom(obj.GetType()))
				{
					result = obj;
					return true;
				}
			}

			// One time cache each type for the base game assets
			if (!sObjectsByType.TryGetValue(type, out Dictionary<string, UnityEngine.Object> dictionary))
			{
				try
				{
					dictionary = new Dictionary<string, UnityEngine.Object>();
					foreach (UnityEngine.Object t in Resources.FindObjectsOfTypeAll(type))
					{
						dictionary[t.name] = t;
					}
					sObjectsByType.Add(type, dictionary);
				}
				catch (Exception e)
                {
					LoggingWrapper.Error(e, $"[Nuterra] ERROR caching assets of type {type}");
                }
			}

			if (dictionary.TryGetValue(name, out obj))
			{
				result = obj;
				return true;
			}
			result = null;
			return false;
		}

		// This will search your mod container and all base game assets
		// TTQMM Ref: Sorta like GameObjectJSON.GetObjectFromUserResources
		public static bool TryFind<T>(string name, ModContents mod, out T result) where T : UnityEngine.Object
		{
			TryInit();

			UnityEngine.Object obj;

			// Try our mod resources first
			if (mod != null)
			{
				obj = mod.FindAsset(name);
				if (obj != null && obj is T)
				{
					result = obj as T;
					return true;
				}
			}

			// One time cache each type for the base game assets
			if (!sObjectsByType.TryGetValue(typeof(T), out Dictionary<string, UnityEngine.Object> dictionary))
			{
				dictionary = new Dictionary<string, UnityEngine.Object>();
				foreach (T t in Resources.FindObjectsOfTypeAll<T>())
				{
					dictionary[t.name] = t;
				}
				sObjectsByType.Add(typeof(T), dictionary);
			}

			if (dictionary.TryGetValue(name, out obj))
			{
				result = obj as T;
				return true;
			}
			result = null;
			return false;
		}
		
		public static T Find<T>(string name, ModContents mod) where T : UnityEngine.Object
		{
			if (TryFind<T>(name, mod, out T result))
				return result;
			return null;
		}

		// Reflection helper, finding Types and caching this info
		private static Dictionary<string, Type> sTypeCache = new Dictionary<string, Type>();
		private static Assembly AssemblyResolver(AssemblyName arg)
		{
			return typeof(GameObject).Assembly;
		}
		private static Type TypeResolver(Assembly arg1, string arg2, bool arg3)
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				var type = assembly.GetType(arg2, false, arg3);
				if (type != null)
					return type;
			}
			return null;
		}

		public static Type GetType(string typeName)
		{
			// See if we already found this type
			if (sTypeCache.TryGetValue(typeName, out Type type))
				return type;

			// Try getting the type with the exact name provided
			type = Type.GetType(typeName, new Func<AssemblyName, Assembly>(AssemblyResolver), new Func<Assembly, string, bool, Type>(TypeResolver), false, true);
			// Fallback, try with UnityEngine.
			if (type == null)
				type = Type.GetType($"UnityEngine.{typeName}", new Func<AssemblyName, Assembly>(AssemblyResolver), new Func<Assembly, string, bool, Type>(TypeResolver), false, true);

			// Cache and return
			sTypeCache.Add(typeName, type);
			return type;
		}
	}
}
