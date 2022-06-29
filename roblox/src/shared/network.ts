import { Networking } from "@flamework/networking";
import { PlayerRequest, VideoStateResponse } from "./types";

interface ServerEvents {
    PlayerRequest: (request: PlayerRequest) => void;
}

interface ClientEvents {
    VideoStateUpdate: (state: VideoStateResponse) => void;
}

interface ServerFunctions {}

interface ClientFunctions {}

export const GlobalEvents = Networking.createEvent<ServerEvents, ClientEvents>();
export const GlobalFunctions = Networking.createFunction<ServerFunctions, ClientFunctions>();
