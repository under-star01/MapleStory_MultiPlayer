using Mirror;

public struct LoadMapMessage : NetworkMessage
{
    public MapId MapId;
}

public struct MapLoadedMessage : NetworkMessage
{
    public MapId MapId;
}

public struct LoadTransitionMapMessage : NetworkMessage
{
    public MapId MapId;
}

public struct TransitionMapLoadedMessage : NetworkMessage
{
    public MapId MapId;
}

public struct CompleteMapTransitionMessage : NetworkMessage
{
    public MapId PreviousMapId;
    public MapId CurrentMapId;
}