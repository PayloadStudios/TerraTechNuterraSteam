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
			NuterraMod.logger.Trace($"🔍 Recursively checking for path {NameOfChild} in transform {transform.name} with hierarchy {HierarchyBuildup}");
			NuterraMod.logger.IncreasePrefix();
			if (NameOfChild == "/")
            {
                NuterraMod.logger.DecreasePrefix();
                return transform;
			}
			string cName = NameOfChild.Substring(NameOfChild.LastIndexOf('/') + 1);

			// Do one flat check on the current GO first
			for (int i = 0; i < transform.childCount; i++)
			{
				var child = transform.GetChild(i);
				NuterraMod.logger.Trace($" 🔎 RecursiveFind check: {child.name}");
				if (child.name == cName)
				{
					string newHierarchy = HierarchyBuildup + "/" + child.name;
					if (newHierarchy.EndsWith(NameOfChild))
					{
						NuterraMod.logger.Trace($"✔️ MATCHED {NameOfChild} to hierarchy {newHierarchy}");
                        NuterraMod.logger.DecreasePrefix();
                        return child;
                    }
                    else
                    {
                        NuterraMod.logger.Trace($" ⚠️ FAILED to match {NameOfChild} to hierarchy {newHierarchy}");
                    }
                }
            }
            NuterraMod.logger.Trace($"⚠️ FAILED to match {NameOfChild} to direct child of {transform.name}");

            // Go to fallback GO check when the first check fails
            for (int i = 0; i < transform.childCount; i++)
			{
				var c = transform.GetChild(i);
				NuterraMod.logger.Trace($"👉 Calling recursiveFind: {c.name}");
				string newHierarchy = HierarchyBuildup + "/" + c.name;
				var child = c.RecursiveFind(NameOfChild, newHierarchy);
				if (child != null)
                {
                    NuterraMod.logger.DecreasePrefix();
                    return child;
				}
            }
            NuterraMod.logger.DecreasePrefix();
            return null;
		}

		public static object RecursiveFindWithProperties(this Transform transform, string nameOfProperty, Transform fallback = null)
		{
			try
			{
				NuterraMod.logger.Debug($"🔍 Searching for {nameOfProperty} under transform {transform.name}, fallback {fallback}");

				int propIndex = nameOfProperty.IndexOf('.');
				if (propIndex == -1)
				{
					var tresult = transform.RecursiveFind(nameOfProperty);
					if (tresult == null && fallback != null) tresult = fallback.RecursiveFind(nameOfProperty);
					return tresult;
				}
				Transform result = transform;

				string propertyPath = nameOfProperty;
				while (true)
				{
					propIndex = propertyPath.IndexOf('.');
					if (propIndex == -1)
					{
						var t = result.RecursiveFind(propertyPath);
						NuterraMod.logger.Trace($"<FindTrans:{propertyPath}>{(t == null ? "EMPTY" : "RETURN")}");
						if (t == null && fallback != null && fallback != transform)
							return fallback.RecursiveFindWithProperties(nameOfProperty);
						return t;
					}
					int reIndex = propertyPath.IndexOf('/', propIndex);
					int lastIndex = propertyPath.LastIndexOf('/', propIndex);
					if (lastIndex > 0)
					{
						string transPath = propertyPath.Substring(0, lastIndex);
						NuterraMod.logger.Trace($"<Find:{transPath}>");
						result = result.RecursiveFind(transPath);
						if (result == null)
						{
							NuterraMod.logger.Trace("EMPTY");
							if (fallback != null && fallback != transform)
								return fallback.RecursiveFindWithProperties(nameOfProperty);
							return null;
						}
					}

					string propPath;
					if (reIndex == -1) propPath = propertyPath.Substring(propIndex);
					else propPath = propertyPath.Substring(propIndex, Math.Max(reIndex - propIndex, 0));
					string propClass = propertyPath.Substring(lastIndex + 1, Math.Max(propIndex - lastIndex - 1, 0));

					NuterraMod.logger.Trace($"<Class:{propClass}>");
					Component component = result.gameObject.GetComponentWithIndex(propClass);
					if (component == null)
					{
						NuterraMod.logger.Warn("EMPTY : Cannot find Component " + propClass + "!");
						if (fallback != null && fallback != transform)
							return fallback.RecursiveFindWithProperties(nameOfProperty);
						NuterraMod.logger.Error(fallback == null ? "FALLBACK SEARCH TRANSFORM IS NULL" : "FALLBACK SEARCH TRANSFORM IS " + fallback.name);
						NuterraMod.logger.Error("RecursiveFindWithProperties failed!");
						return null;
					}
					NuterraMod.logger.Trace($"<Property:{propPath}>");
					object value = component.GetValueFromPath(propPath);

					if (reIndex == -1)
					{
						NuterraMod.logger.Trace(value == null ? "EMPTY" : "RETURN");
						return value;
					}

					NuterraMod.logger.Trace("<GetTrans>");
					result = (value as Component).transform;
					propertyPath = propertyPath.Substring(reIndex);
				}
			}
			catch (Exception)
			{
				if (fallback != null && fallback != transform)
					return fallback.RecursiveFindWithProperties(nameOfProperty);
				//NuterraMod.logger.Log(fallback == null ? "FALLBACK SEARCH TRANSFORM IS NULL" : "FALLBACK SEARCH TRANSFORM IS " + fallback.name);
				//NuterraMod.logger.Log("RecursiveFindWithProperties failed! " + E);
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
					//	NuterraMod.logger.Log("WARNING: " + tfield.Name + " is null!");
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
						//	NuterraMod.logger.Log("WARNING: " + tproperty.Name + " is null!");
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