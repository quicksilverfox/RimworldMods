using System;
using Harmony;
using RimWorld;
using Verse;
using UnityEngine;
using System.Reflection;

namespace AnimalsLogic
{
    /*
     * Adds rename button to animals.
     */

    [StaticConstructorOnStartup]
    class GiveMeName
    {
        public static Texture2D Rename = null;

        [HarmonyPatch]
        static class MainTabWindow_DoInspectPaneButtons_Patch
        {
            static MethodInfo TargetMethod()
            {
                return typeof(MainTabWindow_Inspect).GetMethod("DoInspectPaneButtons", new Type[] { typeof(Rect), Type.GetType("System.Single&") }); // BLAK MAGIK
            }

            static bool Prefix(ref Rect __state, Rect rect)
            {
                __state = rect;
                return true;
            }

            static void Postfix(ref Rect __state, ref MainTabWindow_Inspect __instance)
            {
                if (__state == null)
                    return;

                if (Rename == null) // Lazy init
                {
                    Rename = ContentFinder<Texture2D>.Get("UI/Buttons/Rename", true);
                }

                if (Find.Selector.NumSelected == 1)
                {
                    Thing singleSelectedThing = Find.Selector.SingleSelectedThing;
                    if (singleSelectedThing != null)
                    {
                        if (singleSelectedThing is Pawn pawn && pawn.RaceProps.Animal && pawn.Faction != null && pawn.Faction.IsPlayer)
                        {
                            Rect rect7 = new Rect(__state.width - 78f, 0f, 30f, 30f);
                            TooltipHandler.TipRegion(rect7, new TipSignal("RenameColonist".Translate()));
                            if (Widgets.ButtonImage(rect7, Rename))
                            {
                                Find.WindowStack.Add(new Dialog_ChangeNameSingle(pawn));
                            }
                        }
                    }
                }
            }
        }

        public class Dialog_ChangeNameSingle : Window
        {
            private const int MaxNameLength = 16;

            private Pawn pawn;

            private string curName;

            private NameSingle CurPawnName
            {
                get
                {
                    return new NameSingle(curName);
                }
            }

            public override Vector2 InitialSize
            {
                get
                {
                    return new Vector2(500f, 175f);
                }
            }

            public Dialog_ChangeNameSingle(Pawn pawn)
            {
                this.pawn = pawn;
                this.curName = ((NameSingle)this.pawn.Name).Name;
                this.forcePause = true;
                this.absorbInputAroundWindow = true;
                this.closeOnClickedOutside = true;
            }

            public override void DoWindowContents(Rect inRect)
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(15f, 15f, 500f, 50f), this.CurPawnName.ToString().Replace(" '' ", " "));
                Text.Font = GameFont.Small;
                string text = Widgets.TextField(new Rect(15f, 50f, inRect.width / 2f - 20f, 35f), this.curName);
                if (text.Length < 16)
                {
                    this.curName = text;
                }
                if (Widgets.ButtonText(new Rect(inRect.width / 2f + 20f, inRect.height - 35f, inRect.width / 2f - 20f, 35f), "OK", true, false, true) || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
                {
                    if (this.curName.Length < 1)
                    {
                        int num = 1;
                        while (true)
                        {
                            curName = pawn.KindLabel + " " + num.ToString();
                            if (!NameUseChecker.NameSingleIsUsed(curName))
                            {
                                break;
                            }
                            num++;
                        }
                        this.pawn.Name = new NameSingle(curName, true);
                    }
                    else
                        this.pawn.Name = this.CurPawnName;

                    Find.WindowStack.TryRemove(this, true);
                    Messages.Message("PawnGainsName".Translate(new object[] { this.curName }), this.pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
        }
    }
}
