using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomModules
{
	public static class NuterraDeserializer
	{
		public static void DeserializeComponent(Transform root, Component target, Type componentType, JObject jObject)
		{
			// TODO: Stage 2
		}


		/*
		private static void DeserializeComponent(Transform root, Component target, Type componentType, JObject jObject)
		{
			// Let's get reflective!
			foreach (JProperty jProperty in jObject.Properties())
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
					if (split.Length == 2)
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
				catch (Exception e)
				{
					Debug.LogError(e.StackTrace);
				}
			}

		}

		private static void DeserializeGameObject(Transform root, Transform target, JObject jObject)
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
					if (componentType == null)
					{
						Debug.LogError($"Could not find component type {typeNameAndIndex[0]}");
						continue;
					}

					// See if we have an existing component. If not, make one
					Component component = target.gameObject.GetComponent(componentType, index);
					if (component == null)
					{
						component = target.gameObject.AddComponent(componentType);
					}

					// If we still don't have one, exit
					if (component == null)
					{
						Debug.LogError($"Could not relocate component {typeNameAndIndex[0]}");
						continue;
					}

					DeserializeComponent(root, component, (JObject)kvp.Value);
				}
				else if (split.Length == 2) //
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
							if (ReferenceFinder.GetReferenceFromBlockResource(name, out object reference))
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

								if (referenceObject != null)
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
							if (name.Contains('/') || name.Contains('.'))
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

		private static void SetJSONObject(JObject jObject, object instance, string Spacing, bool Wipe, bool Instantiate, FieldInfo tField, PropertyInfo tProp, bool UseField)
		{
			if (UseField)
			{
				object rewrite = SetJSONObject_Internal(jObject, Spacing, Wipe, Instantiate, Wipe ? null : tField.GetValue(instance), tField.FieldType, tField.Name);
				try { tField.SetValue(instance, rewrite); } catch (Exception E) { Console.WriteLine(Spacing + m_tab + "!!!" + E.ToString()); }
			}
			else
			{
				object rewrite = SetJSONObject_Internal(jObject, Spacing, Wipe, Instantiate, Wipe || !tProp.CanRead ? null : tProp.GetValue(instance, null), tProp.PropertyType, tProp.Name);
				if (tProp.CanWrite)
					try { tProp.SetValue(instance, rewrite, null); } catch (Exception E) { Console.WriteLine(Spacing + m_tab + "!!!" + E.ToString()); }
			}
		}

		static Type[] ForceInstantiateObjectTypes = new Type[]
		{
			typeof(TireProperties),
			typeof(ManWheels.TireProperties)
		};

		private static object SetJSONObject_Internal(JObject jObject, string Spacing, bool Wipe, bool Instantiate, object original, Type type, string name)
		{
			object rewrite;
			if (Wipe || original == null)
			{
				bool isGO = type.IsAssignableFrom(t_go);
				if (isGO || type.IsAssignableFrom(t_tr)) // UnityEngine.Component (Module)
				{

					var oObj = (original as Component).gameObject;
					//bool isActive = oObj.activeInHierarchy;//oObj.activeSelf;
					var nObj = GameObject.Instantiate(oObj);
					//if (Input.GetKey(KeyCode.Alpha9)) nObj.SetActive(true);
					//else if (Input.GetKey(KeyCode.Alpha9)) nObj.SetActive(false);
					//else 
					nObj.SetActive(false);// isActive && !Input.GetKey(KeyCode.O));
					nObj.transform.parent = oObj.transform.parent;
					nObj.transform.position = Vector3.down * 25000f;
					var cacheSearchTransform = SearchTransform;
					CreateGameObject(jObject, nObj.gameObject, Spacing + m_tab + m_tab, firstSearchTransform);
					SearchTransform = cacheSearchTransform;
					if (Input.GetKey(KeyCode.LeftControl))
					{
						Console.WriteLine("Instantiating " + name + " : " + type.ToString());
						Console.WriteLine(LogAllComponents(nObj.transform, false));//BlockLoader.AcceptOverwrite));
					}
					if (isGO)
					{
						if (Wipe && original != null)
							GameObject.DestroyImmediate(original as GameObject);
						rewrite = nObj;
					}
					else
					{
						if (Wipe && original != null)
							GameObject.DestroyImmediate(original as Transform);
						rewrite = nObj.GetComponent(type);
					}
				}
				else
				{
					original = Activator.CreateInstance(type);
					rewrite = ApplyValues(original, type, jObject, Spacing + m_tab);
				}
			}
			else
			{
				if (!Instantiate && !ForceInstantiateObjectTypes.Contains(type))
				{
					rewrite = ApplyValues(original, type, jObject, Spacing + m_tab);
				}
				else // Instantiate
				{
					bool isGO = type.IsAssignableFrom(t_go);
					if (isGO || type.IsSubclassOf(t_comp)) // UnityEngine.Component (Module)
					{
						var oObj = (original as Component).gameObject;
						//bool isActive = oObj.activeInHierarchy;//oObj.activeSelf;
						var nObj = GameObject.Instantiate(oObj);
						//if (Input.GetKey(KeyCode.Alpha9)) nObj.SetActive(true);
						//else if (Input.GetKey(KeyCode.Alpha9)) nObj.SetActive(false);
						//else 
						nObj.SetActive(false);// isActive && !Input.GetKey(KeyCode.O));
						nObj.transform.parent = oObj.transform.parent;
						nObj.transform.position = Vector3.down * 25000f;
						var cacheSearchTransform = SearchTransform;
						CreateGameObject(jObject, nObj.gameObject, Spacing + m_tab + m_tab, firstSearchTransform);
						SearchTransform = cacheSearchTransform;
						if (Input.GetKey(KeyCode.LeftControl))
						{
							Console.WriteLine("Instantiating " + name + " : " + type.ToString());
							Console.WriteLine(LogAllComponents(nObj.transform, false));//BlockLoader.AcceptOverwrite));
						}
						if (isGO)
							rewrite = nObj;
						else
							rewrite = nObj.GetComponent(type);
					}
					else
					{
						object newObj = Activator.CreateInstance(type);
						ShallowCopy(type, original, newObj, true);
						rewrite = ApplyValues(newObj, type, jObject, Spacing + m_tab);
					}
				}
			}

			return rewrite;
		}
		*/
	}
}
