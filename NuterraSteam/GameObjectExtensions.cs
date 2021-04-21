using UnityEngine;
using System;

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

		/*
		 * These are all only used in one place in the deserializer
        public static Component GetComponent(this GameObject obj, Type type, int index)
        {
            Component[] components = obj.GetComponents(type);
            if (components.Length > index)
                return components[index];
            if (components.Length != 0)
                return components[0];
            return null;
        }

        public static Component GetComponent(this GameObject obj, string type, int index)
        {
            Component[] components = obj.GetComponents(GameObjectJSON.GetType(type));
            if (components.Length > index)
                return components[index];
            if (components.Length != 0) 
                return components[0];
            return null;
        }

        public static Component GetComponentWithIndex(this GameObject obj, string type)
        {
            int split = type.IndexOf(' ');
            if (split != -1 && int.TryParse(type.Substring(split + 1), out int index))
            {
                return obj.GetComponent(type.Substring(0, split), index);
            }
            return obj.GetComponent(GameObjectJSON.GetType(type));
        }
		*/
    }
}