using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace CustomModules
{
    public static class GameObjectExtensions
    {
        public static T EnsureComponent<T>(this GameObject obj) where T : Component
        {
            Component comp = obj.GetComponent<T>();
            if (comp == null) comp = obj.AddComponent<T>();
            else try
                {
                    comp.name.Insert(0, "_");
                }
                catch
                {
                    comp = obj.AddComponent<T>();
                }
            return comp as T;
        }

		// These functions are copied from GameObjectJSON
		public static Transform RecursiveFind(this Transform transform, string NameOfChild, string HierarchyBuildup = "")
		{
			if (NameOfChild == "/") return transform;
			string cName = NameOfChild.Substring(NameOfChild.LastIndexOf('/') + 1);
			for (int i = 0; i < transform.childCount; i++)
			{
				var child = transform.GetChild(i);
				//Console.WriteLine(child.name);
				if (child.name == cName)
				{
					HierarchyBuildup += "/" + cName;
					//Console.WriteLine(HierarchyBuildup + "  " + NameOfChild);
					if (HierarchyBuildup.EndsWith(NameOfChild))
					{
						return child;
					}
				}
			}
			for (int i = 0; i < transform.childCount; i++)
			{
				var c = transform.GetChild(i);
				var child = c.RecursiveFind(NameOfChild, HierarchyBuildup + "/" + c.name);
				if (child != null)
				{
					return child;
				}
			}
			return null;
		}

		public static object RecursiveFindWithProperties(this Transform transform, string nameOfProperty, Transform fallback = null)
		{
			try
			{
				int propIndex = nameOfProperty.IndexOf('.');
				if (propIndex == -1)
				{
					var tresult = transform.RecursiveFind(nameOfProperty);
					if (tresult == null && fallback != null) tresult = fallback.RecursiveFind(nameOfProperty);
					return tresult;
				}
				Transform result = transform;
				Console.Write(transform.name);

				string propertyPath = nameOfProperty;
				while (true)
				{
					propIndex = propertyPath.IndexOf('.');
					if (propIndex == -1)
					{
						var t = result.RecursiveFind(propertyPath);
						Console.WriteLine($"<FindTrans:{propertyPath}>{(t == null ? "EMPTY" : "RETURN")}");
						if (t == null && fallback != null && fallback != transform)
							return fallback.RecursiveFindWithProperties(nameOfProperty);
						return t;
					}
					int reIndex = propertyPath.IndexOf('/', propIndex);
					int lastIndex = propertyPath.LastIndexOf('/', propIndex);
					if (lastIndex > 0)
					{
						string transPath = propertyPath.Substring(0, lastIndex);
						Console.Write($"<Find:{transPath}>");
						result = result.RecursiveFind(transPath);
						if (result == null)
						{
							Console.WriteLine("EMPTY");
							if (fallback != null && fallback != transform)
								return fallback.RecursiveFindWithProperties(nameOfProperty);
							return null;
						}
					}

					string propPath;
					if (reIndex == -1) propPath = propertyPath.Substring(propIndex);
					else propPath = propertyPath.Substring(propIndex, Math.Max(reIndex - propIndex, 0));
					string propClass = propertyPath.Substring(lastIndex + 1, Math.Max(propIndex - lastIndex - 1, 0));

					Console.Write($"<Class:{propClass}>");
					Component component = result.gameObject.GetComponentWithIndex(propClass);
					if (component == null)
					{
						Console.WriteLine("EMPTY : Cannot find Component " + propClass + "!");
						if (fallback != null && fallback != transform)
							return fallback.RecursiveFindWithProperties(nameOfProperty);
						Console.WriteLine(fallback == null ? "FALLBACK SEARCH TRANSFORM IS NULL" : "FALLBACK SEARCH TRANSFORM IS " + fallback.name);
						Console.WriteLine("RecursiveFindWithProperties failed!");
						return null;
					}
					Console.Write($"<Property:{propPath}>");
					object value = component.GetValueFromPath(propPath);

					if (reIndex == -1)
					{
						Console.WriteLine(value == null ? "EMPTY" : "RETURN");
						return value;
					}

					Console.Write("<GetTrans>");
					result = (value as Component).transform;
					propertyPath = propertyPath.Substring(reIndex);
				}
			}
			catch (Exception)
			{
				if (fallback != null && fallback != transform)
					return fallback.RecursiveFindWithProperties(nameOfProperty);
				//Console.WriteLine(fallback == null ? "FALLBACK SEARCH TRANSFORM IS NULL" : "FALLBACK SEARCH TRANSFORM IS " + fallback.name);
				//Console.WriteLine("RecursiveFindWithProperties failed! " + E);
				return null;
			}
		}

		public static object GetValueFromPath(this Component component, string PropertyPath)
		{
			Type currentType = component.GetType();
			object currentObject = component;
			var props = PropertyPath.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string pprop in props)
			{
				string prop = pprop;
				int arr = prop.IndexOf('[');
				string[] ind = null;
				if (arr != -1)
				{
					ind = prop.Substring(arr + 1).TrimEnd(']').Split(',');

					prop = prop.Substring(0, arr);
				}
				var tfield = currentType.GetField(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
				if (tfield != null)
				{
					currentObject = tfield.GetValue(currentObject);
					//if (currentObject == null)
					//	Console.WriteLine("WARNING: " + tfield.Name + " is null!");
					if (arr != -1)
					{
						//currentObject = tfield.FieldType.
					}
					currentType = tfield.FieldType;
				}
				else
				{
					var tproperty = currentType.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
					if (tproperty != null)
					{
						currentObject = tproperty.GetValue(currentObject, null);
						//if (currentObject == null)
						//	Console.WriteLine("WARNING: " + tproperty.Name + " is null!");
						currentType = tproperty.PropertyType;
					}
					else return null;
				}
			}
			return currentObject;
		}

		// Takes a string of the form "{type} {index}" like "UnityEngine.Rigidbody 1"
		public static Component GetComponentWithIndex(this GameObject obj, string type)
        {
			// Try to get an index if we provided one
            int split = type.IndexOf(' ');
			int index = 0;
			if (split != -1)
				int.TryParse(type.Substring(split + 1), out index);

			// Then get the component with that index
			Component[] components = obj.GetComponents(TTReferences.GetType(type));
			if (components.Length > index)
			    return components[index];
			if (components.Length != 0) 
				return components[0];
			return null;
        }

		// ---------------------------------------------------------------------------------

		public static Type GetFieldType(this MemberInfo info)
		{
			if (info is FieldInfo fieldInfo)
				return fieldInfo.FieldType;
			else if (info is PropertyInfo propertyInfo)
				return propertyInfo.PropertyType;
			return null;
		}

		public static object GetValueOfField(this MemberInfo info, object target)
		{
			if (info is FieldInfo fieldInfo)
				return fieldInfo.GetValue(target);
			else if (info is PropertyInfo propertyInfo && propertyInfo.CanRead)
				return propertyInfo.GetValue(target, null);
			return null;
		}

		public static void SetValueOfField(this MemberInfo info, object target, object value)
		{
			if (info is FieldInfo fieldInfo)
				fieldInfo.SetValue(target, value);
			else if (info is PropertyInfo propertyInfo && propertyInfo.CanWrite)
				propertyInfo.SetValue(target, value, null);
		}
		// ---------------------------------------------------------------------------------
	}
}