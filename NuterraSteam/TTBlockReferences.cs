using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomModules
{
	public static class TTBlockReferences
	{
		private static bool sInited = false;
		private static Dictionary<string, GameObject> sBlocksByName;
		private static Dictionary<int, GameObject> sBlocksByID;
		private static Dictionary<string, Material> sMaterials;

		public static Material kMissingTextureTankBlock;

		private static string TrimForSafeSearch(string Value) 
			=> Value.Replace("(", "").Replace(")", "").Replace("_", "").Replace(" ", "").ToLower();


		private static void TryInit()
		{
			if (!sInited)
			{
				sInited = true;

				sBlocksByName = new Dictionary<string, GameObject>();
				sBlocksByID = new Dictionary<int, GameObject>();
				sMaterials = new Dictionary<string, Material>();

				foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
				{
					try
					{
						if (go.GetComponent<TankBlock>())
						{
							sBlocksByName.Add(TrimForSafeSearch(go.name), go);
							Visible v = go.GetComponent<Visible>();
							if (v != null)
							{
								sBlocksByID.Add(v.ItemType, go);
							}
						}
					}
					catch { /*fail silently*/ }
				}

				foreach (Material mat in Resources.FindObjectsOfTypeAll<Material>())
				{
					sMaterials.Add(mat.name, mat);
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

		// Will check block IDs first
		public static GameObject FindBlockFromString(string input)
		{
			if (int.TryParse(input, out int id))
				return FindBlockByID(id);
			else
				return FindBlockByName(input);
		}

		public static GameObject FindBlockByName(string name)
		{
			TryInit();
			if(sBlocksByName.TryGetValue(name, out GameObject result))
				return result;
			return null;
		}

		public static GameObject FindBlockByID(int id)
		{
			TryInit();
			if (sBlocksByID.TryGetValue(id, out GameObject result))
				return result;
			return null;
		}

		public static Material FindMaterial(string name)
		{
			TryInit();
			if (sMaterials.TryGetValue(name, out Material result))
				return result;
			return null;
		}

		private static Dictionary<Type, Dictionary<string, UnityEngine.Object>> sObjectsByType;

		// This will search your mod container and all base game assets
		public static T Find<T>(string name, ModContents mod) where T : UnityEngine.Object
		{
			TryInit();

			// Try our mod resources first
			UnityEngine.Object obj = mod.FindAsset(name);
			if(obj != null && obj is T)
			{
				return obj as T;
			}

			// One time cache each type for the base game assets
			if(!sObjectsByType.TryGetValue(typeof(T), out Dictionary<string, UnityEngine.Object> dictionary))
			{
				dictionary = new Dictionary<string, UnityEngine.Object>();
				foreach(T t in Resources.FindObjectsOfTypeAll<T>())
				{
					dictionary.Add(t.name, t);
				}
				sObjectsByType.Add(typeof(T), dictionary);
			}

			if(dictionary.TryGetValue(name, out obj))
			{
				return obj as T;
			}
			return null;
		}
	}
}
