namespace DuoMix;
static class RoutingChecks
{
    public static void Run()
    {
        var i = new DeviceChoice("in", "CABLE Input", "VB-Audio Virtual Cable"); var o = new DeviceChoice("out", "CABLE Output", "VB-Audio Virtual Cable"); var p = new Preferences();
        if (CableRouting.Resolve(p, [i], [o])?.Input != i) throw new Exception("Automatic cable pairing failed");
        if (CableRouting.Resolve(p, [i, i with { Id = "in2" }], [o]) != null) throw new Exception("Ambiguous cable must not be guessed");
        if (CableRouting.Resolve(p, [new("speaker", "Speakers", "Hardware")], [new("mic", "Microphone", "Hardware")]) != null) throw new Exception("Physical hardware must not auto-pair");
        p.CableRenderId = i.Id; p.CableCaptureId = o.Id;
        if (CableRouting.Resolve(p, [i, i with { Id = "in2" }], [o])?.Input != i) throw new Exception("Manual route not honored");
        if (CableRouting.Resolve(p, [i], [o with { Id = "other" }]) != null) throw new Exception("Missing saved endpoint must not silently fall back");
        var saved = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(p))!;
        if (saved.CableRenderId != i.Id || saved.CableCaptureId != o.Id) throw new Exception("Route preference persistence failed");
    }
}
