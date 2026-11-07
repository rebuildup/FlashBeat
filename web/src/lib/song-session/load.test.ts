import { describe, it, expect } from "vitest";
import { loadSongSession } from "./load";
const chart = {name:"B",maxBlock:8,BPM:120,offset:0,notes:[{type:1,num:0,block:0,LPB:4}]};
describe("published song session", () => {
  it("loads exact selected version and song video, never a sample", async () => {
    const urls: string[] = [];
    const request = (async (url: string) => { urls.push(url); return Response.json(url.endsWith("catalog") ? {songs:[{id:"b",title:"B",youtubeId:"video-b"}]} : {songId:"b",chartVersion:2,chart}); }) as typeof fetch;
    const result = await loadSongSession("b","2",request);
    expect(urls).toEqual(["/api/v1/catalog","/api/v1/charts/b?version=2"]);
    expect(result.song.youtubeId).toBe("video-b"); expect(result.chart).toEqual(chart);
  });
  it("rejects malformed version before requesting", async () => {
    await expect(loadSongSession("b","2x")).rejects.toThrow("Invalid");
  });
  it("does not silently substitute an empty chart for missing version", async () => {
    const request = (async (url: string) => url.endsWith("catalog") ? Response.json({songs:[{id:"b"}]}) : new Response(null,{status:404})) as typeof fetch;
    await expect(loadSongSession("b","2",request)).rejects.toThrow("404");
  });
  it("rejects a snapshot for another identity", async () => {
    const request = (async (url: string) => Response.json(url.endsWith("catalog") ? {songs:[{id:"b"}]} : {songId:"a",chartVersion:2,chart})) as typeof fetch;
    await expect(loadSongSession("b","2",request)).rejects.toThrow("identity mismatch");
  });
});
