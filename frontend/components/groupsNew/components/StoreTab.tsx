import { createUseStyles } from 'react-jss';
import {useEffect, useRef} from 'react';
import Section from "./Section";
import NewLink from "../../NewLink";
import GroupsPageStore from "../stores/GroupsPageStore";
import ActionButton from "../../actionButton";
import { getTheme, themeType } from "../../../services/theme";
import CatalogItemCard from "../../CatalogPage/components/CatalogItemCard";
import useButtonStyles from "../../../styles/buttonStyles";

const useStyles = createUseStyles({
    manageGroupItemsContainer: {
        marginBottom: 18,
        fontSize: 16,
        fontWeight: 400,
        lineHeight: '1.5em',
    },
    pageControls: {
        display: 'flex',
        justifyContent: 'center',
        gap: 10,
        alignItems: 'center',
    },
    paginationBtn: {
        aspectRatio: '1 / 1',
        padding: 3,
        display: 'flex',
        borderColor: p => p.theme !== themeType.dark ? 'var(--text-color-secondary)' : 'transparent!important',
        '& span': {
            backgroundSize: '48px auto',
            height: 24,
            width: 24,
            backgroundImage: "url(/img/generic_03112016.svg)",
            backgroundRepeat: "no-repeat",
            display: 'inline-block',
            verticalAlign: 'middle',
            filter: p => p.theme === themeType.dark ? 'invert(1)' : 'none',
        },
        "&.disabled": {
            filter: p => p.theme === themeType.dark ? 'invert(1)' : 'none',
        },
    },
    pages: {
        color: p => p?.theme === themeType?.obc2019 ? "var(--white-color)" : "var(--text-color-primary)"
    },
    backIcon: {
        backgroundPosition:'0 -360px!important',
    },
    forwardIcon: {
        backgroundPosition:'0 -336px!important',
    },

    storeItemsContainer: {
        padding: 0,
        gap: 8,
    },
    headerContainerThemed: {
        "& h3": {
            color: p => p?.theme === themeType?.obc2019 ? "var(--white-color)" : "var(--text-color-primary)"
        },
    },
    spinnerContainer: {},
})

const StoreTab = ({}: {}) => {
    const s = useStyles({theme: getTheme()});
    const buttonStyles = useButtonStyles();
    const store = GroupsPageStore.useContainer();
    const {storeItems} = GroupsPageStore.useContainer();
    const deb = useRef(false);

    useEffect(() => {
        if (storeItems.page === 0 && !store.sdeb.current) store.fetchStoreItems(1, null);
    }, []);

    return <div>
        <Section header="Store" contentSectioned={false} headerContainer={s.headerContainerThemed} headerChildren={<>
            <NewLink href={`/catalog?Category=1&CreatorName=${store.group.name}&CreatorType=Group`}>
                <span className={`link2018 fw-500`}>See All</span>
            </NewLink>
        </>}>
            {
                store.userPerms?.permissions?.groupEconomyPermissions?.createItems || store.userPerms?.permissions?.groupEconomyPermissions?.manageItems ?
                    <div className={`section-content noShadow ${s.manageGroupItemsContainer} flex flex-column text-start`}>
                        <span>Groups have the ability to create and sell official Shirts, Pants, and T-Shirts! All revenue goes to group funds.</span>
                        <div>
                            <NewLink href={`/develop#groupcreations`}>
                                <span className={`link2018`}>Create or manage group items.</span>
                            </NewLink>
                        </div>
                    </div>
                    : null
            }
            {
                storeItems.total > 0 ? <div className={`flex flex-column`}>
                    <ul className={`${s.storeItemsContainer} flex flex-wrap w-100`}>
                        {
                            storeItems.items.length > 0 ? storeItems.items.map(si => {
                                return <CatalogItemCard item={si} />
                            }) : <div className={`section-content-off disabled w-100`}>No results found</div>
                        }
                    </ul>
                    <div className={`${s.pageControls}`}>
                        <ActionButton
                            className={`${s.paginationBtn} ${(storeItems?.items?.length === 0 || storeItems?.prevPage == null) ? 'disabled' : ''}`}
                            buttonStyle={(storeItems?.items?.length === 0 || storeItems?.prevPage == null) ? buttonStyles.newDisabledCancelButton : buttonStyles.newCancelButton}
                            onClick={async e => {
                                e.preventDefault();
                                if (deb.current || store.isLoading || storeItems?.prevPage == null) {
                                    return
                                }
                                deb.current = true
                                await store.fetchStoreItems(storeItems.page-1, storeItems.nextPage);
                                deb.current = false
                            }}
                        >
                            <span className={s.backIcon}/>
                        </ActionButton>
                        <span className={`${s.pages}`}>
                            Page {storeItems?.page === undefined || storeItems?.page === null ? "N/A" : storeItems?.page}
                         </span>
                        <ActionButton
                            className={`${s.paginationBtn} ${(storeItems?.items?.length === 0 || storeItems?.nextPage == null) ? 'disabled' : ''}`}
                            buttonStyle={(storeItems?.items?.length === 0 || storeItems?.nextPage == null) ? buttonStyles.newDisabledCancelButton : buttonStyles.newCancelButton}
                            onClick={async e => {
                                e.preventDefault();
                                if (deb.current || store.isLoading || storeItems?.nextPage == null) {
                                    return
                                }
                                deb.current = true
                                await store.fetchStoreItems(storeItems.page+1, storeItems.nextPage);
                                deb.current = false
                            }}
                        >
                            <span className={s.forwardIcon}/>
                        </ActionButton>
                    </div>
                </div> : deb.current || store.isLoading ? <div className={`${s.spinnerContainer}`}>
                    <span className="spinner" style={{backgroundSize: "auto 36px"}}/>
                </div> : storeItems.total === 0 ? <div className={`section-content-off disabled w-100`}>
                    No items are for sale in this group.
                </div> : null
            }
        </Section>
    </div>
};

export default StoreTab;