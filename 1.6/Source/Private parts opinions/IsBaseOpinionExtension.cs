using Verse; // Базовые классы RimWorld

namespace NudityMattersMore_opinions
{
    /// <summary>
    /// A simple extension to DefModExtension used to mark
    /// "basic" or "abstract" opinion definitions in XML.
    /// Allows you to filter these Defs in code without relying on def.abstract.
    /// </summary>
    public class IsBaseOpinionExtension : DefModExtension { }
}
