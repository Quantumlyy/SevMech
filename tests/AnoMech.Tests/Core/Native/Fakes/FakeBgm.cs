using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Tests;

internal sealed class FakeBgm(SimTimeline timeline) : IBgm
{
    public void Play(ushort bgmId, float secondsIn = 0f)
        => timeline.Add("bgm", id: bgmId.ToString(), name: "BGM", detail: $"seconds_in={SimTimeline.F(secondsIn)}");
    public void Sync(float secondsIn) { }
    public void Switch(ushort bgmId) => timeline.Add("bgm", id: bgmId.ToString(), name: "BGM switch");
    public void Silence() => timeline.Add("bgm", id: "0", name: "BGM silence");
    public void Reset() { }
    public void Tick(float deltaSeconds) { }
    public void LogPosition(string label) { }

    public void Dispose() { }
}
