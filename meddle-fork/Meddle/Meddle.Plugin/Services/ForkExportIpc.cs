// PRIVATE FORK ONLY — never distributed. Lets the Soulweaver plugin trigger a
// character export (same path as "Export All Models With Attaches") without UI.
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Meddle.Plugin.Models;
using Meddle.Plugin.Models.Composer;
using Meddle.Plugin.Models.Layout;
using Meddle.Plugin.Utils;
using Microsoft.Extensions.Logging;
using SharpGLTF.Scenes;
using CSCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using CSCharacterBase = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase;

namespace Meddle.Plugin.Services;

public sealed unsafe class ForkExportIpc : IService, IDisposable
{
    public const int ApiVersion = 2; // 2: ExportCharacterEx (a rider without its mount, a mount by itself)
    public const string ErrBusy = "ERR_BUSY";
    public const string ErrNoObject = "ERR_NO_OBJECT";
    public const string ErrNotHuman = "ERR_NOT_HUMAN";

    private readonly IObjectTable objects;
    private readonly IFramework framework;
    private readonly ResolverService resolver;
    private readonly ComposerFactory composerFactory;
    private readonly Configuration config;
    private readonly ILogger<ForkExportIpc> log;
    private readonly ICallGateProvider<int> apiVersion;
    private readonly ICallGateProvider<string> exportDirectory;
    private readonly ICallGateProvider<float> exportProgress;
    private ExportProgress? currentProgress;
    private readonly ICallGateProvider<int, string, string> exportCharacter;
    private readonly ICallGateProvider<string, string, string> exportLayout;
    private readonly LayoutService layoutService;
    private readonly ICallGateProvider<string, bool, string, string, object> exportFinished;
    private readonly IDalamudPluginInterface pi;
    private int busy;

    public ForkExportIpc(IDalamudPluginInterface pi, IObjectTable objects, IFramework framework, ResolverService resolver,
                         ComposerFactory composerFactory, Configuration config, ILogger<ForkExportIpc> log,
                         LayoutService layoutService)
    {
        this.pi = pi;
        this.layoutService = layoutService;
        this.objects = objects;
        this.framework = framework;
        this.resolver = resolver;
        this.composerFactory = composerFactory;
        this.config = config;
        this.log = log;

        apiVersion = pi.GetIpcProvider<int>("MeddleFork.ApiVersion");
        apiVersion.RegisterFunc(() => ApiVersion);
        exportDirectory = pi.GetIpcProvider<string>("MeddleFork.GetExportDirectory");
        exportDirectory.RegisterFunc(() => config.ExportDirectory); // the folder set in Meddle's options
        exportProgress = pi.GetIpcProvider<float>("MeddleFork.GetExportProgress");
        exportProgress.RegisterFunc(() =>
        {
            var p = currentProgress;
            return p == null || p.Total <= 0 ? 0f : Math.Clamp((float)p.Progress / p.Total, 0f, 1f);
        });
        exportCharacter = pi.GetIpcProvider<int, string, string>("MeddleFork.ExportCharacter");
        exportCharacter.RegisterFunc(ExportCharacter);
        exportCharacterEx = pi.GetIpcProvider<int, string, int, string>("MeddleFork.ExportCharacterEx");
        exportCharacterEx.RegisterFunc((index, outDir, mode) => ExportCharacter(index, outDir, (ExportMode)mode));
        exportLayout = pi.GetIpcProvider<string, string, string>("MeddleFork.ExportLayout");
        exportLayout.RegisterFunc(ExportLayout);
        exportFinished = pi.GetIpcProvider<string, bool, string, string, object>("MeddleFork.ExportFinished");
        pi.UiBuilder.Draw += OnDraw;
        log.LogInformation("MeddleFork export IPC ready (api {Version})", ApiVersion);
    }

    /// <summary>One picked layout item: Soulweaver's stable key and the layout instance's address right now.</summary>
    private sealed record LayoutPick(string Key, long Address);

    private sealed record PendingLayout(string JobId, LayoutPick[] Items, string OutDir, string Name);

    private PendingLayout? pendingLayout;

    /// <summary>
    /// IPC: export picked layout items (props, prop groups, furniture) into one .glb, one root node per item named
    /// "sw:{key}" at its world position. Parsed on the UI draw like characters; composed in the background.
    /// </summary>
    private string ExportLayout(string itemsJson, string outDir)
    {
        LayoutPick[]? items;
        try
        {
            items = JsonSerializer.Deserialize<LayoutPick[]>(itemsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            items = null;
        }
        if (items == null || items.Length == 0) return ErrNoObject;
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) return ErrBusy;
        var job = new PendingLayout(Guid.NewGuid().ToString("N"), items, outDir, "Layout");
        log.LogInformation("Soulweaver layout export {JobId} queued ({Count} items)", job.JobId, items.Length);
        Volatile.Write(ref pendingLayout, job);
        return job.JobId;
    }

