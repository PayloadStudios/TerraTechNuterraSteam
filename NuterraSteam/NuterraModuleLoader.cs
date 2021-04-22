using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CustomModules
{
    public class NuterraModuleLoader : JSONModuleLoader
	{
		private static Dictionary<string, Material> sMaterialCache = new Dictionary<string, Material>();

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


				def.m_MaxHealth = TryParse(jData, "HP", def.m_MaxHealth);

				// ------------------------------------------------------
				#region Reference - Copy a vanilla block
				bool keepRenderers = TryParse(jData, "KeepRenderers", true);
				bool keepReferenceRenderers = TryParse(jData, "KeepReferenceRenderers", true);
				bool keepColliders = TryParse(jData, "KeepColliders", true);

				if (jData.TryGetValue("GamePrefabReference", out JToken jRef) && jRef.Type == JTokenType.String)
				{
					// This code block copies our chosen reference block
					// TTQMM REF: BlockPrefabBuilder.Initialize

					GameObject originalGameObject = TTReferences.FindBlockByName(jRef.ToString());
					TankBlock original = originalGameObject.GetComponent<TankBlock>();
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
				#endregion
				// ------------------------------------------------------

				// ------------------------------------------------------
				// Get some references set up for the next phase, now our prefab is setup
				Damageable damageable = GetOrAddComponent<Damageable>(block);
				ModuleDamage moduleDamage = GetOrAddComponent<ModuleDamage>(block);
				Visible visible = GetOrAddComponent<Visible>(block);
				Transform transform = block.transform;
				transform.position = Vector3.zero;
				transform.rotation = Quaternion.identity;
				transform.localScale = Vector3.one;

				// ------------------------------------------------------
				#region Additional References
				if (jData.TryGetValue("DeathExplosionReference", out JToken jDeathRef) && jDeathRef.Type == JTokenType.String)
				{
					string deathExpRef = jDeathRef.ToString();
					GameObject refBlock = TTReferences.FindBlockFromString(deathExpRef);
					if(refBlock != null)
					{
						moduleDamage.deathExplosion = refBlock.GetComponent<ModuleDamage>().deathExplosion;
					}
				}


				#endregion
				// ------------------------------------------------------

				// ------------------------------------------------------
				#region Tweaks
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
				// TODO: CellMap 
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
				// Some basic block stats
				damageable.DamageableType = (ManDamage.DamageableType)TryParse(jData, "DamageableType", (int)damageable.DamageableType);
				moduleDamage.m_DamageDetachFragility = TryParse(jData, "DetachFragility", moduleDamage.m_DamageDetachFragility);
				block.m_DefaultMass = TryParse(jData, "Mass", block.m_DefaultMass);
				// Center of Mass
				if (jData.TryGetValue("CenterOfMass", out JToken com) && com.Type == JTokenType.Array)
				{
					JArray comVector = (JArray)com;
					Transform comTrans = block.transform.Find("CentreOfMass");
					if (comTrans == null)
					{
						comTrans = new GameObject("CentreOfMass").transform;
						comTrans.SetParent(block.transform);
						comTrans.localScale = Vector3.one;
						comTrans.localRotation = Quaternion.identity;
					}
					comTrans.localPosition = new Vector3(comVector[0].ToObject<float>(), comVector[1].ToObject<float>(), comVector[2].ToObject<float>());

					// TODO: Weird thing about offseting colliders from Nuterra
					//for (int i = 0; i < Prefab.transform.childCount; i++)
					//{
					//	transform = Prefab.transform.GetChild(i);
					//	if (transform.name.Length < 5 && transform.name.EndsWith("col")) // "[a-z]col"
					//		transform.localPosition = CenterOfMass;
					//}
				}
				// IconName override
				if(jData.TryGetValue("IconName", out JToken jIconName) && jIconName.Type == JTokenType.String)
				{
					UnityEngine.Object obj = mod.FindAsset(jIconName.ToString());
					if(obj != null && obj is Sprite)
					{
						def.m_Icon = ((Sprite)obj).texture;
					}
				}

				// TODO: Emission Mode
				// Filepath? For reparse?
				#endregion
				// ------------------------------------------------------

				// Start recursively adding objects with the root. 
				// Calling it this way and treating the root as a sub-object prevents a lot of code duplication
				RecursivelyAddSubObject(block, mod, block.transform, jData, TTReferences.kMissingTextureTankBlock, false);

				return true;
			}
			return false;
		}

		private void RecursivelyAddSubObject(TankBlock block, ModContents mod, Transform parentTransform, JObject jData, Material defaultMaterial, bool isNewSubObject)
		{
			// Material - Used in the next step
			Material mat = null;

			bool hasMaterial = jData.TryGetValue("MeshMaterialName", out JToken jMaterial) && jMaterial.Type == JTokenType.String;
			bool hasAlbedo = jData.TryGetValue("MeshTextureName", out JToken jAlbedo) && jAlbedo.Type == JTokenType.String;
			bool hasGloss = jData.TryGetValue("MeshGlossTextureName", out JToken jGloss) && jGloss.Type == JTokenType.String;
			bool hasEmissive = jData.TryGetValue("MeshEmissionTextureName", out JToken jEmissive) && jEmissive.Type == JTokenType.String;
			bool hasAnyOverrides = hasAlbedo || hasGloss || hasEmissive;

			// Calculate a unique string that defines this material
			string materialName = "";
			if (hasMaterial)
				materialName = $"{materialName}M:{jMaterial.ToString()};";
			if (hasAlbedo)
				materialName = $"{materialName}A:{jAlbedo.ToString()};";
			if (hasGloss)
				materialName = $"{materialName}G:{jGloss.ToString()};";
			if (hasEmissive)
				materialName = $"{materialName}E:{jEmissive.ToString()};";

			if (hasAnyOverrides)
			{
				if (sMaterialCache.TryGetValue(materialName, out Material existingMat))
					mat = existingMat;
				else
				{
					
					// Default to missing texture, then see if we have a base texture reference
					mat = defaultMaterial;
					if (hasMaterial)
					{
						string matName = jMaterial.ToString().Replace("Venture_", "VEN_").Replace("GeoCorp_", "GC_");
						mat = TTReferences.FindMaterial(matName);
					}

					Texture2D albedo = hasAlbedo ? TTReferences.Find<Texture2D>(jAlbedo.ToString(), mod) : null;
					Texture2D gloss = hasGloss ? TTReferences.Find<Texture2D>(jGloss.ToString(), mod) : null;
					Texture2D emissive = hasEmissive ? TTReferences.Find<Texture2D>(jEmissive.ToString(), mod) : null;
					mat = Util.CreateMaterial(mat, true, albedo, gloss, emissive);

					// Cache our newly created material in case it comes up again
					sMaterialCache.Add(materialName, mat);
				}
			}
			else
			{
				// Actually, if we make no references, we can keep official modded block behaviour
				//mat = TTBlockReferences.kMissingTextureTankBlock;
			}

			// Physics Material - Used in the next step
			PhysicMaterial physMat = new PhysicMaterial();
			if (jData.TryGetValue("Friction", out JToken jFriction))
				physMat.dynamicFriction = jFriction.ToObject<float>();
			if (jData.TryGetValue("StaticFriction", out JToken jStaticFriction))
				physMat.staticFriction = jStaticFriction.ToObject<float>();
			if (jData.TryGetValue("Bounciness", out JToken jBounce))
				physMat.bounciness = jBounce.ToObject<float>();
			// MeshName & ColliderMeshName Override
			Mesh mesh = null;
			Mesh colliderMesh = null;
			bool supressBoxColliderFallback = TryParse(jData, "SupressBoxColliderFallback", false);
			if (jData.TryGetValue("MeshName", out JToken jMeshName) && jMeshName.Type == JTokenType.String)
			{
				UnityEngine.Object obj = mod.FindAsset(jMeshName.ToString());
				if (obj != null && obj is Mesh)
				{
					mesh = (Mesh)obj;
				}
			}
			if (jData.TryGetValue("ColliderMeshName", out JToken jColliderMeshName) && jColliderMeshName.Type == JTokenType.String)
			{
				UnityEngine.Object obj = mod.FindAsset(jColliderMeshName.ToString());
				if (obj != null && obj is Mesh)
				{
					colliderMesh = (Mesh)obj;
				}
			}

			// This is the only bit where the root object majorly differs from subobjects
			if (parentTransform == block.transform)
			{
				// Generally speaking, the root of a TT block does not have meshes, so needs to add a child
				// Add mesh / collider mesh sub GameObject if we have either
				if (mesh != null || colliderMesh != null)
				{
					GameObject meshObj = CreateMeshGameObject(parentTransform, mesh, mat, colliderMesh, physMat, supressBoxColliderFallback);
				}
			}
			else // However, if we are poking around in a subobject, we may want to swap out existing renderers
			{
				// If we provided a new mesh, do a full swap
				if (mesh != null)
				{
					parentTransform.gameObject.EnsureComponent<MeshFilter>().sharedMesh = mesh;
					parentTransform.gameObject.EnsureComponent<MeshRenderer>().sharedMaterial = mat;
				}
				else // If we don't want to swap out the mesh we may still want to swap out the properties of existing renderers
				{
					bool forceEmissive = TryParse(jData, "ForceEmission", false);
					foreach(Renderer renderer in parentTransform.GetComponents<Renderer>())
					{
						renderer.sharedMaterial = mat;
						if (renderer is ParticleSystemRenderer psrenderer)
							psrenderer.trailMaterial = mat;

						if(forceEmissive)
							MaterialSwapper.SetMaterialPropertiesOnRenderer(renderer, ManTechMaterialSwap.MaterialColour.Normal, 1f, 0);
					}
				}

				// If we provided a collider mesh, do a full swap
				if(colliderMesh != null)
				{
					MeshCollider mc = parentTransform.gameObject.EnsureComponent<MeshCollider>();
					mc.convex = true;
					mc.sharedMesh = colliderMesh;
					mc.sharedMaterial = physMat;
				}
				// If we want a box collider, try to make one from our mesh
				bool makeBoxCollider = TryParse(jData, "MakeBoxCollider", false);
				if(makeBoxCollider)
				{
					BoxCollider bc = parentTransform.gameObject.EnsureComponent<BoxCollider>();
					bc.sharedMaterial = physMat;
					if (mesh != null)
					{
						mesh.RecalculateBounds();
						bc.size = mesh.bounds.size - Vector3.one * 0.2f;
						bc.center = mesh.bounds.center;
					}
					else 
					{
						bc.size = Vector3.one;
						bc.center = Vector3.zero;
					}
				}
				// Weird option from TTQMM that has a fixed size sphere
				bool makeSphereCollider = TryParse(jData, "MakeSphereCollider", false);
				if(makeSphereCollider)
				{
					SphereCollider sc = parentTransform.gameObject.EnsureComponent<SphereCollider>();
					sc.radius = 0.5f;
					sc.center = Vector3.zero;
					sc.sharedMaterial = physMat;
				}
				// Allow resizing of sub objects
				if(jData.TryGetValue("SubScale", out JToken jSubScale))
				{
					parentTransform.localScale = GetVector3(jSubScale);
				}
			}

			// ------------------------------------------------------
			#region Sub objects
			if (jData.TryGetValue("SubObjects", out JToken jSubObjectList) && jSubObjectList.Type == JTokenType.Array)
			{
				foreach (JToken token in (JArray)jSubObjectList)
				{
					if (token.Type == JTokenType.Object)
					{
						JObject jSubObject = (JObject)token;

						string subObjName = TryParse(jSubObject, "SubOverrideName", "");
						GameObject target = (parentTransform.RecursiveFindWithProperties(subObjName) as Component)?.gameObject;
						bool creatingNew = target = null;
						if (target != null)
						{
							if (jData.TryGetValue("Layer", out JToken jLayer) && jLayer.Type == JTokenType.Integer)
								target.layer = jLayer.ToObject<int>();
						}
						else // Reference was not matched, so we want to add a new subobject
						{
							if (subObjName.NullOrEmpty())
								subObjName = $"SubObject_{parentTransform.childCount + 1}";

							target = new GameObject(subObjName);
							target.transform.parent = parentTransform;
							target.transform.localPosition = Vector3.zero;
							target.transform.localRotation = Quaternion.identity;

							if (jData.TryGetValue("Layer", out JToken jLayer) && jLayer.Type == JTokenType.Integer)
								target.layer = jLayer.ToObject<int>();
							else
								target.layer = 8; // Globals.inst.layerTank;
						}

						// Target acquired, lets tweak a few things
						bool destroyColliders = TryParse(jSubObject, "DestroyExistingColliders", false);
						if(destroyColliders)
						{
							foreach (Collider col in target.GetComponents<Collider>())
								UnityEngine.Object.DestroyImmediate(col);
						}

						bool destroyRenderers = TryParse(jSubObject, "DestroyExistingRenderer", false);
						if (destroyRenderers)
						{
							foreach (Renderer renderer in target.GetComponents<Renderer>())
								UnityEngine.Object.DestroyImmediate(renderer);
							foreach (MeshFilter mf in target.GetComponents<MeshFilter>())
								UnityEngine.Object.DestroyImmediate(mf);
						}

						// If there is already a material set on this sub object ref, use it
						if(!creatingNew && !destroyRenderers)
						{
							Renderer ren = target.GetComponent<Renderer>();
							if (ren)
								mat = ren.sharedMaterial;
						}

						// Optional resize settings
						if (jSubObject.TryGetValue("SubPosition", out JToken jPos) && jPos.Type == JTokenType.Object)
							target.transform.localPosition = GetVector3(jPos);
						if (jSubObject.TryGetValue("SubRotation", out JToken jEuler) && jEuler.Type == JTokenType.Object)
							target.transform.localEulerAngles = GetVector3(jEuler);
						if (jSubObject.TryGetValue("SubScale", out JToken jScale) && jScale.Type == JTokenType.Object)
							target.transform.localScale = GetVector3(jScale);

						RecursivelyAddSubObject(block, mod, target.transform, jSubObject, mat, creatingNew);

					}
				}
			}
			#endregion
			// ------------------------------------------------------

			// ------------------------------------------------------
			#region Deserializers
			if (jData.TryGetValue("Deserializer", out JToken jDeserialObj) && jDeserialObj.Type == JTokenType.Object)
			{
				JObject jDeserializer = (JObject)jDeserialObj;

				// TTQMM Ref: GameObjectJSON.CreateGameObject(jBlock.Deserializer, blockbuilder.Prefab);

				foreach (KeyValuePair<string, JToken> kvp in jDeserializer)
				{
					string[] split = kvp.Key.Split('|');
					if (split.Length == 0)
					{

					}
				}
			}
			#endregion
			// ------------------------------------------------------
		}

		private GameObject CreateMeshGameObject(Transform parent, Mesh mesh, Material mat, Mesh colliderMesh, PhysicMaterial physMat, bool supressBoxColliderFallback)
		{
			// TTQMM Ref: SetModel's various variants
			GameObject model = new GameObject("m_MeshRenderer");
			if (colliderMesh != null)
			{
				// TTQMM Ref: blockbuilder.SetModel(mesh, colliderMesh, true, localmat, localphysmat);
				MeshCollider mc = model.AddComponent<MeshCollider>();
				mc.convex = true;
				mc.sharedMesh = colliderMesh;
				mc.sharedMaterial = physMat;
			}
			else if (!supressBoxColliderFallback) // if(CreateBoxCollider)
			{
				BoxCollider bc = model.AddComponent<BoxCollider>();
				if (mesh != null)
				{
					mesh.RecalculateBounds();
					bc.size = mesh.bounds.size - Vector3.one * 0.2f;
					bc.center = mesh.bounds.center;
				}
				bc.sharedMaterial = physMat;
			}

			if (mesh != null)
			{
				// TTQMM Ref: blockbuilder.SetModel(mesh, !jBlock.SupressBoxColliderFallback, localmat, localphysmat);
				model.AddComponent<MeshFilter>().sharedMesh = mesh;
				model.AddComponent<MeshRenderer>().sharedMaterial = mat ?? TTReferences.kMissingTextureTankBlock;
			}

			model.transform.parent = parent;
			model.transform.localPosition = Vector3.zero;
			model.transform.localRotation = Quaternion.identity;
			model.layer = Globals.inst.layerTank;
			return model;
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