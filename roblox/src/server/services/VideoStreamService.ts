import { Service, OnInit, OnTick } from "@flamework/core";
import { HttpRequest } from "@rbxts/http-queue";
import { HttpService, Players, RunService } from "@rbxts/services";
import { Events } from "server/network";
import { PlayerRequest, PlayerRequestAction, VideoStateResponse } from "shared/types";

const localEndpoint = "http://localhost:8000";
const remoteEndpoint = "";

@Service({})
export class VideoStreamService implements OnInit, OnTick {
    private endpoint = RunService.IsStudio() ? localEndpoint : remoteEndpoint;
    private rate = 8;
    private requestsDue = 0;

    onInit() {
        Events.PlayerRequest.connect((plr, playerRequest) => {
            new HttpRequest(this.endpoint + "/playeraction", "GET", undefined, {
                action: playerRequest.Action,
                time: playerRequest.Time as unknown as number,
                path: playerRequest.VideoPath as unknown as string,
            })
                .Send()
                .then((response) => {
                    print(response.Body);
                })
                .catch((err) => {
                    warn(err as unknown);
                });
        });

        // chat events
        Players.PlayerAdded.Connect((plr) => {
            plr.Chatted.Connect((msg) => {
                if (msg.sub(1, 2) !== "$ ") return;
                const split = msg.split(" ");
                if (split.size() < 2) return;

                const action = split[1];
                const extraData = split[2];

                const playerRequest: PlayerRequest = {
                    Action: action as PlayerRequestAction,
                    VideoPath: action === "Load" ? extraData : undefined,
                    Time: action === "Seek" ? tonumber(extraData) : undefined,
                };
                Events.PlayerRequest.predict(plr, playerRequest);
            });
        });
    }
    onTick(dt: number): void {
        this.requestsDue += dt * this.rate;
        if (this.requestsDue >= 1) {
            this.requestsDue -= 1;

            new HttpRequest(this.endpoint + "/videostate", "GET")
                .Send()
                .then((response) => {
                    const state = HttpService.JSONDecode(response.Body) as VideoStateResponse;
                    Events.VideoStateUpdate.broadcast(state);
                })
                .catch((err) => {
                    warn(err as unknown);
                });
        }
    }
}
