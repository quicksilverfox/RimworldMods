using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SubcoreAutomation.Buildings
{
	/// <summary>
	/// Turret that uses an arbitrary installed ranged weapon instead of a fixed turretGunDef gun.
	/// Implements IThingHolder so weapons are delivered via vanilla HaulToContainer job.
	/// When no weapon installed, the placeholder gun is in place but the turret will not fire.
	/// </summary>
	public class Building_SwappableTurret : Building_TurretGun, IThingHolder
	{
		private static readonly MethodInfo UpdateGunVerbsMethod =
			AccessTools.Method(typeof(Building_TurretGun), "UpdateGunVerbs");

		private ThingOwner<ThingWithComps> innerContainer;
		private ThingWithComps loadedWeapon;
		private ThingWithComps pendingWeapon;
		private bool installRequested;
		private bool removeRequested;

		public ThingWithComps LoadedWeapon => loadedWeapon;
		public ThingWithComps PendingWeapon => pendingWeapon;
		public bool HasWeapon => loadedWeapon != null;
		public bool InstallRequested => installRequested && pendingWeapon != null && !pendingWeapon.Destroyed;
		public bool RemoveRequested => removeRequested;

		public Building_SwappableTurret()
		{
			innerContainer = new ThingOwner<ThingWithComps>(this, oneStackOnly: true);
		}

		#region IThingHolder

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public ThingOwner GetDirectlyHeldThings() => innerContainer;

		public override int HaulToContainerDuration(Thing thing) => 240;

		#endregion

		// Always suppress vanilla top draw. When weapon is installed, we draw it ourselves in DrawAt. When empty, the mount shows no gun.
		public override Material TurretTopMaterial => BaseContent.ClearMat;

		protected override bool CanSetForcedTarget => Faction == Faction.OfPlayer;

		protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			base.DrawAt(drawLoc, flip);
			if (loadedWeapon == null) return;

			Graphic g = loadedWeapon.Graphic;
			if (g == null) return;

			Vector2 weaponDraw = loadedWeapon.def.graphicData?.drawSize ?? Vector2.one;
			float scaleX = weaponDraw.x;
			float scaleZ = weaponDraw.y;

			Vector2 topOffset = def.building.turretTopOffset;
			Vector3 offset = new Vector3(topOffset.x, 0f, topOffset.y);
			float rotation = Top != null ? Top.CurRotation : 0f;
			Verb verb = AttackVerb;
			float? aimOverride = verb?.AimAngleOverride;
			if (aimOverride.HasValue) rotation = aimOverride.Value;
			Vector3 pos = drawLoc + Altitudes.AltIncVect + offset;
			Quaternion q = (TurretTop.ArtworkRotation + rotation).ToQuat();
			Matrix4x4 matrix = Matrix4x4.TRS(pos, q, new Vector3(scaleX, 1f, scaleZ));
			// MatSingleFor seeds variant selection by thing ID for Graphic_Random
			// (unique / quality weapons), preventing per-frame variant flicker.
			Graphics.DrawMesh(MeshPool.plane10, matrix, g.MatSingleFor(loadedWeapon), 0);
		}

		public override LocalTargetInfo TryFindNewTarget()
		{
			if (loadedWeapon == null) return LocalTargetInfo.Invalid;
			return base.TryFindNewTarget();
		}

		// Vanilla draws max/min range rings from the placeholder gun when nothing is
		// installed. An empty mount can't fire, so showing rings is misleading.
		public override void DrawExtraSelectionOverlays()
		{
			if (loadedWeapon == null) return;
			base.DrawExtraSelectionOverlays();
		}

		public override void OrderAttack(LocalTargetInfo targ)
		{
			if (loadedWeapon == null)
			{
				Messages.Message("SubcoreAutomation_SwapTurret_NoWeapon".Translate(), this, MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			base.OrderAttack(targ);
		}

		public static bool IsValidWeapon(Thing thing)
		{
			if (!(thing is ThingWithComps)) return false;
			ThingDef d = thing.def;
			if (d == null || !d.IsRangedWeapon) return false;
			if (!d.IsWeaponUsingProjectiles) return false;
			if (d.weaponTags != null && d.weaponTags.Contains("TurretGun")) return false;
			CompBiocodable bio = thing.TryGetComp<CompBiocodable>();
			if (bio != null && bio.Biocoded) return false;
			if (thing.TryGetComp<CompBladelinkWeapon>() != null) return false;
			return true;
		}

		public void RequestInstall(ThingWithComps weapon)
		{
			if (weapon == null || !IsValidWeapon(weapon)) return;
			pendingWeapon = weapon;
			installRequested = true;
		}

		public void CancelInstall()
		{
			pendingWeapon = null;
			installRequested = false;
		}

		public void RequestRemove()
		{
			if (loadedWeapon == null) return;
			removeRequested = true;
			DoRemove();
		}

		public void CancelRemove()
		{
			removeRequested = false;
		}

		private void InstallWeapon(ThingWithComps weapon)
		{
			if (weapon == null) return;
			if (loadedWeapon != null) DropLoadedWeapon();

			loadedWeapon = weapon;
			gun = weapon;
			UpdateGunVerbsMethod?.Invoke(this, null);
			pendingWeapon = null;
			installRequested = false;
		}

		private void DoRemove()
		{
			if (loadedWeapon == null) return;
			DropLoadedWeapon();
			MakeGun();
			removeRequested = false;
		}

		private void DropLoadedWeapon()
		{
			if (loadedWeapon == null) return;
			ThingWithComps w = loadedWeapon;
			loadedWeapon = null;
			if (Spawned && Map != null)
				GenPlace.TryPlaceThing(w, Position, Map, ThingPlaceMode.Near);
		}

		protected override void Tick()
		{
			if (innerContainer.Count > 0 && loadedWeapon == null)
			{
				ThingWithComps w = innerContainer[0] as ThingWithComps;
				if (w != null)
				{
					innerContainer.Remove(w);
					InstallWeapon(w);
				}
				else
				{
					innerContainer.ClearAndDestroyContents();
				}
			}
			base.Tick();
		}

		public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
		{
			if (mode != DestroyMode.Vanish && Spawned && Map != null)
			{
				if (loadedWeapon != null) DropLoadedWeapon();
				if (innerContainer.Count > 0) innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
			}
			innerContainer?.ClearAndDestroyContents();
			base.Destroy(mode);
		}

		public override void ExposeData()
		{
			ThingWithComps originalGun = null;
			Thing placeholder = null;
			bool weaponIsGun = loadedWeapon != null && gun == loadedWeapon;

			if (Scribe.mode == LoadSaveMode.Saving && weaponIsGun)
			{
				originalGun = loadedWeapon;
				placeholder = ThingMaker.MakeThing(def.building.turretGunDef);
				gun = placeholder;
			}

			base.ExposeData();

			if (Scribe.mode == LoadSaveMode.Saving && weaponIsGun)
			{
				gun = originalGun;
				placeholder = null;
			}

			Scribe_Deep.Look(ref innerContainer, "swapTurret_inner", this);
			Scribe_Deep.Look(ref loadedWeapon, "swapTurret_loadedWeapon");
			Scribe_References.Look(ref pendingWeapon, "swapTurret_pendingWeapon");
			Scribe_Values.Look(ref installRequested, "swapTurret_installRequested", false);
			Scribe_Values.Look(ref removeRequested, "swapTurret_removeRequested", false);

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (innerContainer == null)
					innerContainer = new ThingOwner<ThingWithComps>(this, oneStackOnly: true);
				if (loadedWeapon != null)
				{
					gun = loadedWeapon;
					UpdateGunVerbsMethod?.Invoke(this, null);
				}
			}
		}

		public override string GetInspectString()
		{
			StringBuilder sb = new StringBuilder();
			string s = base.GetInspectString();
			if (!s.NullOrEmpty()) sb.AppendLine(s);
			if (loadedWeapon != null)
				sb.AppendLine("SubcoreAutomation_SwapTurret_WeaponInstalled".Translate(loadedWeapon.LabelCap));
			else
				sb.AppendLine("SubcoreAutomation_SwapTurret_NoWeaponInstalled".Translate());
			if (installRequested && pendingWeapon != null)
				sb.AppendLine("SubcoreAutomation_SwapTurret_AwaitingInstall".Translate(pendingWeapon.LabelCap));
			return sb.ToString().TrimEndNewlines();
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo g in base.GetGizmos()) yield return g;
			if (Faction != Faction.OfPlayer) yield break;

			if (loadedWeapon == null)
			{
				if (!installRequested)
				{
					yield return new Command_Action
					{
						defaultLabel = "SubcoreAutomation_SwapTurret_SelectWeapon".Translate(),
						defaultDesc = "SubcoreAutomation_SwapTurret_SelectWeaponDesc".Translate(),
						icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", true),
						action = OpenWeaponSelector
					};
				}
				else
				{
					yield return new Command_Action
					{
						defaultLabel = "SubcoreAutomation_SwapTurret_CancelInstall".Translate(),
						defaultDesc = "SubcoreAutomation_SwapTurret_CancelInstallDesc".Translate(),
						icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true),
						action = CancelInstall
					};
				}
			}
			else
			{
				yield return new Command_Action
				{
					defaultLabel = "SubcoreAutomation_SwapTurret_RemoveWeapon".Translate(),
					defaultDesc = "SubcoreAutomation_SwapTurret_RemoveWeaponDesc".Translate(loadedWeapon.LabelCap),
					icon = ContentFinder<Texture2D>.Get("UI/Buttons/Drop", true),
					action = RequestRemove
				};
			}

			if (Prefs.DevMode && DebugSettings.godMode)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: Install random weapon",
					action = () =>
					{
						ThingDef wDef = DefDatabase<ThingDef>.AllDefsListForReading
							.Where(d => d.IsRangedWeapon && d.IsWeaponUsingProjectiles && d.PlayerAcquirable
								&& (d.weaponTags == null || !d.weaponTags.Contains("TurretGun")))
							.RandomElementWithFallback();
						if (wDef != null)
						{
							ThingWithComps w = (ThingWithComps)ThingMaker.MakeThing(wDef);
							InstallWeapon(w);
						}
					}
				};
			}
		}

		private void OpenWeaponSelector()
		{
			if (Map == null) return;
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (Thing t in Map.listerThings.AllThings)
			{
				if (!IsValidWeapon(t)) continue;
				if (t.IsForbidden(Faction.OfPlayer)) continue;
				if (!t.Spawned) continue;
				if (Map.reservationManager.IsReservedByAnyoneOf(t, Faction.OfPlayer)) continue;
				ThingWithComps weapon = (ThingWithComps)t;
				options.Add(new FloatMenuOption(weapon.LabelCap, () => RequestInstall(weapon), weapon, Color.white));
			}
			if (options.Count == 0)
			{
				Messages.Message("SubcoreAutomation_SwapTurret_NoWeaponsAvailable".Translate(), this, MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}
	}
}
