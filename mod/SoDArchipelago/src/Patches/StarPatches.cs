using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace SoDArchipelago.Patches
{
    // Shuffled star requirements (StarRequirements): give every star DewResources hands out the seed's level. Load covers
    // fresh loads and lookups by guid (AssetRef); GetByType covers its own cache, which skips Load.
    [HarmonyPatch(typeof(DewResources), nameof(DewResources.Load), typeof(string), typeof(ResourceLoadSettings))]
    internal static class ResourceLoadPatch
    {
        private static void Postfix(string guid, UnityEngine.Object __result)
        {
            try
            {
                if (!StarRequirements.IsStarGuid(guid) || __result == null) return;
                var star = StarRequirements.StarOf(__result);
                if (star != null) StarRequirements.Apply(star);
            }
            catch (Exception e)
            {
                StarPatchErrors.Report(e);
            }
        }
    }

    [HarmonyPatch]
    internal static class ResourceGetByTypePatch
    {
        // The non-generic GetByType(Type, ResourceLoadSettings); a generic overload has the same parameters.
        private static MethodBase TargetMethod() =>
            typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(DewResources.GetByType) && !m.IsGenericMethodDefinition &&
                             m.GetParameters().Select(p => p.ParameterType)
                                 .SequenceEqual(new[] { typeof(Type), typeof(ResourceLoadSettings) }));

        private static void Postfix(UnityEngine.Object __result)
        {
            try
            {
                if (__result is StarEffect star) StarRequirements.Apply(star);
            }
            catch (Exception e)
            {
                StarPatchErrors.Report(e);
            }
        }
    }

    // Never let a star problem break resource loading; report it once.
    internal static class StarPatchErrors
    {
        private static bool _reported;

        public static void Report(Exception e)
        {
            if (_reported) return;
            _reported = true;
            Log.Warn("Applying star requirements failed: " + e);
        }
    }
}
