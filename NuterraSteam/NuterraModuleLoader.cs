using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using CustomModules.Logging;
using CustomModules.LegacyModule;

namespace CustomModules
{
    public class NuterraModuleLoader : JSONModuleLoader
	{
		private static Dictionary<string, Material> sMaterialCache = new Dictionary<string, Material>();
		internal const string ModuleID = "NuterraBlock";

		internal static Dictionary<string, Logging.Logger> loggers = new Dictionary<string, Logging.Logger>();

		private RecipeTable.Recipe ParseRecipe(JObject jData, string corp, int blockID, out int RecipePrice)
        {
			RecipePrice = 0;
			if (jData.TryGetValue("Recipe", out JToken jRecipe))
			{
				NuterraMod.logger.Debug($"Recipe detected: {jRecipe.ToString()}");

				RecipeTable.Recipe recipe = new RecipeTable.Recipe();
				Dictionary<ChunkTypes, RecipeTable.Recipe.ItemSpec> dictionary = new Dictionary<ChunkTypes, RecipeTable.Recipe.ItemSpec>();

				
				if (jRecipe.Type == JTokenType.Object && jRecipe is JObject rObject)
				{
					foreach (var item in rObject)
					{
						RecipePrice += AppendToRecipe(dictionary, item.Key, item.Value.ToObject<int>());
					}
				}
				else if (jRecipe.Type == JTokenType.Array && jRecipe is JArray rArray)
				{
					foreach (var item in rArray)
					{
						RecipePrice += AppendToRecipe(dictionary, item.ToString(), 1);
					}
				}
				else if (jRecipe is JValue rString)
				{
					string[] recipeString = rString.ToObject<string>().Replace(" ", "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
					NuterraMod.logger.Debug($"Adjusted Recipe Str: {recipeString}");
					foreach (string item in recipeString)
					{
						RecipePrice += AppendToRecipe(dictionary, item, 1);
					}
				}

				recipe.m_InputItems = new RecipeTable.Recipe.ItemSpec[dictionary.Count];
				dictionary.Values.CopyTo(recipe.m_InputItems, 0);
				recipe.m_OutputItems[0] = new RecipeTable.Recipe.ItemSpec(new ItemTypeInfo(ObjectTypes.Block, blockID), 1);

				return recipe;
			}
			else
			{
				NuterraMod.logger.Warn($"No Recipe Found");
			}
			return null;
		}

		// This method should add a module to the TankBlock prefab
		public override bool CreateModuleForBlock(int blockSessionID, ModdedBlockDefinition def, TankBlock block, JToken jToken)
		{
			ModContainer container = ManMods.inst.FindMod(def);
			string BlockID = ModUtils.CreateCompoundId(container.ModID, def.name);
			if (loggers.TryGetValue(BlockID, out Logging.Logger logger))
            {
				NuterraMod.logger = logger;
            }
			else
            {
				logger = new Logging.Logger(BlockID, NuterraMod.LoggerTarget);
				loggers.Add(BlockID, logger);
				NuterraMod.logger = logger;
			}

			NuterraMod.logger.Info($"Loading CustomBlock module for {def.name} ({blockSessionID})");
			try
			{
				NuterraMod.logger.Trace(def.m_Json.ToString());
				ModContents mod = container != null ? container.Contents : null;

				if (jToken.Type == JTokenType.Object)
				{
					JObject jData = (JObject)jToken;
					NuterraMod.logger.Debug("CreationStart");
					// Get the mod contents so we can search for additional assets

					// ------------------------------------------------------
					// Basics like name, desc etc. The Official Mod Tool lets us set these already, but we might want to override
					def.m_BlockDisplayName = TryParse(jData, "Name", def.m_BlockDisplayName);
					def.m_BlockDescription = TryParse(jData, "Description", def.m_BlockDescription);

					if (mod == null)
					{
						NuterraMod.logger.Error($"{def.m_BlockDisplayName} | Could not find mod that this unoffical block is part of");
						NuterraMod.logger.Error("Block creation FAILED");
						NuterraMod.logger.Flush();
						return false;
					}

					// We get an ID for backwards compatibility
					int legacyID = CustomParser.LenientTryParseInt(jData, "ID", 0);
					NuterraDeserializer.DeserializingBlock = $"{def.m_BlockDisplayName} ({legacyID} => {blockSessionID})";
					NuterraDeserializer.DeserializingMod = mod;
					if (legacyID != 0)
					{
						NuterraMod.logger.Debug(string.Format(NuterraDeserializer.DeserializingBlock + " Assigning block {0} with legacy ID of {1} to managed ID {2}", def.m_BlockDisplayName, legacyID, blockSessionID));
						try
						{
							NuterraMod.blockIDToLegacyIDs.Add(ModUtils.CreateCompoundId(container.ModID, def.name), legacyID);
							NuterraMod.legacyToSessionIds.Add(legacyID, blockSessionID);
						}
						catch (ArgumentException exception)
                        {
							NuterraMod.logger.Error($"Failed to register block {def.name} to name-ID map:\n{exception.ToString()}");
                        }
					}
					else
                    {
						NuterraMod.nonLegacyBlocks.Add(blockSessionID);
                    }
					NuterraMod.logger.Debug("Details: " + NuterraDeserializer.DeserializingBlock);

					// Recipe
					RecipeTable.Recipe recipe = ParseRecipe(jData, def.m_Corporation, blockSessionID, out int RecipePrice);
					if (recipe != null)
					{
						def.m_Price = RecipePrice * 3;
					}
					int overridePrice = CustomParser.LenientTryParseInt(jData, "Price", 0);
					if (overridePrice > 0) {
						NuterraMod.logger.Debug($"Read override price of {overridePrice}");
						def.m_Price = overridePrice;
					}
					else
                    {
						NuterraMod.logger.Warn($"No price specified. Falling back on calculated recipe price * 3: {def.m_Price}");
					}
					def.m_MaxHealth = CustomParser.LenientTryParseInt(jData, "HP", def.m_MaxHealth);

					// ------------------------------------------------------
					#region Reference - Copy a vanilla block
					NuterraMod.logger.Debug("Starting references");
					bool keepRenderers = CustomParser.LenientTryParseBool(jData, "KeepRenderers", false);
					bool keepReferenceRenderers = CustomParser.LenientTryParseBool(jData, "KeepReferenceRenderers", false);
					bool keepColliders = CustomParser.LenientTryParseBool(jData, "KeepColliders", true);

					GameObject originalGameObject = null;

					if(CustomParser.TryGetStringMultipleKeys(jData, out string referenceBlock, "GamePrefabReference", "PrefabReference"))
					{
						// This code block copies our chosen reference block
						// TTQMM REF: BlockPrefabBuilder.Initialize

						originalGameObject = TTReferences.FindBlockReferenceFromString(referenceBlock);
					}
					else if (CustomParser.TryGetIntMultipleKeys(jData, out int referenceBlockID, 0, "GamePrefabReference", "PrefabReference"))
                    {
						referenceBlock = referenceBlockID.ToString();
						originalGameObject = TTReferences.FindBlockReferenceByID(referenceBlockID);
					}

					if (originalGameObject != null)
					{
						GameObject newObject = GameObject.Instantiate(originalGameObject);
						// Assign this back to block for further processing
						block = GetOrAddComponent<TankBlock>(newObject.transform);

						//TankBlock original = originalGameObject.GetComponent<TankBlock>();
						//TankBlock copy = UnityEngine.Object.Instantiate(original);
						TankBlockTemplate fakeTemplate = newObject.AddComponent<TankBlockTemplate>();
						// Cheeky hack to swap the prefab
						// The official block loader doesn't expect this to happen, but I will assume
						// for now that you are making 100% official or 100% unofficial JSONs
						def.m_PhysicalPrefab = fakeTemplate;

						NuterraMod.logger.Debug($"Found game prefab reference as {newObject}");

						// TTQMM REF: DirectoryBlockLoader.CreateJSONBlock, the handling of these flags is a bit weird
						if (keepRenderers)
						{
							if (!keepColliders)
							{
								RemoveChildren<Collider>(block);
							}
						}
						else if (!keepReferenceRenderers)
						{
							foreach (Transform child in block.transform)
							{
								RemoveChildren<MeshRenderer>(child);
								RemoveChildren<MeshFilter>(child);
							}
							MeshFilter[] rootMeshes = block.GetComponents<MeshFilter>();
							if (rootMeshes != null)
                            {
								foreach (MeshFilter meshFilter in rootMeshes)
                                {
									meshFilter.sharedMesh = null;
									meshFilter.mesh = null;
                                }
                            }

							RemoveChildren<TankTrack>(block);
							RemoveChildren<SkinnedMeshRenderer>(block);

							if (!keepColliders)
								RemoveChildren<Collider>(block);
						}

						newObject.name = def.name;
						newObject.layer = Globals.inst.layerTank;
						newObject.tag = "TankBlock";


						bool hasRefOffset = CustomParser.TryGetTokenMultipleKeys(jData, out JToken jOffset, "ReferenceOffset", "PrefabOffset", "PrefabPosition");
						bool hasRefRotation = CustomParser.TryGetTokenMultipleKeys(jData, out JToken jEuler, "ReferenceRotationOffset", "PrefabRotation");
						bool hasRefScale = CustomParser.TryGetTokenMultipleKeys(jData, out JToken jScale, "ReferenceScale", "PrefabScale");

						if (hasRefOffset || hasRefRotation || hasRefScale)
						{
							Vector3 offset = hasRefOffset ? CustomParser.GetVector3(jOffset) : Vector3.zero;
							Vector3 scale = hasRefScale ? CustomParser.GetVector3(jScale) : Vector3.one;
							Vector3 rotation = hasRefRotation ? CustomParser.GetVector3(jEuler) : Vector3.zero;

							foreach (Transform child in newObject.transform)
							{
								if (hasRefRotation)
								{
									child.Rotate(rotation, Space.Self);
									child.localPosition = Quaternion.Euler(rotation) * child.localPosition;
								}

								if (hasRefScale)
								{
									child.localScale = Vector3.Scale(child.localScale, scale);
									child.localPosition = Vector3.Scale(child.localPosition, scale);
								}

								if (hasRefOffset)
								{
									child.localPosition += offset;
								}
							}
						}
					}
					else
					{
						NuterraMod.logger.Warn($"Failed to find GamePrefabReference {referenceBlock}");
					}
					#endregion


					// Ignore corporation. Custom corps no longer have a fixed ID, so we should use the official tool to set corp IDs.
					//def.m_Corporation = CustomParser.LenientTryParseInt(jData, "Corporation", def.m_Corporation);
					def.m_Category = CustomParser.LenientTryParseEnum<BlockCategories>(jData, "Category", def.m_Category);
					def.m_Category = def.m_Category != BlockCategories.Null ? def.m_Category : BlockCategories.Standard;
					block.m_BlockCategory = def.m_Category;
					NuterraMod.logger.Debug($"Category: {def.m_Category}");
					def.m_Rarity = CustomParser.LenientTryParseEnum<BlockRarity>(jData, "Rarity", def.m_Rarity);
					block.m_BlockRarity = def.m_Rarity;
					NuterraMod.logger.Debug($"Rarity: {def.m_Rarity}");
					def.m_Grade = CustomParser.LenientTryParseInt(jData, "Grade", def.m_Grade);
					NuterraMod.logger.Debug($"Grade: {def.m_Grade}");

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
					if (CustomParser.TryGetStringMultipleKeys(jData, out string referenceExplosion, "DeathExplosionReference", "ExplosionReference"))
					{
						GameObject refBlock = TTReferences.FindBlockReferenceFromString(referenceExplosion);
						if (refBlock != null)
						{
							moduleDamage.deathExplosion = refBlock.GetComponent<ModuleDamage>().deathExplosion;
							NuterraMod.logger.Debug($"Swapped death explosion for {refBlock}");
						}
					}
					#endregion
					// ------------------------------------------------------

					// ------------------------------------------------------
					#region Tweaks
					NuterraMod.logger.Debug($"Handling block stats");

					// Some basic block stats
					damageable.DamageableType = CustomParser.LenientTryParseEnum<ManDamage.DamageableType>(jData, "DamageableType", damageable.DamageableType);
					if(CustomParser.TryGetFloatMultipleKeys(jData, out float fragility, moduleDamage.m_DamageDetachFragility, "DetachFragility", "Fragility"))
					{
						moduleDamage.m_DamageDetachFragility = fragility;
					}

					block.m_DefaultMass = CustomParser.LenientTryParseFloat(jData, "Mass", block.m_DefaultMass);

					// Emission Mode
					ModuleCustomBlock.EmissionMode mode = ModuleCustomBlock.EmissionMode.None;
					if (jData.TryGetValue("EmissionMode", out JToken jEmissionMode))
					{
						mode = CustomParser.LenientTryParseEnum<ModuleCustomBlock.EmissionMode>(jEmissionMode, mode);
					}

					// Center of Mass
					if (CustomParser.TryGetTokenMultipleKeys(jData, out JToken comToken, new string[] { "CenterOfMass", "CentreOfMass" }))
					{
						Transform comTrans = block.transform.Find("CentreOfMass");
						if (comTrans == null)
						{
							comTrans = new GameObject("CentreOfMass").transform;
							comTrans.SetParent(block.transform);
							comTrans.localScale = Vector3.one;
							comTrans.localRotation = Quaternion.identity;
						}

						Vector3 CenterOfMass = CustomParser.GetVector3(comToken);
						comTrans.localPosition = CenterOfMass;

						// TODO: Weird thing about offseting colliders from Nuterra
						// Absolutely no idea what it does
						for (int i = 0; i < block.transform.childCount; i++)
						{
							transform = block.transform.GetChild(i);
							if (transform.name.Length < 5 && transform.name.EndsWith("col")) // "[a-z]col"
								transform.localPosition = CenterOfMass;
						}

						ModuleCustomBlock customBlock = block.gameObject.EnsureComponent<ModuleCustomBlock>();
						customBlock.HasInjectedCenterOfMass = true;
						customBlock.InjectedCenterOfMass = CenterOfMass;
						if (mode != ModuleCustomBlock.EmissionMode.None)
						{
							customBlock.BlockEmissionMode = mode;
						}
					}
					else if (mode != ModuleCustomBlock.EmissionMode.None)
                    {
						ModuleCustomBlock customBlock = block.gameObject.EnsureComponent<ModuleCustomBlock>();
						customBlock.BlockEmissionMode = mode;
                    }

					// Get compound ID
					string CompoundID = ModUtils.CreateCompoundId(container.ModID, def.name);

					// RotationGroup
					if (jData.TryGetValue("RotationGroup", out JToken rotGroup) && rotGroup.Type == JTokenType.String)
                    {
						string RotationGroupName = rotGroup.ToString();
						BlockRotationTable.GroupIndexLookup newLookup = NuterraMod.AddRotationGroupsOverride(blockSessionID, RotationGroupName);
						NuterraMod.addedRotationGroups.Add(newLookup);
						NuterraMod.rotationGroupsMap[CompoundID] = RotationGroupName;
					}

					// IconName override
					if (CustomParser.TryGetTokenMultipleKeys(jData, out JToken jIconName, new string[] { "IconName", "Icon" }) && jIconName.Type == JTokenType.String)
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
								NuterraMod.logger.Warn($"Found unknown object type {obj.GetType()} for icon override for {block.name}");
							}
						}
					}

					// DropFromCrates no longer useful => all official modded blocks drop from crates by default
					// PairedBlock
					int PairedBlock = -1;
					if (jData.TryGetValue("PairedBlock", out JToken jPairedBlock) && jPairedBlock.Type == JTokenType.Integer)
					{
						PairedBlock = jPairedBlock.ToObject<int>();
						if (PairedBlock >= 0)
						{
							bool seenBlock = false;

							// Check current block pairs to see if anything has seen our pair
							for (int i = 0; i < Globals.inst.m_BlockPairsList.m_BlockPairs.Length; i++)
							{
								// If we've matched a legacy ID, then we override it
								BlockPairsList.BlockPairs pair = Globals.inst.m_BlockPairsList.m_BlockPairs[i];
								if (legacyID != 0 && pair.m_Block == (BlockTypes)legacyID)
								{
									pair.m_Block = (BlockTypes)blockSessionID;
									seenBlock = true;
								}
								else if (
									pair.m_Block == (BlockTypes)PairedBlock ||
									(NuterraMod.TryGetSessionID(PairedBlock, out int PairedSessionID) && pair.m_Block == (BlockTypes)PairedSessionID)
								)
								{
									seenBlock = true;
								}
								if (pair.m_PairedBlock == (BlockTypes)legacyID)
								{
									pair.m_PairedBlock = (BlockTypes)blockSessionID;
								}
							}

							// If our pair is not present, then add it
							if (!seenBlock)
							{
								var arr = Globals.inst.m_BlockPairsList.m_BlockPairs;
								Array.Resize(ref arr, arr.Length + 1);
								arr[arr.Length - 1] = new BlockPairsList.BlockPairs()
								{
									m_Block = (BlockTypes)blockSessionID,
									m_PairedBlock = (BlockTypes)(NuterraMod.TryGetSessionID(PairedBlock, out int PairedSessionID) ? PairedSessionID : PairedBlock)
								};
								Globals.inst.m_BlockPairsList.m_BlockPairs = arr;
							}
						}
					}

					// Filepath? For reparse?
					#endregion
					// ------------------------------------------------------

					bool blockSuccess = true;
					try
					{
						NuterraMod.logger.Debug($"Handling SubObjects");
						// Start recursively adding objects with the root. 
						// Calling it this way and treating the root as a sub-object prevents a lot of code duplication
						RecursivelyAddSubObject(block, mod, block.transform, jData, TTReferences.kMissingTextureTankBlock, false);
					}
					catch (Exception e)
					{
						blockSuccess = false;
						NuterraMod.logger.Error("Block creation FAILED");
						NuterraMod.logger.Error($"Caught exception:\n{e.ToString()}");
					}

                    #region cells
                    NuterraMod.logger.Debug($"Handling block cells");
					// BlockExtents is a way of quickly doing a cuboid Filled Cell setup
					bool usedBlockExtents = false;
					bool cellsProcessed = false;
					IntVector3 size = new IntVector3();

					// Use the very first cell processor that gives you a non-empty result
					// CellMap / CellsMap
					if (CustomParser.TryGetTokenMultipleKeys(jData, out JToken jCellMap, "CellMap", "CellsMap"))
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
						if (cells.Count != 0 || (block.filledCells == null || block.filledCells.Length == 0))
						{
							block.filledCells = cells.ToArray();
							usedBlockExtents = false;
							cellsProcessed = true;
							NuterraMod.logger.Debug($"Using CellMap");
						}
						else
						{
							NuterraMod.logger.Warn($"CellMap FAILED");
						}
					}
					// old cells
					if (!cellsProcessed && jData.TryGetValue("Cells", out JToken jCells) && jCells.Type == JTokenType.Array)
					{
						List<IntVector3> filledCells = new List<IntVector3>();
						foreach (JObject jCell in (JArray)jCells)
						{
							filledCells.Add(CustomParser.GetVector3Int(jCell));
						}
						if (filledCells.Count != 0 || (block.filledCells == null || block.filledCells.Length == 0))
						{
							block.filledCells = filledCells.ToArray();
							usedBlockExtents = false;
							cellsProcessed = true;
						}
						NuterraMod.logger.Debug($"Using old cells");
					}
					// BlockExtents
					if (!cellsProcessed && jData.TryGetValue("BlockExtents", out JToken jExtents) && jExtents.Type == JTokenType.Object)
					{
						List<IntVector3> filledCells = new List<IntVector3>();
						size = CustomParser.GetVector3Int(jExtents);

						for (int i = 0; i < size.x; i++)
						{
							for (int j = 0; j < size.y; j++)
							{
								for (int k = 0; k < size.z; k++)
								{
									filledCells.Add(new IntVector3(i, j, k));
								}
							}
						}
						block.filledCells = filledCells.ToArray();
						usedBlockExtents = true;
						NuterraMod.logger.Debug($"Overwrote BlockExtents");
					}

