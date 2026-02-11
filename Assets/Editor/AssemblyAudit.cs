using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

public static class AssemblyAudit
{
    [MenuItem("Tools/Assembly Audit/Dump Player Assemblies")]
    public static void DumpPlayerAssemblies()
    {
        Debug.Log("=== AssemblyAudit: Player assemblies ===");
        var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
        Debug.Log($"Player assembly count: {playerAssemblies.Length}");

        foreach (var asm in playerAssemblies.OrderBy(a => a.name))
        {
            var refs = asm.assemblyReferences == null
                ? "(none)"
                : string.Join(", ", asm.assemblyReferences.Select(r => r.name).OrderBy(n => n));
            Debug.Log($"PlayerAssembly: {asm.name} | flags: {asm.flags} | output: {asm.outputPath} | refs: {refs}");
        }

        var mirrorInPlayer = playerAssemblies.Any(a => a.name == "Mirror");
        Debug.Log($"Mirror in player assemblies: {mirrorInPlayer}");

        var focusNames = new[] { "Game.Runtime", "Mirror.Transports", "Mirror.Authenticators", "Game.Network", "Game.Network.Transport.Mirror" };
        foreach (var name in focusNames)
        {
            var asm = playerAssemblies.FirstOrDefault(a => a.name == name);
            if (asm == null)
            {
                Debug.Log($"FocusAssembly: {name} | NOT FOUND");
                continue;
            }

            Debug.Log($"FocusAssembly: {name}");
            Debug.Log($"  Flags: {asm.flags}");
            Debug.Log($"  Output: {asm.outputPath}");
            Debug.Log($"  Defines: {string.Join(", ", asm.defines ?? new string[0])}");
            Debug.Log($"  SourceFiles: {asm.sourceFiles?.Length ?? 0}");
            if (asm.sourceFiles != null && asm.sourceFiles.Length > 0)
            {
                foreach (var file in asm.sourceFiles.Take(5))
                {
                    Debug.Log($"    Source: {file}");
                }
            }
        }

        Debug.Log("=== AssemblyAudit: Mirror asmdef assets ===");
        foreach (var guid in AssetDatabase.FindAssets("Mirror t:asmdef"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var fileText = File.Exists(path) ? File.ReadAllText(path) : "<missing file>";
            Debug.Log($"Asmdef: {path}\n{fileText}");
        }

        var sampleScript = "Assets/Game/Runtime/Mirror/Transport.cs";
        var sampleAsmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromScriptPath(sampleScript);
        Debug.Log($"Script '{sampleScript}' asmdef: {sampleAsmdef}");

        var editorScript = "Assets/Mirror/Transports/SimpleWeb/Editor/ClientWebsocketSettingsDrawer.cs";
        var editorAsmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromScriptPath(editorScript);
        Debug.Log($"Script '{editorScript}' asmdef: {editorAsmdef}");

        var compilerSymbolsScript = "Assets/Mirror/CompilerSymbols/PreprocessorDefine.cs";
        var compilerSymbolsAsmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromScriptPath(compilerSymbolsScript);
        Debug.Log($"Script '{compilerSymbolsScript}' asmdef: {compilerSymbolsAsmdef}");

        Debug.Log("=== AssemblyAudit: Done ===");
        EditorApplication.Exit(0);
    }

    [MenuItem("Tools/Assembly Audit/Dump Player Compile")]
    public static void DumpPlayerCompile()
    {
        Debug.Log("=== AssemblyAudit: Player compile ===");

        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
        Debug.Log($"Active build target: {target} | group: {group}");

        string mirrorPath = "Assets/Game/Runtime/Game.Runtime.asmdef";
        string mirrorGuid = AssetDatabase.AssetPathToGUID(mirrorPath);
        Debug.Log($"Game.Runtime asmdef path {mirrorPath} GUID: {mirrorGuid}");

        string outputDir = Path.Combine("Library", "PlayerScriptAssembliesDump");
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }
        Directory.CreateDirectory(outputDir);

        Debug.Log($"Compile output dir: {outputDir}");
        object result = TryCompilePlayerScripts(group, target, outputDir);
        if (result == null)
        {
            Debug.LogError("CompilePlayerScripts API not found. Skipping player compile dump.");
            EditorApplication.Exit(1);
            return;
        }

        DumpCompileResult(result);

        Debug.Log("=== AssemblyAudit: Player compile done ===");
        EditorApplication.Exit(0);
    }

