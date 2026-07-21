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
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    const reload = useCallback(() => {
        setLoading(true);
        setError(null);
        return Promise.all([apiGetJson("/garage/buckets"), apiGetJson("/garage/keys")])
            .then(([bucketsResult, keysResult]) => {
                setBuckets(bucketsResult);
                setKeys(keysResult);
            })
            .catch(err => setError(err.message || String(err)))
            .finally(() => setLoading(false));
    }, []);

    useEffect(() => { reload(); }, [reload]);

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--pf-t--global--spacer--lg)" }}>
            <ErrorBanner message={error} />
            {loading
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
