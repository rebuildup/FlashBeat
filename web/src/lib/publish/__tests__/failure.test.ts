import { describe, expect, it } from "vitest";
import { describeFailure } from "@/lib/publish/failure";

describe("describeFailure", () => {
  it("puts a rejected token on the token field", () => {
    const view = describeFailure({
      code: "unauthorized",
      status: 401,
      message: "The staff token was rejected. Check the token of the operator.",
      details: ["Incorrect staff token"],
    });

    expect(view.tokenError).toBe("The staff token was rejected. Check the token of the operator.");
    expect(view.requestError).toBeUndefined();
    expect(view.details).toEqual(["Incorrect staff token"]);
  });

  it("puts an invalid song on the request, not on the token", () => {
    const view = describeFailure({
      code: "invalid",
      status: 422,
      message: "The API rejected the song. Fix the listed problems and publish again.",
      details: ["level must be between 1 and 99"],
    });

    expect(view.tokenError).toBeUndefined();
    expect(view.requestError).toBe(
      "The API rejected the song. Fix the listed problems and publish again.",
    );
    expect(view.details).toEqual(["level must be between 1 and 99"]);
  });

  it("treats every other failure as a request problem", () => {
    for (const code of ["not-found", "server", "network", "malformed"] as const) {
      const view = describeFailure({ code, message: "something went wrong" });

      expect(view.tokenError).toBeUndefined();
      expect(view.requestError).toBe("something went wrong");
      expect(view.details).toEqual([]);
    }
  });

  it("survives a failure without details", () => {
    const view = describeFailure({ code: "network", message: "The request did not arrive." });

    expect(view.details).toEqual([]);
    expect(view.requestError).toBe("The request did not arrive.");
  });
});