    static object TryCompilePlayerScripts(BuildTargetGroup group, BuildTarget target, string outputDir)
    {
        var playerBuildInterfaceType = Type.GetType("UnityEditor.Build.Player.PlayerBuildInterface, UnityEditor");
        if (playerBuildInterfaceType == null)
            return null;

        var settingsType =
            Type.GetType("UnityEditor.Build.Player.ScriptCompilationSettings, UnityEditor") ??
            Type.GetType("UnityEditor.Compilation.ScriptCompilationSettings, UnityEditor");
        if (settingsType == null)
            return null;

        var optionsType =
            Type.GetType("UnityEditor.Build.Player.ScriptCompilationOptions, UnityEditor") ??
            Type.GetType("UnityEditor.Compilation.ScriptCompilationOptions, UnityEditor");

        object settings = Activator.CreateInstance(settingsType);
        SetMember(settings, "group", group);
        SetMember(settings, "target", target);
        if (optionsType != null)
        {
            object noneValue = Enum.ToObject(optionsType, 0);
            SetMember(settings, "options", noneValue);
        }

        var method = playerBuildInterfaceType.GetMethod(
            "CompilePlayerScripts",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { settingsType, typeof(string) },
            null);
        if (method == null)
            return null;

        return method.Invoke(null, new object[] { settings, outputDir });
    }

    static void DumpCompileResult(object result)
    {
        var resultType = result.GetType();
        Debug.Log($"Compile result type: {resultType.FullName}");
        Debug.Log($"Result properties: {string.Join(", ", resultType.GetProperties().Select(p => p.Name))}");
        Debug.Log($"Result fields: {string.Join(", ", resultType.GetFields().Select(f => f.Name))}");
        var assemblies = GetMember(result, "assemblies") as System.Collections.IEnumerable;
        var messages = GetMember(result, "messages") as System.Collections.IEnumerable;

        int assemblyCount = CountEnumerable(assemblies);
        int messageCount = CountEnumerable(messages);
        Debug.Log($"Compile result: {assemblyCount} assemblies, {messageCount} messages");

        if (messages != null)
        {
            foreach (var message in messages)
            {
                var type = GetMember(message, "type");
                var text = GetMember(message, "message");
                var file = GetMember(message, "file");
                var line = GetMember(message, "line");
                Debug.Log($"{type}: {text} ({file}:{line})");
            }
        }

        if (assemblies != null)
        {
            foreach (var asm in assemblies)
            {
                var name = GetMember(asm, "name");
                var output = GetMember(asm, "outputPath");
                var asmdef = GetMember(asm, "assemblyDefinitionFilePath");
                Debug.Log($"CompiledAssembly: {name} | output: {output} | asmdef: {asmdef}");
            }
        }
    }

    static void SetMember(object target, string memberName, object value)
    {
        var type = target.GetType();
        var prop = type.GetProperty(memberName);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(target, value);
            return;
        }

        var field = type.GetField(memberName);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }

    static object GetMember(object target, string memberName)
    {
        if (target == null)
            return null;

        var type = target.GetType();
        var prop = type.GetProperty(memberName);
        if (prop != null)
            return prop.GetValue(target);

        var field = type.GetField(memberName);
        if (field != null)
            return field.GetValue(target);

        return null;
    }

    static int CountEnumerable(System.Collections.IEnumerable items)
    {
        if (items == null)
            return 0;

        int count = 0;
        foreach (var _ in items)
            count++;
        return count;
    }
}
