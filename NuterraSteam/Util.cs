using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomModules
{
	public static class Util
	{
		public static Material CreateMaterial(Material reference, bool copy, Texture2D albedo, Texture2D gloss, Texture2D emissive)
		{
			// Material - Used in the next step
			Material material = reference;

			bool hasMaterial = reference != null;
			bool hasAlbedo = albedo != null;
			bool hasGloss = gloss != null;
			bool hasEmissive = emissive != null;
			bool hasAnyOverrides = hasAlbedo || hasGloss || hasEmissive;

			if (copy && hasAnyOverrides)
				material = new Material(material);

			// Now apply overrides
			List<string> shaderKeywords = new List<string>(material.shaderKeywords);
			if (hasAlbedo)
				material.SetTexture("_MainTex", albedo);
			if (hasGloss)
			{
				material.SetTexture("_MetallicGlossMap", gloss);
				if (!shaderKeywords.Contains("_METALLICGLOSSMAP"))
					shaderKeywords.Add("_METALLICGLOSSMAP");
			}
			if (hasEmissive)
			{
				material.SetTexture("_EmissionMap", emissive);
				if (!shaderKeywords.Contains("_EMISSION"))
					shaderKeywords.Add("_EMISSION");
				material.SetColor("_EmissionColor", Color.white);
			}
			material.shaderKeywords = shaderKeywords.ToArray();

			return material;
		}
	}
}
