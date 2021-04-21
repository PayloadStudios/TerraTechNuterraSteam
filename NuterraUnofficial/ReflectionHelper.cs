using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Nuterra.BlockInjector
{
	public static class ReflectionHelper
	{
		private static Type t_ilist = typeof(IList);
		private static Type t_shader = typeof(Shader);
		private static Type t_uobj = typeof(UnityEngine.Object);
		private static Type t_comp = typeof(UnityEngine.Component);
		private static Type t_go = typeof(UnityEngine.GameObject);
		private static Type t_tr = typeof(UnityEngine.Transform);
		private static Type t_jt = typeof(JToken);

		static Type[] ForceInstantiateObjectTypes = new Type[]
		{
			typeof(TireProperties),
			typeof(ManWheels.TireProperties)
		};

		public static void SetJSONObject(JObject jObject, object instance, string Spacing, bool Wipe, bool Instantiate, FieldInfo tField, PropertyInfo tProp, bool UseField)
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
	}
}
