import React, { useCallback, useEffect, useState } from "react";
import { Spinner } from "@patternfly/react-core";
import { apiGetJson } from "../api.js";
import { ErrorBanner } from "./ErrorBanner.jsx";
import { BucketsPanel } from "./BucketsPanel.jsx";
import { KeysPanel } from "./KeysPanel.jsx";

// Buckets and keys are loaded together and refreshed together -- granting/revoking a permission,
// or toggling a key's create-bucket flag, changes what each panel needs to show about the other
// (e.g. BucketsPanel's grants modal lists every key; KeysPanel could later show bucket counts), so
// keeping one shared refresh here avoids the two panels drifting out of sync the way DiskWeaver's
// own App.jsx explicitly avoided for pools/disks (see its "collapsing these into one" comment) --
// the difference here is buckets/keys are two views of the *same* underlying grant relationships,
// where pools/disks are genuinely independent, so one shared load is the right call in this case.
export function ObjectStorage() {
    const [buckets, setBuckets] = useState([]);
    const [keys, setKeys] = useState([]);
    // Only gates the *first* load's spinner -- every later reload (triggered by a child panel's
    // onChanged, e.g. after creating a key/bucket or toggling a grant) must NOT flip this back to
    // true. Doing so used to swap the whole <BucketsPanel>/<KeysPanel> tree out for a bare
    // <Spinner>, unmounting both -- which silently destroyed KeysPanel's own local state along with
    // it, including the freshly-created key's one-time secret modal (set via setCreatedKey right
    // before onChanged() fired in the very same .then()). The modal was created and torn down in
    // the same render batch, so the secret was never actually shown. See KeysPanel's createdKey.
    const [initialLoading, setInitialLoading] = useState(true);
    const [error, setError] = useState(null);

    const reload = useCallback(() => {
        setError(null);
        return Promise.all([apiGetJson("/garage/buckets"), apiGetJson("/garage/keys")])
            .then(([bucketsResult, keysResult]) => {
                setBuckets(bucketsResult);
                setKeys(keysResult);
            })
            .catch(err => setError(err.message || String(err)))
            .finally(() => setInitialLoading(false));
    }, []);

    useEffect(() => { reload(); }, [reload]);

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--pf-t--global--spacer--lg)" }}>
            <ErrorBanner message={error} />
            {initialLoading
                ? <Spinner size="md" />
                : (
                    <>
                        <BucketsPanel buckets={buckets} keys={keys} onChanged={reload} />
                        <KeysPanel keys={keys} onChanged={reload} />
                    </>
                )}
        </div>
    );
}
