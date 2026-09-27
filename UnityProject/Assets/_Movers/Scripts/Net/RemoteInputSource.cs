namespace Movers
{
    // The host's source for P2 online: the client's input frames, played out on the host's
    // timeline (NETCODE_SLICE 9.1, 9.2). Never reports CrewButton.Pause. Stub from CORE: the
    // PLAYERS track fills Poll.
    public sealed class RemoteInputSource : ICrewInputSource
    {
        public string Label => "Remote";
        public bool IsGamepad { get; set; }

        public void Poll(ref CrewInputFrame frame, float dt)
        {
            frame = default;
        }
    }
}
