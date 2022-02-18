using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;


namespace CustomModules
{
    public static class CustomParser
    {
		public static T LenientTryParseEnum<T>(JToken jtoken, T defaultValue) where T : Enum
		{
			string enumString = null;
			switch (jtoken.Type)
			{
				case JTokenType.Float:
					float value = jtoken.ToObject<float>();
					float floored = Mathf.Floor(value);
					if (value == floored)
					{
						enumString = ((int)floored).ToString();
					}
					break;
				case JTokenType.Integer:
					enumString = jtoken.ToObject<int>().ToString();
					break;
				case JTokenType.String:
					enumString = jtoken.ToString();
					break;
				case JTokenType.Boolean:
					enumString = jtoken.ToObject<bool>() ? "1" : "0";
					break;
			}
			if (enumString != null)
			{
				return (T)((object)Enum.Parse(typeof(T), enumString));
			}
			return defaultValue;
		}

		public static T LenientTryParseEnum<T>(JObject obj, string key, T defaultValue) where T : Enum
		{
			JToken jtoken;
			if (obj.TryGetValue(key, out jtoken))
			{
				return LenientTryParseEnum<T>(jtoken, defaultValue);
			}
			return defaultValue;
		}

		public static int LenientTryParseInt(JObject obj, string key, int defaultValue)
		{
			JToken jtoken;
			if (obj.TryGetValue(key, out jtoken))
			{
				if (jtoken.Type == JTokenType.Float)
				{
					return Mathf.FloorToInt(jtoken.ToObject<float>());
				}
				else if (jtoken.Type == JTokenType.Integer)
				{
					return jtoken.ToObject<int>();
				}
				else if (jtoken.Type == JTokenType.String)
				{
					if (int.TryParse(jtoken.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out int parsed))
					{
						return parsed;
					}
					else if (float.TryParse(jtoken.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float parsedFloat))
					{
						return Mathf.FloorToInt(parsedFloat);
					}
				}
				else if (jtoken.Type == JTokenType.Boolean)
				{
					return jtoken.ToObject<bool>() ? 1 : 0;
				}
			}
			return defaultValue;
		}

		public static float LenientTryParseFloat(JObject obj, string key, float defaultValue)
		{
			JToken jtoken;
			if (obj.TryGetValue(key, out jtoken))
			{
				if (jtoken.Type == JTokenType.Float)
				{
					return jtoken.ToObject<float>();
				}
				else if (jtoken.Type == JTokenType.Integer)
				{
					return (float) jtoken.ToObject<int>();
				}
				else if (jtoken.Type == JTokenType.String)
                {
					if (float.TryParse(jtoken.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float parsed))
                    {
						return parsed;
                    }
                }
				else if (jtoken.Type == JTokenType.Boolean)
                {
					return jtoken.ToObject<bool>() ? 1.0f : 0.0f;
                }
			}
			return defaultValue;
		}

		public static bool LenientTryParseBool(JObject obj, string key, bool defaultValue)
		{
			JToken jtoken;
			if (obj.TryGetValue(key, out jtoken))
			{
				if (jtoken.Type == JTokenType.Float)
				{
					return jtoken.ToObject<float>() != 0.0f;
				}
				else if (jtoken.Type == JTokenType.Integer)
				{
					return jtoken.ToObject<int>() != 0;
				}
				else if (jtoken.Type == JTokenType.String)
				{
					if (float.TryParse(jtoken.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float parsed))
					{
						return parsed != 0.0f;
					}
					else if (jtoken.ToString().ToLowerInvariant().Equals("false"))
                    {
						return false;
                    }
					return true;
				}
				else if (jtoken.Type == JTokenType.Boolean)
				{
					return jtoken.ToObject<bool>();
				}
			}
			return defaultValue;
		}

		public static Vector3 GetVector3(JToken token)
		{
			Vector3 result = Vector3.zero;
			if (token.Type == JTokenType.Object)
			{
				JObject jData = (JObject)token;

				if (jData.TryGetValue("x", out JToken xToken) && (xToken.Type == JTokenType.Integer || xToken.Type == JTokenType.Float))
				{
					result.x = xToken.ToObject<float>();

				}
				else if (jData.TryGetValue("X", out xToken) && (xToken.Type == JTokenType.Integer || xToken.Type == JTokenType.Float))
				{
					result.x = xToken.ToObject<float>();
				}

				if (jData.TryGetValue("y", out JToken yToken) && (yToken.Type == JTokenType.Integer || yToken.Type == JTokenType.Float))
				{
					result.y = yToken.ToObject<float>();

				}
				else if (jData.TryGetValue("Y", out yToken) && (yToken.Type == JTokenType.Integer || yToken.Type == JTokenType.Float))
				{
					result.y = yToken.ToObject<float>();
				}

				if (jData.TryGetValue("z", out JToken zToken) && (zToken.Type == JTokenType.Integer || zToken.Type == JTokenType.Float))
				{
					result.z = zToken.ToObject<float>();

				}
				else if (jData.TryGetValue("Z", out zToken) && (zToken.Type == JTokenType.Integer || zToken.Type == JTokenType.Float))
				{
					result.z = zToken.ToObject<float>();
				}
			}
			else if (token.Type == JTokenType.Array)
			{
				JArray jList = (JArray)token;
				for (int i = 0; i < Math.Min(3, jList.Count); i++)
				{
					switch (i)
					{
						case 0:
							result.x = jList[i].ToObject<float>();
							break;
						case 1:
							result.y = jList[i].ToObject<float>();
							break;
						case 2:
							result.z = jList[i].ToObject<float>();
							break;
					}
				}
			}
			return result;
		}

