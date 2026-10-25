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
            currentScenario = __state;
        }

        internal static Scenario CurrentScenario
        {
            get { return currentScenario; }
        }
    }

    [HarmonyPatch]
    internal static class ScenarioPartControlsPatch
    {
        private static readonly FieldInfo ScenarioPartsField = AccessTools.Field(typeof(Scenario), "parts");

        private static IEnumerable<MethodBase> TargetMethods()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                    Log.Warning("[Scenario Part Clipboard] Some types could not be inspected in " +
                                assembly.GetName().Name + ": " + exception.Message);
                }

                foreach (Type type in types)
                {
                    if (type == null || !typeof(ScenPart).IsAssignableFrom(type))
                        continue;

                    MethodInfo method = type.GetMethod(
                        "DoEditInterface",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly,
                        null,
                        new[] { typeof(Listing_ScenEdit) },
                        null);
                    if (method != null)
                        yield return method;
                }
            }
        }

        private static void Postfix(ScenPart __instance, Listing_ScenEdit __0)
        {
            Scenario scenario = ScenarioEditorContextPatch.CurrentScenario;
            if (scenario == null)
                return;

            List<ScenPart> parts = GetParts(scenario);
            if (parts == null)
                return;

            int index = parts.IndexOf(__instance);
            if (index < 0)
                return;

            Rect controls = __0.GetRect(24f);
            float buttonWidth = (controls.width - 8f) / 3f;
            Rect copyRect = new Rect(controls.x, controls.y, buttonWidth, controls.height);
            Rect pasteRect = new Rect(copyRect.xMax + 4f, controls.y, buttonWidth, controls.height);
            Rect duplicateRect = new Rect(pasteRect.xMax + 4f, controls.y, buttonWidth, controls.height);

            if (Widgets.ButtonText(copyRect, "Copy"))
                ScenarioPartClipboardActions.Copy(__instance);

            if (Widgets.ButtonText(pasteRect, "Paste"))
                ScenarioPartClipboardActions.PasteAfter(__instance, scenario);

            if (Widgets.ButtonText(duplicateRect, "Duplicate"))
                ScenarioPartClipboardActions.DuplicateAfter(__instance, scenario);
        }

        internal static List<ScenPart> GetParts(Scenario scenario)
        {
            if (ScenarioPartsField == null)
                throw new MissingFieldException(typeof(Scenario).FullName, "parts");
            return (List<ScenPart>)ScenarioPartsField.GetValue(scenario);
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

            List<ScenPart> parts = ScenarioPartControlsPatch.GetParts(scenario);
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