					// Handle failure to get cells
					if (block.filledCells == null || block.filledCells.Length == 0)
					{
						NuterraMod.logger.Warn($"FAILED to set cells");
						block.filledCells = new IntVector3[] { new IntVector3(0, 0, 0) };
					}

					NuterraMod.logger.Debug($"Handling block APs");
					// APs
					bool manualAPs = false;
					if (jData.TryGetValue("APs", out JToken jAPList) && jAPList.Type == JTokenType.Array)
					{
						List<Vector3> aps = new List<Vector3>();
						foreach (JToken token in (JArray)jAPList)
						{
							aps.Add(CustomParser.GetVector3(token));
						}
						block.attachPoints = aps.ToArray();
						manualAPs = block.attachPoints.Length > 0;
						NuterraMod.logger.Debug($"APS set");
					}

					// TODO: APsOnlyAtBottom / MakeAPsAtBottom
					if (usedBlockExtents && !manualAPs)
					{
						List<Vector3> aps = new List<Vector3>();
						bool doAllPoints = true;
						if (jData.TryGetValue("APsOnlyAtBottom", out JToken APMode) && APMode.Type == JTokenType.Boolean)
						{
							doAllPoints = !APMode.ToObject<bool>();
						}

						for (int x = 0; x < size.x; x++)
						{
							for (int y = 0; y < size.y; y++)
							{
								for (int z = 0; z < size.z; z++)
								{
									if (y == 0)
									{
										aps.Add(new Vector3(x, -0.5f, z));
									}
									if (doAllPoints)
									{
										if (x == 0)
										{
											aps.Add(new Vector3(-0.5f, y, z));
										}
										if (x == size.x - 1)
										{
											aps.Add(new Vector3(x + 0.5f, y, z));
										}
										if (y == size.y - 1)
										{
											aps.Add(new Vector3(x, y + 0.5f, z));
										}
										if (z == 0)
										{
											aps.Add(new Vector3(x, y, -0.5f));
										}
										if (z == size.z - 1)
										{
											aps.Add(new Vector3(x, y, z + 0.5f));
										}
									}
								}
							}
						}

						block.attachPoints = aps.ToArray();
						NuterraMod.logger.Debug($"Auto-Set APs");
					}
					#endregion