		public static IntVector3 GetVector3Int(JToken token)
		{
			IntVector3 result = IntVector3.zero;
			if (token.Type == JTokenType.Object)
			{
				JObject jData = (JObject)token;

				if (jData.TryGetValue("x", out JToken xToken) && xToken.Type == JTokenType.Integer)
				{
					result.x = xToken.ToObject<int>();

				}
				else if (jData.TryGetValue("X", out xToken) && xToken.Type == JTokenType.Integer)
				{
					result.x = xToken.ToObject<int>();
				}

				if (jData.TryGetValue("y", out JToken yToken) && yToken.Type == JTokenType.Integer)
				{
					result.y = yToken.ToObject<int>();

				}
				else if (jData.TryGetValue("Y", out yToken) && yToken.Type == JTokenType.Integer)
				{
					result.y = yToken.ToObject<int>();
				}

				if (jData.TryGetValue("z", out JToken zToken) && zToken.Type == JTokenType.Integer)
				{
					result.z = zToken.ToObject<int>();

				}
				else if (jData.TryGetValue("Z", out zToken) && zToken.Type == JTokenType.Integer)
				{
					result.z = zToken.ToObject<int>();
				}
			}
			else if (token.Type == JTokenType.Array)
			{
				JArray jList = (JArray)token;
				for (int i = 0; i < Math.Min(3, jList.Count); i++)
				{
					switch (i)
					{
						case 0:
							result.x = jList[i].ToObject<int>();
							break;
						case 1:
							result.y = jList[i].ToObject<int>();
							break;
						case 2:
							result.z = jList[i].ToObject<int>();
							break;
					}
				}
			}
			return result;
		}

		public static Vector3 LenientTryParseVector3(JObject obj, string key, Vector3 defaultValue)
		{
			if (obj.TryGetValue(key, out JToken jtoken))
			{
				return GetVector3(jtoken);
			}
			return defaultValue;
		}

		public static IntVector3 LenientTryParseIntVector3(JObject obj, string key, IntVector3 defaultValue)
		{
			if (obj.TryGetValue(key, out JToken jtoken))
			{
				return GetVector3Int(jtoken);
			}
			return defaultValue;
		}

		private static Dictionary<string, HashSet<string>> GetCasePropertyMap(JObject jData)
		{
			Dictionary<string, HashSet<string>> lowercaseMap = new Dictionary<string, HashSet<string>>();
			foreach (JProperty property in jData.Properties())
			{
				string lower = property.Name.ToLower();
				if (lowercaseMap.ContainsKey(lower))
				{
					lowercaseMap[lower].Add(property.Name);
				}
				else
				{
					lowercaseMap[lower] = new HashSet<string> { property.Name };
				}
			}
			return lowercaseMap;
		}

		private static int CompareStringPrefix(string a, string b)
		{
			if (a is null || b is null)
			{
				return 0;
			}
			int score = 0;
			for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
			{
				if (a[i] == b[i])
				{
					score++;
				}
				else
				{
					return score;
				}
			}
			return score;
		}

		private static string GetClosestString(string value, HashSet<string> values)
		{
			int bestScore = 0;
			string bestTarget = values.First();
			foreach (string target in values)
			{
				int score = CompareStringPrefix(value, target);
				if (score > bestScore)
				{
					bestScore = score;
					bestTarget = target;
				}
			}
			return bestTarget;
		}

