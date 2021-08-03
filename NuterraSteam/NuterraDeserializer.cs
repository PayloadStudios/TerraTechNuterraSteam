using Newtonsoft.Json.Linq;
using System;
using System.Collections;
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
		private static Type kTypeShader = typeof(Shader);
		private static Type kTypeUnityObject = typeof(UnityEngine.Object);
		private static Type kTypeComponent = typeof(UnityEngine.Component);
		private static Type kTypeGameObject = typeof(UnityEngine.GameObject);
		private static Type kTypeTransform = typeof(UnityEngine.Transform);
		private static Type kTypeJToken = typeof(JToken);

		internal static string DeserializingBlock = "UNKNOWN";

		// TTQMM Ref: GameObjectJSON.ApplyValues(object instance, Type instanceType, JObject json, string Spacing)
		// TTQMM Ref: GameObjectJSON.ApplyValue(object instance, Type instanceType, JProperty jsonProperty, string Spacing)
		// I've embedded these two functions
		public static object DeserializeJSONObject(object target, Type targetType, JObject jObject)
		{
			// Let's get reflective!
			foreach (JProperty jProperty in jObject.Properties())
			{
				Debug.Log($"[Nuterra - {DeserializingBlock}] Attempting to deserialize {targetType.ToString()}.{jProperty.Name}");
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

					FieldInfo fieldInfo = targetType.GetField(name, bind);
					PropertyInfo propertyInfo = targetType.GetProperty(name, bind);

					MemberInfo memberInfo = null;
					Type memberType = null;
					if (propertyInfo != null)
					{
						memberType = propertyInfo.PropertyType;
						memberInfo = propertyInfo;
					}
					else if(fieldInfo != null)
					{
						memberType = fieldInfo.FieldType;
						memberInfo = fieldInfo;
					}
					else
					{
						Debug.LogError($"[Nuterra - {DeserializingBlock}] Property '{name}' does not exist in type '{targetType}'");
						continue;
					}

					if (jProperty.Value != null)
                    {
						bool pass = false;
						switch (memberInfo.MemberType)
                        {
							case MemberTypes.Event:
								{
									Debug.LogError($"[Nuterra - {DeserializingBlock}] Trying to assign value to a Event");
									break;
								}
							case MemberTypes.Constructor:
                                {
									Debug.LogError($"[Nuterra - {DeserializingBlock}] Trying to assign value to a Constructor");
									break;
                                }
							case MemberTypes.Method:
                                {
									Debug.LogError($"[Nuterra - {DeserializingBlock}] Trying to assign value to a Method");
									break;
                                }
							case MemberTypes.TypeInfo:
                                {
									Debug.LogError($"[Nuterra - {DeserializingBlock}] Trying to assign value to a Type");
									break;
                                }
							case MemberTypes.NestedType:
                                {
									Debug.LogError($"[Nuterra - {DeserializingBlock}] Trying to assign value to a NestedType");
									break;
                                }
							case MemberTypes.Custom:
                                {
									Debug.LogError($"[Nuterra - {DeserializingBlock}] Trying to assign value to an custom MemberInfo");
									break;
                                }
							default:
                                {
									pass = true;
									break;
                                }
                        }
						if (!pass)
                        {
							throw new Exception("Attempting to assign a value to an invalid member type");
                        }
						else
                        {
							Type propertyType;
							if (propertyInfo != null)
                            {
								propertyType = propertyInfo.PropertyType;
                            }
							else
                            {
								propertyType = fieldInfo.FieldType;
                            }

							if (typeof(Delegate).IsAssignableFrom(propertyType))
                            {
								throw new Exception("Attempting to assign a value to a delegate");
                            }
						}
                    }

					// Switch on the type of JSON we are provided with
					switch (jProperty.Value.Type)
					{
						case JTokenType.Object:
						{
							// Handle objects in our large function, as they can mean new GameObjects, new Components, instantiators, duplicators, all sorts...
							JObject jChild = jProperty.Value as JObject;

							SetJSONObject(jChild, target, wipe, instantiate, memberInfo);
							break;
						}
						case JTokenType.Array:
						{
							// Handle arrays
							JArray jArray = jProperty.Value as JArray;
							object sourceArray = null;

							if (!wipe)
								sourceArray = memberInfo.GetValueOfField(target);

							// Helper function to populate the array contents
							object newArray = MakeJSONArray(sourceArray, memberInfo.GetFieldType(), jArray, wipe);

							// Then set it back to the field / property
							memberInfo.SetValueOfField(target, newArray);

							break;
						}
						default:
						{
							// The leaf node, parse the value into place
							if (jProperty.Value is JValue jValue)
							{
								DeserializeValueIntoTarget(target, memberInfo, jValue);
							}
							break;
						}
					}
				}
				catch (Exception e)
				{
					Debug.LogError($"[Nuterra - {DeserializingBlock}] Failed to deserialize Json object");
					Debug.LogError(e.Message);
					Debug.LogError(e.StackTrace);
				}
			}

			return target;
		}

		// TTQMM Ref: JsonToGameObject.CreateGameObject(JObject json, GameObject GameObjectToPopulate = null, string Spacing = "", Transform searchParent = null)
		public static GameObject DeserializeIntoGameObject(JObject jObject, GameObject target)
		{
			if (target == null)
				target = new GameObject("New Deserialized Object");

			PushSearchTransform(target.transform);
			GameObject result = DeserializeIntoGameObject_Internal(jObject, target);
			PopSearchTransform();
			return result;
		}

		// TTQMM Ref: JsonToGameObject.CreateGameObject_Internal(JObject json, GameObject GameObjectToPopulate, string Spacing, Component instantiated = null, Type instantiatedType = null)
		private static GameObject DeserializeIntoGameObject_Internal(JObject jObject, GameObject target)
		{
			// Ensure we have a target object
			if (target == null)
				target = new GameObject("Deserialized Object");

			// Then read each JSON property and act accordingly
			foreach (JProperty jProperty in jObject.Properties())
			{
				string[] split = jProperty.Name.Split('|');
				if (split.Length == 1) // "ModuleVision", "UnityEngine.Transform" etc.
				{
					Debug.Log($"[Nuterra - {DeserializingBlock}] Deserializer adding component {split[0]}");

					// Format will be "{ComponentType} {Index}" where the index specifies the child index if there are multiple targets
					string typeNameAndIndex = split[0];
					string typeName = typeNameAndIndex.Split(' ')[0];
					Type type = TTReferences.GetType(typeName);

					if (type != null)
					{
						Debug.Log($"[Nuterra - {DeserializingBlock}] Deserializer ready to find or create instance of type {type}");

						// See if we have an existing component
						Component component = target.GetComponentWithIndex(typeNameAndIndex);

						// A null JSON token means we should delete this object
						if (jProperty.Value.Type == JTokenType.Null)
						{
							if (component != null)
								Component.DestroyImmediate(component);
							else
								Debug.LogError($"[Nuterra - {DeserializingBlock}] Could not find component of type {typeNameAndIndex} to destroy");
						}
						else // We have some data, let's process it
						{
							// If we couldn't find the component, make a new one
							if (component == null)
								component = target.gameObject.AddComponent(type);

							// If we still can't find one, get it. This should like never happen, right?
							if (component == null)
							{
								Debug.LogError($"[Nuterra - {DeserializingBlock}] Failed to find {typeNameAndIndex}, failed to AddComponent, but trying GetComponent");
								component = target.gameObject.GetComponent(type);
							}

							// If we still don't have one, exit
							if (component == null)
							{
								Debug.LogError($"[Nuterra - {DeserializingBlock}] Could not find component {typeNameAndIndex}");
								continue;
							}

							// Now deserialize the JSON into the new Component
							Debug.LogError($"[Nuterra - {DeserializingBlock}] Preparing deserialization");
							DeserializeJSONObject(component, type, jProperty.Value as JObject);
						}

						Debug.Log($"[Nuterra - {DeserializingBlock}] Processing complete for type {type}");
					}
					else
					{
						Debug.LogError($"[Nuterra - {DeserializingBlock}] Could not find type {typeNameAndIndex}");
					}
				}
				else if (split.Length == 2) //
				{
					GameObject childObject = null;
					string name = split[1];

					switch (split[0])
					{
						case "Reference": // Copy a child object or component from another prefab
						{
							Debug.Log($"[Nuterra - {DeserializingBlock}] Deserializing Reference {name}");
							if (TTReferences.GetReferenceFromBlockResource(name, out object reference))
							{
								Debug.Log($"[Nuterra - {DeserializingBlock}] Found reference {reference}");
								if (reference is GameObject || reference is Transform)
								{
									// If the reference was to a GameObject or a Transform, then we just want to copy that whole object
									GameObject referenceObject = reference is GameObject ? (GameObject)reference : ((Transform)reference).gameObject;

									childObject = GameObject.Instantiate(referenceObject);
									string newName = name.Substring(1 + name.LastIndexOfAny(new char[] { '/', '.' }));
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
								else if (reference is Component)
								{
									// If we referenced a Component, we want to place a copy of that component on our target
									// However, if we already have a Component of the same type, we most likely want to override its values. 
									// This functionality is as per TTQMM
									Type type = reference.GetType();
									Component existingComponent = target.GetComponent(type);
									if (existingComponent == null)
									{
										Debug.Log($"[Nuterra - {DeserializingBlock}] Could not find Component of type {type} - creating one now");
										existingComponent = target.AddComponent(type);
									}

									// Copy the reference and then deserialize our JSON into it
									ShallowCopy(type, reference, existingComponent, false);
									DeserializeJSONObject(existingComponent, type, jProperty.Value as JObject);
									continue;
								}
								else
								{
									Debug.LogError($"[Nuterra - {DeserializingBlock}] Unknown object {reference} found as reference");
									continue;
								}
							}
							else
							{
								Debug.LogError($"[Nuterra - {DeserializingBlock}] Could not find reference for {name} in deserialization");
								continue;
							}
							break;
						}
						case "Duplicate": // Copy a child object from this prefab
						{
							Debug.Log($"[Nuterra - {DeserializingBlock}] Deserializing Duplicate {name}");
							if (name.Contains('/') || name.Contains('.'))
							{
								object foundObject = GetCurrentSearchTransform().RecursiveFindWithProperties(name);
								if (foundObject != null)
								{
									if (foundObject is Component foundComponent)
										childObject = foundComponent.gameObject;
									else if (foundObject is GameObject foundGameObject)
										childObject = foundGameObject;
								}
							}

							if (childObject == null)
								childObject = target.transform.Find(name)?.gameObject;

							break;
						}
						case "GameObject": // Create a new child object
						case "Instantiate": // Instantiate something
						default:
						{
							Debug.Log($"[Nuterra - {DeserializingBlock}] Deserializing {split[0]}|{name}");

							if (childObject == null)
								childObject = target.transform.Find(name)?.gameObject;

							break;
						}
					}

					// Fallback, just make an empty object
					if (childObject == null)
					{
						if (jProperty.Value.Type == JTokenType.Null)
						{
							Debug.Log($"[Nuterra - {DeserializingBlock}] Deserializing failed to find {name} to delete");
							continue;
						}
						else
						{
							childObject = new GameObject(name);
							childObject.transform.parent = target.transform;
							childObject.transform.localPosition = Vector3.zero;
							childObject.transform.localRotation = Quaternion.identity;
							childObject.transform.localScale = Vector3.one;
						}
					}
					else
					{
						// If we've got no JSON data, that means we want to delete this target
						if(jProperty.Value.Type == JTokenType.Null)
						{
							GameObject.DestroyImmediate(childObject);
							childObject = null;
							continue;
						}

						// We're duplicating this
						else if (split[0] == "Duplicate")
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
							childObject.transform.localPosition = Vector3.zero;
							childObject.transform.localRotation = Quaternion.identity;
							childObject.transform.localScale = Vector3.one;
						}
					}

					Debug.Log($"[Nuterra - {DeserializingBlock}] Deserializing jProp {jProperty.Name} --- {jProperty.Value}");
					DeserializeIntoGameObject_Internal((JObject)jProperty.Value, childObject);
				}
			}
			return target;
		}

		// TTQMM Ref: JsonToGameObject.SetJSONValue(JValue jValue, JProperty jsonProperty, object _instance, bool UseField, FieldInfo tField = null, PropertyInfo tProp = null)
		private static void DeserializeValueIntoTarget(object target, MemberInfo member, JValue jValue)
		{
			member.SetValueOfField(target, DeserializeValue(jValue, member.GetFieldType()));
		}
		private static object DeserializeValue(JValue jValue, Type type)
		{
			try // Try transforming to the target type
			{
				return jValue.ToObject(type);
			}
			catch // If we failed, we can try interpreting the jValue as a reference string
			{
				string referenceString = jValue.ToObject<string>();
				// Trim anything before the |
				string targetName = referenceString.Substring(referenceString.IndexOf('|') + 1);
				// 
				return DeserializeValueReference(targetName, referenceString, type);
			}
		}

		// TTQMM Ref: JsonToGameObject.GetValueFromString
		// This function is for setting a value based on a reference
		public static object DeserializeValueReference(string search, string searchFull, Type outType)
		{
			if (searchFull.StartsWith("Reference"))
			{
				if (TTReferences.GetReferenceFromBlockResource(search, out var result)) // Get value from a block in the game
					return result;
			}
			else if (TTReferences.TryFind(search, null, outType, out object result))
			{
				return result; // Get value from a value in the user database
			}
			else
			{
				try
				{
					// Last fallback, we try searching our current working tree
					var recursive = GetCurrentSearchTransform().RecursiveFindWithProperties(searchFull, GetRootSearchTransform());
					if (recursive != null)
						return recursive; // Get value from this block
				}
				catch
				{ }
			}
			return null;
		}

		// TTQMM Ref JsonToGameObject.MakeJSONArray(object originalArray, Type ArrayType, JArray Deserialize, string Spacing, bool Wipe)
		private static object MakeJSONArray(object originalArray, Type arrayType, JArray jArray, bool wipe)
		{
			IList newList;
			IList sourceList = wipe ? null : originalArray as IList;

			// If the target is a JToken array, then we are basically done
			if (arrayType == kTypeJToken)
				return jArray;

			Type itemType;
			if (arrayType.IsGenericType)
				itemType = arrayType.GetGenericArguments()[0];
			else
				itemType = arrayType.GetElementType();

			int count = jArray.Count;
			try
			{
				// newCount here tells fixed arrays how many items to have. List<> arrays get starting capacity, but is empty.
				newList = Activator.CreateInstance(arrayType, count) as IList;

				// Must be a List<> then, which means it can be expanded with the following...
				while (newList.Count < count)
				{
					object def = itemType.IsClass ? null : Activator.CreateInstance(itemType); // Get default (Avoid creation if not needed)
					newList.Add(def); // Populate empty list from 0 to length
				}

				// Populate the list from our JSON
				for (int i = 0; i < count; i++) 
				{
					// WP: Do not reference the original object! (Corruption risk)
					object element = newList[i]; 

					if (jArray[i] is JObject jObject)
					{
						// Make an element if we don't have one
						if (element == null)
						{
							element = Activator.CreateInstance(itemType); // Create instance, because is needed
							if (sourceList != null && sourceList.Count != 0) // Copy current or last element
							{
								ShallowCopy(itemType, sourceList[Math.Min(i, sourceList.Count - 1)], element, true); // WP: Helpful, trust me
							}
						}
						// Then deserialize into that element
						DeserializeJSONObject(element, itemType, jObject);
					}
					else if (jArray[i] is JArray jSubArray)
					{
						element = MakeJSONArray(element, itemType, jSubArray, false);
					}
					else if (jArray[i] is JValue jValue)
					{
						try
						{
							element = jValue.ToObject(itemType);
						}
						catch
						{
							string cache = jValue.ToObject<string>();
							string targetName = cache.Substring(cache.IndexOf('|') + 1);
							element = DeserializeValueReference(targetName, cache, itemType);
						}
					}
					newList[i] = element;
				}

				return newList;
			}
			catch(Exception)
			{
				return null;
			}
		}

		private static Type[] kForceInstantiateObjectTypes = new Type[]
		{
			typeof(TireProperties),
			typeof(ManWheels.TireProperties)
		};

		// TODO: Do we have to have this??
		private static Transform sCurrentSearchTransform = null;
		private static Stack<Transform> sTransformSearchStack = new Stack<Transform>();
		private static Transform GetRootSearchTransform() { return sTransformSearchStack.First(); }
		private static Transform GetCurrentSearchTransform() { return sTransformSearchStack.Peek(); }
		private static void PushSearchTransform(Transform t) { sTransformSearchStack.Push(t); }
		private static void PopSearchTransform() { sTransformSearchStack.Pop(); }

		// TTQMM Ref : GameObjectJSON.SetJSONObject(JObject jObject, object instance, string Spacing, bool Wipe, bool Instantiate, FieldInfo tField, PropertyInfo tProp, bool UseField)
		private static void SetJSONObject(JObject jObject, object target, bool wipe, bool instantiate, MemberInfo memberInfo)
		{
			object originalObject = null;

			if (!wipe)
				originalObject = memberInfo.GetValueOfField(target);

			object rewrittenObject = SetJSONObject_Internal(jObject, wipe, instantiate, originalObject, memberInfo.GetFieldType(), memberInfo.Name);

			memberInfo.SetValueOfField(target, rewrittenObject);
		}

		// TTQMM Ref: GameObjectJSON.SetJSONObject_Internal(JObject jObject, string Spacing, bool Wipe, bool Instantiate, object original, Type type, string name)
		private static object SetJSONObject_Internal(JObject jObject, bool wipe, bool instantiate, object original, Type originalType, string name)
		{
			object rewrite = null;

			// First point of order, some types have to be instantiated
			if (kForceInstantiateObjectTypes.Contains(originalType))
				instantiate = true;

			bool isGO = originalType.IsAssignableFrom(kTypeGameObject);
			bool isTransform = originalType.IsAssignableFrom(kTypeTransform);
			bool isComponent = originalType.IsSubclassOf(kTypeComponent);

			// If wipe or we have nothing to start with
			if (wipe || original == null)
			{
				if (isGO || isTransform) // UnityEngine.Component (Module)
				{
					// Instantiate the original object
					GameObject originalObject = null;
					if (isGO)
						originalObject = original as GameObject;
					if(isTransform)
						originalObject = (original as Transform).gameObject;
					GameObject newObject = GameObject.Instantiate(originalObject);

					// Initialise its transforms
					newObject.SetActive(false);
					newObject.transform.parent = originalObject.transform.parent;
					newObject.transform.position = Vector3.down * 25000f; // What? Bye bye transform?!

					DeserializeIntoGameObject(jObject, newObject.gameObject);
					
					if (isGO)
					{
						if (wipe && original != null)
							GameObject.DestroyImmediate(original as GameObject);
						rewrite = newObject;
					}
					else
					{
						if (wipe && original != null)
							GameObject.DestroyImmediate(original as Component);
						rewrite = newObject.GetComponent(originalType);
					}
				}
				else // Something other than a GameObject or Transform
				{
					// Create an instance with new() and deserialize our JSON into it
					try
					{
						original = Activator.CreateInstance(originalType);
					}
					catch
					{
						// We can try seeing if our parameters fit any of the other constructors
						foreach (ConstructorInfo constructor in originalType.GetConstructors())
						{
							try
							{
								// Look for constructors of that fit
								ParameterInfo[] parameters = constructor.GetParameters();
								object[] values = new object[parameters.Length];
								for (int i = 0; i < parameters.Length; i++)
								{
									if (jObject.TryGetValue(parameters[i].Name, out JToken jValue))
										values[i] = jValue.ToObject(parameters[i].ParameterType);
									else if (parameters[i].HasDefaultValue)
										values[i] = parameters[i].DefaultValue;
									else
										values[i] = null;
								}
								original = constructor.Invoke(values);
								break;
							}
							catch
							{
								Debug.LogWarning($"[Nuterra - {DeserializingBlock}] Failed to match constructor for {originalType}");
							}
						}
					}
					if (original != null)
						rewrite = DeserializeJSONObject(original, originalType, jObject);
					else
						Debug.LogError($"[Nuterra - {DeserializingBlock}] Failed to create instance of {originalType}");
				}
			}
			else // We are not wiping the source and we have a reference original
			{
				if (instantiate)
				{
					if (isGO || isComponent)
					{
						GameObject originalObject = (original as Component).gameObject;
						GameObject newObject = GameObject.Instantiate(originalObject);
	
						newObject.SetActive(false);
						newObject.transform.parent = originalObject.transform.parent;
						newObject.transform.position = Vector3.down * 25000f;

						DeserializeIntoGameObject(jObject, newObject.gameObject);

						if (isGO)
							rewrite = newObject;
						else
							rewrite = newObject.GetComponent(originalType);
					}
					else // Some data structure, not extending Component
					{
						object newObj = null;
						try
						{
							newObj = Activator.CreateInstance(originalType);
						}
						catch
						{
							// We can try seeing if our parameters fit any of the other constructors
							foreach (ConstructorInfo constructor in originalType.GetConstructors())
							{
								try
								{
									// Look for constructors of that fit
									ParameterInfo[] parameters = constructor.GetParameters();
									object[] values = new object[parameters.Length];
									for (int i = 0; i < parameters.Length; i++)
									{
										if (jObject.TryGetValue(parameters[i].Name, out JToken jValue))
											values[i] = jValue.ToObject(parameters[i].ParameterType);
										else if (parameters[i].HasDefaultValue)
											values[i] = parameters[i].DefaultValue;
										else
											values[i] = null;
									}
									original = constructor.Invoke(values);
									break;
								}
								catch
								{
									Debug.LogWarning($"[Nuterra - {DeserializingBlock}] Failed to match constructor for {originalType}");
								}
							}
						}

						ShallowCopy(originalType, original, newObj, true);

						if (newObj != null)
							rewrite = DeserializeJSONObject(newObj, originalType, jObject);
						else
							Debug.LogError($"[Nuterra - {DeserializingBlock}] Failed to create instance of {originalType}");
					}
				}
				else // !instantiate
				{
					rewrite = DeserializeJSONObject(original, originalType, jObject);
				}
			}

			return rewrite;
		}

		// -----------------------------------------------------------------------------------
		#region Copy, Deserialize, Serialize Helpers
		// -----------------------------------------------------------------------------------


		public static void ShallowCopy(Type sharedType, object source, object target, bool declaredVarsOnly)
		{
			BindingFlags bf = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
			if (declaredVarsOnly)
				bf |= BindingFlags.DeclaredOnly;
			var fields = sharedType.GetFields(bf);
			foreach (var field in fields)
			{
				try
				{
					field.SetValue(target, field.GetValue(source));
				}
				catch { }
			}
			var props = sharedType.GetProperties(bf);
			foreach (var prop in props)
			{
				try
				{
					if (prop.CanRead && prop.CanWrite)
						prop.SetValue(target, prop.GetValue(source), null);
				}
				catch { }
			}
		}

		public static void ShallowCopy(Type sharedType, object source, object target, string[] filter)
		{
			var bf = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
			foreach (string search in filter)
			{
				var field = sharedType.GetField(search, bf);
				if (field != null)
				{
					try
					{
						field.SetValue(target, field.GetValue(source));
					}
					catch { }
				}
				else
				{
					var prop = sharedType.GetProperty(search, bf);
					if (prop != null)
					{
						try
						{
							if (prop.CanRead && prop.CanWrite)
								prop.SetValue(target, prop.GetValue(source), null);
						}
						catch { }
					}
				}
			}
		}
		#endregion
		// -----------------------------------------------------------------------------------
	}
}
