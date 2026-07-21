import React, { useState } from "react";
import { Page, PageSection, Title, Tabs, Tab, TabTitleText } from "@patternfly/react-core";
import { ObjectStorage } from "./ObjectStorage.jsx";

// One tab per component today (just Object Storage/Garage) -- Samba, NFS, and rsync each get
// their own Tab here as they're added, same shape DiskWeaver's own App.jsx uses for Pools/Disk
// inventory. sidebar={null} is required, not cosmetic -- see DiskWeaver.Cockpit's App.jsx comment
// for why Page only drops its reserved sidebar column when the prop is null, not merely absent.
export function App() {
    const [activeTabKey, setActiveTabKey] = useState("object-storage");

    return (
        <Page sidebar={null}>
            <PageSection>
                <Title headingLevel="h1" style={{ marginBottom: "1rem" }}>Appliance Manager</Title>
                <Tabs activeKey={activeTabKey} onSelect={(_, key) => setActiveTabKey(key)}>
                    <Tab eventKey="object-storage" title={<TabTitleText>Object storage</TabTitleText>}>
                        <PageSection>
                            <ObjectStorage />
                        </PageSection>
                    </Tab>
                </Tabs>
            </PageSection>
        </Page>
    );
}
