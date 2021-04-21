using Newtonsoft.Json.Linq;
using Nuterra.BlockInjector;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CustomModules
{
    public class NuterraModuleLoader : JSONModuleLoader
	{
		// This method should add a module to the TankBlock prefab
		public override bool CreateModuleForBlock(int blockID, ModdedBlockDefinition def, TankBlock block, JToken jToken)
		{
			if (jToken.Type == JTokenType.Object)
			{
				JObject jData = (JObject)jToken;

				// Get the mod contents so we can search for additional assets
				ModContainer container = ManMods.inst.FindMod(def);
				ModContents mod = container != null ? container.Contents : null;
				if(mod == null)
				{
					Debug.LogError("Could not find mod that this unoffical block is part of");
					return false;
				}

				//BlockTypes template = TryParseEnum<BlockTypes>(jData, "TemplateBlock", BlockTypes.GSOBlock_111);
				//BlockPrefabBuilder prefabBuilder = new BlockPrefabBuilder(template);
				//DirectoryBlockLoader.CreateJSONBlock(def.name, def.name, jData);

				// ------------------------------------------------------
				// Basics like name, desc etc. The Official Mod Tool lets us set these already, but we might want to override
				def.m_BlockDisplayName = TryParse(jData, "Name", def.m_BlockDisplayName);
				def.m_BlockDescription = TryParse(jData, "Description", def.m_BlockDescription);
				// Ignore ID
				//def.m_Corporation = TryParse(jData, "Description", def.m_Corporation); TODO: Convert to int and back
				def.m_Category = TryParseEnum(jData, "Category", def.m_Category);
				def.m_Rarity = TryParseEnum(jData, "Rarity", def.m_Rarity);
				def.m_Grade = TryParse(jData, "Grade", def.m_Grade);
				def.m_Price = TryParseEnum(jData, "Price", def.m_Price);
				// TODO: Recipe

				// TODO: IconName - Do we override?
				// TODO: MeshName - Do we override?
				// TODO: ColliderMeshName
				// TODO: SupressBoxColliderFallback
				// TODO: Mass
				// TODO: MeshMaterialName
				// TODO: DamageableType
				// TODO: Fragility
				def.m_MaxHealth = TryParse(jData, "HP", def.m_MaxHealth);

				// ------------------------------------------------------
				// Reference - Copy a vanilla block
				bool keepRenderers = TryParse(jData, "KeepRenderers", true);
				bool keepReferenceRenderers = TryParse(jData, "KeepReferenceRenderers", true);
				bool keepColliders = TryParse(jData, "KeepColliders", true);

				if (jData.TryGetValue("GamePrefabReference", out JToken jRef) && jRef.Type == JTokenType.String)
				{
					// This code block copies our chosen reference block
					// TTQMM REF: BlockPrefabBuilder.Initialize

					BlockTypes blockType = (BlockTypes)Enum.Parse(typeof(BlockTypes), jRef.ToString());

					TankBlock original = ManSpawn.inst.GetBlockPrefab(blockType);
					TankBlock copy = UnityEngine.Object.Instantiate(original);
					TankBlockTemplate fakeTemplate = copy.gameObject.AddComponent<TankBlockTemplate>();
					// Cheeky hack to swap the prefab
					// The official block loader doesn't expect this to happen, but I will assume
					// for now that you are making 100% official or 100% unofficial JSONs
					def.m_PhysicalPrefab = fakeTemplate;

					// TTQMM REF: DirectoryBlockLoader.CreateJSONBlock, the handling of these flags is a bit weird
					if (keepRenderers && !keepColliders)
						RemoveChildren<Collider>(copy);
					if (!keepRenderers && !keepReferenceRenderers)
					{
						RemoveChildren<MeshRenderer>(copy);
						RemoveChildren<TankTrack>(copy);
						RemoveChildren<SkinnedMeshRenderer>(copy);
						RemoveChildren<MeshFilter>(copy);

						if (!keepColliders)
							RemoveChildren<Collider>(copy);
					}

					copy.gameObject.layer = Globals.inst.layerTank;
					copy.gameObject.tag = "TankBlock";

					bool hasRefOffset = jData.TryGetValue("ReferenceOffset", out JToken jOffset);
					bool hasRefRotation = jData.TryGetValue("ReferenceRotationOffset", out JToken jEuler);
					bool hasRefScale = jData.TryGetValue("ReferenceScale", out JToken jScale);

					if (hasRefOffset || hasRefRotation || hasRefScale)
					{
						Vector3 offset = hasRefOffset ? GetVector3(jOffset) : Vector3.zero;
						Vector3 scale = hasRefScale ? GetVector3(jScale) : Vector3.one;
						Vector3 euler = hasRefRotation ? GetVector3(jEuler) : Vector3.zero;

						foreach(Transform child in copy.transform)
						{
							if (hasRefOffset)
								child.localPosition = offset;
							if (hasRefRotation)
								child.localEulerAngles = euler;
							if (hasRefScale)
								child.localScale = scale;
						}
					}

					// Assign this back to block for further processing
					block = copy;
				}
				// ------------------------------------------------------
				// Get some references set up for the next phase
				Damageable damageable = GetOrAddComponent<Damageable>(block);
				ModuleDamage moduleDamage = GetOrAddComponent<ModuleDamage>(block);
				Visible visible = GetOrAddComponent<Visible>(block);
				Transform transform = block.transform;
				transform.position = Vector3.zero;
				transform.rotation = Quaternion.identity;
				transform.localScale = Vector3.one;
				// ------------------------------------------------------
				// Apply tweaks

				// BlockExtents is a way of quickly doing a cuboid Filled Cell setup
				if(jData.TryGetValue("BlockExtents", out JToken jExtents) && jExtents.Type == JTokenType.Object)
				{
					List<IntVector3> filledCells = new List<IntVector3>();
					int x = ((JObject)jExtents).GetValue("x").ToObject<int>();
					int y = ((JObject)jExtents).GetValue("y").ToObject<int>();
					int z = ((JObject)jExtents).GetValue("z").ToObject<int>();
					for(int i = 0; i < x; i++)
						for(int j = 0; j < y; j++)
							for(int k = 0; k < z; k++)
							{
								filledCells.Add(new IntVector3(i, j, k));
							}
					block.filledCells = filledCells.ToArray();
				}
				if (jData.TryGetValue("Cells", out JToken jCells) && jCells.Type == JTokenType.Array)
				{
					List<IntVector3> filledCells = new List<IntVector3>();
					foreach(JObject jCell in (JArray)jCells)
					{
						filledCells.Add(GetVector3Int(jCell));
					}
					block.filledCells = filledCells.ToArray();
				}
				// APs
				if (jData.TryGetValue("APs", out JToken jAPList) && jAPList.Type == JTokenType.Array)
				{
					List<Vector3> aps = new List<Vector3>();
					foreach(JToken token in (JArray)jAPList)
					{
						aps.Add(GetVector3(token));
					}
					block.attachPoints = aps.ToArray();
				}

				// ------------------------------------------------------
				// Sub objects
				if (jData.TryGetValue("SubObjects", out JToken jSubObjectList) && jSubObjectList.Type == JTokenType.Array)
				{
					foreach(JToken token in (JArray)jSubObjectList)
					{
						if(token.Type == JTokenType.Object)
						{
							JObject jSubObject = (JObject)token;

							string subObjName = TryParse(jSubObject, "SubOverrideName", "");
							GameObject target = (block.transform.RecursiveFindWithProperties(subObjName) as Component)?.gameObject;
							if(target != null)
							{
								// Target acquired, lets tweak
								bool destroyColliders = TryParse(jSubObject, "DestroyExistingColliders", false);
								bool destroyRenderers = TryParse(jSubObject, "DestroyExistingRenderer", false);
								bool supressBoxColliderFallback = TryParse(jSubObject, "SupressBoxColliderFallback", false);
								// Why destroy and replace?

								MeshRenderer meshRenderer = target.GetComponent<MeshRenderer>();
								MeshFilter meshFilter = target.GetComponent<MeshFilter>();
								if (meshRenderer != null && meshFilter != null)
								{
									if (jSubObject.TryGetValue("MeshName", out JToken jMeshName) && jMeshName.Type == JTokenType.String)
									{
										// Load mesh from mod's additional assets
										meshFilter.mesh = (Mesh)mod.FindAsset(jMeshName.ToString());
									}

									// TODO: MeshMaterialName
								}

								// TODO: ColliderMeshName

								// Optional resize settings
								if (jSubObject.TryGetValue("SubPosition", out JToken jPos) && jPos.Type == JTokenType.Object)
									target.transform.localPosition = GetVector3(jPos);
								if (jSubObject.TryGetValue("SubRotation", out JToken jEuler) && jEuler.Type == JTokenType.Object)
									target.transform.localEulerAngles = GetVector3(jEuler);
								if (jSubObject.TryGetValue("SubScale", out JToken jScale) && jScale.Type == JTokenType.Object)
									target.transform.localScale = GetVector3(jScale);


							}
							else
							{
								Debug.LogError($"Could not find sub object {subObjName} in {block.name}");
							}
						}
					}
				}

				// TODO: Death Explosion Reference
				// TODO: Emission Mode
				// Filepath? For reparse?
				

				// ------------------------------------------------------
				// Deserializers
				if (jData.TryGetValue("Deserializer", out JToken jDeserialObj) && jDeserialObj.Type == JTokenType.Object)
				{
					JObject jDeserializer = (JObject)jDeserialObj;

					foreach (KeyValuePair<string, JToken> kvp in jDeserializer)
					{
						string[] split = kvp.Key.Split('|');
						if(split.Length == 0)
						{

						}
					}
				}


				return true;
			}
			return false;
		}



		private void DeserializeComponent(Transform root, Component target, Type componentType, JObject jObject)
		{
			// Let's get reflective!
			foreach(JProperty jProperty in jObject.Properties())
			{
				BindingFlags bind = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				try
				{
					// Parse our string
					string[] split = jProperty.Name.Split('|');
					if (split.Length == 0)
						continue;

					// See if we have a prefix |
					string name = split[0];
					bool wipe = false;
					bool instantiate = false;
					if(split.Length == 2)
					{
						wipe = split[0] == "Wipe";
						instantiate = split[0] == "Instantiate";
						name = split[1];
					}

					FieldInfo tField = componentType.GetField(name, bind);
					PropertyInfo tProp = componentType.GetProperty(name, bind);
					
					bool tryFieldInstead = tProp == null;

					if (tryFieldInstead && tField == null)
					{
						Debug.LogError($"!!! Property '{name}' does not exist in type '{componentType}'");
						continue;
					}

					if (jProperty.Value is JObject jChild)
					{
						SetJSONObject(jChild, target, wipe, instantiate, tField, tProp, tryFieldInstead);
					}
					else if (jProperty.Value is JArray jArray)
					{
						object sourceArray = Wipe ? null : (
							tryFieldInstead ? tField.GetValue(instance) : (
								tProp.CanRead ? tProp.GetValue(instance, null) : null));
						var newArray = MakeJSONArray(sourceArray, tryFieldInstead ? tField.FieldType : tProp.PropertyType, jArray, Spacing, Wipe); // add Wipe param, copy custom names to inside new method
						if (tryFieldInstead)
						{
							tField.SetValue(instance, newArray);
						}
						else if (tProp.CanWrite)
						{
							tProp.SetValue(instance, newArray, null);
						}
						else throw new TargetException("Property is read-only!");
					}
					else if (jProperty.Value is JValue jValue)
					{
						SetJSONValue(jValue, jsonProperty, instance, tryFieldInstead, tField, tProp);
					}
				}
				catch (Exception E)
				{
					Console.WriteLine(Spacing + "!!! Error on modifying property " + jsonProperty.Name);
					Console.WriteLine(Spacing + "!!! " + E/*+"\n"+E.StackTrace*/);
				}
			}

		}

		private void DeserializeGameObject(Transform root, Transform target, JObject jObject)
		{

			foreach (KeyValuePair<string, JToken> kvp in jObject)
			{
				if (kvp.Value.Type != JTokenType.Object)
				{
					Debug.LogError($"Unexpected token type {kvp.Value.Type} for token {kvp.Key}");
					continue;
				}

				string[] split = kvp.Key.Split('|');
				if (split.Length == 1) // "ModuleVision", "UnityEngine.Transform" etc.
				{
					// Format will be "{ComponentType} {Index}" where the index specifies the child index if there are multiple targets
					string[] typeNameAndIndex = split[0].Split(' ');
					if (typeNameAndIndex.Length == 0)
						continue;

					// See if we have an index
					int index = 0;
					if (typeNameAndIndex.Length >= 2)
						int.TryParse(typeNameAndIndex[1], out index);

					// Try and find our type
					Type componentType = ReferenceFinder.GetType(typeNameAndIndex[0]);
					if(componentType == null)
					{
						Debug.LogError($"Could not find component type {typeNameAndIndex[0]}");
						continue;
					}

					// See if we have an existing component. If not, make one
					Component component = target.gameObject.GetComponent(componentType, index);
					if(component == null)
					{
						component = target.gameObject.AddComponent(componentType);
					}

					// If we still don't have one, exit
					if(component == null)
					{
						Debug.LogError($"Could not relocate component {typeNameAndIndex[0]}");
						continue;
					}

					DeserializeComponent(root, component, (JObject)kvp.Value);
				}
				else if(split.Length == 2) //
				{
					GameObject childObject = null;
					string name = split[1];
					
					switch (split[0])
					{
						case "GameObject": // Create a new child object
						{
							childObject = new GameObject(split[1]);
							childObject.transform.SetParent(target);
							childObject.transform.localPosition = Vector3.zero;
							childObject.transform.localRotation = Quaternion.identity;
							childObject.transform.localScale = Vector3.one;
							break;
						}
						case "Reference": // Copy a child object from another prefab
						{
							if(ReferenceFinder.GetReferenceFromBlockResource(name, out object reference))
							{
								GameObject referenceObject = null;
								if (reference is GameObject)
									referenceObject = (GameObject)reference;
								else if (reference is Transform)
									referenceObject = ((Transform)reference).gameObject;
								else if (reference is Component)
								{
									// TODO: line 644ish JsonToGameObject
								}
								else
									Debug.LogError("Unknown object found as reference");

								if(referenceObject != null)
								{
									childObject = GameObject.Instantiate(referenceObject);
									string newName = name;
									int count = 1;
									while (target.transform.Find(newName))
									{
										newName = $"{name}_{++count}";
									}
									childObject.name = newName;
									childObject.transform.SetParent(target.transform);
									childObject.transform.localPosition = referenceObject.transform.localPosition;
									childObject.transform.localRotation = referenceObject.transform.localRotation;
									childObject.transform.localScale = referenceObject.transform.localScale;
								}
							}
							break;
						}
						case "Duplicate": // Copy a child object from this prefab
						{
							if(name.Contains('/') || name.Contains('.'))
							{
								var nGO = root.RecursiveFindWithProperties(name);
								if (nGO != null)
								{
									if (nGO is Component nGOc)
										childObject = nGOc.gameObject;
									else if (nGO is GameObject nGOg)
										childObject = nGOg;
								}
							}
							break;
						}
						case "Instantiate": // Instantiate something
						{
							break;
						}
					}

					// First fallback, try looking up the object by the full string
					if (childObject == null)
						childObject = target.transform.Find(name)?.gameObject;

					// Final fallback, just make an empty object
					if (childObject == null)
					{
						childObject = new GameObject(name);
						childObject.transform.parent = target.transform;
					}
					else // Not sure if this should be else, see JsonToGameObject.cs:702
					{
						if (split[0] == "Duplicate")
						{
							childObject = GameObject.Instantiate(childObject);
							name = name.Substring(1 + name.LastIndexOfAny(new char[] { '/', '.' }));
							string newName = $"{name}_copy";
							int count = 1;
							while (target.transform.Find(newName))
							{
								newName = $"{name}_copy_{(++count)}";
							}
							childObject.name = newName;
							childObject.transform.parent = target.transform;
						}
					}

					if (kvp.Value.Type == JTokenType.Object)
						DeserializeGameObject(root, childObject.transform, (JObject)kvp.Value);
				}
			}
		}

		private void RemoveChildren<T>(Component obj) where T : Component
		{
			foreach (Component c in obj.gameObject.GetComponentsInChildren<T>())
				UnityEngine.Object.DestroyImmediate(c);
		}

		private Vector3 GetVector3(JToken token)
		{
			if (token.Type == JTokenType.Object
			&& ((JObject)token).TryGetValue("x", out JToken xToken)
			&& ((JObject)token).TryGetValue("y", out JToken yToken)
			&& ((JObject)token).TryGetValue("z", out JToken zToken))
				return new Vector3(xToken.ToObject<float>(), yToken.ToObject<float>(), zToken.ToObject<float>());
			return Vector3.zero;
		}

		private IntVector3 GetVector3Int(JToken token)
		{
			if (token.Type == JTokenType.Object
			&& ((JObject)token).TryGetValue("x", out JToken xToken)
			&& xToken.Type == JTokenType.Integer
			&& ((JObject)token).TryGetValue("y", out JToken yToken)
			&& yToken.Type == JTokenType.Integer
			&& ((JObject)token).TryGetValue("z", out JToken zToken)
			&& zToken.Type == JTokenType.Integer)
				return new IntVector3(xToken.ToObject<int>(), yToken.ToObject<int>(), zToken.ToObject<int>());
			return IntVector3.zero;
		}

		// This is the JSON key that we check for in custom blocks
		public override string GetModuleKey()
		{
			return "NuterraBlock";
		}
	}
}