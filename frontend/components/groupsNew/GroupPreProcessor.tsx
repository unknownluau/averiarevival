import {getInfo, GroupWithShout} from "../../services/groups-typed";
import GroupsPageStore from "./stores/GroupsPageStore";
import {useEffect, useRef} from "react";
import GroupsNew from "./index";
import AuthenticationStore from "../../stores/authentication";
import {useRouter} from "next/router";
import {itemNameToEncodedName} from "../../services/catalog";
import UserGroupsStore from "./stores/UserGroupsStore";

// load all of the data required for the group page here
const GroupPreProcessor = ({group, loadDefault}: {group?:GroupWithShout|null,loadDefault?:boolean}) => {
    const store = GroupsPageStore.useContainer();
    const ustore = UserGroupsStore.useContainer();
    const auth = AuthenticationStore.useContainer();
    const router = useRouter();
    const requestIdRef = useRef(0);

    useEffect(() => {
        let cancelled = false;
        const requestId = ++requestIdRef.current;
        const isCurrent = () => !cancelled && requestIdRef.current === requestId;

        const loadGroup = async () => {
            store.setGroupNotFound(false);

            let nextGroup = group;
            if (nextGroup === undefined) {
                store.setLoading(true);
                void ustore.fetchData();
                return;
            }

            if (!loadDefault && nextGroup === null) {
                store.setGroup(null);
                store.setGroupNotFound(true);
                store.setLoading(false);
                void ustore.fetchData();
                return;
            }

            if (nextGroup !== null) {
                let expectedUrl = `/groups/${nextGroup.id}/${itemNameToEncodedName(nextGroup.name)}`;
                if (typeof window !== 'undefined' && window.location.pathname !== expectedUrl) {
                    void router.replace(expectedUrl);
                }
            }

            if (loadDefault && !nextGroup) {
                if (auth.isPending) return;
                let ug = await ustore.fetchData();
                if (!isCurrent()) return;
                if (!ug || ug.length <= 0) {
                    void router.replace("/search/groups");
                    return;
                }
                let defaultGroup = ug.find(g => g.isPrimary);
                if (!defaultGroup) defaultGroup = ug[0];
                if (!defaultGroup) {
                    void router.replace("/search/groups");
                    return;
                }

                try {
                    nextGroup = await getInfo({groupId: defaultGroup.group.id});
                } catch (e) {
                    console.error(e);
                    if (isCurrent()) {
                        store.setGroup(null);
                        store.setGroupNotFound(true);
                        store.setLoading(false);
                    }
                    return;
                }
            } else {
                void ustore.fetchData();
            }

            if (nextGroup && isCurrent()) {
                void store.fetchData(nextGroup, true);
            }
        }

        void loadGroup();

        return () => {
            cancelled = true;
        };
    }, [auth.isPending, group, loadDefault]);

    return <GroupsNew />
}

export default GroupPreProcessor;
