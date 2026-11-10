import type {
  FullConfig,
  FullResult,
  Reporter,
  Suite,
  TestCase,
  TestResult,
} from "@playwright/test/reporter";

/**
 * Quality-gate reporter of the browser E2E (runs next to the `list`
 * reporter, prints nothing per test).
 *
 * A green Playwright run must mean "every collected test ran in a real
 * browser". Two degenerate outcomes must not be able to pass:
 *
 * - zero collected tests (a broken `testMatch`, a config typo);
 * - every test skipped (a prerequisite guard that silently bails — a missing
 *   browser, an unreachable server).
 *
 * `onEnd` returns `{ status: "failed" }` for both, which forces the run's
 * exit code to non-zero.
 */
class GateReporter implements Reporter {
  private total = 0;
  private skipped = 0;

  onBegin(_config: FullConfig, suite: Suite): void {
    this.total = suite.allTests().length;
    this.skipped = 0;
  }

  onTestEnd(_test: TestCase, result: TestResult): void {
    if (result.status === "skipped") {
      this.skipped += 1;
    }
  }

  async onEnd(_result: FullResult): Promise<{ status: "failed" } | void> {
    if (this.total === 0) {
      console.error(
        `[gate] FAIL: 0 tests were collected by tests/e2e/playwright.config.ts. ` +
          `The browser E2E proved nothing; refusing to report success.`,
      );
      return { status: "failed" };
    }
    if (this.skipped === this.total) {
      console.error(
        `[gate] FAIL: all ${this.total} collected tests were skipped. ` +
          `Prerequisites (browser, dev server, seeded D1) must fail loudly, not skip.`,
      );
      return { status: "failed" };
    }
    if (this.skipped > 0) {
      console.error(`[gate] warning: ${this.skipped}/${this.total} tests were skipped.`);
    }
    return undefined;
  }
}

export default GateReporter;