					// Forcibly reset ColliderSwapper so it gets recalculated correctly every time.
					ColliderSwapper swapper = block.transform.GetComponentInChildren<ColliderSwapper>();
					if (swapper != null)
					{
						swapper.m_Colliders = null;
					}

					// Weird export fix up for meshes
					// Flip everything in x
					bool toFlip = true;
					if (CustomParser.TryGetTokenMultipleKeys(jData, out JToken flipToken, new string[] { "AutoImported", "PreserveModel" }) && flipToken.Type == JTokenType.Boolean)
                    {
						toFlip = !flipToken.ToObject<bool>();
                    }
					if (toFlip)
					{
						foreach (MeshRenderer mr in block.GetComponentsInChildren<MeshRenderer>())
						{
							mr.transform.localScale = new Vector3(-mr.transform.localScale.x, mr.transform.localScale.y, mr.transform.localScale.z);
						}
						foreach (MeshCollider mc in block.GetComponentsInChildren<MeshCollider>())
						{
							if (mc.GetComponent<MeshRenderer>() == null) // Skip ones with both. Don't want to double flip
								mc.transform.localScale = new Vector3(-mc.transform.localScale.x, mc.transform.localScale.y, mc.transform.localScale.z);
						}
					}

					if (blockSuccess)
					{
						NuterraMod.logger.Info("Block creation DONE");
					}
					NuterraMod.logger.Flush();
					return blockSuccess;
				}
				NuterraMod.logger.Error("Block creation FAILED");
				NuterraMod.logger.Flush();
				return false;
			}
			catch(Exception e)
			{
				NuterraMod.logger.Error($"Caught exception:\n{e.ToString()}");
				NuterraMod.logger.Error("Block creation FAILED");
				NuterraMod.logger.Flush();
				return false;
			}
		}

		public override bool InjectBlock(int blockID, ModdedBlockDefinition def, JToken jToken)
        {
			JObject jData = (JObject)jToken;
			RecipeTable.Recipe recipe = ParseRecipe(jData, def.m_Corporation, blockID, out int price);

			if (recipe != null)
			{
				try
				{
					Singleton.Manager<RecipeManager>.inst.RegisterCustomBlockFabricatorRecipe(blockID, def.m_Corporation, recipe);
				}
				catch (Exception e)
				{
					NuterraMod.logger.Error("Failed to inject custom recipe:");
					NuterraMod.logger.Error(e);
				}
			}
			else
			{
				NuterraMod.logger.Warn("Unable to inject Recipe");
			}
			return true;
        }

        private void RecursivelyAddSubObject(TankBlock block, ModContents mod, Transform targetTransform, JObject jData, Material defaultMaterial, bool isNewSubObject)
		{
			NuterraMod.logger.Debug($"Called RecursivelyAddSubObject");

			// Material - Used in the next step
			Material mat = null;

			bool hasMaterial = CustomParser.TryGetStringMultipleKeys(jData, out string materialName, "MaterialName", "MeshMaterialName");
			bool hasAlbedo = CustomParser.TryGetStringMultipleKeys(jData, out string albedoName, "MeshTextureName", "TextureName");
			bool hasGloss = CustomParser.TryGetStringMultipleKeys(jData, out string glossName, "MetallicTextureName", "GlossTextureName", "MeshGlossTextureName");
			bool hasEmissive = CustomParser.TryGetStringMultipleKeys(jData, out string emissiveName, "EmissionTextureName", "MeshEmissionTextureName");
			bool hasAnyOverrides = hasAlbedo || hasGloss || hasEmissive || hasMaterial;
			bool isSubObject = targetTransform != block.transform;

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
						string matName = materialName.Replace("Venture_", "VEN_").Replace("GeoCorp_", "GC_").Replace("Hawkeye_", "HE_");
						Material refMaterial = TTReferences.FindMaterial(matName);
						if (refMaterial != null) {
							mat = refMaterial;
						}
						else
                        {
							// Material name is bad
							if (!hasAlbedo)
                            {
								hasAlbedo = true;
								albedoName = materialName;
                            }
                        }
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
				// ^ That fails to hold if Material name is *NOT* set in root - results in White (null texture) block
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
			bool supressBoxColliderFallback = CustomParser.LenientTryParseBool(jData, "SupressBoxColliderFallback", CustomParser.LenientTryParseBool(jData, "NoBoxCollider", false));
			bool RemoveExistingMesh = false;
			if (CustomParser.TryGetStringMultipleKeysAllowNull(jData, out string meshName, "MeshName", "ModelName"))
			{
				if (meshName != null)
				{
					foreach (UnityEngine.Object obj in mod.FindAllAssets(meshName))
					{
						if (obj != null)
						{
							if (obj is Mesh)
								mesh = (Mesh)obj;
							else if (obj is GameObject)
								mesh = ((GameObject)obj).GetComponentInChildren<MeshFilter>().sharedMesh;
						}
					}
					Debug.Assert(mesh != null, $"Failed to find mesh with name {meshName}");
				}
				else
                {
					RemoveExistingMesh = true;
                }
			}
			if (CustomParser.TryGetStringMultipleKeys(jData, out string meshColliderName, "ColliderMeshName", "MeshColliderName"))
			{
				foreach (UnityEngine.Object obj in mod.FindAllAssets(meshColliderName))
				{
					if (obj is Mesh)
						colliderMesh = (Mesh)obj;
					else if (obj is GameObject)
						colliderMesh = ((GameObject)obj).GetComponentInChildren<MeshFilter>().sharedMesh;
				}
				Debug.Assert(colliderMesh != null, $"Failed to find collider mesh with name {meshColliderName}");
			}

			// Remove the existing mesh if we set it to null
			/*
			if (RemoveExistingMesh) {
				if (targetTransform.gameObject.GetComponent<MeshFilter>() is MeshFilter mf)
				{
					GameObject.DestroyImmediate(mf);
				}
				if (targetTransform.gameObject.GetComponent<MeshRenderer>() is MeshRenderer mr)
				{
					GameObject.Destroy(mr);
				}
			} */

			// This is the only bit where the root object majorly differs from subobjects
			if (!isSubObject)
			{
				// Generally speaking, the root of a TT block does not have meshes, so needs to add a child
				// Add mesh / collider mesh sub GameObject if we have either
				if (mesh != null || colliderMesh != null)
				{
					GameObject meshObj = CreateMeshGameObject(targetTransform, mesh, mat, colliderMesh, physMat, supressBoxColliderFallback);
				}
				else if (mat != null && hasAnyOverrides)
                {
					NuterraMod.logger.Debug("PROCESSING MATERIAL OVERRIDE");
					// if no mesh is provided, and we specify a material override, override the root object
					MeshRenderer meshRenderer = block.GetComponent<MeshRenderer>();
					if (meshRenderer != null)
                    {
						meshRenderer.sharedMaterial = mat;
                    }
					else
                    {
						NuterraMod.logger.Error("FAILED to find MeshRenderer to override");
                    }
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
					bool forceEmissive = CustomParser.LenientTryParseBool(jData, "ForceEmission", false);
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
				bool makeBoxCollider = CustomParser.GetBoolMultipleKeys(jData, false, "MakeBoxCollider", "GenerateBoxCollider");
				if(makeBoxCollider)
				{
					NuterraMod.logger.Debug($"Generating box collider for {targetTransform.name}");
					BoxCollider bc = targetTransform.gameObject.EnsureComponent<BoxCollider>();
					bc.sharedMaterial = physMat;
					if (mesh != null)
					{
						mesh.RecalculateBounds();
						Vector3 size = mesh.bounds.size * 0.9f;
						NuterraMod.logger.Debug($"- Adding BoxCollider of size {size}");
						bc.size = size;
						bc.center = mesh.bounds.center;
					}
					else 
					{
						bc.size = Vector3.one;
						bc.center = Vector3.zero;
					}
				}
				// Weird option from TTQMM that has a fixed size sphere
				bool makeSphereCollider = CustomParser.LenientTryParseBool(jData, "MakeSphereCollider", false);
				if(makeSphereCollider)
				{
					NuterraMod.logger.Debug($"Generating sphere collider for {block.name}");
					SphereCollider sc = targetTransform.gameObject.EnsureComponent<SphereCollider>();
					sc.radius = 0.5f;
					sc.center = Vector3.zero;
					sc.sharedMaterial = physMat;
				}
			}

			// ------------------------------------------------------
			#region Deserializers
			if(CustomParser.TryGetTokenMultipleKeys(jData, out JToken jDeserialObj, "Deserializer", "JSONBLOCK") && jDeserialObj.Type == JTokenType.Object)
			{
				JObject jDeserializer = (JObject)jDeserialObj;

				// TTQMM Ref: GameObjectJSON.CreateGameObject(jBlock.Deserializer, blockbuilder.Prefab);
				NuterraDeserializer.sCurrentSearchTransform = block.gameObject.transform;
				NuterraDeserializer.DeserializeIntoGameObject(jDeserializer, block.gameObject);
				NuterraMod.logger.Trace("DONE with main GO hierarchy");
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
						GameObject subObject;
						if (CustomParser.TryGetStringMultipleKeys(jSubObject, out string subObjName, "SubOverrideName", "OverrideName", "ObjectName"))
						{
							subObject = (targetTransform.RecursiveFindWithProperties(subObjName) as Component)?.gameObject;
						}
						else
						{
							NuterraMod.logger.Warn($"Failed to find SubOverrideName tag in sub object JSON - assuming create new");
							subObject = null;
						}

						bool creatingNew = subObject == null;
						if (subObject != null)
						{
							if (CustomParser.TryGetTokenMultipleKeys(jSubObject, out JToken jLayer, "Layer", "PhysicsLayer") && jLayer.Type == JTokenType.Integer)
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

							if (CustomParser.TryGetTokenMultipleKeys(jSubObject, out JToken jLayer, "Layer", "PhysicsLayer") && jLayer.Type == JTokenType.Integer)
								subObject.layer = jLayer.ToObject<int>();
							else
								subObject.layer = 8; // Globals.inst.layerTank;
						}

						// Target acquired, lets tweak a few things
						bool destroyColliders = CustomParser.GetBoolMultipleKeys(jSubObject, false, "DestroyExistingColliders", "DestroyColliders");
						if (destroyColliders)
						{
							foreach (Collider col in subObject.GetComponents<Collider>())
								UnityEngine.Object.DestroyImmediate(col);

							UnityEngine.Object.DestroyImmediate(subObject.GetComponentInParents<ColliderSwapper>());
						}

						bool destroyRenderers = CustomParser.GetBoolMultipleKeys(jSubObject, false, "DestroyExistingRenderer", "DestroyExistingRenderers", "DestroyRenderers");
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
						if (CustomParser.TryGetTokenMultipleKeys(jSubObject, out JToken jPos, "SubPosition", "Position") && jPos.Type == JTokenType.Object)
							subObject.transform.localPosition = CustomParser.GetVector3(jPos);
						if (CustomParser.TryGetTokenMultipleKeys(jSubObject, out JToken jEuler, "SubRotation", "Rotation") && jEuler.Type == JTokenType.Object)
							subObject.transform.localEulerAngles = CustomParser.GetVector3(jEuler);
						if (CustomParser.TryGetTokenMultipleKeys(jSubObject, out JToken jScale, "SubScale", "Scale") && jScale.Type == JTokenType.Object)
							subObject.transform.localScale = CustomParser.GetVector3(jScale);

						RecursivelyAddSubObject(block, mod, subObject.transform, jSubObject, matForSubObject, creatingNew);
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
			foreach (Component c in obj.gameObject.GetComponentsInChildren<T>(true))
				UnityEngine.Object.DestroyImmediate(c);
		}

		// This is the JSON key that we check for in custom blocks
		public override string GetModuleKey()
		{
			return ModuleID;
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
					RecipeBuilder.Add(chunk, new RecipeTable.Recipe.ItemSpec(new ItemTypeInfo(ObjectTypes.Chunk, (int)chunk), Count));
				}
				else
				{
					RecipeBuilder[chunk].m_Quantity += Count;
				}
				NuterraMod.logger.Debug($"Chunk of type {chunk} added to recipe");
				return RecipeManager.inst.GetChunkPrice(chunk);
			}
			else
			{
				NuterraMod.logger.Error($"No ChunkTypes found matching given name, nor could parse as ID (int): " + Type);
			}
			return 0;
		}
	}
}