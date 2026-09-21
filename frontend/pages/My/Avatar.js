import AdBanner from "../../components/ad/adBanner";
import React from "react";
import AvatarEditor from "../../components/AvatarEditorPage";
import AvatarInfoStore from "../../components/AvatarEditorPage/stores/avatarInfoStore";
import Theme2016 from "../../components/theme2016";
import AvatarPageStore from "../../components/AvatarEditorPage/stores/avatarPageStore";
import Head from "next/head";
import { createUseStyles } from "react-jss";
import { getTheme, themeType } from "../../services/theme";

const useStyles = createUseStyles({
    avPageWrapper: {
        background: p => p.theme === themeType.obc2019 ? 'var(--background-color)' : 'transparent',
    },
});

const AvatarPage = () => {
    const s = useStyles({theme: getTheme()});
    return <Theme2016>
        <Head>
            <title>Avatar - Averia</title>
        </Head>
        <div className={`${s.avPageWrapper} container flex flex-column ssp`}>
            <AdBanner context="MyCharacterPage"/>
            <AvatarInfoStore.Provider>
                <AvatarPageStore.Provider>
                    <AvatarEditor />
                </AvatarPageStore.Provider>
            </AvatarInfoStore.Provider>
        </div>
    </Theme2016>
}

export default AvatarPage;
