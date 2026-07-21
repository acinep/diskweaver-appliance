import React, { useState } from "react";
import {
    Button, Modal, ModalVariant, ModalHeader, ModalBody, ModalFooter, TextInput, Switch, Title, ClipboardCopy,
} from "@patternfly/react-core";
import { TrashIcon } from "@patternfly/react-icons";
import { Table, Thead, Tbody, Tr, Th, Td } from "@patternfly/react-table";
import { apiPostJson, apiDeleteJson } from "../api.js";
import { ErrorBanner } from "./ErrorBanner.jsx";

export function KeysPanel({ keys, onChanged }) {
    const [creating, setCreating] = useState(false);
    const [newName, setNewName] = useState("");
    // Holds the freshly-created key's one-time secret -- see GarageKey.SecretKey's doc comment on
    // the daemon side: this is the only response that will ever carry a real (non-redacted) one,
    // so it has to be shown here, right now, or it's gone for good.
    const [createdKey, setCreatedKey] = useState(null);
    const [error, setError] = useState(null);
    const [busy, setBusy] = useState(false);

    function createKey() {
        setBusy(true);
        setError(null);
        apiPostJson("/garage/keys", { name: newName.trim() })
            .then(key => {
                setCreating(false);
                setNewName("");
                setCreatedKey(key);
                onChanged();
            })
            .catch(err => setError(err.message || String(err)))
            .finally(() => setBusy(false));
    }

    function deleteKey(id) {
        setBusy(true);
        setError(null);
        apiDeleteJson(`/garage/keys/${encodeURIComponent(id)}`)
            .then(onChanged)
            .catch(err => setError(err.message || String(err)))
            .finally(() => setBusy(false));
    }

    function setCanCreateBuckets(id, allow) {
        setBusy(true);
        setError(null);
        apiPostJson(`/garage/keys/${encodeURIComponent(id)}/can-create-buckets`, { allow })
            .then(onChanged)
            .catch(err => setError(err.message || String(err)))
            .finally(() => setBusy(false));
    }

    return (
        <div>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", marginBottom: "0.5rem" }}>
                <Title headingLevel="h2">Access keys</Title>
                <Button variant="primary" onClick={() => setCreating(true)}>Create key</Button>
            </div>
            <ErrorBanner message={error} />
            <div className="table-scroll">
                <Table variant="compact">
                    <Thead>
                        <Tr>
                            <Th>Name</Th>
                            <Th>ID</Th>
                            <Th>Created</Th>
                            <Th>Can create buckets</Th>
                            <Th>Buckets granted</Th>
                            <Th></Th>
                        </Tr>
                    </Thead>
                    <Tbody>
                        {keys.map(key => (
                            <Tr key={key.id}>
                                <Td dataLabel="Name">{key.name}</Td>
                                <Td dataLabel="ID"><span title={key.id}>{key.id}</span></Td>
                                <Td dataLabel="Created">{key.createdAt}</Td>
                                <Td dataLabel="Can create buckets">
                                    <Switch
                                        id={`can-create-${key.id}`}
                                        isChecked={key.canCreateBuckets}
                                        isDisabled={busy}
                                        onChange={(_e, checked) => setCanCreateBuckets(key.id, checked)}
                                    />
                                </Td>
                                <Td dataLabel="Buckets granted">{key.buckets.length}</Td>
                                <Td dataLabel="Actions">
                                    <Button variant="danger" icon={<TrashIcon />} onClick={() => deleteKey(key.id)} isDisabled={busy}>
                                        Delete
                                    </Button>
                                </Td>
                            </Tr>
                        ))}
                        {keys.length === 0 && (
                            <Tr><Td colSpan={6}>No access keys yet.</Td></Tr>
                        )}
                    </Tbody>
                </Table>
            </div>

            {creating && (
                <Modal variant={ModalVariant.small} isOpen onClose={() => setCreating(false)}>
                    <ModalHeader title="Create access key" />
                    <ModalBody>
                        <ErrorBanner message={error} />
                        <TextInput
                            aria-label="Key name"
                            placeholder="my-key"
                            value={newName}
                            onChange={(_event, value) => setNewName(value)}
                        />
                    </ModalBody>
                    <ModalFooter>
                        <Button variant="primary" onClick={createKey} isDisabled={busy || !newName.trim()}>Create</Button>
                        <Button variant="link" onClick={() => setCreating(false)}>Cancel</Button>
                    </ModalFooter>
                </Modal>
            )}

            {createdKey && (
                <Modal variant={ModalVariant.small} isOpen onClose={() => setCreatedKey(null)}>
                    <ModalHeader title={`Key "${createdKey.name}" created`} />
                    <ModalBody>
                        <p>
                            This secret is shown <strong>once</strong> -- copy it now. Garage never displays it
                            again; losing it means creating a new key.
                        </p>
                        <p><strong>Key ID:</strong> {createdKey.id}</p>
                        <p><strong>Secret key:</strong></p>
                        <ClipboardCopy isReadOnly hoverTip="Copy" clickTip="Copied">{createdKey.secretKey}</ClipboardCopy>
                    </ModalBody>
                    <ModalFooter>
                        <Button variant="primary" onClick={() => setCreatedKey(null)}>I've saved it</Button>
                    </ModalFooter>
                </Modal>
            )}
        </div>
    );
}