    private void DrawLayout()
    {
        var job = Interlocked.Exchange(ref pendingLayout, null);
        if (job == null) return;
        List<(string Key, ParsedInstance Instance)> picked = [];
        try
        {
            var origin = objects.LocalPlayer?.Position ?? System.Numerics.Vector3.Zero;
            var all = new Dictionary<nint, ParsedInstance>();
            void Add(ParsedInstance i)
            {
                all.TryAdd(i.Id, i);
                if (i is ParsedSharedInstance shared)
                    foreach (var child in shared.Children) Add(child);
            }
            foreach (var i in layoutService.GetWorldState(origin) ?? []) Add(i);
            foreach (var item in job.Items)
                if (all.TryGetValue((nint)item.Address, out var instance)) picked.Add((item.Key, instance));
            resolver.ResolveInstances(picked.Select(p => p.Instance).ToArray());
        }
        catch (Exception e)
        {
            log.LogError(e, "Soulweaver layout export {JobId}: parsing failed", job.JobId);
        }
        if (picked.Count == 0)
        {
            Finish(job.JobId, false, "", "None of the picked objects could be read");
            return;
        }
        var exportConfig = config.ExportConfig.Clone();
        exportConfig.SetDefaultCloneOptions();
        exportConfig.ExportType = ExportType.GLB;
        exportConfig.CacheFileTypes = 0;
        Task.Run(() =>
        {
            var started = DateTime.UtcNow;
            string glbPath = "", error = "";
            var ok = false;
            try
            {
                Directory.CreateDirectory(job.OutDir);
                var composer = composerFactory.CreateComposer(job.OutDir, exportConfig);
                var scene = new SceneBuilder();
                currentProgress = new ExportProgress(picked.Count, "Layout");
                foreach (var (key, instance) in picked)
                {
                    var node = composer.ComposeInstance(instance, scene, currentProgress);
                    if (node != null) node.Name = "sw:" + key;
                }
                ExportUtil.SaveAsType(scene.ToGltf2(), exportConfig.ExportType, job.OutDir, job.Name);
                glbPath = Path.Combine(job.OutDir, job.Name + ".glb");
                ok = File.Exists(glbPath);
                if (!ok) error = "Meddle didn't write the .glb";
            }
            catch (Exception e)
            {
                log.LogError(e, "Soulweaver layout export {JobId} failed", job.JobId);
                error = e.Message;
            }
            log.LogInformation("Soulweaver layout export {JobId} {Result} in {Seconds:0.0}s", job.JobId, ok ? "done" : "failed",
                               (DateTime.UtcNow - started).TotalSeconds);
            Finish(job.JobId, ok, glbPath, error);
        });
    }

    /// <summary>Whole: as Meddle exports it (a rider fused onto its mount). Rider: the character without the mount it
    /// sits on. Mount: the mount of that character, by itself (no riders).</summary>
    private enum ExportMode { Whole = 0, Rider = 1, Mount = 2 }

    private ICallGateProvider<int, string, int, string>? exportCharacterEx;

    private sealed record PendingExport(string JobId, int ObjectIndex, string OutDir, string Name, ExportMode Mode = ExportMode.Whole);

    /// <summary>The character to read: the object itself, or (Mount) the mount it sits on, looked up right now.</summary>
    private CSCharacter* Target(int objectIndex, ExportMode mode)
    {
        var obj = objects[objectIndex];
        if (obj == null || obj.Address == nint.Zero) return null;
        var character = (CSCharacter*)obj.Address;
        return mode == ExportMode.Mount ? character->Mount.MountObject : character;
    }

    // Set by the IPC call, picked up in the next UI draw (one at a time; guarded by `busy`).
    private PendingExport? pending;

    /// <summary>
    /// IPC entry point (Soulweaver calls it from the framework thread). Only checks the target and queues the job:
    /// parsing reads textures from the GPU, which is only safe while Dalamud draws the UI (that's where Meddle's own
    /// Export button does it). Parsing from the framework thread races the render thread and can freeze the game.
    /// </summary>
    private string ExportCharacter(int objectIndex, string outDir) => ExportCharacter(objectIndex, outDir, ExportMode.Whole);

