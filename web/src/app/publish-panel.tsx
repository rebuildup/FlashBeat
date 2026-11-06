import { Portal } from "@ark-ui/react/portal";
import { Button, Dialog, Field, Input } from "@/components/ui";
import { useState } from "react";
import { css } from "styled-system/css";
import { StateBlock, panelBody, panelFrame, panelHeader, panelSubtitle, panelTitle } from "./panel";

const form = css({
  display: "grid",
  gap: "4",
  maxWidth: "28rem",
});

const accent = css({ colorPalette: "teal" });

/**
 * Publish form shell.
 *
 * The staff token is typed by the event operator at publish time and is never
 * stored. The submit action stays disabled until the publish endpoint (#16)
 * is deployed, so nobody can accidentally publish against a dead API.
 */
export function PublishPanel() {
  const [songId, setSongId] = useState("");
  const [token, setToken] = useState("");

  return (
    <section className={panelFrame}>
      <header className={panelHeader}>
        <div>
          <h2 className={panelTitle}>Publish</h2>
          <p className={panelSubtitle}>One atomic PUT of metadata and chart</p>
        </div>
      </header>

      <div className={panelBody}>
        <StateBlock
          title="Publishing is not connected yet"
          description="The publish endpoint lands with issue #16. The form below shows the shape of the request the editor will send."
        />

        <div className={form}>
          <Field.Root>
            <Field.Label>Song id</Field.Label>
            <Input
              placeholder="who"
              value={songId}
              onChange={(event) => setSongId(event.currentTarget.value)}
            />
            <Field.HelperText>
              Lowercase letters, digits and dashes. 3 to 64 characters.
            </Field.HelperText>
          </Field.Root>

          <Dialog.Root>
            <Dialog.Trigger asChild>
              <Button className={accent} size="md" disabled={songId.trim().length === 0}>
                Review publish
              </Button>
            </Dialog.Trigger>
            <Portal>
              <Dialog.Backdrop />
              <Dialog.Positioner>
                <Dialog.Content>
                  <Dialog.Header>
                    <Dialog.Title>Publish “{songId}”</Dialog.Title>
                    <Dialog.Description>
                      The chart snapshot, metadata and catalog version are written in one request.
                    </Dialog.Description>
                  </Dialog.Header>

                  <Dialog.Body>
                    <Field.Root>
                      <Field.Label>Staff token</Field.Label>
                      <Input
                        type="password"
                        placeholder="FLASHBEAT_ADMIN_TOKEN"
                        value={token}
                        onChange={(event) => setToken(event.currentTarget.value)}
                      />
                      <Field.HelperText>
                        Held in the browser only for this request. It is never written to disk or to
                        the bundle.
                      </Field.HelperText>
                    </Field.Root>
                  </Dialog.Body>

                  <Dialog.Footer>
                    <Dialog.ActionTrigger asChild>
                      <Button variant="outline" size="md">
                        Cancel
                      </Button>
                    </Dialog.ActionTrigger>
                    <Button className={accent} size="md" disabled>
                      Publish (waiting for #16)
                    </Button>
                  </Dialog.Footer>
                </Dialog.Content>
              </Dialog.Positioner>
            </Portal>
          </Dialog.Root>
        </div>
      </div>
    </section>
  );
}
