import { Controller, OnInit, OnRender } from "@flamework/core";
import { Workspace } from "@rbxts/services";
import { Events } from "client/network";
import { FrameData } from "shared/types";

const PIXEL_SIZE = 0.05;
const SCREEN_ORIGIN = new Vector3(0, 10, -10);
const VIDEO_DELAY = 1000; // in milliseconds

const CHARSET = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

@Controller({})
export class VideoStreamController implements OnInit, OnRender {
    private parts: Part[] = [];
    private partContainer?: Model;
    private currentSizeX = 0;
    private currentSizeY = 0;

    private time = 0;
    private frames: Map<number, FrameData> = new Map();
    private lastFrameTime = 0;

    createPartScreen(sizeX: number, sizeY: number) {
        if (!this.partContainer) return;

        this.partContainer.Parent = undefined;
        this.partContainer.ClearAllChildren();

        this.parts = [];
        for (let i = 0; i < sizeX * sizeY; i++) {
            const part = new Instance("Part");
            part.Name = "Part" + i;
            part.Size = new Vector3(PIXEL_SIZE, PIXEL_SIZE, PIXEL_SIZE);
            part.Anchored = true;
            part.Color = BrickColor.random().Color;
            part.CanCollide = false;
            part.CanQuery = false;
            part.CFrame = new CFrame(
                SCREEN_ORIGIN.X + (i % sizeX) * PIXEL_SIZE,
                SCREEN_ORIGIN.Y - math.floor(i / sizeX) * PIXEL_SIZE,
                SCREEN_ORIGIN.Z,
            );
            part.Parent = this.partContainer;
            this.parts[i] = part;
        }
        this.partContainer.Parent = Workspace;
    }

    onInit() {
        // create part screen
        const container = new Instance("Model");
        container.Name = "PartScreen";
        this.partContainer = container;

        container.Parent = Workspace;

        Events.VideoStateUpdate.connect((state) => {
            this.time = state.UnixTime;
            state.FrameData.forEach((frame) => {
                this.frames.set(frame.UnixTime, frame);
            });
        });
    }

    update(frame: FrameData) {
        if (frame.UnixTime === this.lastFrameTime) return;
        this.lastFrameTime = frame.UnixTime;

        const sizeX = frame.SizeX;
        const sizeY = frame.SizeY;
        if (sizeX !== this.currentSizeX || sizeY !== this.currentSizeY) {
            this.currentSizeX = sizeX;
            this.currentSizeY = sizeY;
            this.createPartScreen(sizeX, sizeY);
        }

        this.parts.forEach((part, i) => {
            const colorString = frame.Colors.sub(i * 3 + 1, i * 3 + 4);
            const R = ((CHARSET.find(colorString.sub(1, 1))[0] as number) - 1) * 4;
            const G = ((CHARSET.find(colorString.sub(2, 2))[0] as number) - 1) * 4;
            const B = ((CHARSET.find(colorString.sub(3, 3))[0] as number) - 1) * 4;
            if (G > 255) print(G);
            const color = Color3.fromRGB(R, G, B);
            if (part.Color !== color) {
                part.Color = color;
            }
        });
    }

    onRender(dt: number): void {
        this.time += dt * 1000;
        const currentTime = this.time - VIDEO_DELAY;

        if (this.frames.size() === 0) return;

        // find closest frame
        let closest: FrameData | undefined;
        this.frames.forEach((frame, time) => {
            if (time > currentTime) {
                if (!closest || time - currentTime < closest.UnixTime - currentTime) {
                    closest = frame;
                }
            } else {
                this.frames.delete(time);
            }
        });
        if (closest) {
            this.update(closest);
        }
    }
}
