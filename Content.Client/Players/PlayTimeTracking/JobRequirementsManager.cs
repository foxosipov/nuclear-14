﻿using System.Diagnostics.CodeAnalysis;
using System.Text; // Nuclear 14
using Content.Shared.CCVar;
using Content.Shared.Customization.Systems;
using Content.Shared.Players;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Content.Shared._NC.Roles; // Nuclear 14
using Robust.Client;
using Robust.Client.Player;
using Content.Client.Preferences; // Nuclear 14
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Preferences; // Nuclear 14
using Robust.Shared.Utility;

namespace Content.Client.Players.PlayTimeTracking;

public sealed partial class JobRequirementsManager : ISharedPlaytimeManager
{
    [Dependency] private readonly IBaseClient _client = default!;
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
	[Dependency] private readonly IClientPreferencesManager _clientPreferences = default!; // Nuclear 14

    private readonly Dictionary<string, TimeSpan> _roles = new();
    private readonly List<string> _roleBans = new();

    private ISawmill _sawmill = default!;

    public event Action? Updated;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("job_requirements");

        // Yeah the client manager handles role bans and playtime but the server ones are separate DEAL.
        _net.RegisterNetMessage<MsgRoleBans>(RxRoleBans);
        _net.RegisterNetMessage<MsgPlayTime>(RxPlayTime);
        _net.RegisterNetMessage<MsgWhitelist>(RxWhitelist);

        _client.RunLevelChanged += ClientOnRunLevelChanged;
    }

    private void ClientOnRunLevelChanged(object? sender, RunLevelChangedEventArgs e)
    {
        if (e.NewLevel == ClientRunLevel.Initialize)
        {
            // Reset on disconnect, just in case.
            _roles.Clear();
        }
    }

    private void RxRoleBans(MsgRoleBans message)
    {
        _sawmill.Debug($"Received roleban info containing {message.Bans.Count} entries.");

        if (_roleBans.Equals(message.Bans))
            return;

        _roleBans.Clear();
        _roleBans.AddRange(message.Bans);
        Updated?.Invoke();
    }

    private void RxPlayTime(MsgPlayTime message)
    {
        _roles.Clear();

        // NOTE: do not assign _roles = message.Trackers due to implicit data sharing in integration tests.
        foreach (var (tracker, time) in message.Trackers)
        {
            _roles[tracker] = time;
        }

        /*var sawmill = Logger.GetSawmill("play_time");
        foreach (var (tracker, time) in _roles)
        {
            sawmill.Info($"{tracker}: {time}");
        }*/
        Updated?.Invoke();
    }

    public TimeSpan FetchOverallPlaytime()
    {
        return _roles.TryGetValue("Overall", out var overallPlaytime) ? overallPlaytime : TimeSpan.Zero;
    }

    public Dictionary<string, TimeSpan> FetchPlaytimeByRoles()
    {
        var jobsToMap = _prototypes.EnumeratePrototypes<JobPrototype>();
        var ret = new Dictionary<string, TimeSpan>();

        foreach (var job in jobsToMap)
            if (_roles.TryGetValue(job.PlayTimeTracker, out var locJobName))
                ret.Add(job.Name, locJobName);

        return ret;
    }


    public Dictionary<string, TimeSpan> GetPlayTimes()
    {
        var dict = FetchPlaytimeByRoles();
        dict.Add(PlayTimeTrackingShared.TrackerOverall, FetchOverallPlaytime());
        return dict;
    }

    public Dictionary<string, TimeSpan> GetRawPlayTimeTrackers()
    {
        return _roles;
    }
}
