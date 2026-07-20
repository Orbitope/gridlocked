/// <summary>
/// Central platform switch. The research tooling — CSV logging, graph analysis,
/// runtime puzzle generation, and the difficulty/satisfaction rating capture —
/// is desktop/editor only. A WebGL build has no filesystem and slower WASM, so
/// it ships as a clean player build (curated levels + hex + audio).
/// </summary>
public static class BuildConfig
{
#if UNITY_WEBGL && !UNITY_EDITOR
    public const bool ResearchEnabled = false;
#else
    // Editor-settable so the web-only layout can be previewed without a build.
    public static bool ResearchEnabled = true;
#endif
}
