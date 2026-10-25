using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScenarioPartClipboard
{
    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        static Bootstrap()
        {
            new Harmony("chatgpt.scenariopartclipboard").PatchAll();
            Log.Message("[Scenario Part Clipboard] Loaded. Per-part scenario editor controls registered.");
        }
    }

    [HarmonyPatch]
    internal static class ScenarioEditorContextPatch
    {
        private static Scenario currentScenario;

        private static MethodBase TargetMethod()
        {
            Type scenarioUi = AccessTools.TypeByName("RimWorld.ScenarioUI");
            MethodBase method = scenarioUi == null
                ? null
                : AccessTools.Method(scenarioUi, "DrawScenarioEditInterface");
            if (method == null)
                Log.Error("[Scenario Part Clipboard] Could not find ScenarioUI.DrawScenarioEditInterface.");
            return method;
        }

        private static void Prefix(object[] __args, out Scenario __state)
        {
            __state = currentScenario;
            currentScenario = null;
            foreach (object argument in __args)
            {
                currentScenario = argument as Scenario;
                if (currentScenario != null)
                    break;
            }
        }

        private static void Postfix(Scenario __state)
        {
            ScenarioPartDragState.CompletePendingDrop(currentScenario);
            if (Input.GetMouseButtonUp(0))
                ScenarioPartDragState.Cancel();
            currentScenario = __state;
        }

        internal static Scenario CurrentScenario
        {
            get { return currentScenario; }
        }
    }

    [HarmonyPatch(typeof(Listing_ScenEdit), "GetScenPartRect")]
    internal static class ScenarioPartHeaderControlsPatch
    {
        private static readonly FieldInfo ScenarioPartsField = AccessTools.Field(typeof(Scenario), "parts");

        private static void Postfix(ScenPart __0, Rect __result)
        {
            Scenario scenario = ScenarioEditorContextPatch.CurrentScenario;
            if (scenario == null)
                return;

            List<ScenPart> parts = GetParts(scenario);
            if (parts == null || !parts.Contains(__0))
                return;

            const float buttonSize = 20f;
            const float buttonGap = 2f;
            float rowLeft = __result.x - __result.width;
            float controlsLeft = rowLeft + 72f;
            Rect dragRect = new Rect(controlsLeft, __result.y + 2f, buttonSize, buttonSize);
            Rect copyRect = new Rect(dragRect.xMax + buttonGap, dragRect.y, buttonSize, buttonSize);
            Rect pasteRect = new Rect(copyRect.xMax + buttonGap, dragRect.y, buttonSize, buttonSize);
            Rect duplicateRect = new Rect(pasteRect.xMax + buttonGap, dragRect.y, buttonSize, buttonSize);

            if (Widgets.ButtonImage(copyRect, TexButton.Copy, true, "Copy scenario part"))
                ScenarioPartClipboardActions.Copy(__0);

            if (Widgets.ButtonImage(pasteRect, TexButton.Paste, true, "Paste scenario part below"))
                ScenarioPartClipboardActions.PasteAfter(__0, scenario);

            if (Widgets.ButtonImage(duplicateRect, TexButton.NewItem, true, "Duplicate scenario part"))
                ScenarioPartClipboardActions.DuplicateAfter(__0, scenario);

            if (scenario.CanReorder(__0, ReorderDirection.Up) ||
                scenario.CanReorder(__0, ReorderDirection.Down))
            {
                Widgets.DraggableResult dragResult =
                    Widgets.ButtonImageDraggable(dragRect, TexButton.DragHash);
                if (dragResult == Widgets.DraggableResult.Dragged)
                    ScenarioPartDragState.Begin(__0, scenario);

                Event currentEvent = Event.current;
                if (ScenarioPartDragState.IsDragging &&
                    Input.GetMouseButtonUp(0) &&
                    currentEvent != null &&
                    currentEvent.mousePosition.y >= __result.y &&
                    currentEvent.mousePosition.y <= __result.yMax)
                {
                    ScenarioPartDragState.QueueDropOn(
                        __0,
                        scenario,
                        currentEvent.mousePosition.y >= __result.center.y);
                }
            }
        }

        internal static List<ScenPart> GetParts(Scenario scenario)
        {
            if (ScenarioPartsField == null)
                throw new MissingFieldException(typeof(Scenario).FullName, "parts");
            return (List<ScenPart>)ScenarioPartsField.GetValue(scenario);
        }
    }

    internal static class ScenarioPartDragState
    {
        private static ScenPart draggedPart;
        private static Scenario draggedScenario;
        private static ScenPart pendingTarget;
        private static bool pendingAfterTarget;

        internal static bool IsDragging
        {
            get { return draggedPart != null; }
        }

        internal static void Begin(ScenPart part, Scenario scenario)
        {
            draggedPart = part;
            draggedScenario = scenario;
        }

        internal static void QueueDropOn(ScenPart target, Scenario scenario, bool afterTarget)
        {
            if (draggedPart == null || draggedScenario != scenario)
            {
                Cancel();
                return;
            }

            pendingTarget = target;
            pendingAfterTarget = afterTarget;
        }

        internal static void CompletePendingDrop(Scenario scenario)
        {
            if (pendingTarget == null)
                return;

            if (draggedPart == null || draggedScenario != scenario || draggedPart == pendingTarget)
            {
                Cancel();
                return;
            }

            List<ScenPart> parts = ScenarioPartHeaderControlsPatch.GetParts(scenario);
            int sourceIndex = parts.IndexOf(draggedPart);
            int targetIndex = parts.IndexOf(pendingTarget);
            if (sourceIndex < 0 || targetIndex < 0)
            {
                Cancel();
                return;
            }

            int destinationIndex = targetIndex + (pendingAfterTarget ? 1 : 0);
            if (sourceIndex < destinationIndex)
                destinationIndex--;

            while (sourceIndex < destinationIndex &&
                   scenario.CanReorder(draggedPart, ReorderDirection.Down))
            {
                scenario.Reorder(draggedPart, ReorderDirection.Down);
                sourceIndex++;
            }

            while (sourceIndex > destinationIndex &&
                   scenario.CanReorder(draggedPart, ReorderDirection.Up))
            {
                scenario.Reorder(draggedPart, ReorderDirection.Up);
                sourceIndex--;
            }

            Cancel();
        }

        internal static void Cancel()
        {
            draggedPart = null;
            draggedScenario = null;
            pendingTarget = null;
            pendingAfterTarget = false;
        }
    }

    internal static class ScenarioPartClipboardActions
    {
        private static ScenPart clipboardPart;

        internal static void Copy(ScenPart source)
        {
            clipboardPart = ClonePart(source);
            if (clipboardPart != null)
                Messages.Message("Scenario part copied.", MessageTypeDefOf.PositiveEvent, false);
        }

        internal static void PasteAfter(ScenPart target, Scenario scenario)
        {
            if (clipboardPart == null)
            {
                Messages.Message("Clipboard is empty. Copy a scenario part first.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            InsertAfter(target, ClonePart(clipboardPart), scenario);
        }

        internal static void DuplicateAfter(ScenPart target, Scenario scenario)
        {
            InsertAfter(target, ClonePart(target), scenario);
        }

        private static void InsertAfter(ScenPart target, ScenPart part, Scenario scenario)
        {
            if (part == null || scenario == null)
                return;

            List<ScenPart> parts = ScenarioPartHeaderControlsPatch.GetParts(scenario);
            if (parts == null)
                return;

            int index = parts.IndexOf(target);
            if (index < 0)
                return;

            parts.Insert(index + 1, part);
        }

        private static ScenPart ClonePart(ScenPart source)
        {
            if (source == null)
                return null;

            try
            {
                ScenPart clone = (ScenPart)Activator.CreateInstance(source.GetType(), true);
                CopyFields(source, clone);
                return clone;
            }
            catch (Exception exception)
            {
                Log.Warning("[Scenario Part Clipboard] Could not clone " +
                            source.GetType().FullName + ": " + exception);
                Messages.Message("This scenario part could not be cloned. Check Player.log.",
                    MessageTypeDefOf.RejectInput, false);
                return null;
            }
        }

        private static void CopyFields(ScenPart source, ScenPart target)
        {
            for (Type type = source.GetType();
                 type != null && typeof(ScenPart).IsAssignableFrom(type);
                 type = type.BaseType)
            {
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                foreach (FieldInfo field in fields)
                {
                    if (field.IsStatic || field.IsInitOnly)
                        continue;

                    object value = field.GetValue(source);
                    field.SetValue(target, CopyCollection(value));
                }
            }
        }

        private static object CopyCollection(object value)
        {
            if (value == null)
                return null;

            Array array = value as Array;
            if (array != null)
                return array.Clone();

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                IDictionary copy = (IDictionary)Activator.CreateInstance(value.GetType());
                foreach (DictionaryEntry entry in dictionary)
                    copy.Add(entry.Key, entry.Value);
                return copy;
            }

            IList list = value as IList;
            if (list != null)
            {
                IList copy = (IList)Activator.CreateInstance(value.GetType());
                foreach (object item in list)
                    copy.Add(item);
                return copy;
            }

            return value;
        }
    }
}
