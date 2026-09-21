import Head from 'next/head';
import Theme2016 from '../../../components/theme2016';
import GroupsPageStore from "../../../components/groupsNew/stores/GroupsPageStore";
import GroupPreProcessor from "../../../components/groupsNew/GroupPreProcessor";
import {getInfo, GroupWithShout} from "../../../services/groups-typed";
import React, {useEffect, useState} from "react";
import MyGroupsStore from "../../../components/myGroups/stores/myGroupsStore";
import { getGroupPagesStyle } from "../../../services/theme";
import GroupPageStore from "../../../components/myGroups/stores/groupPageStore";
import MyGroups from "../../../components/myGroups";
import UserGroupsStore from "../../../components/groupsNew/stores/UserGroupsStore";
import {useRouter} from "next/router";

const GamePage = () => {
    const router = useRouter();
    const [group, setGroup] = useState<GroupWithShout|null|undefined>(undefined);
    const routeGroupId = typeof router.query.groupId === "string" ? router.query.groupId : null;

    useEffect(() => {
        if (!router.isReady) return;

        let cancelled = false;
        const groupId = routeGroupId ? parseInt(routeGroupId) : NaN;
        if (!routeGroupId || Number.isNaN(groupId)) {
            console.error("Invalid groupId", router.query.groupId);
            setGroup(null);
            return;
        }

        setGroup(undefined);
        const loadGroup = async () => {
            try {
                const info = await getInfo({groupId});
                if (!cancelled) setGroup(info);
            } catch (error) {
                console.error(error);
                if (!cancelled) setGroup(null);
            }
        };

        void loadGroup();

        return () => {
            cancelled = true;
        };
    }, [router.isReady, routeGroupId]);

    if (getGroupPagesStyle() !== 'Modern') return <MyGroupsStore.Provider>
        <GroupPageStore.Provider>
            <MyGroups id={routeGroupId}/>
        </GroupPageStore.Provider>
    </MyGroupsStore.Provider>;

    return (
        <>
            {group !== null && group !== undefined && (
                <Head>
                    <title>{group.name} - Averia</title>
                    <meta property="og:title" content={group.name} />
                    <meta property="og:url" content={`https://averia.lol/groups/${group.id}/--`} />
                    <meta property="og:type" content="profile" />
                    <meta property="og:description" content={group.description} />
                    <meta property="og:image" content={`https://averia.lol/Thumbs/GroupIcon.ashx?assetId=${group.id}`} />
                    <meta name="twitter:card" content="summary_large_image" />
                    <meta name="og:site_name" content="Averia" />
                    <meta name="theme-color" content="#E2231A" />
                </Head>
            )}
            <Theme2016>
                <UserGroupsStore.Provider>
                    <GroupsPageStore.Provider>
                        <GroupPreProcessor group={group} />
                    </GroupsPageStore.Provider>
                </UserGroupsStore.Provider>
            </Theme2016>
        </>
    );
}
export default GamePage;
