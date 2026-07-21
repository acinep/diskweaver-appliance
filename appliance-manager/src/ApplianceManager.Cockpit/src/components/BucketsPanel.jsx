import React, { useState } from "react";
import {
    Button, Modal, ModalVariant, ModalHeader, ModalBody, ModalFooter, TextInput, Checkbox, Title,
} from "@patternfly/react-core";
import { TrashIcon, KeyIcon } from "@patternfly/react-icons";
import { Table, Thead, Tbody, Tr, Th, Td } from "@patternfly/react-table";
import { apiPostJson, apiDeleteJson } from "../api.js";
import { ErrorBanner } from "./ErrorBanner.jsx";

function formatBytes(bytes) {
    if (!bytes) return "0 B";
    const units = ["B", "KB", "MB", "GB", "TB", "PB"];
    const exponent = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
    return `${(bytes / 1024 ** exponent).toFixed(exponent === 0 ? 0 : 2)} ${units[exponent]}`;
}

export function BucketsPanel({ buckets, keys, onChanged }) {
    const [creating, setCreating] = useState(false);
    const [newName, setNewName] = useState("");
    const [grantsBucket, setGrantsBucket] = useState(null);
    const [error, setError] = useState(null);
    const [busy, setBusy] = useState(false);

    function createBucket() {
        setBusy(true);
        setError(null);
        apiPostJson("/garage/buckets", { name: newName.trim() })
            .then(() => {
                setCreating(false);
                setNewName("");
                onChanged();
            })
            .catch(err => setError(err.message || String(err)))
            .finally(() => setBusy(false));
    }

    function deleteBucket(name) {
        setBusy(true);
        setError(null);
        apiDeleteJson(`/garage/buckets/${encodeURIComponent(name)}`)
            .then(onChanged)
            .catch(err => setError(err.message || String(err)))
            .finally(() => setBusy(false));
    }

    return (
        <div>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", marginBottom: "0.5rem" }}>
                <Title headingLevel="h2">Buckets</Title>
                <Button variant="primary" onClick={() => setCreating(true)}>Create bucket</Button>
            </div>
            <ErrorBanner message={error} />
            <div className="table-scroll">
                <Table variant="compact">
                    <Thead>
                        <Tr>
                            <Th>Name</Th>
                            <Th>Size</Th>
                            <Th>Objects</Th>
                            <Th>Created</Th>
                            <Th>Keys granted</Th>
                            <Th></Th>
                        </Tr>
                    </Thead>
                    <Tbody>
                        {buckets.map(bucket => (
                            <Tr key={bucket.id}>
                                <Td dataLabel="Name">{bucket.name ?? <em>(no global alias)</em>}</Td>
                                <Td dataLabel="Size">{formatBytes(bucket.sizeBytes)}</Td>
                                <Td dataLabel="Objects">{bucket.objectCount}</Td>
                                <Td dataLabel="Created">{bucket.createdAt}</Td>
                                <Td dataLabel="Keys granted">{bucket.keys.length}</Td>
                                <Td dataLabel="Actions">
                                    <Button variant="secondary" icon={<KeyIcon />} onClick={() => setGrantsBucket(bucket)}>
                                        Grants
                                    </Button>{" "}
                                    <Button variant="danger" icon={<TrashIcon />} onClick={() => deleteBucket(bucket.name)} isDisabled={busy}>
                                        Delete
                                    </Button>
                                </Td>
                            </Tr>
                        ))}
                        {buckets.length === 0 && (
                            <Tr><Td colSpan={6}>No buckets yet.</Td></Tr>
                        )}
                    </Tbody>
                </Table>
            </div>

            {creating && (
                <Modal variant={ModalVariant.small} isOpen onClose={() => setCreating(false)}>
                    <ModalHeader title="Create bucket" />
                    <ModalBody>
                        <ErrorBanner message={error} />
                        <TextInput
                            aria-label="Bucket name"
                            placeholder="my-bucket"
                            value={newName}
                            onChange={(_event, value) => setNewName(value)}
                        />
                    </ModalBody>
                    <ModalFooter>
                        <Button variant="primary" onClick={createBucket} isDisabled={busy || !newName.trim()}>Create</Button>
                        <Button variant="link" onClick={() => setCreating(false)}>Cancel</Button>
                    </ModalFooter>
                </Modal>
            )}

            {grantsBucket && (
                <BucketGrantsModal
                    bucket={grantsBucket}
                    keys={keys}
                    onClose={() => setGrantsBucket(null)}
                    onChanged={() => { onChanged(); setGrantsBucket(null); }}
                />
            )}
        </div>
    );
}

// Every key's permission on this one bucket, editable in place -- rather than a separate "add a
// grant" flow, every known key always has a row here (unchecked == no grant yet) so revoking is
// just unchecking all three boxes, symmetric with granting.
function BucketGrantsModal({ bucket, keys, onClose, onChanged }) {
    const [error, setError] = useState(null);
    const [busy, setBusy] = useState(false);
    const grantByKeyId = Object.fromEntries(bucket.keys.map(g => [g.keyId, g]));

    function setGrant(keyId, read, write, owner) {
        setBusy(true);
        setError(null);
        const request = read || write || owner
            ? apiPostJson(`/garage/buckets/${encodeURIComponent(bucket.name)}/grants`, { keyId, read, write, owner })
            : apiDeleteJson(`/garage/buckets/${encodeURIComponent(bucket.name)}/grants/${encodeURIComponent(keyId)}`);
        request.then(onChanged).catch(err => setError(err.message || String(err))).finally(() => setBusy(false));
    }

    return (
        <Modal variant={ModalVariant.medium} isOpen onClose={onClose}>
            <ModalHeader title={`Grants for "${bucket.name}"`} />
            <ModalBody>
                <ErrorBanner message={error} />
                <div className="table-scroll">
                    <Table variant="compact">
                        <Thead>
                            <Tr>
                                <Th>Key</Th>
                                <Th>Read</Th>
                                <Th>Write</Th>
                                <Th>Owner</Th>
                            </Tr>
                        </Thead>
                        <Tbody>
                            {keys.map(key => {
                                const grant = grantByKeyId[key.id];
                                const read = grant?.read ?? false;
                                const write = grant?.write ?? false;
                                const owner = grant?.owner ?? false;
                                return (
                                    <Tr key={key.id}>
                                        <Td dataLabel="Key">{key.name}</Td>
                                        <Td dataLabel="Read">
                                            <Checkbox
                                                id={`read-${key.id}`}
                                                isChecked={read}
                                                isDisabled={busy}
                                                onChange={(_e, checked) => setGrant(key.id, checked, write, owner)}
                                            />
                                        </Td>
                                        <Td dataLabel="Write">
                                            <Checkbox
                                                id={`write-${key.id}`}
                                                isChecked={write}
                                                isDisabled={busy}
                                                onChange={(_e, checked) => setGrant(key.id, read, checked, owner)}
                                            />
                                        </Td>
                                        <Td dataLabel="Owner">
                                            <Checkbox
                                                id={`owner-${key.id}`}
                                                isChecked={owner}
                                                isDisabled={busy}
                                                onChange={(_e, checked) => setGrant(key.id, read, write, checked)}
                                            />
                                        </Td>
                                    </Tr>
                                );
                            })}
                            {keys.length === 0 && (
                                <Tr><Td colSpan={4}>No access keys yet -- create one in the Access keys panel first.</Td></Tr>
                            )}
                        </Tbody>
                    </Table>
                </div>
            </ModalBody>
            <ModalFooter>
                <Button variant="primary" onClick={onClose}>Done</Button>
            </ModalFooter>
        </Modal>
    );
}
