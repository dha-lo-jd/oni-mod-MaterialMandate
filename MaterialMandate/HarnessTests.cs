#if DEBUG
// In-game tests run by Gravitas.TestHarness (python Gravitas.TestProbe/probe.py run --project <this repo> --tests).
// The harness matches these types by simple name, so this mod does not reference the harness assembly.
using System;

namespace MaterialMandate;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class HarnessTestAttribute : Attribute
{
    public string Name { get; set; }
    public string Tags { get; set; }
}

internal sealed class HarnessAssertionException : Exception
{
    public HarnessAssertionException(string message) : base(message) { }
}

internal static class HarnessTests
{
    [HarnessTest(Name = "materialmandate.mod_loaded", Tags = "materialmandate")]
    public static void ModLoaded()
    {
        if (MaterialMandateMod.modPath == null)
            throw new HarnessAssertionException("MaterialMandateMod.OnLoad has not run");
    }
}
#endif