		public static bool TryGetFloatMultipleKeys(JObject jData, out float result, float defaultValue, params string[] args)
		{
			foreach (string arg in args)
			{
				if (jData.TryGetValue(arg, out JToken jToken))
				{
					if (jToken.Type == JTokenType.Float)
					{
						result = jToken.ToObject<float>();
						return true;
					}
					else if (jToken.Type == JTokenType.Integer)
					{
						result = jToken.ToObject<int>();
						return true;
					}
				}
			}
			Dictionary<string, HashSet<string>> lowerMap = GetCasePropertyMap(jData);
			foreach (string arg in args)
			{
				if (lowerMap.TryGetValue(arg.ToLower(), out HashSet<string> values))
				{
					if (jData.TryGetValue(GetClosestString(arg, values), out JToken jToken))
					{
						if (jToken.Type == JTokenType.Float)
						{
							result = jToken.ToObject<float>();
							return true;
						}
						else if (jToken.Type == JTokenType.Integer)
						{
							result = jToken.ToObject<int>();
							return true;
						}
					}
				}
			}
			result = defaultValue;
			return false;
		}
		public static bool TryGetIntMultipleKeys(JObject jData, out int result, int defaultValue, params string[] args)
		{
			foreach (string arg in args)
			{
				if (jData.TryGetValue(arg, out JToken jToken))
				{
					if (jToken.Type == JTokenType.Integer)
					{
						result = jToken.ToObject<int>();
						return true;
					}
				}
			}
			Dictionary<string, HashSet<string>> lowerMap = GetCasePropertyMap(jData);
			foreach (string arg in args)
			{
				if (lowerMap.TryGetValue(arg.ToLower(), out HashSet<string> values))
				{
					if (jData.TryGetValue(GetClosestString(arg, values), out JToken jToken))
					{
						if (jToken.Type == JTokenType.Integer)
						{
							result = jToken.ToObject<int>();
							return true;
						}
					}
				}
			}
			result = defaultValue;
			return false;
		}

		public static bool TryGetTokenMultipleKeys(JObject jData, out JToken token, params string[] args)
		{
			foreach (string arg in args)
			{
				if (jData.TryGetValue(arg, out JToken jToken))
				{
					token = jToken;
					return true;
				}
			}
			Dictionary<string, HashSet<string>> lowerMap = GetCasePropertyMap(jData);
			foreach (string arg in args)
			{
				if (lowerMap.TryGetValue(arg.ToLower(), out HashSet<string> values))
				{
					if (jData.TryGetValue(GetClosestString(arg, values), out JToken jToken))
					{
						token = jToken;
						return true;
					}
				}
			}
			token = null;
			return false;
		}

		public static bool TryGetBool(JObject jData, bool defaultValue, string key)
		{
			if (jData.TryGetValue(key, out JToken jToken) && jToken.Type == JTokenType.Boolean)
			{
				return jToken.ToObject<bool>();
			}
			else
			{
				Dictionary<string, HashSet<string>> lowerMap = GetCasePropertyMap(jData);
				if (lowerMap.TryGetValue(key.ToLower(), out HashSet<string> values))
				{
					if (jData.TryGetValue(GetClosestString(key, values), out jToken) && jToken.Type == JTokenType.Boolean)
					{
						return jToken.ToObject<bool>();
					}
				}
			}
			return defaultValue;
		}

		public static bool GetBoolMultipleKeys(JObject jData, bool defaultValue, params string[] args)
		{
			foreach (string arg in args)
			{
				if (jData.TryGetValue(arg, out JToken jToken) && jToken.Type == JTokenType.Boolean)
				{
					return jToken.ToObject<bool>();
				}
			}
			Dictionary<string, HashSet<string>> lowerMap = GetCasePropertyMap(jData);
			foreach (string arg in args)
			{
				if (lowerMap.TryGetValue(arg.ToLower(), out HashSet<string> values))
				{
					if (jData.TryGetValue(GetClosestString(arg, values), out JToken jToken) && jToken.Type == JTokenType.Boolean)
					{
						return jToken.ToObject<bool>();
					}
				}
			}
			return defaultValue;
		}

		public static bool TryGetStringMultipleKeysAllowNull(JObject jData, out string result, params string[] args)
		{
			foreach (string arg in args)
			{
				if (jData.TryGetValue(arg, out JToken jToken))
				{
					if (jToken.Type == JTokenType.Null)
					{
						result = null;
						return true;
					}
					else if (jToken.Type == JTokenType.String)
					{
						string test = jToken.ToString();
						if (test != "null")
						{
							result = test;
							return true;
						}
					}
				}
			}
			Dictionary<string, HashSet<string>> lowerMap = GetCasePropertyMap(jData);
			foreach (string arg in args)
			{
				if (lowerMap.TryGetValue(arg.ToLower(), out HashSet<string> values))
				{
					if (jData.TryGetValue(GetClosestString(arg, values), out JToken jToken))
					{
						if (jToken.Type == JTokenType.Null)
						{
							result = null;
							return true;
						}
						else if (jToken.Type == JTokenType.String)
						{
							string test = jToken.ToString();
							if (test != "null")
							{
								result = test;
								return true;
							}
						}
					}
				}
			}

			result = null;
			return false;
		}

		public static bool TryGetStringMultipleKeys(JObject jData, out string result, params string[] args)
		{
			result = null;
			return TryGetStringMultipleKeysAllowNull(jData, out result, args) && result != null;
		}
	}
}
