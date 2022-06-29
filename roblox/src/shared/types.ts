export interface FrameData {
    UnixTime: number;
    Colors: string;
    SizeX: number;
    SizeY: number;
}

export interface VideoStateResponse {
    FrameData: FrameData[];
    VideoPath: string;
    CurrentVideoTime: number;
    TotalVideoLength: number;
    CurrentCaption: string;
    UnixTime: number;
}

export type DirectoryResponse = string[];

export type PlayerRequestAction = "Resume" | "Pause" | "Stop" | "Seek" | "Load";

export interface PlayerRequest {
    Action: PlayerRequestAction;
    Time?: number;
    VideoPath?: string;
}