    private string ExportCharacter(int objectIndex, string outDir, ExportMode mode)
    {
        var obj = objects[objectIndex];
        var character = Target(objectIndex, mode);
        if (obj == null || character == null) return ErrNoObject;
        var drawObject = character->DrawObject;
        if (drawObject == null) return ErrNoObject;
        // Players and humanoid NPCs (Human), the Moogle and similar (DemiHuman), minions, enemies and bosses (Monster):
        // Meddle's parser and composer handle all three; only a bare weapon or a non-character object is refused.
        if (drawObject->GetObjectType() != ObjectType.CharacterBase
            || ((CSCharacterBase*)drawObject)->GetModelType() is not (CSCharacterBase.ModelType.Human
                or CSCharacterBase.ModelType.DemiHuman or CSCharacterBase.ModelType.Monster))
            return ErrNotHuman;
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) return ErrBusy;

        var name = obj.Name.TextValue.SanitizeFileName();
        if (string.IsNullOrWhiteSpace(name)) name = "Character";
        if (mode == ExportMode.Mount) name = (name + "_mount").SanitizeFileName(); // the saved .glb gets exactly this name
        var job = new PendingExport(Guid.NewGuid().ToString("N"), objectIndex, outDir, name, mode);
        log.LogInformation("Soulweaver export {JobId} queued for {Name}", job.JobId, name);
        Volatile.Write(ref pending, job);
        return job.JobId;
    }

    /// <summary>UiBuilder.Draw: parse the queued character here (same place as Meddle's Export button), then compose in the background.</summary>
    private void OnDraw()
    {
        DrawLayout();
        var job = Interlocked.Exchange(ref pending, null);
        if (job == null) return;

        ParsedCharacterInfo? info = null;
        try
        {
            var target = Target(job.ObjectIndex, job.Mode); // looked up again now: never a pointer from the IPC call
            if (target != null) info = resolver.ParseCharacter(target);
            if (info != null && job.Mode == ExportMode.Rider)
                info.Attaches = info.Attaches.Where(a => a.Attach.ExecuteType != 0).ToList(); // not on its mount: its own rig, where it sits
            if (info != null && job.Mode == ExportMode.Mount)
                info.Attaches = []; // the mount alone: riders are sent as their own characters
        }
        catch (Exception e)
        {
            log.LogError(e, "Soulweaver export {JobId}: parsing failed", job.JobId);
        }
        if (info == null)
        {
            Finish(job.JobId, false, "", "Couldn't read the character (did it leave?)");
            return;
        }

        var exportConfig = config.ExportConfig.Clone();
        exportConfig.SetDefaultCloneOptions(); // same defaults as Meddle's own export
        exportConfig.ExportType = ExportType.GLB;
        // MeddleTools only reads the PNGs: skip copying raw .tex/.mdl/.mtrl/.shpk (~130 MB per character).
        exportConfig.CacheFileTypes = 0;

        Task.Run(() =>
        {
            var started = DateTime.UtcNow;
            string glbPath = "", error = "";
            var ok = false;
            try
            {
                Directory.CreateDirectory(job.OutDir);
                File.WriteAllText(Path.Combine(job.OutDir, $"{job.Name}_blob.json"),
                                  JsonSerializer.Serialize(info, MaterialComposer.JsonOptions));
                var composer = composerFactory.CreateCharacterComposer(job.OutDir, exportConfig);
                var scene = new SceneBuilder();
                var root = new NodeBuilder($"Character-{job.Name}");
                scene.AddNode(root);
                currentProgress = new ExportProgress(info.Models.Count, "Character");
                composer.Compose(info, scene, root, currentProgress);
                ExportUtil.SaveAsType(scene.ToGltf2(), exportConfig.ExportType, job.OutDir, job.Name);
                glbPath = Path.Combine(job.OutDir, job.Name + ".glb");
                ok = File.Exists(glbPath);
                if (!ok) error = "Meddle didn't write the .glb";
            }
            catch (Exception e)
            {
                log.LogError(e, "Soulweaver export {JobId} failed", job.JobId);
                error = e.Message;
            }
            log.LogInformation("Soulweaver export {JobId} {Result} in {Seconds:0.0}s", job.JobId, ok ? "done" : "failed",
                               (DateTime.UtcNow - started).TotalSeconds);
            Finish(job.JobId, ok, glbPath, error);
        });
    }

    private void Finish(string jobId, bool ok, string glbPath, string error)
    {
        currentProgress = null;
        Interlocked.Exchange(ref busy, 0);
        framework.RunOnFrameworkThread(() => exportFinished.SendMessage(jobId, ok, glbPath, error));
    }

    public void Dispose()
    {
        pi.UiBuilder.Draw -= OnDraw;
        apiVersion.UnregisterFunc();
        exportDirectory.UnregisterFunc();
        exportProgress.UnregisterFunc();
        exportCharacter.UnregisterFunc();
        exportLayout.UnregisterFunc();
    }
}
