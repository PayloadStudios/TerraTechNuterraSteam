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

		private RecipeTable.Recipe ParseRecipe(JObject jData, string corp, int blockID, out int RecipePrice)
        {
			RecipePrice = 0;
			if (jData.TryGetValue("Recipe", out JToken jRecipe))
			{
				Debug.Log($"[Nuterra] Recipe detected: {jRecipe.ToString()}");

				RecipeTable.Recipe recipe = new RecipeTable.Recipe();
				Dictionary<ChunkTypes, RecipeTable.Recipe.ItemSpec> dictionary = new Dictionary<ChunkTypes, RecipeTable.Recipe.ItemSpec>();

				if (jRecipe is JValue rString)
				{
					string[] recipeString = rString.ToObject<string>().Replace(" ", "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
					Debug.Log($"[Nuterra] Adjusted Recipe Str: {recipeString}");
					foreach (string item in recipeString)
					{
						RecipePrice += AppendToRecipe(dictionary, item, 1);
					}
				}
				else if (jRecipe is JObject rObject)
				{
					foreach (var item in rObject)
					{
						RecipePrice += AppendToRecipe(dictionary, item.Key, item.Value.ToObject<int>());
					}
				}
				else if (jRecipe is JArray rArray)
				{
					foreach (var item in rArray)
					{
						RecipePrice += AppendToRecipe(dictionary, item.ToString(), 1);
					}
				}

				recipe.m_InputItems = new RecipeTable.Recipe.ItemSpec[dictionary.Count];
				dictionary.Values.CopyTo(recipe.m_InputItems, 0);
				recipe.m_OutputItems[0] = new RecipeTable.Recipe.ItemSpec(new ItemTypeInfo(ObjectTypes.Block, blockID), 1);
				Singleton.Manager<RecipeManager>.inst.RegisterCustomBlockFabricatorRecipe(blockID, corp, recipe);

				return recipe;
			}
			else
			{
				Debug.Log("[Nuterra] No Recipe Found");
			}
			return null;
		}

		// This method should add a module to the TankBlock prefab
		public override bool CreateModuleForBlock(int blockID, ModdedBlockDefinition def, TankBlock block, JToken jToken)
		{
			try
			{
				Debug.Log("[Nuterra] Loading CustomBlock module");

				if (jToken.Type == JTokenType.Object)
				{
					JObject jData = (JObject)jToken;

					// Get the mod contents so we can search for additional assets
					ModContainer container = ManMods.inst.FindMod(def);
					ModContents mod = container != null ? container.Contents : null;
					if (mod == null)
					{
						Debug.LogError("[Nuterra] Could not find mod that this unoffical block is part of"); 
						return false;
					}

					// ------------------------------------------------------
					// Basics like name, desc etc. The Official Mod Tool lets us set these already, but we might want to override
					def.m_BlockDisplayName = TryParse(jData, "Name", def.m_BlockDisplayName);
					def.m_BlockDescription = TryParse(jData, "Description", def.m_BlockDescription);
					// Ignore block ID. Official loader handles IDs automatically.
					// Ignore corporation. Custom corps no longer have a fixed ID, so we should use the official tool to set corp IDs.
					//def.m_Corporation = TryParse(jData, "Corporation", def.m_Corporation);
					block.m_BlockCategory = def.m_Category = TryParseEnum(jData, "Category", def.m_Category);
					def.m_Rarity = TryParseEnum(jData, "Rarity", def.m_Rarity);
					def.m_Grade = TryParse(jData, "Grade", def.m_Grade);

					// Recipe
					ParseRecipe(jData, def.m_Corporation, blockID, out int RecipePrice);
					def.m_Price = RecipePrice * 3;
					int overridePrice = TryParse(jData, "Price", 0);
					if (overridePrice > 0) {
						Debug.Log($"[Nuterra] Read override price of {overridePrice}");
						def.m_Price = overridePrice;
					}
					else
                    {
						Debug.Log($"[Nuterra] No price specified. Falling back on calculated recipe price * 3: {def.m_Price}");
					}
					def.m_MaxHealth = TryParse(jData, "HP", def.m_MaxHealth);

					// ------------------------------------------------------
					#region Reference - Copy a vanilla block
					Debug.Log("[Nuterra] Starting references");
					bool keepRenderers = TryParse(jData, "KeepRenderers", true);
					bool keepReferenceRenderers = TryParse(jData, "KeepReferenceRenderers", true);
					bool keepColliders = TryParse(jData, "KeepColliders", true);

					if(TryGetStringMultipleKeys(jData, out string referenceBlock, "GamePrefabReference", "PrefabReference"))
					{
						// This code block copies our chosen reference block
						// TTQMM REF: BlockPrefabBuilder.Initialize

						GameObject originalGameObject = TTReferences.FindBlockFromString(referenceBlock);
						if (originalGameObject != null)
						{
							GameObject newObject = UnityEngine.Object.Instantiate(originalGameObject);
							// Assign this back to block for further processing
							block = GetOrAddComponent<TankBlock>(newObject.transform);
							//TankBlock original = originalGameObject.GetComponent<TankBlock>();
							//TankBlock copy = UnityEngine.Object.Instantiate(original);
							TankBlockTemplate fakeTemplate = newObject.AddComponent<TankBlockTemplate>();
							// Cheeky hack to swap the prefab
							// The official block loader doesn't expect this to happen, but I will assume
							// for now that you are making 100% official or 100% unofficial JSONs
							def.m_PhysicalPrefab = fakeTemplate;

							Debug.Log($"[Nuterra] Found game prefab reference as {newObject}");

							// TTQMM REF: DirectoryBlockLoader.CreateJSONBlock, the handling of these flags is a bit weird
							if (keepRenderers && !keepColliders)
								RemoveChildren<Collider>(block);
							if (!keepRenderers && !keepReferenceRenderers)
							{
								RemoveChildren<MeshRenderer>(block);
								RemoveChildren<TankTrack>(block);
								RemoveChildren<SkinnedMeshRenderer>(block);
								RemoveChildren<MeshFilter>(block);

								if (!keepColliders)
									RemoveChildren<Collider>(block);
							}

							newObject.layer = Globals.inst.layerTank;
							newObject.tag = "TankBlock";

							
							bool hasRefOffset = TryGetTokenMultipleKeys(jData, out JToken jOffset, "ReferenceOffset", "PrefabOffset", "PrefabPosition");
							bool hasRefRotation = TryGetTokenMultipleKeys(jData, out JToken jEuler, "ReferenceRotationOffset", "PrefabRotation");
							bool hasRefScale = TryGetTokenMultipleKeys(jData, out JToken jScale, "ReferenceScale", "PrefabScale");

							if (hasRefOffset || hasRefRotation || hasRefScale)
							{
								Vector3 offset = hasRefOffset ? GetVector3(jOffset) : Vector3.zero;
								Vector3 scale = hasRefScale ? GetVector3(jScale) : Vector3.one;
								Vector3 euler = hasRefRotation ? GetVector3(jEuler) : Vector3.zero;

								foreach (Transform child in newObject.transform)
								{
									if (hasRefOffset)
										child.localPosition += offset;
									if (hasRefRotation)
										child.localEulerAngles += euler;
									if (hasRefScale)
										child.localScale += scale;
								}
							}
						}
						else
						{
							Debug.LogError($"[Nuterra] Failed to find GamePrefabReference {referenceBlock}");
						}
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
					if (TryGetStringMultipleKeys(jData, out string referenceExplosion, "DeathExplosionReference", "ExplosionReference"))
					{
						GameObject refBlock = TTReferences.FindBlockFromString(referenceExplosion);
						if (refBlock != null)
						{
							moduleDamage.deathExplosion = refBlock.GetComponent<ModuleDamage>().deathExplosion;
							Debug.Log($"[Nuterra] Swapped death explosion for {refBlock}");
						}
					}
					#endregion
					// ------------------------------------------------------

					// ------------------------------------------------------
					#region Tweaks
					// BlockExtents is a way of quickly doing a cuboid Filled Cell setup
					if (jData.TryGetValue("BlockExtents", out JToken jExtents) && jExtents.Type == JTokenType.Object)
					{
						List<IntVector3> filledCells = new List<IntVector3>();
						int x = ((JObject)jExtents).GetValue("x").ToObject<int>();
						int y = ((JObject)jExtents).GetValue("y").ToObject<int>();
						int z = ((JObject)jExtents).GetValue("z").ToObject<int>();
						for (int i = 0; i < x; i++)
							for (int j = 0; j < y; j++)
								for (int k = 0; k < z; k++)
								{
									filledCells.Add(new IntVector3(i, j, k));
								}
						block.filledCells = filledCells.ToArray();
						Debug.Log("[Nuterra] Overwrote BlockExtents");
					}
					// CellMap / CellsMap
					if(TryGetTokenMultipleKeys(jData, out JToken jCellMap, "CellMap", "CellsMap"))
                    {
						string[][] ZYXCells = jCellMap.ToObject<string[][]>();
						List<IntVector3> cells = new List<IntVector3>();
						for (int z = 0; z < ZYXCells.Length; z++)
						{
							string[] YXslice = ZYXCells[z];
							if (YXslice == null)
								continue;

							for (int y = 0, ry = YXslice.Length - 1; ry >= 0; y++, ry--)
							{
								string Xline = YXslice[ry];
								if (Xline == null)
									continue;

								for (int x = 0; x < Xline.Length; x++)
								{
									char cell = Xline[x];
									if (cell != ' ')
										cells.Add(new IntVector3(x, y, z));
								}
							}
						}
						block.filledCells = cells.ToArray();
					}
					// TODO: APsOnlyAtBottom / MakeAPsAtBottom
					if (jData.TryGetValue("Cells", out JToken jCells) && jCells.Type == JTokenType.Array)
					{
						List<IntVector3> filledCells = new List<IntVector3>();
						foreach (JObject jCell in (JArray)jCells)
						{
							filledCells.Add(GetVector3Int(jCell));
						}
						block.filledCells = filledCells.ToArray();
					}
					// APs
					if (jData.TryGetValue("APs", out JToken jAPList) && jAPList.Type == JTokenType.Array)
					{
						List<Vector3> aps = new List<Vector3>();
						foreach (JToken token in (JArray)jAPList)
						{
							aps.Add(GetVector3(token));
						}
						block.attachPoints = aps.ToArray();
					}
					// Some basic block stats
					damageable.DamageableType = (ManDamage.DamageableType)TryParse(jData, "DamageableType", (int)damageable.DamageableType);
					if(TryGetFloatMultipleKeys(jData, out float fragility, moduleDamage.m_DamageDetachFragility, "DetachFragility", "Fragility"))
					{
						moduleDamage.m_DamageDetachFragility = fragility;
					}

					block.m_DefaultMass = TryParse(jData, "Mass", block.m_DefaultMass);

					// Center of Mass
					JArray jComVector = null;
					if (jData.TryGetValue("CenterOfMass", out JToken com1) && com1.Type == JTokenType.Array)
						jComVector = (JArray)com1;
					if (jData.TryGetValue("CentreOfMass ", out JToken com2) && com2.Type == JTokenType.Array)
						jComVector = (JArray)com2;
					if (jComVector != null)
					{
						Transform comTrans = block.transform.Find("CentreOfMass");
						if (comTrans == null)
						{
							comTrans = new GameObject("CentreOfMass").transform;
							comTrans.SetParent(block.transform);
							comTrans.localScale = Vector3.one;
							comTrans.localRotation = Quaternion.identity;
						}
						comTrans.localPosition = new Vector3(jComVector[0].ToObject<float>(), jComVector[1].ToObject<float>(), jComVector[2].ToObject<float>());

						// TODO: Weird thing about offseting colliders from Nuterra
						//for (int i = 0; i < Prefab.transform.childCount; i++)
						//{
						//	transform = Prefab.transform.GetChild(i);
						//	if (transform.name.Length < 5 && transform.name.EndsWith("col")) // "[a-z]col"
						//		transform.localPosition = CenterOfMass;
						//}
					}

					// TODO: RotationGroup

					// IconName override
					if (jData.TryGetValue("IconName", out JToken jIconName) && jIconName.Type == JTokenType.String)
					{
						UnityEngine.Object obj = mod.FindAsset(jIconName.ToString());
						if (obj != null)
						{
							if (obj is Sprite sprite)
								def.m_Icon = sprite.texture;
							else if (obj is Texture2D texture)
								def.m_Icon = texture;
							else
							{
								Debug.LogWarning($"Found unknown object type {obj.GetType()} for icon override for {block.name}");
							}
						}
					}

					// TODO: Emission Mode
					// Filepath? For reparse?
					#endregion
					// ------------------------------------------------------

					// Start recursively adding objects with the root. 
					// Calling it this way and treating the root as a sub-object prevents a lot of code duplication
					RecursivelyAddSubObject(block, mod, block.transform, jData, TTReferences.kMissingTextureTankBlock, false);

					// Forcibly reset ColliderSwapper so it gets recalculated correctly every time.
					ColliderSwapper swapper = block.transform.GetComponentInChildren<ColliderSwapper>();
					if (swapper != null)
                    {
						swapper.m_Colliders = null;
                    }

					// Weird export fix up for meshes
					// Flip everything in x
					foreach (MeshRenderer mr in block.GetComponentsInChildren<MeshRenderer>())
					{
						mr.transform.localScale = new Vector3(-mr.transform.localScale.x, mr.transform.localScale.y, mr.transform.localScale.z);
					}
					foreach (MeshCollider mc in block.GetComponentsInChildren<MeshCollider>())
					{
						if(mc.GetComponent<MeshRenderer>() == null) // Skip ones with both. Don't want to double flip
							mc.transform.localScale = new Vector3(-mc.transform.localScale.x, mc.transform.localScale.y, mc.transform.localScale.z);
					}

					return true;
				}
				return false;
			}
			catch(Exception e)
			{
				Debug.LogError($"[Nuterra] Caught exception {e}");
				return false;
			}
		}

        public override bool InjectBlock(int blockID, ModdedBlockDefinition def, JToken jToken)
        {
			JObject jData = (JObject)jToken;
			RecipeTable.Recipe recipe = ParseRecipe(jData, def.m_Corporation, blockID, out int price);

			if (recipe != null)
			{
				Singleton.Manager<RecipeManager>.inst.RegisterCustomBlockFabricatorRecipe(blockID, def.m_Corporation, recipe);
			}
			else
			{
				Debug.Log("[Nuterra] Unable to inject Recipe");
			}
			return base.InjectBlock(blockID, def, jToken);
        }

        private void RecursivelyAddSubObject(TankBlock block, ModContents mod, Transform targetTransform, JObject jData, Material defaultMaterial, bool isNewSubObject)
		{
			Debug.Log("[Nuterra] Called RecursivelyAddSubObject");

			// Material - Used in the next step
			Material mat = null;

			bool hasMaterial = TryGetStringMultipleKeys(jData, out string materialName, "MaterialName", "MeshMaterialName");
			bool hasAlbedo = TryGetStringMultipleKeys(jData, out string albedoName, "MeshTextureName", "TextureName");
			bool hasGloss = TryGetStringMultipleKeys(jData, out string glossName, "MetallicTextureName", "GlossTextureName", "MeshGlossTextureName");
			bool hasEmissive = TryGetStringMultipleKeys(jData, out string emissiveName, "EmissionTextureName", "MeshEmissionTextureName");
			bool hasAnyOverrides = hasAlbedo || hasGloss || hasEmissive;

			// Calculate a unique string that defines this material
			string compoundMaterialName = "";
			if (hasMaterial)
				compoundMaterialName = $"{compoundMaterialName}M:{materialName};";
			if (hasAlbedo)
				compoundMaterialName = $"{compoundMaterialName}A:{albedoName};";
			if (hasGloss)
				compoundMaterialName = $"{compoundMaterialName}G:{glossName};";
			if (hasEmissive)
				compoundMaterialName = $"{compoundMaterialName}E:{emissiveName};";

			if (hasAnyOverrides)
			{
				if (sMaterialCache.TryGetValue(compoundMaterialName, out Material existingMat))
					mat = existingMat;
				else
				{
					
					// Default to missing texture, then see if we have a base texture reference
					mat = defaultMaterial;
					if (hasMaterial)
					{
						string matName = materialName.Replace("Venture_", "VEN_").Replace("GeoCorp_", "GC_");
						mat = TTReferences.FindMaterial(matName);
					}

					Texture2D albedo = hasAlbedo ? TTReferences.Find<Texture2D>(albedoName, mod) : null;
					Texture2D gloss = hasGloss ? TTReferences.Find<Texture2D>(glossName, mod) : null;
					Texture2D emissive = hasEmissive ? TTReferences.Find<Texture2D>(emissiveName, mod) : null;
					mat = Util.CreateMaterial(mat, true, albedo, gloss, emissive);

					// Cache our newly created material in case it comes up again
					sMaterialCache.Add(compoundMaterialName, mat);
				}
			}
			else
			{
				// Actually, if we make no references, we can keep official modded block behaviour
				mat = defaultMaterial;
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
			bool supressBoxColliderFallback = TryParse(jData, "SupressBoxColliderFallback", TryParse(jData, "NoBoxCollider", false));
			if (TryGetStringMultipleKeys(jData, out string meshName, "MeshName", "ModelName"))
			{
				foreach(UnityEngine.Object obj in mod.FindAllAssets(meshName))
				{
					if (obj != null)
					{
						if (obj is Mesh)
							mesh = (Mesh)obj;
						else if (obj is GameObject)
							mesh = ((GameObject)obj).GetComponentInChildren<MeshFilter>().sharedMesh;
					}
				}
				Debug.Assert(mesh != null, $"[Nuterra] Failed to find mesh with name {meshName}");
			}
			if (TryGetStringMultipleKeys(jData, out string meshColliderName, "ColliderMeshName", "MeshColliderName"))
			{
				foreach (UnityEngine.Object obj in mod.FindAllAssets(meshColliderName))
				{
					if (obj is Mesh)
						colliderMesh = (Mesh)obj;
					else if (obj is GameObject)
						colliderMesh = ((GameObject)obj).GetComponentInChildren<MeshFilter>().sharedMesh;
				}
				Debug.Assert(colliderMesh != null, $"[Nuterra] Failed to find collider mesh with name {meshColliderName}");
			}

			// This is the only bit where the root object majorly differs from subobjects
			if (targetTransform == block.transform)
			{
				// Generally speaking, the root of a TT block does not have meshes, so needs to add a child
				// Add mesh / collider mesh sub GameObject if we have either
				if (mesh != null || colliderMesh != null)
				{
					GameObject meshObj = CreateMeshGameObject(targetTransform, mesh, mat, colliderMesh, physMat, supressBoxColliderFallback);
				}
			}
			else // However, if we are poking around in a subobject, we may want to swap out existing renderers
			{
				// If we provided a new mesh, do a full swap
				if (mesh != null)
				{
					targetTransform.gameObject.EnsureComponent<MeshFilter>().sharedMesh = mesh;
					targetTransform.gameObject.EnsureComponent<MeshRenderer>().sharedMaterial = mat;
				}
				else // If we don't want to swap out the mesh we may still want to swap out the properties of existing renderers
				{
					bool forceEmissive = TryParse(jData, "ForceEmission", false);
					foreach(Renderer renderer in targetTransform.GetComponents<Renderer>())
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
					MeshCollider mc = targetTransform.gameObject.EnsureComponent<MeshCollider>();
					mc.convex = true;
					mc.sharedMesh = colliderMesh;
					mc.sharedMaterial = physMat;
				}
				// If we want a box collider, try to make one from our mesh
				bool makeBoxCollider = GetBoolMultipleKeys(jData, false, "MakeBoxCollider", "GenerateBoxCollider");
				if(makeBoxCollider)
				{
					Debug.Log($"[Nuterra] Generating box collider for {block.name}");
					BoxCollider bc = targetTransform.gameObject.EnsureComponent<BoxCollider>();
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
					Debug.Log($"[Nuterra] Generating sphere collider for {block.name}");
					SphereCollider sc = targetTransform.gameObject.EnsureComponent<SphereCollider>();
					sc.radius = 0.5f;
					sc.center = Vector3.zero;
					sc.sharedMaterial = physMat;
				}
			}

			// ------------------------------------------------------
			#region Deserializers
			if(TryGetTokenMultipleKeys(jData, out JToken jDeserialObj, "Deserializer", "JSONBLOCK") && jDeserialObj.Type == JTokenType.Object)
			{
				JObject jDeserializer = (JObject)jDeserialObj;

				// TTQMM Ref: GameObjectJSON.CreateGameObject(jBlock.Deserializer, blockbuilder.Prefab);
				NuterraDeserializer.DeserializeIntoGameObject(jDeserializer, block.gameObject);
			}
			#endregion
			// ------------------------------------------------------

			// ------------------------------------------------------
			#region Sub objects
			if (jData.TryGetValue("SubObjects", out JToken jSubObjectList) && jSubObjectList.Type == JTokenType.Array)
			{
				foreach (JToken token in (JArray)jSubObjectList)
				{
					if (token.Type == JTokenType.Object)
					{
						JObject jSubObject = (JObject)token;
						if (TryGetStringMultipleKeys(jSubObject, out string subObjName, "SubOverrideName", "OverrideName", "ObjectName"))
						{
							GameObject subObject = (targetTransform.RecursiveFindWithProperties(subObjName) as Component)?.gameObject;
							bool creatingNew = subObject == null;
							if (subObject != null)
							{
								if (TryGetTokenMultipleKeys(jSubObject, out JToken jLayer, "Layer", "PhysicsLayer") && jLayer.Type == JTokenType.Integer)
									subObject.layer = jLayer.ToObject<int>();
							}
							else // Reference was not matched, so we want to add a new subobject
							{
								if (subObjName.NullOrEmpty())
									subObjName = $"SubObject_{targetTransform.childCount + 1}";

								subObject = new GameObject(subObjName);
								subObject.transform.parent = targetTransform;
								subObject.transform.localPosition = Vector3.zero;
								subObject.transform.localRotation = Quaternion.identity;

								if (TryGetTokenMultipleKeys(jSubObject, out JToken jLayer, "Layer", "PhysicsLayer") && jLayer.Type == JTokenType.Integer)
									subObject.layer = jLayer.ToObject<int>();
								else
									subObject.layer = 8; // Globals.inst.layerTank;
							}

							// Target acquired, lets tweak a few things
							bool destroyColliders = GetBoolMultipleKeys(jSubObject, false, "DestroyExistingColliders", "DestroyColliders");
							if (destroyColliders)
							{
								foreach (Collider col in subObject.GetComponents<Collider>())
									UnityEngine.Object.DestroyImmediate(col);

								UnityEngine.Object.DestroyImmediate(subObject.GetComponentInParents<ColliderSwapper>());
							}

							bool destroyRenderers = GetBoolMultipleKeys(jSubObject, false, "DestroyExistingRenderer", "DestroyExistingRenderers", "DestroyRenderers");
							if (destroyRenderers)
							{
								foreach (Renderer renderer in subObject.GetComponents<Renderer>())
									UnityEngine.Object.DestroyImmediate(renderer);
								foreach (MeshFilter mf in subObject.GetComponents<MeshFilter>())
									UnityEngine.Object.DestroyImmediate(mf);
							}

							// If there is already a material set on this sub object ref, use it
							Material matForSubObject = mat;
							if (!creatingNew && !destroyRenderers)
							{
								Renderer ren = subObject.GetComponent<Renderer>();
								if (ren)
									matForSubObject = ren.sharedMaterial;
							}

							// Optional resize settings
							if (TryGetTokenMultipleKeys(jSubObject, out JToken jPos, "SubPosition", "Position") && jPos.Type == JTokenType.Object)
								subObject.transform.localPosition = GetVector3(jPos);
							if (TryGetTokenMultipleKeys(jSubObject, out JToken jEuler, "SubRotation", "Rotation") && jEuler.Type == JTokenType.Object)
								subObject.transform.localEulerAngles = GetVector3(jEuler);
							if (TryGetTokenMultipleKeys(jSubObject, out JToken jScale, "SubScale", "Scale") && jScale.Type == JTokenType.Object)
								subObject.transform.localScale = GetVector3(jScale);

							RecursivelyAddSubObject(block, mod, subObject.transform, jSubObject, matForSubObject, creatingNew);
						}
						else
						{
							Debug.LogError($"[Nuterra] Failed to find SubOverrideName tag in sub object JSON");
						}
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
			{
				return new Vector3(xToken.ToObject<float>(), yToken.ToObject<float>(), zToken.ToObject<float>());
			}
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

		private bool TryGetFloatMultipleKeys(JObject jData, out float result, float defaultValue, params string[] args)
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
			result = defaultValue;
			return false;
		}

		private bool TryGetTokenMultipleKeys(JObject jData, out JToken token, params string[] args)
		{
			foreach (string arg in args)
			{
				if (jData.TryGetValue(arg, out JToken jToken))
				{
					token = jToken;
					return true;
				}
			}
			token = null;
			return false;
		}

		private bool TryGetBool(JObject jData, bool defaultValue, string key)
        {
			if (jData.TryGetValue(key, out JToken jToken) && jToken.Type == JTokenType.Boolean)
			{
				return jToken.ToObject<bool>();
			}
			return defaultValue;
		}

		private bool GetBoolMultipleKeys(JObject jData, bool defaultValue, params string[] args)
		{
			foreach (string arg in args)
			{
				if (jData.TryGetValue(arg, out JToken jToken) && jToken.Type == JTokenType.Boolean)
				{
					return jToken.ToObject<bool>();
				}
			}
			return defaultValue;
		}

		private bool TryGetStringMultipleKeys(JObject jData, out string result, params string[] args)
		{
			foreach(string arg in args)
			{
				if(jData.TryGetValue(arg, out JToken jToken) && jToken.Type == JTokenType.String)
				{
					result = jToken.ToString();
					return true;
				}
			}
			result = null;
			return false;
		}

		// This is the JSON key that we check for in custom blocks
		public override string GetModuleKey()
		{
			return "NuterraBlock";
		}

		static int AppendToRecipe(Dictionary<ChunkTypes, RecipeTable.Recipe.ItemSpec> RecipeBuilder, string Type, int Count)
		{
			if (!Enum.TryParse(Type, true, out ChunkTypes chunk))
			{
				if (int.TryParse(Type, out int result))
				{
					chunk = (ChunkTypes)result;
				}
			}
			if (chunk != ChunkTypes.Null)
			{
				if (!RecipeBuilder.ContainsKey(chunk))
				{
					RecipeBuilder.Add(chunk, new RecipeTable.Recipe.ItemSpec(new ItemTypeInfo(ObjectTypes.Chunk, (int)chunk), 1));
				}
				else
				{
					RecipeBuilder[chunk].m_Quantity += Count;
				}
				Debug.Log($"[Nuterra] Chunk of type {chunk} added to recipe");
				return RecipeManager.inst.GetChunkPrice(chunk);
			}
			else
			{
				Console.WriteLine("No ChunkTypes found matching given name, nor could parse as ID (int): " + Type);
			}
			return 0;
		}
	}
}